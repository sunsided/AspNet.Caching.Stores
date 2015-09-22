using System;
using MongoDB.Bson.Serialization.Attributes;

namespace AspNet.Caching.MongoDb
{
    internal class MongoDbCacheEntry
    {
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
