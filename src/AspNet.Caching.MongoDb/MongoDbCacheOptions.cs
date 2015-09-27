/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Caching.Stores
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using Microsoft.Framework.OptionsModel;

namespace AspNet.Caching.MongoDb {
    public class MongoDbCacheOptions : IOptions<MongoDbCacheOptions> {
        public string ConnectionString { get; set; } = "mongodb://localhost:27017";

        public string Database { get; set; } = "caching";

        public string Collection { get; set; } = "cache";

        public TimeSpan DefaultRelativeExpiration { get; set; } = TimeSpan.FromDays(30);
        
        MongoDbCacheOptions IOptions<MongoDbCacheOptions>.Value => this;
    }
}
