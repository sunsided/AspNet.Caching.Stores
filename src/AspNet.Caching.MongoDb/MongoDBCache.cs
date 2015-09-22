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
            ConnectAsync().Wait();
        }

        public async Task ConnectAsync() {
            var client = _client;
            if (client != null) return;

            _client = client = new MongoClient(_options.ConnectionString);
            var database = client.GetDatabase(_options.Database);
            var collection = _collection = database.GetCollection<MongoDbCacheEntry>(_options.Collection);

            // Create the index to expire on the "expire at" value
            await collection.Indexes.CreateOneAsync(
                Builders<MongoDbCacheEntry>.IndexKeys.Ascending(x => x.ExpireAt),
                new CreateIndexOptions {
                    ExpireAfter = TimeSpan.FromSeconds(0)
                })
                .ConfigureAwait(false);

            // Create the index to expire on the "sliding expiration" value
            await collection.Indexes.CreateOneAsync(
                Builders<MongoDbCacheEntry>.IndexKeys.Ascending(x => x.SlidingExpireAt),
                new CreateIndexOptions {
                    ExpireAfter = TimeSpan.FromSeconds(0)
                })
                .ConfigureAwait(false);
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

            await ConnectAsync().ConfigureAwait(false);

            var data = await _collection.FindOneAndUpdateAsync<MongoDbCacheEntry, MongoDbCacheEntry>(x => x.Key == key,
                Builders<MongoDbCacheEntry>.Update.Set(x => x.LastAccess, DateTime.UtcNow),
                new FindOneAndUpdateOptions<MongoDbCacheEntry> {
                    ReturnDocument = ReturnDocument.After,
                    Projection = Builders<MongoDbCacheEntry>.Projection.Include(x => x.CacheData)
                })
                .ConfigureAwait(false);

            return data?.CacheData;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            SetAsync(key, value, options).Wait();
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
                .Set(x => x.CreatedAt, now.UtcDateTime)
                .Set(x => x.ExpireAt, expireAt.UtcDateTime)
                .Set(x => x.SlidingExpireAt, slidingExpireAt.UtcDateTime);

            var updateOptions = new FindOneAndUpdateOptions<MongoDbCacheEntry, string> {
                IsUpsert = true,
                // we actually don't need anything back, this is just to keep the data returned small
                Projection = Builders<MongoDbCacheEntry>.Projection.Include(x => x.Key)
            };

            await ConnectAsync().ConfigureAwait(false);
            await _collection.FindOneAndUpdateAsync(x => x.Key == key,
                update,
                updateOptions)
                .ConfigureAwait(false);
        }

        public void Refresh(string key) {
            RefreshAsync(key).Wait();
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
                })
                .ConfigureAwait(false);

            if (!await cursor.MoveNextAsync().ConfigureAwait(false)) {
                return;
            }

            var entry = cursor.Current.First();

            var now = _clock.UtcNow;
            var sex = entry.SlidingExpiration;
            var sat = sex == default(TimeSpan) ? entry.ExpireAt : now.Add(sex).UtcDateTime;

            // now we just recalculate the key and store
            var update = Builders<MongoDbCacheEntry>.Update
                .Set(x => x.LastAccess, now.UtcDateTime)
                .Set(x => x.SlidingExpireAt, sat);

            await _collection
                .FindOneAndUpdateAsync(x => x.Key == key, update)
                .ConfigureAwait(false);
        }

        public void Remove(string key) {
            RemoveAsync(key).Wait();
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
