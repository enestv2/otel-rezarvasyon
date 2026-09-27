using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;
using RPAOtelRezervasyon.IntegrationTests.Support;

namespace RPAOtelRezervasyon.IntegrationTests.Persistence;

/// <summary>
/// G-6: MongoDB erişilemezse önbellek zorunlu bağımlılık değildir; okuma isabetsiz döner, yazma
/// atlanır ve çağıran taraf istisna görmez (bkz. docs/security.md, plan 0001 madde 7).
/// </summary>
public sealed class MongoGeocodeCacheDegradedTests
{
    private static MongoGeocodeCache CreateCacheAgainstUnreachableServer()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(1);
        var collection = new MongoClient(settings)
            .GetDatabase("rpaotelrezervasyon")
            .GetCollection<GeocodeCacheDocument>("geocode_cache");

        return new MongoGeocodeCache(collection, NullLogger<MongoGeocodeCache>.Instance);
    }

    [Fact]
    public async Task FindAsync_when_mongo_unreachable_returns_null_without_throwing()
    {
        var cache = CreateCacheAgainstUnreachableServer();

        var cached = await cache.FindAsync("geoapify", "ulasilamayan yer", CancellationToken.None);

        Assert.Null(cached);
    }

    [Fact]
    public async Task StoreAsync_when_mongo_unreachable_does_not_throw()
    {
        var capture = new CapturingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capture));
        var cache = new MongoGeocodeCache(
            CreateCollectionAgainstUnreachableServer(),
            loggerFactory.CreateLogger<MongoGeocodeCache>());

        await cache.StoreAsync(
            "geoapify",
            "ulasilamayan yer",
            new GeoPoint(39.9208, 32.8541),
            "Unreachable",
            CancellationToken.None);

        var warning = Assert.Single(capture.Messages);
        Assert.Contains("Geocode cache operation skipped", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("ulasilamayan yer", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("39.9208", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("32.8541", warning, StringComparison.Ordinal);
    }

    private static IMongoCollection<GeocodeCacheDocument> CreateCollectionAgainstUnreachableServer()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://127.0.0.1:1");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(1);
        return new MongoClient(settings)
            .GetDatabase("rpaotelrezervasyon")
            .GetCollection<GeocodeCacheDocument>("geocode_cache");
    }
}
