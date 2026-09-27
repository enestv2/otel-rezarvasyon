using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Caching;

/// <summary>
/// BR-4: geocoding'den **önce** kalıcı önbellek kontrol edilir; isabet varsa dış sağlayıcı hiç
/// çağrılmaz (AC-14). Önbellek anahtarı normalize edilmiş ad + sağlayıcı kimliğidir (AC-16, G-7);
/// başarısız çözümlemeler yazılmaz (G-8).
/// </summary>
public sealed class CachedGeocodingProvider(IGeocodingProvider inner, IGeocodeCache cache) : IGeocodingProvider
{
    public string ProviderId => inner.ProviderId;

    public Task<GeocodingOutcome> GeocodeAsync(string placeName, CancellationToken cancellationToken) =>
        GeocodeAsync(placeName, GeocodingContext.Empty, cancellationToken);

    public async Task<GeocodingOutcome> GeocodeAsync(
        string placeName,
        GeocodingContext context,
        CancellationToken cancellationToken)
    {
        var normalizedName = Normalize(placeName);
        var cacheKey = BuildContextKey(normalizedName, context);

        var cached = await cache.FindAsync(inner.ProviderId, cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return GeocodingOutcome.Found(inner.ProviderId, cached.Location, cached.DisplayName);
        }

        var outcome = await inner.GeocodeAsync(placeName, context, cancellationToken).ConfigureAwait(false);

        if (outcome.Status == OutcomeStatus.Found && outcome.Location is not null)
        {
            await cache.StoreAsync(
                inner.ProviderId,
                cacheKey,
                outcome.Location,
                outcome.DisplayName,
                cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }

    private static string BuildContextKey(string name, GeocodingContext context) =>
        $"{Escape(NormalizeOptional(context.City))}|{Escape(NormalizeOptional(context.Country))}|{Escape(name)}";

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : Normalize(value);

    private static string Escape(string value) => Uri.EscapeDataString(value);

    /// <summary>G-7: kırp + ardışık boşlukları teke indir + invariant küçük harf. Aksan dönüşümü yok.</summary>
    private static string Normalize(string placeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeName);

        var parts = placeName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return string.Join(' ', parts).ToLowerInvariant();
    }
}
