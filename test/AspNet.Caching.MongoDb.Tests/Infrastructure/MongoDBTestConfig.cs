/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using System.Diagnostics;
using System.Linq;

namespace AspNet.Caching.MongoDb.Tests
{
    public static class MongoDbTestConfig
    {
        internal const string FunctionalTestsMongoDBServerExeName = "mongod";

        private static volatile Process _mongoDbServerProcess; // null implies if server exists it was not started by this code
        private static readonly object _mongoDbServerProcessLock = new object();

        public static string MongoDbHost = "localhost";
        public static int MongoDbPort = 27017; // override default so that do not interfere with anyone else's server

        public static MongoDbCache CreateCacheInstance(string instanceName)
        {
            return new MongoDbCache(new MongoDbCacheOptions()
            {
                ConnectionString = $"mongodb://{MongoDbHost}:{MongoDbPort}",
                Database = "caching",
                Collection = "cache"
            });
        }

        public static void GetOrStartServer()
        {
            if (UserHasStartedOwnServer())
            {
                // user claims they have started their own
                return;
            }

            if (AlreadyOwnRunningServer())
            {
                return;
            }

            TryConnectToOrStartServer();
        }

        private static bool AlreadyOwnRunningServer()
        {
            // Does mongoDbTestConfig already know about a running server?
            if (_mongoDbServerProcess != null
                && !_mongoDbServerProcess.HasExited)
            {
                return true;
            }

            return false;
        }

        private static bool TryConnectToOrStartServer()
        {
            if (CanFindExistingServer())
            {
                return true;
            }

            throw new InvalidOperationException("A running MongoDB server is required.");
        }

        public static void StopmongoDbServer()
        {
            if (UserHasStartedOwnServer())
            {
                // user claims they have started their own - they are responsible for stopping it
                return;
            }

            if (CanFindExistingServer())
            {
                lock (_mongoDbServerProcessLock)
                {
                    if (_mongoDbServerProcess != null)
                    {
                        _mongoDbServerProcess.Kill();
                        _mongoDbServerProcess = null;
                    }
                }
            }
        }

        private static bool CanFindExistingServer()
        {
            var process = Process.GetProcessesByName(FunctionalTestsMongoDBServerExeName).SingleOrDefault();
            if (process == null || process.HasExited)
            {
                lock (_mongoDbServerProcessLock)
                {
                    _mongoDbServerProcess = null;
                }
                return false;
            }

            lock (_mongoDbServerProcessLock)
            {
                _mongoDbServerProcess = process;
            }
            return true;
        }

        public static bool UserHasStartedOwnServer()
        {
            // if the user sets this environment variable they are claiming they've started
            // their own mongoDb Server and are responsible for starting/stopping it
            return (Environment.GetEnvironmentVariable("STARTED_OWN_MONGODB_SERVER") != null);
        }
    }
}
