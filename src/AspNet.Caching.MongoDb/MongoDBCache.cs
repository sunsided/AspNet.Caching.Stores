/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.Internal;
using Microsoft.Framework.OptionsModel;
using MongoDB.Driver;

namespace AspNet.Caching.MongoDb {
    public sealed class MongoDbCache : IDistributedCache {

        private readonly MongoDbCacheOptions _options;
        private readonly ISystemClock _clock;

        private IMongoClient _client;
        private IMongoCollection<MongoDbCacheEntry> _collection;

        public MongoDbCache(IOptions<MongoDbCacheOptions> optionsAccessor = null) {
            _options = optionsAccessor?.Value ?? new MongoDbCacheOptions();

            if (string.IsNullOrWhiteSpace(_options.ConnectionString)) {
                throw new InvalidOperationException("The MongoDB connection string must be nonempty.");
            }

            if (string.IsNullOrWhiteSpace(_options.Database)) {
                throw new InvalidOperationException("The MongoDB database name must be nonempty.");
            }

            if (string.IsNullOrWhiteSpace(_options.Collection)) {
                throw new InvalidOperationException("The MongoDB collection name must be nonempty.");
            }

            _clock = _options.Clock ?? new SystemClock();
        }

        public void Connect() {
            ConnectAsync().GetAwaiter().GetResult();
        }

        private bool CreateMongoClientSynchronized() {
            lock (_options.ConnectionString) {
                if (_client != null) return true;
                _client = new MongoClient(_options.ConnectionString);
                return false;
            }
        }

        public async Task ConnectAsync() {
            var connectionAlreadyEstablished = CreateMongoClientSynchronized();
            if (connectionAlreadyEstablished) {
                return;
            }

            var database = _client.GetDatabase(_options.Database);
            var collection = _collection = database.GetCollection<MongoDbCacheEntry>(_options.Collection);

            // Create the index to expire on the "expire at" value
            await collection.Indexes.CreateOneAsync(
                Builders<MongoDbCacheEntry>.IndexKeys.Ascending(x => x.ExpireAt),
                new CreateIndexOptions {
                    ExpireAfter = TimeSpan.FromSeconds(0)
                });

            // Create the index to expire on the "sliding expiration" value
            await collection.Indexes.CreateOneAsync(
                Builders<MongoDbCacheEntry>.IndexKeys.Ascending(x => x.SlidingExpireAt),
                new CreateIndexOptions {
                    ExpireAfter = TimeSpan.FromSeconds(0)
                });
        }

        public byte[] Get(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            return GetAsync(key).Result;
        }

        public async Task<byte[]> GetAsync(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            await ConnectAsync();

            var list = await _collection.Find(x => x.Key == key)
                .Project(x => new {
                                  x.CacheData,
                                  x.ExpireAt,
                                  x.SlidingExpiration
                              })
                .ToListAsync();

            var data = list.FirstOrDefault();
            if (data == null) return null;

            await RefreshAsync(key, data.ExpireAt, data.SlidingExpiration);
            return data.CacheData;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            SetAsync(key, value, options).GetAwaiter().GetResult();
        }

        public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            var update = Builders<MongoDbCacheEntry>.Update
                .Set(x => x.CacheData, value);

            var now = _clock.UtcNow;
            var expireAt = CalculateExpireAt(options, now);

            var slidingExpireAt = expireAt;
            if (options.SlidingExpiration.HasValue) {
                slidingExpireAt = now.Add(options.SlidingExpiration.Value);
                update = update.Set(x => x.SlidingExpiration, options.SlidingExpiration.Value);
            }

            update = update
                .Set(x => x.ExpireAt, expireAt.UtcDateTime)
                .Set(x => x.SlidingExpireAt, slidingExpireAt.UtcDateTime);

            var updateOptions = new FindOneAndUpdateOptions<MongoDbCacheEntry, string> {
                IsUpsert = true,
                // we actually don't need anything back, this is just to keep the data returned small
                Projection = Builders<MongoDbCacheEntry>.Projection.Include(x => x.Key)
            };

            await ConnectAsync();
            await _collection.FindOneAndUpdateAsync(x => x.Key == key,
                update,
                updateOptions);
        }

        public void Refresh(string key) {
            RefreshAsync(key).GetAwaiter().GetResult();
        }

        public async Task RefreshAsync(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            // refreshing is nasty because we need a roundtrip to Mongo
            // to obtain the sliding expiration value
            var cursor = await _collection.FindAsync(x => x.Key == key,
                new FindOptions<MongoDbCacheEntry> {
                    Limit = 1,
                    Projection = Builders<MongoDbCacheEntry>.Projection
                    .Include(x => x.ExpireAt)
                    .Include(x => x.SlidingExpiration)
                });

            if (!await cursor.MoveNextAsync()) {
                return;
            }

            var entry = cursor.Current.First();
            await RefreshAsync(key, entry.ExpireAt, entry.SlidingExpiration);
        }

        private Task RefreshAsync(string key, DateTimeOffset expireAt, TimeSpan slidingExpiration)
        {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            // we don't care if sat > eat, because the index that triggers first wins
            var now = _clock.UtcNow;
            var sat = slidingExpiration == default(TimeSpan) ? expireAt : now.Add(slidingExpiration).UtcDateTime;

            var update = Builders<MongoDbCacheEntry>.Update
                .Set(x => x.SlidingExpireAt, sat);

            return _collection.FindOneAndUpdateAsync(x => x.Key == key, update);
        }

        public void Remove(string key) {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        public Task RemoveAsync(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            return _collection.DeleteOneAsync(x => x.Key == key);
        }

        /// <summary>
        /// Calculates the expiration time based on the current time
        /// </summary>
        /// <param name="options">The expiration options</param>
        /// <param name="now">The current time</param>
        /// <returns>The absolute expiration time</returns>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="MemoryCacheEntryOptions.AbsoluteExpiration"/> was in the past.</exception>
        private static DateTimeOffset CalculateExpireAt(DistributedCacheEntryOptions options, DateTimeOffset now) {
            if (options.AbsoluteExpirationRelativeToNow.HasValue) {
                return now.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }

            if (!options.AbsoluteExpiration.HasValue) {
                return DateTimeOffset.MaxValue;
            }

            if (options.AbsoluteExpiration.Value < now) {
                throw new ArgumentOutOfRangeException(nameof(DistributedCacheEntryOptions.AbsoluteExpiration),
                    options.AbsoluteExpiration.Value,
                    "The absolute expiration value must be in the future.");
            }

            return options.AbsoluteExpiration.Value;
        }
    }
}
