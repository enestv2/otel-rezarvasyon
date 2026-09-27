using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RPAOtelRezervasyon.Application.Caching;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;
using RPAOtelRezervasyon.IntegrationTests.Support;

namespace RPAOtelRezervasyon.IntegrationTests.Persistence;

/// <summary>
/// AC-15: konum önbelleği kalıcıdır ve isabet sonrası dış sağlayıcıya gidilmez. Gerçek MongoDB
/// gerektirdiği için isteğe bağlıdır; <c>MONGO_TEST_CONNECTION</c> tanımlı değilse test "skipped"
/// olarak raporlanır ve `scripts/check` buna bağlı değildir (bkz. ADR-0002, plan 0001 madde 8).
/// </summary>
[Trait("Category", "Mongo")]
public sealed class MongoGeocodeCacheTests
{
    [MongoFact]
    public async Task StoreAsync_then_FindAsync_returns_persisted_location_and_cache_key()
    {
        var connectionString = RequireConnectionString();
        var databaseName = $"rpaotelrezervasyon_test_{Guid.NewGuid():N}";
        var client = new MongoClient(connectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<GeocodeCacheDocument>("geocode_cache");

        try
        {
            var capture = new CapturingLoggerProvider();
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(capture));
            var writer = new MongoGeocodeCache(collection, loggerFactory.CreateLogger<MongoGeocodeCache>());
            var location = new GeoPoint(39.9208, 32.8541);

            await writer.StoreAsync("geoapify", "grand hotel ankara", location, "Grand Hotel, Ankara", CancellationToken.None);
            var successLog = Assert.Single(capture.Messages);
            Assert.Contains("Geocode cache upsert completed", successLog, StringComparison.Ordinal);
            Assert.DoesNotContain("grand hotel ankara", successLog, StringComparison.Ordinal);
            Assert.DoesNotContain("39.9208", successLog, StringComparison.Ordinal);

            // "Yeniden başlatma": okuma yeni bir cache örneğiyle yapılır (AC-15).
            var reader = new MongoGeocodeCache(collection, NullLogger<MongoGeocodeCache>.Instance);
            var cached = await reader.FindAsync("geoapify", "grand hotel ankara", CancellationToken.None);

            Assert.NotNull(cached);
            Assert.Equal("geoapify", cached.ProviderId);
            Assert.Equal("Grand Hotel, Ankara", cached.DisplayName);
            Assert.Equal(location.Latitude, cached.Location.Latitude, precision: 4);
            Assert.Equal(location.Longitude, cached.Location.Longitude, precision: 4);

            var document = await collection
                .Find(Builders<GeocodeCacheDocument>.Filter.Eq(item => item.Id, "geoapify:grand hotel ankara"))
                .FirstOrDefaultAsync();

            Assert.NotNull(document);
            Assert.Equal("grand hotel ankara", document.NormalizedName);
            Assert.Equal(DateTimeKind.Utc, document.CreatedAt.Kind);

            var health = await new MongoGeocodeCacheHealthCheck(collection.Database)
                .CheckHealthAsync(new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext());
            Assert.Equal(Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy, health.Status);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [MongoFact]
    public async Task Restart_simulation_serves_location_from_cache_without_calling_provider()
    {
        var connectionString = RequireConnectionString();
        var databaseName = $"rpaotelrezervasyon_test_{Guid.NewGuid():N}";
        var client = new MongoClient(connectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<GeocodeCacheDocument>("geocode_cache");

        try
        {
            var location = new GeoPoint(39.9208, 32.8541);
            var writer = new MongoGeocodeCache(collection, NullLogger<MongoGeocodeCache>.Instance);
            await writer.StoreAsync("fake", "grand hotel ankara", location, "Grand Hotel, Ankara", CancellationToken.None);

            // Yeniden başlatma sonrası: yeni cache örneği + yeni sağlayıcı zinciri.
            var restartedCache = new MongoGeocodeCache(collection, NullLogger<MongoGeocodeCache>.Instance);
            var provider = new FakeGeocodingProvider();
            var cachedProvider = new CachedGeocodingProvider(provider, restartedCache);

            var outcome = await cachedProvider.GeocodeAsync("Grand Hotel Ankara", CancellationToken.None);

            Assert.Equal(OutcomeStatus.Found, outcome.Status);
            Assert.Equal(0, provider.CallCount);
            Assert.NotNull(outcome.Location);
            Assert.Equal(location.Latitude, outcome.Location!.Latitude, precision: 4);
            Assert.Equal(location.Longitude, outcome.Location.Longitude, precision: 4);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [MongoFact]
    public async Task StoreAsync_when_called_twice_overwrites_single_document()
    {
        var connectionString = RequireConnectionString();
        var databaseName = $"rpaotelrezervasyon_test_{Guid.NewGuid():N}";
        var client = new MongoClient(connectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<GeocodeCacheDocument>("geocode_cache");

        try
        {
            var cache = new MongoGeocodeCache(collection, NullLogger<MongoGeocodeCache>.Instance);

            await cache.StoreAsync("geoapify", "grand hotel ankara", new GeoPoint(39.9208, 32.8541), null, CancellationToken.None);
            await cache.StoreAsync("geoapify", "grand hotel ankara", new GeoPoint(39.9300, 32.8600), null, CancellationToken.None);

            var count = await collection.CountDocumentsAsync(FilterDefinition<GeocodeCacheDocument>.Empty);
            Assert.Equal(1L, count);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    [MongoFact]
    public async Task FindAsync_when_unknown_name_returns_null()
    {
        var connectionString = RequireConnectionString();
        var databaseName = $"rpaotelrezervasyon_test_{Guid.NewGuid():N}";
        var client = new MongoClient(connectionString);
        var collection = client.GetDatabase(databaseName).GetCollection<GeocodeCacheDocument>("geocode_cache");

        try
        {
            var cache = new MongoGeocodeCache(collection, NullLogger<MongoGeocodeCache>.Instance);

            var cached = await cache.FindAsync("geoapify", "bilinmeyen yer", CancellationToken.None);

            Assert.Null(cached);
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
        }
    }

    private static string RequireConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(MongoFactAttribute.ConnectionVariable);

        return connectionString!;
    }
}
