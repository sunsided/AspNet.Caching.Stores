namespace AspNet.Caching.MongoDb
{
    internal static class FieldNames {
        public const string Key = "_id";
        public const string ModifiedAt = "m@";
        public const string ExpireAt = "e@";
        public const string SlidingExpireAt = "s@";
        public const string SlidingExpiration = "sx";
        public const string CacheData = "d";
    }
}
