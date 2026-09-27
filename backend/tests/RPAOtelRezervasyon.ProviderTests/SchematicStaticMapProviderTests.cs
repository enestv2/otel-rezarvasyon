using System.Buffers.Binary;
using SkiaSharp;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

namespace RPAOtelRezervasyon.ProviderTests;

/// <summary>
/// Şematik coğrafi görsel sağlayıcısı: PNG üretimi, boyutlar, determinizm ve dejenere yayılım.
/// Ağa çıkılmaz.
/// </summary>
public sealed class SchematicStaticMapProviderTests
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    [Fact]
    public async Task RenderAsync_uses_only_a_full_width_overview_even_when_walking_routes_exist()
    {
        var venue = new GeoPoint(41, 29);
        var hotel = new GeoPoint(41.001, 29.001);
        var request = new StaticMapRequest(venue,
        [
            new StaticMapMarker("Otel 1", hotel, true, [venue, hotel],
                new RouteMetrics(100, 60, DistanceKind.Road, [venue, hotel])),
            new StaticMapMarker("Otel 2", new GeoPoint(41.002, 29.002), false),
            new StaticMapMarker("Otel 3", new GeoPoint(41.003, 29.003), false),
        ], 640, 360);

        var outcome = await new SchematicStaticMapProvider().RenderAsync(request, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.False(outcome.IncludesDetailView);
        using var bitmap = SKBitmap.Decode(outcome.Image!.Value.ToArray())!;
        Assert.Equal((640, 360), (bitmap.Width, bitmap.Height));
    }

    [Fact]
    public void Overlay_does_not_draw_a_straight_connector_when_route_geometry_is_missing()
    {
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.White);
        var venue = new GeoPoint(41, 29);
        var hotel = new GeoPoint(41.1, 29.1);
        var request = new StaticMapRequest(venue, [new StaticMapMarker("Otel", hotel, false)], 100, 100);

        StaticMapOverlay.Draw(surface.Canvas, request,
            point => new SKPoint(point == venue ? 10 : 90, point == venue ? 10 : 90));

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.White, bitmap.GetPixel(50, 50));
    }

    [Fact]
    public async Task RenderAsync_returns_a_png_with_the_requested_dimensions()
    {
        var provider = new SchematicStaticMapProvider();
        var request = Request(width: 320, height: 180);

        var outcome = await provider.RenderAsync(request, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal(SchematicStaticMapProvider.Id, outcome.ProviderId);
        Assert.Equal("image/png", outcome.ContentType);

        var image = outcome.Image!.Value.ToArray();
        Assert.Equal(PngSignature, image.Take(8));

        // IHDR genişlik/yükseklik alanları (big-endian) istekle eşleşmelidir.
        Assert.Equal(320, BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(16, 4)));
        Assert.Equal(180, BinaryPrimitives.ReadInt32BigEndian(image.AsSpan(20, 4)));
    }

    [Fact]
    public async Task RenderAsync_composes_a_full_width_overview_with_every_ranked_hotel()
    {
        var venue = new GeoPoint(41, 29);
        var markers = Enumerable.Range(1, 7)
            .Select(index => new StaticMapMarker(
                $"Hotel {index}",
                new GeoPoint(41 + (index == 7 ? 0.2 : index * 0.001), 29 + (index == 7 ? 0.2 : index * 0.001)),
                index == 1,
                [venue, new GeoPoint(41 + (index == 7 ? 0.2 : index * 0.001), 29 + (index == 7 ? 0.2 : index * 0.001))],
                new RouteMetrics(index * 100, index * 60, DistanceKind.Road,
                    [venue, new GeoPoint(41 + (index == 7 ? 0.2 : index * 0.001), 29 + (index == 7 ? 0.2 : index * 0.001))])))
            .Reverse()
            .ToArray();
        var request = new StaticMapRequest(venue, markers, 640, 360);

        var outcome = await new SchematicStaticMapProvider().RenderAsync(request, CancellationToken.None);
        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.False(outcome.IncludesDetailView);
        using var bitmap = SKBitmap.Decode(outcome.Image!.Value.ToArray())!;
        Assert.Equal((640, 360), (bitmap.Width, bitmap.Height));
        Assert.Equal(Enumerable.Range(0, 7), StaticMapComposition.OverviewMarkerRanks(request));
        Assert.Equal(markers.Select(marker => marker.Label), StaticMapComposition.CreateOverviewRequest(request).Markers.Select(marker => marker.Label));
        Assert.Equal(7, StaticMapComposition.OverviewMarkerRanks(request).Count);
    }

    [Fact]
    public void Overview_composition_keeps_all_markers_in_ranked_list_order()
    {
        var venue = new GeoPoint(41, 29);
        var firstHotel = new GeoPoint(41.001, 29);
        var geometryLessHotel = new GeoPoint(41.002, 29);
        var secondHotel = new GeoPoint(40.999, 29);
        var thirdHotel = new GeoPoint(41.01, 29);
        var request = new StaticMapRequest(venue,
        [
            new StaticMapMarker("First", firstHotel, false, walkingMetrics: new RouteMetrics(1800, 1200, DistanceKind.Road, [venue, firstHotel])),
            new StaticMapMarker("No geometry", geometryLessHotel, false, walkingMetrics: new RouteMetrics(400, 300, DistanceKind.Road)),
            new StaticMapMarker("Second", secondHotel, false, walkingMetrics: new RouteMetrics(900, 600, DistanceKind.Road, [venue, secondHotel])),
            new StaticMapMarker("Third", thirdHotel, false, walkingMetrics: new RouteMetrics(200, 180, DistanceKind.Road, [venue, thirdHotel])),
        ], 640, 360);

        var overview = StaticMapComposition.CreateOverviewRequest(request);

        Assert.Equal(new[] { 0, 1, 2, 3 }, StaticMapComposition.OverviewMarkerRanks(request));
        Assert.Equal(new[] { "First", "No geometry", "Second", "Third" }, overview.Markers.Select(marker => marker.Label));
    }

    [Fact]
    public void Overview_composition_uses_the_entire_available_width()
    {
        var venue = new GeoPoint(41, 29);
        var hotel = new GeoPoint(41.001, 29);
        var request = new StaticMapRequest(venue,
        [
            new StaticMapMarker("Hotel", hotel, false, walkingMetrics: new RouteMetrics(900, 600, DistanceKind.Road, [venue, hotel])),
            new StaticMapMarker("No geometry", new GeoPoint(41.002, 29), false, walkingMetrics: new RouteMetrics(400, 300, DistanceKind.Road)),
        ], 640, 360);

        var overview = StaticMapComposition.CreateOverviewRequest(request);

        Assert.Equal(640, overview.WidthPx);
        Assert.Equal(330, overview.HeightPx);
        Assert.Equal(request.Markers, overview.Markers);
    }

    [Fact]
    public void Overview_composition_retains_markers_without_route_geometry()
    {
        var point = new GeoPoint(41, 29);
        var request = new StaticMapRequest(point, [new StaticMapMarker("No geometry", point, false)], 640, 360);

        var overview = StaticMapComposition.CreateOverviewRequest(request);

        Assert.Equal(request.Markers, overview.Markers);
        Assert.Empty(overview.Markers[0].PathSegments);
    }

    [Fact]
    public void Overlay_does_not_draw_a_guessed_line_when_route_geometry_is_missing()
    {
        var point = new GeoPoint(41, 29);
        var marker = new StaticMapMarker("Hotel", new GeoPoint(41.01, 29.01), false,
            walkingMetrics: new RouteMetrics(900, 600, DistanceKind.Road));
        var request = new StaticMapRequest(point, [marker], 100, 100);
        using var surface = SKSurface.Create(new SKImageInfo(100, 100));
        surface.Canvas.Clear(SKColors.White);
        StaticMapOverlay.Draw(surface.Canvas, request,
            location => location == point ? new SKPoint(10, 10) : new SKPoint(90, 90));
        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.White, bitmap.GetPixel(50, 50));
    }
    [Fact]
    public async Task RenderAsync_is_deterministic_for_the_same_request()
    {
        var provider = new SchematicStaticMapProvider();

        var first = await provider.RenderAsync(Request(width: 200, height: 120), CancellationToken.None);
        var second = await provider.RenderAsync(Request(width: 200, height: 120), CancellationToken.None);

        Assert.Equal(first.Image!.Value.ToArray(), second.Image!.Value.ToArray());
    }

    [Fact]
    public async Task RenderAsync_handles_a_degenerate_span_when_all_points_are_identical()
    {
        var provider = new SchematicStaticMapProvider();
        var point = new GeoPoint(41.0, 29.0);
        var request = new StaticMapRequest(
            point,
            [new StaticMapMarker("Tek Otel", point, true), new StaticMapMarker("Diğer Otel", point, false)],
            160,
            120);

        var outcome = await provider.RenderAsync(request, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.NotEmpty(outcome.Image!.Value.ToArray());
    }

    [Fact]
    public async Task RenderAsync_handles_a_one_pixel_canvas_without_failing()
    {
        var provider = new SchematicStaticMapProvider();

        var outcome = await provider.RenderAsync(Request(width: 1, height: 1), CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);
        Assert.Equal(PngSignature, outcome.Image!.Value.ToArray().Take(8));
    }

    [Fact]
    public async Task RenderAsync_cancels_when_the_token_is_already_cancelled()
    {
        var provider = new SchematicStaticMapProvider();
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.RenderAsync(Request(width: 200, height: 120), source.Token));
    }

    [Fact]
    public async Task RenderAsync_draws_the_route_path_and_keeps_it_in_frame()
    {
        var venue = new GeoPoint(41.0, 29.0);
        var hotel = new GeoPoint(41.0, 29.2);
        var detour = new[]
        {
            new GeoPoint(41.0, 29.0),
            new GeoPoint(40.85, 29.1),
            new GeoPoint(41.0, 29.2),
        };

        var straight = await RenderAsync(new StaticMapRequest(venue, [new StaticMapMarker("Otel", hotel, true)], 400, 300));
        var withPath = await RenderAsync(new StaticMapRequest(venue, [new StaticMapMarker("Otel", hotel, true, detour)], 400, 300));

        // AC-13: güzergâh verilince görsel düz çizgiden farklı olur.
        Assert.NotEqual(straight, withPath);

        var straightBottom = LowestSelectedPixelY(straight);
        var pathBottom = LowestSelectedPixelY(withPath);

        Assert.True(straightBottom >= 0, "Düz çizgi görselinde seçili renk bulunamadı.");
        Assert.True(
            pathBottom > straightBottom,
            $"Güzergâh çizgisi düz çizgiden aşağıya inmiyor: {pathBottom} <= {straightBottom}.");

        // Sınırlar güzergâhı kapsamalı; en güneydeki nokta karenin içinde kalmalı.
        Assert.True(pathBottom < 300 - 2, $"Güzergâh noktası kare dışına taşmış: {pathBottom}.");
    }

    [Fact]
    public async Task RenderAsync_is_deterministic_with_a_route_path()
    {
        var detour = new[]
        {
            new GeoPoint(41.0, 29.0),
            new GeoPoint(40.9, 29.1),
            new GeoPoint(41.0, 29.2),
        };
        var request = new StaticMapRequest(
            new GeoPoint(41.0, 29.0),
            [new StaticMapMarker("Otel", new GeoPoint(41.0, 29.2), true, detour)],
            200,
            120);

        var first = await RenderAsync(request);
        var second = await RenderAsync(request);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task RenderAsync_distinguishes_nearby_markers_and_keeps_the_venue_moderate()
    {
        var point = new GeoPoint(41.0, 29.0);
        var request = new StaticMapRequest(
            point,
            [
                new StaticMapMarker("Seçilen", point, true),
                new StaticMapMarker("Otel iki", point, false),
                new StaticMapMarker("Otel üç", point, false),
            ],
            360,
            240);

        var png = await RenderAsync(request);
        using var bitmap = SKBitmap.Decode(png)!;

        Assert.True(CountColor(bitmap, StaticMapOverlay.HotelColor(1)) > 100);
        Assert.True(CountColor(bitmap, StaticMapOverlay.HotelColor(2)) > 100);
        Assert.True(CountColor(bitmap, StaticMapOverlay.DestinationColor) > 100);
        Assert.True(CountColor(bitmap, StaticMapOverlay.DestinationColor) < 1500);
        Assert.Equal(1.1f, StaticMapOverlay.DestinationPinScale);
        // The destination pin has a white center distinct from numbered hotel badges.
        Assert.Equal(SKColors.White, bitmap.GetPixel(180, 135));

        var renderedLabels = StaticMapOverlay.PlaceMarkerLabels(
            Enumerable.Repeat(new SKPoint(180, 120), 3).ToArray(),
            new SKPoint(180, 120),
            new SKRect(0, 0, 360, 240),
            selectedIndex: 0);
        Assert.All(renderedLabels, center => Assert.True(CountWhiteBadgePixels(bitmap, center) > 0));

        var points = Enumerable.Repeat(new SKPoint(180, 120), 20).ToArray();
        var centers = StaticMapOverlay.PlaceMarkerLabels(points, new SKPoint(180, 120), new SKRect(0, 0, 360, 240));
        Assert.Equal(20, centers.Count);
        var palette = Enumerable.Range(0, 20).Select(StaticMapOverlay.HotelColor).ToArray();
        Assert.Equal(20, palette.Distinct().Count());
        Assert.DoesNotContain(palette, color => IsOrangeOrRedHue(color));
        for (var first = 0; first < centers.Count; first++)
        {
            var firstBounds = new SKRect(centers[first].X - 9.5f, centers[first].Y - 9.5f, centers[first].X + 9.5f, centers[first].Y + 9.5f);
            Assert.True(firstBounds.Left >= 0 && firstBounds.Top >= 0 && firstBounds.Right <= 360 && firstBounds.Bottom <= 240);
            Assert.False(Intersects(firstBounds, new SKRect(161, 101, 199, 139)), $"Marker label {first + 1} overlaps the venue.");
            for (var second = first + 1; second < centers.Count; second++)
            {
                var secondBounds = new SKRect(centers[second].X - 9.5f, centers[second].Y - 9.5f, centers[second].X + 9.5f, centers[second].Y + 9.5f);
                Assert.False(Intersects(firstBounds, secondBounds), $"Marker labels {first + 1} and {second + 1} overlap.");
            }

            Assert.False(Intersects(firstBounds, new SKRect(175, 115, 185, 125)), $"Marker label {first + 1} hides the exact location.");
        }

        var separated = StaticMapOverlay.PlaceMarkerLabels(
            [new SKPoint(40, 40), new SKPoint(150, 40)], new SKPoint(300, 200), new SKRect(0, 0, 360, 240));
        Assert.Equal(new SKPoint(40, 40), separated[0]);
        Assert.Equal(new SKPoint(150, 40), separated[1]);

        var slightOverlap = StaticMapOverlay.PlaceMarkerLabels(
            [new SKPoint(100, 100), new SKPoint(116, 100)], new SKPoint(330, 210), new SKRect(0, 0, 360, 240));
        Assert.Equal(new SKPoint(100, 100), slightOverlap[0]);
        var overlapDx = slightOverlap[1].X - 116;
        var overlapDy = slightOverlap[1].Y - 100;
        Assert.True(MathF.Sqrt((overlapDx * overlapDx) + (overlapDy * overlapDy)) <= 4.1f,
            "A slight overlap should move the badge only by the small clearance it needs.");

        Assert.False(Intersects(
            new SKRect(slightOverlap[0].X - 9.5f, slightOverlap[0].Y - 9.5f, slightOverlap[0].X + 9.5f, slightOverlap[0].Y + 9.5f),
            new SKRect(slightOverlap[1].X - 9.5f, slightOverlap[1].Y - 9.5f, slightOverlap[1].X + 9.5f, slightOverlap[1].Y + 9.5f)));
    }

    [Fact]
    public void Overlay_renders_matching_colored_routes_with_solid_selected_and_dashed_other_routes()
    {
        using var surface = SKSurface.Create(new SKImageInfo(240, 120));
        surface.Canvas.Clear(SKColors.White);
        var venueLocation = new GeoPoint(0, 0);
        var selectedLocation = new GeoPoint(1, 1);
        var otherLocation = new GeoPoint(-1, 1);
        var venue = new SKPoint(20, 60);
        var selectedEnd = new SKPoint(228, 20);
        var otherEnd = new SKPoint(228, 100);
        var request = new StaticMapRequest(
            venueLocation,
            [
                new StaticMapMarker("Seçilen", selectedLocation, true, [venueLocation, selectedLocation]),
                new StaticMapMarker("Diğer", otherLocation, false, [venueLocation, otherLocation]),
            ],
            240,
            120);
        var projected = new Dictionary<GeoPoint, SKPoint>
        {
            [venueLocation] = venue,
            [selectedLocation] = selectedEnd,
            [otherLocation] = otherEnd,
        };
        StaticMapOverlay.Draw(surface.Canvas, request, point => projected[point]);

        using var image = surface.Snapshot();
        using var bitmap = SKBitmap.FromImage(image);
        var selectedPixels = CountColorIn(bitmap, new SKColor(234, 88, 12), new SKRect(80, 25, 170, 55));
        var otherPixels = CountColorIn(bitmap, StaticMapOverlay.HotelColor(1), new SKRect(80, 70, 170, 95));
        var selectedLineThickness = Enumerable.Range(35, 11)
            .Count(y => bitmap.GetPixel(120, y) == new SKColor(234, 88, 12));
        var selectedBadgePixels = CountColorIn(bitmap, new SKColor(234, 88, 12), new SKRect(214, 6, 239, 34));
        var otherBadgePixels = CountColorIn(bitmap, StaticMapOverlay.HotelColor(1), new SKRect(214, 86, 239, 114));
        var venuePixels = CountColorIn(bitmap, StaticMapOverlay.DestinationColor, new SKRect(0, 35, 40, 85));
        Assert.True(selectedPixels > 0, "Selected route should retain its orange badge color.");
        Assert.True(otherPixels > 0, "Other route should retain its hotel badge color.");
        Assert.True(selectedPixels < 300, "Selected route should be drawn as a fine line.");
        Assert.InRange(selectedLineThickness, 1, 3);
        Assert.True(otherPixels < 160, "The non-selected route should render as a dashed line, not a solid stroke.");
        Assert.True(selectedBadgePixels > otherBadgePixels, "The selected badge should be only modestly larger than a hotel badge.");
        Assert.True(venuePixels is > 100 and < 600, "The destination pin should remain distinct and compact.");
    }

    [Fact]
    public void Composition_renders_only_a_full_width_overview_panel()
    {
        using var overviewSurface = SKSurface.Create(new SKImageInfo(640, 330));
        overviewSurface.Canvas.Clear(SKColors.White);
        using var routePaint = new SKPaint { Color = new SKColor(234, 88, 12), StrokeWidth = 6, IsAntialias = true };
        overviewSurface.Canvas.DrawLine(20, 20, 300, 100, routePaint);
        using var overviewImage = overviewSurface.Snapshot();
        using var overview = SKBitmap.FromImage(overviewImage);

        var png = StaticMapComposition.ComposePng(overview, headerHeight: 30, scaleFactor: 1);

        using var composed = SKBitmap.Decode(png)!;
        Assert.Equal(640, composed.Width);
        Assert.Equal(360, composed.Height);
        Assert.Equal(new SKColor(234, 88, 12), composed.GetPixel(100, 73));
        Assert.Equal(new SKColor(241, 245, 249), composed.GetPixel(600, 10));
        Assert.True(CountColorIn(composed, new SKColor(30, 41, 59), new SKRect(4, 3, 160, 27)) > 0,
            "The general overview title should be rasterized in the single panel header.");
    }

    [Theory]
    [InlineData(120, 20)]
    [InlineData(360, 30)]
    public void HeaderHeight_is_bounded_by_panel_height(int imageHeight, int expected) =>
        Assert.Equal(expected, StaticMapComposition.HeaderHeight(imageHeight));
    private static int CountColorIn(SKBitmap bitmap, SKColor expected, SKRect area)
    {
        var count = 0;
        for (var y = Math.Max(0, (int)area.Top); y < Math.Min(bitmap.Height, (int)area.Bottom); y++)
        {
            for (var x = Math.Max(0, (int)area.Left); x < Math.Min(bitmap.Width, (int)area.Right); x++)
            {
                if (bitmap.GetPixel(x, y) == expected)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static async Task<byte[]> RenderAsync(StaticMapRequest request)
    {
        var provider = new SchematicStaticMapProvider();

        var outcome = await provider.RenderAsync(request, CancellationToken.None);

        Assert.Equal(OutcomeStatus.Found, outcome.Status);

        return outcome.Image!.Value.ToArray();
    }

    private static int LowestSelectedPixelY(byte[] png)
    {
        using var bitmap = SKBitmap.Decode(png)
            ?? throw new InvalidOperationException("PNG çözümlenemedi.");

        var lowest = -1;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (IsSelectedColor(bitmap.GetPixel(x, y)))
                {
                    lowest = y;
                }
            }
        }

        return lowest;
    }

    private static int CountColor(SKBitmap bitmap, SKColor expected)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y) == expected)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static int CountWhiteBadgePixels(SKBitmap bitmap, SKPoint center)
    {
        var count = 0;
        for (var y = Math.Max(0, (int)center.Y - 11); y <= Math.Min(bitmap.Height - 1, (int)center.Y + 11); y++)
        {
            for (var x = Math.Max(0, (int)center.X - 11); x <= Math.Min(bitmap.Width - 1, (int)center.X + 11); x++)
            {
                var dx = x - center.X;
                var dy = y - center.Y;
                if ((dx * dx) + (dy * dy) <= 121 && bitmap.GetPixel(x, y) == SKColors.White)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static bool Intersects(SKRect first, SKRect second) =>
        first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;

    private static bool IsSelectedColor(SKColor color) =>
        Math.Abs(color.Red - 249) <= 60 && Math.Abs(color.Green - 115) <= 60 && Math.Abs(color.Blue - 22) <= 60;

    private static bool IsOrangeOrRedHue(SKColor color)
    {
        var red = color.Red / 255f;
        var green = color.Green / 255f;
        var blue = color.Blue / 255f;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var delta = maximum - minimum;
        var hue = maximum == red
            ? 60 * (((green - blue) / delta) % 6)
            : maximum == green
                ? 60 * (((blue - red) / delta) + 2)
                : 60 * (((red - green) / delta) + 4);
        hue = (hue + 360) % 360;
        return hue <= 45 || hue >= 330;
    }
    private static StaticMapRequest Request(int width, int height) =>
        new(
            new GeoPoint(41.0, 29.0),
            [
                new StaticMapMarker("Yakın Otel", new GeoPoint(41.1, 29.1), true),
                new StaticMapMarker("Uzak Otel", new GeoPoint(41.4, 29.6), false),
            ],
            width,
            height);
}
