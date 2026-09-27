using System.Globalization;
using RPAOtelRezervasyon.Domain.Models;
using SkiaSharp;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

internal static class StaticMapOverlay
{
    private static readonly SKColor SelectedConnectorColor = new(234, 88, 12);
    internal static readonly SKColor DestinationColor = new(220, 38, 38);
    internal const float DestinationPinScale = 1.1f;
    private static readonly SKColor[] HotelColors =
    [
        new(0, 121, 107), new(94, 53, 177), new(21, 101, 192), new(0, 150, 136),
        new(85, 139, 47), new(0, 131, 143), new(142, 36, 170), new(69, 90, 100),
        new(130, 119, 23), new(63, 81, 181), new(0, 121, 75), new(0, 96, 128),
        new(0, 105, 180), new(84, 110, 122), new(74, 111, 0), new(0, 144, 190),
        new(0, 113, 145), new(40, 53, 147), new(0, 96, 100), new(104, 159, 56),
    ];
    private static readonly float[][] RouteDashPatterns =
    [
        [10, 4], [3, 3], [10, 3, 2, 3], [5, 3],
    ];
    private const float LabelRadius = 9.5f;
    private const float SelectedLabelRadius = 10.5f;
    private const float RouteCasingWidth = 3;
    private const float RouteWidth = 2;

    public static void Draw(SKCanvas canvas, StaticMapRequest request, Func<GeoPoint, SKPoint> project,
        IReadOnlyList<int>? markerRanks = null, bool selectedRouteOnly = false, bool drawRoutes = true)
    {
        var venue = project(request.Venue);
        if (drawRoutes)
        {
            DrawRoutes(canvas, request, project, markerRanks, selectedRouteOnly);
        }
        DrawHotels(canvas, request, venue, project, markerRanks);
        DrawVenue(canvas, venue);
    }

    private static void DrawRoutes(SKCanvas canvas, StaticMapRequest request, Func<GeoPoint, SKPoint> project,
        IReadOnlyList<int>? markerRanks, bool selectedRouteOnly)
    {
        using var casing = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = RouteCasingWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
        };
        foreach (var indexedMarker in request.Markers.Select((marker, index) => (marker, index)).Reverse())
        {
            var (marker, index) = indexedMarker;
            if (selectedRouteOnly && !marker.IsSelected)
            {
                continue;
            }

            if (marker.PathSegments.Count == 0)
            {
                continue;
            }

            var rank = markerRanks?[index] ?? index;
            using var dash = marker.IsSelected ? null : SKPathEffect.CreateDash(RouteDashPatterns[rank % RouteDashPatterns.Length], 0);
            casing.PathEffect = dash;
            using var paint = new SKPaint
            {
                Color = RouteColor(marker.IsSelected, rank),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = RouteWidth,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                PathEffect = dash,
            };
            using var pathBuilder = new SKPathBuilder();
            if (marker.PathSegments.Count > 0)
            {
                foreach (var segment in marker.PathSegments)
                {
                    var points = segment.Select(project).ToArray();
                    pathBuilder.MoveTo(points[0].X, points[0].Y);
                    for (var pathIndex = 1; pathIndex < points.Length; pathIndex++)
                    {
                        pathBuilder.LineTo(points[pathIndex].X, points[pathIndex].Y);
                    }
                }
            }
            using var path = pathBuilder.Detach();
            canvas.DrawPath(path, casing);
            canvas.DrawPath(path, paint);
        }
    }

    private static void DrawHotels(SKCanvas canvas, StaticMapRequest request, SKPoint venue, Func<GeoPoint, SKPoint> project,
        IReadOnlyList<int>? markerRanks)
    {
        var points = request.Markers.Select(marker => project(marker.Location)).ToArray();
        var selectedIndex = request.Markers
            .Select((marker, index) => (marker, index))
            .Where(item => item.marker.IsSelected)
            .Select(item => item.index)
            .DefaultIfEmpty(-1)
            .First();
        var labelCenters = PlaceMarkerLabels(points, venue, canvas.LocalClipBounds, selectedIndex);
        var labels = new List<SKRect>(points.Length);
        using var leaderCasing = new SKPaint
        {
            Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 4,
            StrokeCap = SKStrokeCap.Round, IsAntialias = true,
        };
        using var leader = new SKPaint
        {
            Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round, IsAntialias = true,
        };
        using var anchorHalo = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var anchor = new SKPaint { IsAntialias = true };
        using var halo = new SKPaint { Color = SKColors.White, IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 10);
        using var labelPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        for (var index = 0; index < points.Length; index++)
        {
            var marker = request.Markers[index];
            var point = points[index];
            var rank = markerRanks?[index] ?? index;
            var color = RouteColor(marker.IsSelected, rank);
            var labelCenter = labelCenters[index];
            var radius = BadgeRadius(index, selectedIndex);
            var labelBounds = new SKRect(labelCenter.X - radius, labelCenter.Y - radius,
                labelCenter.X + radius, labelCenter.Y + radius);
            labels.Add(labelBounds);

            leader.Color = color;
            var direction = labelCenter - point;
            var directionLength = Math.Max(1, MathF.Sqrt((direction.X * direction.X) + (direction.Y * direction.Y)));
            if (directionLength > 1)
            {
                var labelEdge = new SKPoint(
                    labelCenter.X - ((direction.X / directionLength) * (radius + 1)),
                    labelCenter.Y - ((direction.Y / directionLength) * (radius + 1)));
                canvas.DrawLine(point, labelEdge, leaderCasing);
                canvas.DrawLine(point, labelEdge, leader);
                anchor.Color = color;
                canvas.DrawCircle(point, 3, anchorHalo);
                canvas.DrawCircle(point, 1.8f, anchor);
            }

            canvas.DrawCircle(labelCenter, radius + 1.5f, halo);
            halo.Color = SKColors.White;
            using var badge = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawCircle(labelCenter, radius, badge);
            DrawCenteredText(canvas, (rank + 1).ToString(CultureInfo.InvariantCulture), labelCenter, font, labelPaint);
        }
    }

    private static void DrawVenue(SKCanvas canvas, SKPoint point)
    {
        using var halo = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 4, StrokeJoin = SKStrokeJoin.Round, IsAntialias = true };
        using var paint = new SKPaint { Color = DestinationColor, Style = SKPaintStyle.Fill, IsAntialias = true };
        using var target = new SKPaint { Color = SKColors.White, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        using var destination = new SKPathBuilder();
        destination.MoveTo(0, -12);
        destination.CubicTo(7, -12, 10, -6, 9, 0);
        destination.CubicTo(8, 5, 0, 12, 0, 12);
        destination.CubicTo(0, 12, -8, 5, -9, 0);
        destination.CubicTo(-10, -6, -7, -12, 0, -12);
        destination.Close();
        using var path = destination.Detach();
        canvas.Save();
        canvas.Translate(point.X, point.Y);
        canvas.Scale(DestinationPinScale);
        canvas.DrawPath(path, halo);
        canvas.DrawPath(path, paint);
        canvas.DrawCircle(0, -3, 3.5f, target);
        canvas.Restore();
    }

    private static void DrawCenteredText(SKCanvas canvas, string text, SKPoint center, SKFont font, SKPaint paint)
    {
        var metrics = font.Metrics;
        var y = center.Y - ((metrics.Ascent + metrics.Descent) / 2);
        canvas.DrawText(text, center.X, y, SKTextAlign.Center, font, paint);
    }

    internal static IReadOnlyList<SKPoint> PlaceMarkerLabels(IReadOnlyList<SKPoint> points, SKPoint venue, SKRect clip,
        int selectedIndex = -1)
    {
        var labels = new List<SKRect>(points.Count);
        var centers = new List<SKPoint>(points.Count);
        for (var index = 0; index < points.Count; index++)
        {
            var radius = BadgeRadius(index, selectedIndex);
            var center = PlaceLabel(points[index], venue, index, points, labels, clip, radius);
            centers.Add(center);
            labels.Add(new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius));
        }

        return centers;
    }

    private static SKPoint PlaceLabel(SKPoint point, SKPoint venue, int index, IReadOnlyList<SKPoint> points,
        IReadOnlyList<SKRect> labels, SKRect clip, float radius)
    {
        var best = point;
        var bestScore = float.MaxValue;
        var venueBounds = new SKRect(venue.X - 19, venue.Y - 19, venue.X + 19, venue.Y + 19);
        for (var distance = 0f; distance <= 96; distance += 2)
        {
            var steps = distance == 0 ? 1 : 32;
            for (var step = 0; step < steps; step++)
            {
                var angle = ((step + (index * 5)) % steps) * (MathF.Tau / steps);
                var candidate = new SKPoint(point.X + (MathF.Cos(angle) * distance), point.Y + (MathF.Sin(angle) * distance));
                var bounds = new SKRect(candidate.X - radius, candidate.Y - radius, candidate.X + radius, candidate.Y + radius);
                var score = distance;
                score += IntersectionArea(bounds, venueBounds) * 1000;
                foreach (var occupied in labels)
                {
                    score += IntersectionArea(bounds, occupied) * 1000;
                }

                for (var markerIndex = 0; markerIndex < points.Count; markerIndex++)
                {
                    if (markerIndex == index)
                    {
                        continue;
                    }

                    var anchor = points[markerIndex];
                    score += IntersectionArea(bounds, new SKRect(anchor.X - 4, anchor.Y - 4, anchor.X + 4, anchor.Y + 4)) * 1000;
                }

                score += (Math.Max(0, clip.Left - bounds.Left) + Math.Max(0, bounds.Right - clip.Right)
                    + Math.Max(0, clip.Top - bounds.Top) + Math.Max(0, bounds.Bottom - clip.Bottom)) * 1000;
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
        }

        return best;
    }

    private static float IntersectionArea(SKRect first, SKRect second)
    {
        var width = Math.Max(0, Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left));
        var height = Math.Max(0, Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top));
        return width * height;
    }

    private static float BadgeRadius(int index, int selectedIndex) => index == selectedIndex ? SelectedLabelRadius : LabelRadius;

    internal static SKColor HotelColor(int index) => HotelColors[index % HotelColors.Length];

    internal static SKColor RouteColor(bool selected, int rank) => selected ? SelectedConnectorColor : HotelColor(rank);
}
