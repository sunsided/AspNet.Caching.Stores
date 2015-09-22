/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
 * for more information concerning the license and the contributors participating to this project.
 */

using System;
using Microsoft.Framework.Caching.Distributed;
using Microsoft.Framework.DependencyInjection;
using Microsoft.Framework.DependencyInjection.Extensions;

namespace AspNet.Caching.MongoDb {
    /// <summary>
    /// Extension methods for setting up MongoDB distributed cache related services in an
    /// <see cref="IServiceCollection" />.
    /// </summary>
    public static class MongoDbCacheServicesExtensions {

        /// <summary>
        /// Adds MongoDB distributed caching services to the specified <see cref="IServiceCollection" />.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null" />.</exception>
        public static IServiceCollection AddMongoDbCache(this IServiceCollection services) {
            if (services == null) {
                throw new ArgumentNullException(nameof(services));
            }

            services.AddOptions();
            services.TryAdd(ServiceDescriptor.Singleton<IDistributedCache, MongoDbCache>());
            return services;
        }
    }
}
