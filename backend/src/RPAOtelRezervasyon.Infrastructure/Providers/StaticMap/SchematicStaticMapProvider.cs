using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using SkiaSharp;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

/// <summary>
/// External-network-free map renderer. It displays all ranked markers and only route geometries
/// returned by the provider; it does not invent straight connectors for missing routes.
/// </summary>
public sealed class SchematicStaticMapProvider : IStaticMapProvider
{
    public const string Id = "schematic";

    private const int DefaultPaddingPx = 28;
    private const double MinimumSpanDegrees = 0.002;

    private static readonly SKColor BackgroundColor = new(248, 250, 252);
    private static readonly SKColor BorderColor = new(226, 232, 240);

    public string ProviderId => Id;

    public Task<StaticMapOutcome> RenderAsync(StaticMapRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var overviewRequest = StaticMapComposition.CreateOverviewRequest(request);
            using var overview = RenderView(overviewRequest, overviewRequest.WidthPx, overviewRequest.HeightPx,
                StaticMapComposition.OverviewMarkerRanks(request), selectedRouteOnly: false);
            if (overview is null)
            {
                return Task.FromResult(StaticMapOutcome.ProviderError(Id));
            }

            var composed = StaticMapComposition.ComposePng(overview,
                StaticMapComposition.HeaderHeight(request.HeightPx), scaleFactor: 1);
            return Task.FromResult(StaticMapOutcome.Found(Id, composed, "image/png", includesDetailView: false));
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(StaticMapOutcome.ProviderError(Id));
        }
    }

    private static SKBitmap? RenderView(StaticMapRequest request, int width, int height, IReadOnlyList<int> markerRanks,
        bool selectedRouteOnly = false, bool includeRouteGeometry = true)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        if (surface is null)
        {
            return null;
        }

        var canvas = surface.Canvas;
        canvas.Clear(BackgroundColor);
        DrawBorder(canvas, width, height);
        var project = CreateProjector(request, width, height, selectedRouteOnly, includeRouteGeometry);
        StaticMapOverlay.Draw(canvas, request, project, markerRanks, selectedRouteOnly);
        using var image = surface.Snapshot();
        return SKBitmap.FromImage(image);
    }

    private static Func<GeoPoint, SKPoint> CreateProjector(StaticMapRequest request, int width, int height,
        bool selectedRouteOnly = false, bool includeRouteGeometry = true)
    {
        var latitudes = new List<double>(request.Markers.Count + 1) { request.Venue.Latitude };
        var longitudes = new List<double>(request.Markers.Count + 1) { request.Venue.Longitude };
        foreach (var marker in request.Markers)
        {
            latitudes.Add(marker.Location.Latitude);
            longitudes.Add(marker.Location.Longitude);

            if ((selectedRouteOnly && !marker.IsSelected) || !includeRouteGeometry)
            {
                continue;
            }

            foreach (var point in marker.PathSegments.SelectMany(segment => segment))
            {
                latitudes.Add(point.Latitude);
                longitudes.Add(point.Longitude);
            }
        }

        var (minLatitude, maxLatitude) = EnsureSpan(latitudes.Min(), latitudes.Max());
        var (minLongitude, maxLongitude) = EnsureSpan(longitudes.Min(), longitudes.Max());
        var maximumPadding = Math.Max(0, (Math.Min(width, height) - 1) / 2.0);
        var padding = (float)Math.Min(DefaultPaddingPx, maximumPadding);
        var drawableWidth = width - (2 * padding);
        var drawableHeight = height - (2 * padding);
        float ToX(double longitude) => (float)(padding + ((longitude - minLongitude) / (maxLongitude - minLongitude) * drawableWidth));
        float ToY(double latitude) => (float)(padding + ((maxLatitude - latitude) / (maxLatitude - minLatitude) * drawableHeight));
        return point => new SKPoint(ToX(point.Longitude), ToY(point.Latitude));
    }

    private static void DrawBorder(SKCanvas canvas, int width, int height)
    {
        using var paint = new SKPaint { Color = BorderColor, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(0.5f, 0.5f, width - 0.5f, height - 0.5f), 8, 8, paint);
    }

    /// <summary>Bir eksendeki yayılım sıfıra yakınsa (tek nokta/aynı konum) merkezi koruyarak açar.</summary>
    private static (double Min, double Max) EnsureSpan(double min, double max)
    {
        var span = max - min;
        if (span >= MinimumSpanDegrees)
        {
            return (min, max);
        }

        var center = (min + max) / 2;
        return (center - (MinimumSpanDegrees / 2), center + (MinimumSpanDegrees / 2));
    }
}
