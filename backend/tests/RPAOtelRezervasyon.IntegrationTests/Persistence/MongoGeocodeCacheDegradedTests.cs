using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

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
        var cache = CreateCacheAgainstUnreachableServer();

        await cache.StoreAsync(
            "geoapify",
            "ulasilamayan yer",
            new GeoPoint(39.9208, 32.8541),
            "Unreachable",
            CancellationToken.None);
    }
}
