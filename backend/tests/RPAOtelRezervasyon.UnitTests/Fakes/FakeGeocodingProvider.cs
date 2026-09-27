using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using System.Collections.Concurrent;

namespace RPAOtelRezervasyon.UnitTests.Fakes;

/// <summary>Test amaçlı geocoding sağlayıcısı; ağa çıkmaz ve çağrı sayısını sayar.</summary>
public sealed class FakeGeocodingProvider(string providerId = "fake") : IGeocodingProvider
{
    private readonly Dictionary<string, GeoPoint> _locations = new(StringComparer.OrdinalIgnoreCase);

    public string ProviderId { get; } = providerId;

    public int CallCount { get; private set; }

    public ConcurrentQueue<GeocodingContext> Contexts { get; } = new();

    /// <summary>Ad bulunamadığında dönecek durum (NotFound veya TransientError).</summary>
    public OutcomeStatus MissingStatus { get; set; } = OutcomeStatus.NotFound;

    public FakeGeocodingProvider With(string placeName, double latitude, double longitude)
    {
        _locations[placeName] = new GeoPoint(latitude, longitude);
        return this;
    }

    public Task<GeocodingOutcome> GeocodeAsync(string placeName, CancellationToken cancellationToken)
    {
        CallCount++;

        if (_locations.TryGetValue(placeName, out var location))
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

    public Task<GeocodingOutcome> GeocodeAsync(string placeName, GeocodingContext context, CancellationToken cancellationToken)
    {
        Contexts.Enqueue(context);
        return GeocodeAsync(placeName, cancellationToken);
    }
}
