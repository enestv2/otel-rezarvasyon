using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

/// <summary>Uçtan uca testlerde ağa çıkmayan geocoding sağlayıcısı; çağrı sayısını kaydeder.</summary>
internal sealed class FakeGeocodingProvider : IGeocodingProvider
{
    private int _callCount;

    public string ProviderId => "fake";

    public Dictionary<string, GeoPoint> Locations { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int CallCount => _callCount;

    /// <summary>Konumu bilinmeyen adlar için dönecek durum (varsayılan NotFound).</summary>
    public OutcomeStatus MissingStatus { get; set; } = OutcomeStatus.NotFound;

    public Task<GeocodingOutcome> GeocodeAsync(string placeName, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);

        if (Locations.TryGetValue(placeName.Trim(), out var location))
        {
            return Task.FromResult(GeocodingOutcome.Found(ProviderId, location, placeName));
        }

        return Task.FromResult(MissingStatus switch
        {
            OutcomeStatus.TransientError => GeocodingOutcome.TransientError(ProviderId),
            OutcomeStatus.ProviderError => GeocodingOutcome.ProviderError(ProviderId),
            _ => GeocodingOutcome.NotFound(ProviderId),
        });
    }
}
