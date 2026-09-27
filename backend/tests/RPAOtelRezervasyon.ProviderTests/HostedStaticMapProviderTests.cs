using System.Net;
using Microsoft.Extensions.Options;
using System.Text.Json;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;
using RPAOtelRezervasyon.ProviderTests.Support;
using SkiaSharp;

namespace RPAOtelRezervasyon.ProviderTests;

public sealed class HostedStaticMapProviderTests
{
    [Fact]
    public async Task RenderAsync_does_not_synthesize_a_polyline_when_route_geometry_is_missing()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            var dimensions = RequestDimensions(request);
            var content = new ByteArrayContent(CreatePng(dimensions.Width * 2, dimensions.Height * 2));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));
        var venue = new GeoPoint(41, 29);
        var request = new StaticMapRequest(venue,
            [new StaticMapMarker("Geometrisiz otel", new GeoPoint(41.1, 29.1), false)], 640, 360);

        var outcome = await provider.RenderAsync(request, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Single(stub.RequestBodies);
        using var body = JsonDocument.Parse(stub.RequestBodies.Single()!);
        Assert.False(body.RootElement.TryGetProperty("geometries", out _));
        Assert.False(outcome.IncludesDetailView);
    }

    [Fact]
    public async Task RenderAsync_requests_a_png_base_map_and_adds_the_overlay()
    {
        var stub = new StubHttpMessageHandler(_ =>
        {
            var dimensions = RequestDimensions(_);
            var content = new ByteArrayContent(CreatePng(dimensions.Width * 2, dimensions.Height * 2));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal("image/png", outcome.ContentType);
        Assert.Equal("Powered by Geoapify", outcome.Attribution);
        Assert.False(outcome.IncludesDetailView);
        var uris = stub.RequestUris;
        Assert.Single(uris);
        Assert.All(stub.RequestMethods, method => Assert.Equal(HttpMethod.Post, method));
        Assert.All(uris, uri =>
        {
            Assert.Equal("maps.geoapify.com", uri.Host);
            Assert.Contains("apiKey=test-secret", uri.Query, StringComparison.Ordinal);
        });
        Assert.Equal("640x330", stub.RequestBodies
            .Select(body => JsonDocument.Parse(body!).RootElement)
            .Select(root => $"{root.GetProperty("width").GetInt32()}x{root.GetProperty("height").GetInt32()}")
            .Single());
        using var overviewBody = JsonDocument.Parse(stub.RequestBodies.Single()!);
        var overviewRoutes = overviewBody.RootElement.GetProperty("geometries").EnumerateArray()
            .Where(geometry => geometry.GetProperty("linecolor").GetString() != "#ffffff")
            .ToArray();
        Assert.Contains(overviewRoutes, geometry => geometry.GetProperty("linecolor").GetString() == "#EA580C");
        Assert.Contains(overviewRoutes, geometry => geometry.GetProperty("linecolor").GetString() == "#5E35B1");
        Assert.Contains(overviewRoutes, geometry => geometry.GetProperty("linecolor").GetString() == "#1565C0");
        Assert.All(overviewRoutes, geometry => Assert.Equal(2, geometry.GetProperty("linewidth").GetInt32()));
        var selectedComponents = overviewRoutes
            .Where(geometry => geometry.GetProperty("linecolor").GetString() == "#EA580C")
            .ToArray();
        Assert.Equal(2, selectedComponents.Length);
        Assert.Equal(41.012, selectedComponents[0].GetProperty("value")[1].GetProperty("lat").GetDouble(), 3);
        Assert.Equal(41.02, selectedComponents[1].GetProperty("value")[0].GetProperty("lat").GetDouble(), 2);
        var geometries = overviewBody.RootElement.GetProperty("geometries");
        Assert.Equal(8, geometries.GetArrayLength());
        Assert.Equal(new[] { 3, 3, 3, 3, 2, 2, 2, 2 }, geometries.EnumerateArray()
            .Select(geometry => geometry.GetProperty("linewidth").GetInt32()).ToArray());
        var routeColors = geometries.EnumerateArray().Skip(4)
            .Select(geometry => geometry.GetProperty("linecolor").GetString()).ToArray();
        Assert.Equal(new[] { "#EA580C", "#EA580C", "#5E35B1", "#1565C0" }, routeColors);
        Assert.Equal("polyline", geometries[0].GetProperty("type").GetString());
        var firstSegment = geometries[0].GetProperty("value");
        var secondSegment = geometries[1].GetProperty("value");
        Assert.Equal(2, firstSegment.GetArrayLength());
        Assert.Equal(2, secondSegment.GetArrayLength());
        Assert.Equal(41.0082, firstSegment[0].GetProperty("lat").GetDouble(), 4);
        Assert.Equal(41.012, firstSegment[1].GetProperty("lat").GetDouble(), 3);
        Assert.Equal(41.02, secondSegment[0].GetProperty("lat").GetDouble(), 2);
        Assert.Equal(41.06, secondSegment[1].GetProperty("lat").GetDouble(), 2);
        using var decoded = SKBitmap.Decode(outcome.Image!.Value.ToArray());
        Assert.Equal(1280, decoded.Width);
        Assert.Equal(720, decoded.Height);
        var pixels = Enumerable.Range(0, decoded.Width * decoded.Height)
            .Select(index => decoded.GetPixel(index % decoded.Width, index / decoded.Width))
            .ToArray();
        Assert.Contains(pixels, color =>
        {
            return color.Red > 200 && color.Green is > 70 and < 150 && color.Blue < 50;
        });
        Assert.Contains(pixels, color => color.Blue > 150 && color.Red < 80);
        Assert.Contains(pixels, color => color.Red > 180 && color.Green < 60 && color.Blue < 60);
        Assert.Contains(pixels, color => color == StaticMapOverlay.HotelColor(1));
        Assert.Contains(pixels, color => color == StaticMapOverlay.HotelColor(2));
    }

    [Fact]
    public async Task RenderAsync_returns_a_failure_when_the_only_full_width_map_request_fails()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.TransientError, outcome.Status);
        Assert.False(outcome.IncludesDetailView);
        Assert.Null(outcome.Image);
        Assert.Single(stub.RequestUris);
    }

    [Fact]
    public async Task RenderAsync_rejects_an_image_with_dimensions_that_do_not_match_the_full_width_request()
    {
        var stub = new StubHttpMessageHandler(request =>
        {
            var content = new ByteArrayContent(CreatePng(50, 50));
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        });
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "test-secret" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.ProviderError, outcome.Status);
        Assert.Null(outcome.Image);
        Assert.Single(stub.RequestUris);
    }

    [Fact]
    public async Task RenderAsync_without_an_api_key_does_not_call_the_service()
    {
        var stub = new StubHttpMessageHandler(_ => throw new InvalidOperationException("Unexpected network call."));
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions()));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.ProviderError, outcome.Status);
        Assert.Empty(stub.RequestUris);
    }

    [Fact]
    public async Task RenderAsync_maps_http_failures_to_provider_outcomes_without_echoing_the_key()
    {
        var stub = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var client = new HttpClient(stub);
        var provider = new HostedStaticMapProvider(client, Options.Create(new StaticMapOptions { ApiKey = "never-expose-this" }));

        var outcome = await provider.RenderAsync(Request(), CancellationToken.None);

        Assert.Equal(OutcomeStatus.TransientError, outcome.Status);
        Assert.Null(outcome.Image);
        Assert.Null(outcome.Attribution);
    }

    private static byte[] CreatePng(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        surface.Canvas.Clear(SKColors.White);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data!.ToArray();
    }

    private static (int Width, int Height) RequestDimensions(HttpRequestMessage request)
    {
        using var document = JsonDocument.Parse(request.Content!.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = document.RootElement;
        return (root.GetProperty("width").GetInt32(), root.GetProperty("height").GetInt32());
    }

    private static StaticMapRequest Request() => new(
        new GeoPoint(41.0082, 28.9784),
        [
            new StaticMapMarker("Otel", new GeoPoint(41.06, 28.987), true,
                [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.03, 28.98), new GeoPoint(41.06, 28.987)],
                new RouteMetrics(800, 500, DistanceKind.Road,
                    [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.012, 28.98), new GeoPoint(41.02, 28.982), new GeoPoint(41.06, 28.987)],
                    [[new GeoPoint(41.0082, 28.9784), new GeoPoint(41.012, 28.98)], [new GeoPoint(41.02, 28.982), new GeoPoint(41.06, 28.987)]]),
                [[new GeoPoint(41.0082, 28.9784), new GeoPoint(41.012, 28.98)], [new GeoPoint(41.02, 28.982), new GeoPoint(41.06, 28.987)]]),
            new StaticMapMarker("Diğer otel", new GeoPoint(41.02, 28.95), false, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.015, 28.965), new GeoPoint(41.02, 28.95)], new RouteMetrics(400, 300, DistanceKind.Road, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.02, 28.95)])),
            new StaticMapMarker("Üçüncü otel", new GeoPoint(41.03, 29.01), false, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.02, 29.0), new GeoPoint(41.03, 29.01)], new RouteMetrics(500, 400, DistanceKind.Road, [new GeoPoint(41.0082, 28.9784), new GeoPoint(41.03, 29.01)])),
        ],
        640,
        360);
}
