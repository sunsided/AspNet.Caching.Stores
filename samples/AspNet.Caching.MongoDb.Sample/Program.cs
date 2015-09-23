/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Caching.Stores
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;

namespace AspNet.Caching.MongoDb.Sample {
    public class Program {
        /// <summary>
        /// This sample assumes that a MongoDB server is running on the local machine.
        /// </summary>
        /// <param name="args"></param>
        public static async Task Main(string[] args) {

            var key = "myKey";
            var message = "Hello, World!";
            var value = Encoding.UTF8.GetBytes(message);

            Console.WriteLine("Connecting to cache");
            var cache = new MongoDbCache(new MongoDbCacheOptions {
                ConnectionString = "mongodb://localhost?w=0&j=false",
                Database = "caching",
                Collection = "cache"
            });
            Console.WriteLine("Connected");

            Console.WriteLine($"Setting value '{message}' in cache");
            await cache.SetAsync(key, value, new DistributedCacheEntryOptions());
            Console.WriteLine("Set");

            Console.WriteLine("Getting value from cache");
            value = await cache.GetAsync(key);
            if (value != null) {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value));
            }
            else {
                Console.WriteLine("Not Found");
            }

            Console.WriteLine("Refreshing value in cache");
            await cache.RefreshAsync(key);
            Console.WriteLine("Refreshed");

            Console.WriteLine("Removing value from cache");
            await cache.RemoveAsync(key);
            Console.WriteLine("Removed");

            Console.WriteLine("Getting value from cache again");
            value = await cache.GetAsync(key);
            if (value != null) {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value));
            }
            else {
                Console.WriteLine("Not Found (that's good.)");
            }

            value = Encoding.UTF8.GetBytes(message);
            Console.WriteLine($"Setting value '{message}' in cache with sliding expiration");
            await cache.SetAsync(key, value, new DistributedCacheEntryOptions
                                                 {
                                                     SlidingExpiration = TimeSpan.FromSeconds(1)
                                                 });
            Console.WriteLine("Set");

            Console.WriteLine("Getting value from cache");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value) + " (that's good)");
            }
            else
            {
                Console.WriteLine("Not Found (that's bad)");
            }

            await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false);

            Console.WriteLine("Getting value from cache again");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value) + " (that's bad)");
            }
            else
            {
                Console.WriteLine("Not Found (that's good.)");
            }

            Console.WriteLine("Press key to exit.");
            Console.ReadKey(true);
        }
    }
}
