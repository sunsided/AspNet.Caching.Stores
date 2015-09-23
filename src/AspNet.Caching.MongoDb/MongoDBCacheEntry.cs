/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Caching.Stores
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using MongoDB.Bson.Serialization.Attributes;

namespace AspNet.Caching.MongoDb {
    internal class MongoDbCacheEntry {
        [BsonId]
        public string Key { get; internal set; }

        [BsonElement("dat")]
        public byte[] CacheData { get; internal set; }

        [BsonElement("eat")]
        public DateTime ExpireAt { get; internal set; }

        [BsonElement("sex")]
        public TimeSpan SlidingExpiration { get; internal set; }

        [BsonElement("sat")]
        public DateTime SlidingExpireAt { get; internal set; }
    }
}
