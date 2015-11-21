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
        public static void Main(string[] args) {
            MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args) {
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

            await RetrieveInParallel(cache);

            await StoreAndRetrieveWithoutExpiration(message, cache, key, value);

            await StoreAndRetrieveWithExpiration(message, cache, key);

            await StoreAndRetrieveWithSlidingExpiration(message, cache, key);

            Console.WriteLine("Press key to exit.");
            Console.ReadKey(true);
        }

        private static Task RetrieveInParallel(MongoDbCache cache)
        {
            Console.WriteLine("Running concurrency test");

            var list = new Task[20];
            for (var index = 0; index < list.Length; index++)
            {
                var localIndex = index;
                list[index] = Task.Run(async () => {
                    try
                    {
                        var item = await cache.GetAsync("key");
                        Console.WriteLine("Returned from task " + localIndex);
                    }

                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                });
            }

            return Task.WhenAll(list);
        }

        private static async Task StoreAndRetrieveWithoutExpiration(string message, MongoDbCache cache, string key, byte[] value)
        {
            Console.WriteLine($"Setting value '{message}' in cache");
            await cache.SetAsync(key, value, new DistributedCacheEntryOptions());
            Console.WriteLine("Set");

            Console.WriteLine("Getting value from cache");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value));
            }
            else
            {
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
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value) + " (that's bad)");
                BlockOnKeypress();
            }
            else
            {
                Console.WriteLine("Not Found (that's good.)");
            }
        }

        private static void BlockOnKeypress()
        {
            Console.WriteLine("Press key to continue");
            Console.ReadKey(false);
        }

        private static async Task StoreAndRetrieveWithExpiration(string message, MongoDbCache cache, string key)
        {
            byte[] value;
            value = Encoding.UTF8.GetBytes(message);
            Console.WriteLine($"Setting value '{message}' in cache with relative expiration");
            await
                cache.SetAsync(
                    key,
                    value,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(1) });
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
                BlockOnKeypress();
            }

            Console.WriteLine("Giving the cache time to think ...");
            await Task.Delay(TimeSpan.FromSeconds(4)).ConfigureAwait(false);

            Console.WriteLine("Getting value from cache again");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value) + " (that's bad)");
                BlockOnKeypress();
            }
            else
            {
                Console.WriteLine("Not Found (that's good.)");
            }
        }

        private static async Task StoreAndRetrieveWithSlidingExpiration(string message, MongoDbCache cache, string key)
        {
            byte[] value;
            value = Encoding.UTF8.GetBytes(message);
            Console.WriteLine($"Setting value '{message}' in cache with sliding expiration");
            await cache.SetAsync(key, value, new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(2) });
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
                BlockOnKeypress();
            }

            Console.WriteLine("Giving the cache time to think ...");
            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);

            Console.WriteLine("Refreshing value in cache");
            await cache.RefreshAsync(key);
            Console.WriteLine("Refreshed");

            Console.WriteLine("Giving the cache time to think ...");
            await Task.Delay(TimeSpan.FromSeconds(0.5)).ConfigureAwait(false);

            Console.WriteLine("Getting value from cache again");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value) + " (that's good)");
            }
            else
            {
                Console.WriteLine("Not Found (that's bad.)");
                BlockOnKeypress();
            }

            Console.WriteLine("Giving the cache time to think ...");
            await Task.Delay(TimeSpan.FromSeconds(2)).ConfigureAwait(false);

            Console.WriteLine("Getting value from cache again");
            value = await cache.GetAsync(key);
            if (value != null)
            {
                Console.WriteLine("Retrieved: " + Encoding.UTF8.GetString(value) + " (that's bad)");
                BlockOnKeypress();
            }
            else
            {
                Console.WriteLine("Not Found (that's good.)");
            }
        }
    }
}
