using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.DistanceRouting;

/// <summary>
/// Konum çifti için mesafe/süre üretir (BR-2).
/// <list type="bullet">
/// <item>Yol mesafesi bulunursa onu kullanır.</item>
/// <item>Sağlayıcı "rota yok" derse düz çizgi fallback'ine düşer ve <see cref="DistanceKind.StraightLine"/>
/// ile işaretler; **süre tahmin edilmez**, 0 (ölçülmedi) olur (AC-11, ADR-0005).</item>
/// <item>Sağlayıcı geçici hata verirse <c>null</c> döner; otel ölçülemedi sayılır ve sıralamaya girmez (AC-7).</item>
/// </list>
/// </summary>
public sealed class DistanceRoutingService(IRouteDistanceProvider routeDistanceProvider)
{
    private const double EarthRadiusMeters = 6_371_000;

    public async Task<RouteMetrics?> ResolveMetricsAsync(
        GeoPoint origin,
        GeoPoint destination,
        CancellationToken cancellationToken)
    {
        var outcome = await routeDistanceProvider.GetRouteAsync(origin, destination, cancellationToken).ConfigureAwait(false);

        return outcome.Status switch
        {
            OutcomeStatus.Found => outcome.Metrics,
            OutcomeStatus.NotFound => StraightLineFallback(origin, destination),
            _ => null,
        };
    }

    private static RouteMetrics StraightLineFallback(GeoPoint origin, GeoPoint destination)
    {
        var distanceMeters = (int)Math.Round(HaversineMeters(origin, destination), MidpointRounding.AwayFromZero);

        // ADR-0005: ölçülmemiş süre için tahmin üretilmez. 0 = "ölçülmedi"; sıralamada süre olarak
        // kullanılmaz (mesafe türü StraightLine olduğundan ölçülmüş yolların ardında yer alır).
        return new RouteMetrics(distanceMeters, durationSeconds: 0, DistanceKind.StraightLine);
    }

    private static double HaversineMeters(GeoPoint origin, GeoPoint destination)
    {
        var latitudeDelta = ToRadians(destination.Latitude - origin.Latitude);
        var longitudeDelta = ToRadians(destination.Longitude - origin.Longitude);

        var a = Math.Sin(latitudeDelta / 2) * Math.Sin(latitudeDelta / 2)
            + Math.Cos(ToRadians(origin.Latitude)) * Math.Cos(ToRadians(destination.Latitude))
            * Math.Sin(longitudeDelta / 2) * Math.Sin(longitudeDelta / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return EarthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
