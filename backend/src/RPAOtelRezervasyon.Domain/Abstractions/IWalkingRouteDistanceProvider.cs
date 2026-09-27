using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>Etkinlik alanı ile otel arasındaki yürüme rotasını ölçer.</summary>
public interface IWalkingRouteDistanceProvider
{
    string ProviderId { get; }

    Task<RouteOutcome> GetWalkingRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken);
}
