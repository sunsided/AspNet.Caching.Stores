﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.Caching.Distributed;

namespace AspNet.Caching.MongoDb.Sample
{
    public class Program
    {
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
                Console.WriteLine("Not Found");
            }

            Console.ReadLine();
        }
    }
}