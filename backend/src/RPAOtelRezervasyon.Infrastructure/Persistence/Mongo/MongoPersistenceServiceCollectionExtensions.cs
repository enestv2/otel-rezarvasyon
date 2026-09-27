using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RPAOtelRezervasyon.Domain.Abstractions;

namespace RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

/// <summary>Konum önbelleği (MongoDB) adaptörünün DI kaydı (ADR-0002).</summary>
public static class MongoPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMongoGeocodeCache(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<MongoGeocodeCacheOptions>()
            .Bind(configuration.GetSection(MongoGeocodeCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IMongoClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoGeocodeCacheOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });

        services.AddSingleton<IMongoDatabase>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoGeocodeCacheOptions>>().Value;
            return serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(options.Database);
        });

        services.AddSingleton<IMongoCollection<GeocodeCacheDocument>>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<MongoGeocodeCacheOptions>>().Value;
            return serviceProvider.GetRequiredService<IMongoDatabase>().GetCollection<GeocodeCacheDocument>(options.GeocodeCollection);
        });

        services.AddSingleton<IGeocodeCache, MongoGeocodeCache>();
        services.AddHostedService<MongoIndexInitializer>();

        return services;
    }
}
