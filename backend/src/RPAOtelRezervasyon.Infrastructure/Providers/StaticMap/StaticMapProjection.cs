using RPAOtelRezervasyon.Domain.Models;
using SkiaSharp;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

internal sealed record StaticMapProjection(double CenterLongitude, double CenterLatitude, double Zoom)
{
    private const double TileSize = 256;
    private const double MaxLatitude = 85.05112878;

    public static StaticMapProjection Fit(StaticMapRequest request, int maxZoom, int paddingPx = 32,
        bool selectedRouteOnly = false, bool includeRouteGeometry = true)
    {
        var points = EnumeratePoints(request, selectedRouteOnly, includeRouteGeometry).ToArray();
        var minLongitude = points.Min(point => point.Longitude);
        var maxLongitude = points.Max(point => point.Longitude);
        var minY = points.Min(point => MercatorY(point.Latitude));
        var maxY = points.Max(point => MercatorY(point.Latitude));
        var centerLongitude = (minLongitude + maxLongitude) / 2;
        var centerY = (minY + maxY) / 2;
        var centerLatitude = InverseMercatorY(centerY);
        var availableWidth = Math.Max(1, request.WidthPx - (paddingPx * 2));
        var availableHeight = Math.Max(1, request.HeightPx - (paddingPx * 2));
        var zoom = 1d;

        for (var candidate = maxZoom; candidate >= 1; candidate--)
        {
            var scale = TileSize * Math.Pow(2, candidate);
            if ((maxLongitude - minLongitude) / 360 * scale <= availableWidth &&
                (maxY - minY) * scale <= availableHeight)
            {
                zoom = candidate;
                break;
            }
        }

        return new StaticMapProjection(centerLongitude, centerLatitude, zoom);
    }

    public SKPoint ToPixel(GeoPoint point, StaticMapRequest request)
    {
        var scale = TileSize * Math.Pow(2, Zoom);
        var longitudeOffset = (point.Longitude - CenterLongitude) / 360 * scale;
        var latitudeOffset = (MercatorY(point.Latitude) - MercatorY(CenterLatitude)) * scale;
        return new SKPoint(
            (float)(request.WidthPx / 2d + longitudeOffset),
            (float)(request.HeightPx / 2d + latitudeOffset));
    }

    private static IEnumerable<GeoPoint> EnumeratePoints(StaticMapRequest request, bool selectedRouteOnly, bool includeRouteGeometry)
    {
        yield return request.Venue;
        foreach (var marker in request.Markers)
        {
            yield return marker.Location;
            if (selectedRouteOnly && !marker.IsSelected)
            {
                continue;
            }

            if (!includeRouteGeometry)
            {
                continue;
            }

            foreach (var point in marker.PathSegments.SelectMany(segment => segment))
            {
                yield return point;
            }
        }
    }

    private static double MercatorY(double latitude)
    {
        var clamped = Math.Clamp(latitude, -MaxLatitude, MaxLatitude) * Math.PI / 180;
        return (1 - Math.Log(Math.Tan(clamped) + (1 / Math.Cos(clamped))) / Math.PI) / 2;
    }

    private static double InverseMercatorY(double y) =>
        Math.Atan(Math.Sinh(Math.PI * (1 - (2 * y)))) * 180 / Math.PI;
}
