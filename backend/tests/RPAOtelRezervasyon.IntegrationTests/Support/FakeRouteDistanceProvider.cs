using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

/// <summary>Uçtan uca testlerde ağa çıkmayan routing sağlayıcısı; varsayılan yanıt "rota yok".</summary>
internal sealed class FakeRouteDistanceProvider : IRouteDistanceProvider
{
    private int _callCount;

    public string ProviderId => "fake";

    public int CallCount => _callCount;

    public Func<GeoPoint, GeoPoint, RouteOutcome> Handler { get; set; } = (_, _) => RouteOutcome.NotFound();

    public Task<RouteOutcome> GetRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _callCount);

        return Task.FromResult(Handler(origin, destination));
    }
}
