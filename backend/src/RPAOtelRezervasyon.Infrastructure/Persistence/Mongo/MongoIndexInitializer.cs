using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

/// <summary>
/// Açılışta konum önbelleği indekslerini kurar. BR-7: `createdAt` üzerinde TTL indeksi; süre
/// <see cref="MongoGeocodeCacheOptions.CacheTtlDays"/> ile yapılandırılır.
/// </summary>
public sealed class MongoIndexInitializer(
    IMongoCollection<GeocodeCacheDocument> collection,
    IOptions<MongoGeocodeCacheOptions> options,
    ILogger<MongoIndexInitializer> logger) : IHostedService
{
    private const string TtlIndexName = "createdAt_ttl";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var expireAfter = TimeSpan.FromDays(options.Value.CacheTtlDays);
        var keys = Builders<GeocodeCacheDocument>.IndexKeys.Ascending(document => document.CreatedAt);
        var model = new CreateIndexModel<GeocodeCacheDocument>(
            keys,
            new CreateIndexOptions { Name = TtlIndexName, ExpireAfter = expireAfter });

        try
        {
            await collection.Indexes.CreateOneAsync(model, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (MongoException ex)
        {
            logger.LogWarning(ex, "Konum önbelleği TTL indeksi kurulamadı; önbellek TTL olmadan çalışacak.");
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex, "Konum önbelleği TTL indeksi kurulamadı; önbellek TTL olmadan çalışacak.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
