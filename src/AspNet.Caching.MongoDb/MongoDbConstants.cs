using System;
using MongoDB.Bson;
using MongoDB.Driver;

namespace AspNet.Caching.MongoDb
{
    internal static class MongoDbConstants {
        public const string KeyField = "_id";
        public const string ModifiedAtField = "m@";
        public const string ExpireAtField = "e@";
        public const string SlidingExpireAtField = "s@";
        public const string SlidingExpirationField = "sx";
        public const string CacheDataField = "d";

        public static readonly FieldDefinition<BsonDocument, string> Key = KeyField;
        public static readonly FieldDefinition<BsonDocument, DateTime> ModifiedAt =ModifiedAtField;
        public static readonly FieldDefinition<BsonDocument, DateTime> ExpireAt = ExpireAtField;
        public static readonly FieldDefinition<BsonDocument, DateTime> SlidingExpireAt = SlidingExpireAtField;
        public static readonly FieldDefinition<BsonDocument, double> SlidingExpiration = SlidingExpirationField;
        public static readonly FieldDefinition<BsonDocument, byte[]> CacheData = CacheDataField;

        public static readonly FieldDefinition<BsonDocument> Key1 = KeyField;
        public static readonly FieldDefinition<BsonDocument> ModifiedAt1 = ModifiedAtField;
        public static readonly FieldDefinition<BsonDocument> ExpireAt1 = ExpireAtField;
        public static readonly FieldDefinition<BsonDocument> SlidingExpireAt1 = SlidingExpireAtField;
        public static readonly FieldDefinition<BsonDocument> SlidingExpiration1 = SlidingExpirationField;
        public static readonly FieldDefinition<BsonDocument> CacheData1 = CacheDataField;
    }
}
