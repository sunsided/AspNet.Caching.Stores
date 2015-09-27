/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Caching.Stores
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.Caching.Memory;
using Microsoft.Framework.OptionsModel;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspNet.Caching.MongoDb {
    public sealed class MongoDbCache : IDistributedCache {

        private readonly MongoDbCacheOptions _options;

        private IMongoClient _client;

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

            if (_options.DefaultRelativeExpiration < TimeSpan.Zero)
            {
                throw new InvalidOperationException("The default relative expiration value must be a positive time span.");
            }
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

        private async Task<IMongoCollection<BsonDocument>>  GetCollectionAsync() {
            await ConnectAsync();
            return _client.GetDatabase(_options.Database)
                .GetCollection<BsonDocument>(_options.Collection);
        }

        public async Task ConnectAsync() {
            var connectionAlreadyEstablished = CreateMongoClientSynchronized();
            if (connectionAlreadyEstablished) {
                return;
            }

            var collection = _client.GetDatabase(_options.Database)
                .GetCollection<BsonDocument>(_options.Collection);

            // Create the index to expire on the "expire at" value
            await collection.Indexes.CreateOneAsync(
                Builders<BsonDocument>.IndexKeys.Ascending(MongoDbConstants.ExpireAt),
                new CreateIndexOptions {
                    ExpireAfter = TimeSpan.FromSeconds(0)
                });

            // Create the index to expire on the "sliding expiration" value
            await collection.Indexes.CreateOneAsync(
                Builders<BsonDocument>.IndexKeys.Ascending(MongoDbConstants.SlidingExpireAt),
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

            var filter = GetIdMatchFilter(key);
            var projection = Builders<BsonDocument>.Projection
                .Include(MongoDbConstants.CacheData)
                .Include(MongoDbConstants.ExpireAt)
                .Include(MongoDbConstants.SlidingExpireAt)
                .Include(MongoDbConstants.SlidingExpiration);

            var collection = await GetCollectionAsync();
            var list = await collection.Find(filter).Project(projection).ToListAsync();

            var entry = list.FirstOrDefault();
            if (entry == null) return null;

            var now = DateTimeOffset.UtcNow;

            DateTime expireAtUtc;
            TimeSpan slidingExpiration;
            if (HasSlidingExpirationValue(entry, out expireAtUtc, out slidingExpiration) && (expireAtUtc >= now)) {
                await RefreshAsync(key, expireAtUtc, slidingExpiration);
            }

            // the index doesn't seem to delete at the exact time given, so manually check for expiration as well
            var expirationTime = expireAtUtc;
            BsonValue slidingExpireAt;
            if (entry.TryGetValue(MongoDbConstants.SlidingExpireAt, out slidingExpireAt)) {
                var sat = slidingExpireAt.ToUniversalTime();
                if (sat < expirationTime) {
                    expirationTime = sat;
                }
            }

            return (expirationTime < now)
                ? null
                : entry[MongoDbConstants.CacheData].AsByteArray;
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

            var now = DateTimeOffset.UtcNow;
            var expireAt = CalculateExpireAt(options, now);

            var update = Builders<BsonDocument>.Update
                .Set(MongoDbConstants.ModifiedAt, now.UtcDateTime)
                .Set(MongoDbConstants.CacheData, value)
                .Set(MongoDbConstants.ExpireAt, expireAt.UtcDateTime);

            if (options.SlidingExpiration.HasValue) {
                var slidingExpireAt = now.Add(options.SlidingExpiration.Value);
                update = update.Set(MongoDbConstants.SlidingExpiration, options.SlidingExpiration.Value.TotalSeconds)
                               .Set(MongoDbConstants.SlidingExpireAt, slidingExpireAt.UtcDateTime);
            }

            var updateOptions = new FindOneAndUpdateOptions<BsonDocument> {
                IsUpsert = true,
                // we actually don't need anything back, this is just to keep the data returned small
                Projection = Builders<BsonDocument>.Projection.Include(MongoDbConstants.Key)
            };

            var filter = GetIdMatchFilter(key);

            var collection = await GetCollectionAsync();
            await collection.FindOneAndUpdateAsync(filter, update, updateOptions);
        }

        public void Refresh(string key) {
            RefreshAsync(key).GetAwaiter().GetResult();
        }

        public async Task RefreshAsync(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            var filter = GetIdMatchFilter(key);
            var projection = Builders<BsonDocument>.Projection
                .Include(MongoDbConstants.ExpireAt)
                .Include(MongoDbConstants.SlidingExpiration);

            var options = new FindOptions<BsonDocument> { Limit = 1, Projection = projection };

            // refreshing is nasty because we need a roundtrip to Mongo
            // to obtain the sliding expiration value
            var collection = await GetCollectionAsync();
            var cursor = await collection.FindAsync(filter, options);

            if (!await cursor.MoveNextAsync()) {
                return;
            }

            var entry = cursor.Current.First();

            DateTime expireAtUtc;
            TimeSpan slidingExpiration;
            if (HasSlidingExpirationValue(entry, out expireAtUtc, out slidingExpiration)) {
                await RefreshAsync(key, expireAtUtc, slidingExpiration);
            }
        }

        private static bool HasSlidingExpirationValue(BsonDocument entry, out DateTime expireAtUtc, out TimeSpan slidingExpiration) {
            expireAtUtc = entry[MongoDbConstants.ExpireAt].ToUniversalTime();
            slidingExpiration = default(TimeSpan);

            BsonValue value;
            if (!entry.TryGetValue(MongoDbConstants.SlidingExpiration, out value)) {
                return false;
            }

            slidingExpiration = TimeSpan.FromSeconds(value.AsDouble);
            return true;
        }

        private async Task RefreshAsync(string key, DateTimeOffset expireAt, TimeSpan slidingExpiration)
        {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            // we don't care if sat > eat, because the index that triggers first wins
            var now = DateTimeOffset.UtcNow;
            var sat = slidingExpiration == default(TimeSpan)
                ? expireAt.UtcDateTime
                : now.Add(slidingExpiration).UtcDateTime;

            var filter = GetIdMatchFilter(key);

            var update = Builders<BsonDocument>.Update
                .Set(MongoDbConstants.SlidingExpireAt, sat);

            var collection = await GetCollectionAsync();
            await collection.FindOneAndUpdateAsync(filter, update);
        }

        public void Remove(string key) {
            RemoveAsync(key).GetAwaiter().GetResult();
        }

        public async Task RemoveAsync(string key) {
            if (key == null) {
                throw new ArgumentNullException(nameof(key));
            }

            var collection = await GetCollectionAsync();
            await collection.DeleteOneAsync(GetIdMatchFilter(key));
        }

        /// <summary>
        /// Calculates the expiration time based on the current time
        /// </summary>
        /// <param name="options">The expiration options</param>
        /// <param name="now">The current time</param>
        /// <returns>The absolute expiration time</returns>
        /// <exception cref="ArgumentOutOfRangeException"><see cref="MemoryCacheEntryOptions.AbsoluteExpiration"/> was in the past.</exception>
        private DateTimeOffset CalculateExpireAt(DistributedCacheEntryOptions options, DateTimeOffset now) {
            if (options.AbsoluteExpirationRelativeToNow.HasValue) {
                return now.Add(options.AbsoluteExpirationRelativeToNow.Value);
            }

            if (!options.AbsoluteExpiration.HasValue) {
                return options.SlidingExpiration != null 
                    ? DateTimeOffset.MaxValue 
                    : now.Add(_options.DefaultRelativeExpiration);
            }

            if (options.AbsoluteExpiration.Value < now) {
                throw new ArgumentOutOfRangeException(
                    nameof(DistributedCacheEntryOptions.AbsoluteExpiration),
                    options.AbsoluteExpiration.Value,
                    "The absolute expiration value must be in the future.");
            }

            return options.AbsoluteExpiration.Value;
        }

        private static FilterDefinition<BsonDocument> GetIdMatchFilter(string key) {
            return Builders<BsonDocument>.Filter.Eq(MongoDbConstants.Key, key);
        }
    }
}
