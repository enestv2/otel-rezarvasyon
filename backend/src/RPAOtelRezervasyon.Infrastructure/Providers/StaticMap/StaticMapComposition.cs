using RPAOtelRezervasyon.Domain.Models;
using SkiaSharp;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

internal static class StaticMapComposition
{
    private const int HeaderHeightPx = 30;

    public static IReadOnlyList<int> OverviewMarkerRanks(StaticMapRequest request) =>
        Enumerable.Range(0, request.Markers.Count).ToArray();

    public static int HeaderHeight(int imageHeight) => Math.Min(HeaderHeightPx, Math.Max(1, imageHeight / 6));

    public static StaticMapRequest CreateOverviewRequest(StaticMapRequest request)
    {
        var height = Math.Max(1, request.HeightPx - HeaderHeight(request.HeightPx));
        return new StaticMapRequest(request.Venue, request.Markers, request.WidthPx, height);
    }

    public static byte[] ComposePng(SKBitmap overview, int headerHeight, int scaleFactor)
    {
        var headerPixels = headerHeight * scaleFactor;
        var outputHeight = overview.Height + headerPixels;
        using var surface = SKSurface.Create(new SKImageInfo(overview.Width, outputHeight, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (surface is null)
        {
            throw new InvalidOperationException("Harita kompozisyonu için çizim yüzeyi oluşturulamadı.");
        }

        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        using var header = new SKPaint { Color = new SKColor(241, 245, 249), Style = SKPaintStyle.Fill, IsAntialias = true };
        using var border = new SKPaint { Color = new SKColor(203, 213, 225), Style = SKPaintStyle.Stroke, StrokeWidth = scaleFactor, IsAntialias = true };
        using var text = new SKPaint { Color = new SKColor(30, 41, 59), IsAntialias = true };
        using var typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Normal);
        using var font = new SKFont(typeface, 10.5f * scaleFactor);

        canvas.DrawRect(0, 0, overview.Width, headerPixels, header);
        canvas.DrawText("GENEL GÖRÜNÜM", 8 * scaleFactor, headerPixels * 0.68f, SKTextAlign.Left, font, text);
        canvas.DrawBitmap(overview, 0, headerPixels, SKSamplingOptions.Default);
        canvas.DrawRect(new SKRect(0.5f, 0.5f, overview.Width - 0.5f, outputHeight - 0.5f), border);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data?.ToArray() ?? throw new InvalidOperationException("Harita kompozisyonu PNG olarak kodlanamadı.");
    }
}
