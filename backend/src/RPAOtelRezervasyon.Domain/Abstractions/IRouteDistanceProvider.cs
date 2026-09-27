using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>İki konum arasındaki yol mesafesi/süresi. Sağlayıcı değişebilir (ADR-0001).</summary>
public interface IRouteDistanceProvider
{
    string ProviderId { get; }

    Task<RouteOutcome> GetRouteAsync(GeoPoint origin, GeoPoint destination, CancellationToken cancellationToken);
}
