using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>Kalıcı önbellekten okunan konum kaydı.</summary>
public sealed record CachedLocation(GeoPoint Location, string ProviderId, string? DisplayName, DateTimeOffset CreatedAt);

/// <summary>
/// Ad → konum eşlemesinin kalıcı önbelleği (BR-4, BR-7). İsabet varsa dış sağlayıcıya gidilmez.
/// Sağlayıcı kimliği anahtarın parçasıdır; sağlayıcı değişince eski konumlar karışmaz.
/// </summary>
public interface IGeocodeCache
{
    Task<CachedLocation?> FindAsync(string providerId, string placeName, CancellationToken cancellationToken);

    Task StoreAsync(
        string providerId,
        string placeName,
        GeoPoint location,
        string? displayName,
        CancellationToken cancellationToken);
}
