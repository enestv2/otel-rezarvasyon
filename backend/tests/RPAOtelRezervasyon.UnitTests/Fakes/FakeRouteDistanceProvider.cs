using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Fakes;

/// <summary>Test amaçlı routing sağlayıcısı; varsayılan olarak yol rotası döner.</summary>
public sealed class FakeRouteDistanceProvider(string providerId = "fake") : IRouteDistanceProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(GeoPoint Origin, GeoPoint Destination)> calls = new();

    public string ProviderId { get; } = providerId;

    public int CallCount { get; private set; }

    public IReadOnlyCollection<(GeoPoint Origin, GeoPoint Destination)> Calls => calls.ToArray();

    public Func<GeoPoint, GeoPoint, RouteOutcome> RouteResolver { get; set; } =
        (_, _) => RouteOutcome.Found(new RouteMetrics(1000, 600, DistanceKind.Road));

    public Task<RouteOutcome> GetRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken)
    {
        CallCount++;
        calls.Enqueue((origin, destination));

        return Task.FromResult(RouteResolver(origin, destination));
    }
}
