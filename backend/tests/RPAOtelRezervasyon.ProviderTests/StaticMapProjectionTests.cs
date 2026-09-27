using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class StaticMapProjectionTests
{
    [Fact]
    public void Fit_centers_venue_and_hotel_coordinates_in_the_map_viewport()
    {
        var request = Request();
        var projection = StaticMapProjection.Fit(request, 18);

        var center = projection.ToPixel(request.Venue, request);
        Assert.InRange(center.X, 0, request.WidthPx);
        Assert.InRange(center.Y, 0, request.HeightPx);
        Assert.InRange(projection.Zoom, 1, 18);
    }

    [Fact]
    public void Fit_can_keep_the_detail_view_local_when_a_route_has_a_long_detour()
    {
        var venue = new GeoPoint(41, 29);
        var hotel = new GeoPoint(41.002, 29.002);
        var request = new StaticMapRequest(
            venue,
            [new StaticMapMarker("Otel", hotel, true, [venue, new GeoPoint(40.4, 29.8), hotel])],
            288,
            162);

        var routeBounds = StaticMapProjection.Fit(request, 18);
        var localBounds = StaticMapProjection.Fit(request, 18, includeRouteGeometry: false);

        Assert.True(localBounds.Zoom >= routeBounds.Zoom + 3,
            $"Detail should stay around the venue and hotel rather than follow the detour: {localBounds.Zoom} <= {routeBounds.Zoom}.");
        var eventPixel = localBounds.ToPixel(venue, request);
        var hotelPixel = localBounds.ToPixel(hotel, request);
        Assert.InRange(eventPixel.X, 0, request.WidthPx);
        Assert.InRange(hotelPixel.X, 0, request.WidthPx);
    }

    private static StaticMapRequest Request() => new(
        new GeoPoint(41.0082, 28.9784),
        [new StaticMapMarker("Otel", new GeoPoint(41.06, 28.987), true)],
        640,
        360);
}
