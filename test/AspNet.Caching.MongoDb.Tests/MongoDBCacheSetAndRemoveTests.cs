/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
 * for more information concerning the license and the contributors participating to this project.
 */

using Microsoft.Framework.Caching.Distributed;
using Xunit;

namespace AspNet.Caching.MongoDb.Tests
{
    // These tests require MongoDb server to be started on the machine. Make sure to change the value of
    // "MongoDbTestConfig.MongoDb" accordingly.
    class MongoDbCacheSetAndRemoveTests
    {
        [Fact]
        public void GetMissingKeyReturnsNull()
        {
            var cache = MongoDbTestConfig.CreateCacheInstance(GetType().Name);
            string key = "non-existent-key";

            var result = cache.Get(key);
            Assert.Null(result);
        }

        [Fact]
        public void SetAndGetReturnsObject()
        {
            var cache = MongoDbTestConfig.CreateCacheInstance(GetType().Name);
            var value = new byte[1];
            string key = "myKey";

            cache.Set(key, value, new DistributedCacheEntryOptions());

            var result = cache.Get(key);
            Assert.Equal(value, result);
        }

        [Fact]
        public void SetAndGetWorksWithCaseSensitiveKeys()
        {
            var cache = MongoDbTestConfig.CreateCacheInstance(GetType().Name);
            var value = new byte[1];
            string key1 = "myKey";
            string key2 = "Mykey";

            cache.Set(key1, value);

            var result = cache.Get(key1);
            Assert.Equal(value, result);

            result = cache.Get(key2);
            Assert.Null(result);
        }

        [Fact]
        public void SetAlwaysOverwrites()
        {
            var cache = MongoDbTestConfig.CreateCacheInstance(GetType().Name);
            var value1 = new byte[1] { 1 };
            string key = "myKey";

            cache.Set(key, value1);
            var result = cache.Get(key);
            Assert.Equal(value1, result);

            var value2 = new byte[1] { 2 };
            cache.Set(key, value2);
            result = cache.Get(key);
            Assert.Equal(value2, result);
        }

        [Fact]
        public void RemoveRemoves()
        {
            var cache = MongoDbTestConfig.CreateCacheInstance(GetType().Name);
            var value = new byte[1];
            string key = "myKey";

            cache.Set(key, value);
            var result = cache.Get(key);
            Assert.Equal(value, result);

            cache.Remove(key);
            result = cache.Get(key);
            Assert.Null(result);
        }
    }
}