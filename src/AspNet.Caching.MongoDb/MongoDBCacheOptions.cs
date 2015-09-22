using Microsoft.Framework.Internal;
using Microsoft.Framework.OptionsModel;

namespace AspNet.Caching.MongoDb
{
    public class MongoDbCacheOptions : IOptions<MongoDbCacheOptions>
    {
        public string ConnectionString { get; set; } = "mongodb://localhost:27017";

        public string Database { get; set; } = "caching";

        public string Collection { get; set; } = "cache";

        public ISystemClock Clock { get; set; }

        MongoDbCacheOptions IOptions<MongoDbCacheOptions>.Value => this;
    }
}
