using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

/// <summary>
/// <see cref="IGeocodeCache"/>'in MongoDB uygulaması (ADR-0002). Önbellek zorunlu bir bağımlılık
/// değildir: Mongo erişilemezse (G-6) istek düşmez; okuma isabetsiz, yazma ise sessizce atlanır ve
/// servis dış sağlayıcıyla çalışmaya devam eder.
/// <para>
/// Sözleşme: <c>placeName</c> çağıran tarafından zaten normalize edilmiş gelir (kırp + ardışık
/// boşlukları teke indir + invariant küçük harf — G-7). Uygulama ikinci kez normalleştirmez.
/// </para>
/// </summary>
public sealed class MongoGeocodeCache(IMongoCollection<GeocodeCacheDocument> collection, ILogger<MongoGeocodeCache> logger)
    : IGeocodeCache
{
    public async Task<CachedLocation?> FindAsync(string providerId, string placeName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(placeName);

        var id = BuildId(providerId, placeName);

        try
        {
            var filter = Builders<GeocodeCacheDocument>.Filter.Eq(document => document.Id, id);
            var document = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            return document is null ? null : ToCachedLocation(document);
        }
        catch (MongoException ex)
        {
            LogCacheUnavailable(ex);
            return null;
        }
        catch (TimeoutException ex)
        {
            LogCacheUnavailable(ex);
            return null;
        }
    }

    public async Task StoreAsync(
        string providerId,
        string placeName,
        GeoPoint location,
        string? displayName,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(placeName);
        ArgumentNullException.ThrowIfNull(location);

        var document = new GeocodeCacheDocument
        {
            Id = BuildId(providerId, placeName),
            Name = placeName,
            NormalizedName = placeName,
            ProviderId = providerId,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            DisplayName = displayName,
            CreatedAt = DateTime.UtcNow,
        };

        try
        {
            var filter = Builders<GeocodeCacheDocument>.Filter.Eq(existing => existing.Id, document.Id);
            await collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation("Geocode cache upsert completed.");
        }
        catch (MongoException ex)
        {
            LogCacheUnavailable(ex);
        }
        catch (TimeoutException ex)
        {
            LogCacheUnavailable(ex);
        }
    }

    /// <summary>Önbellek anahtarı konvansiyonu: <c>&lt;sağlayıcı&gt;:&lt;normalize-ad&gt;</c> (docs/conventions.md).</summary>
    internal static string BuildId(string providerId, string placeName) =>
        $"{providerId.ToLowerInvariant()}:{placeName}";

    private static CachedLocation ToCachedLocation(GeocodeCacheDocument document)
    {
        var location = new GeoPoint(document.Latitude, document.Longitude);
        var createdAt = new DateTimeOffset(DateTime.SpecifyKind(document.CreatedAt, DateTimeKind.Utc));

        return new CachedLocation(location, document.ProviderId, document.DisplayName, createdAt);
    }

    private void LogCacheUnavailable(Exception exception) =>
        logger.LogWarning("Geocode cache operation skipped ({ExceptionType}).", exception.GetType().Name);
}
