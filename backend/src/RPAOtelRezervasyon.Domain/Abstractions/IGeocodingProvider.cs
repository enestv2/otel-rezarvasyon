using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>Ad → konum çevirimi. Sağlayıcı değişebilir (ADR-0001); çekirdek yalnızca bu arayüzü bilir.</summary>
public interface IGeocodingProvider
{
    string ProviderId { get; }

    Task<GeocodingOutcome> GeocodeAsync(string placeName, CancellationToken cancellationToken);

    Task<GeocodingOutcome> GeocodeAsync(
        string placeName,
        GeocodingContext context,
        CancellationToken cancellationToken) => GeocodeAsync(placeName, cancellationToken);
}
