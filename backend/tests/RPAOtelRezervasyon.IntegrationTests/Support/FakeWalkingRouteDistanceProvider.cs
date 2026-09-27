using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

internal sealed class FakeWalkingRouteDistanceProvider : IWalkingRouteDistanceProvider
{
    private int callCount;

    public string ProviderId => "fake-walk";

    public int CallCount => callCount;

    public Func<GeoPoint, GeoPoint, RouteOutcome> Handler { get; set; } =
        (origin, destination) => RouteOutcome.Found(new RouteMetrics(800, 600, DistanceKind.Road, [origin, destination]));

    public Task<RouteOutcome> GetWalkingRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref callCount);
        return Task.FromResult(Handler(origin, destination));
    }
}
