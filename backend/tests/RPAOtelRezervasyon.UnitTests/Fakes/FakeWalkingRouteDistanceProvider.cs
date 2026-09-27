using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Fakes;

public sealed class FakeWalkingRouteDistanceProvider : IWalkingRouteDistanceProvider
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(GeoPoint Origin, GeoPoint Destination)> calls = new();

    public string ProviderId => "fake-walk";

    public IReadOnlyCollection<(GeoPoint Origin, GeoPoint Destination)> Calls => calls.ToArray();

    public Func<GeoPoint, GeoPoint, RouteOutcome> RouteResolver { get; set; } =
        (origin, destination) => RouteOutcome.Found(new RouteMetrics(850, 620, DistanceKind.Road, [origin, destination]));

    public Task<RouteOutcome> GetWalkingRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken)
    {
        calls.Enqueue((origin, destination));
        return Task.FromResult(RouteResolver(origin, destination));
    }
}
