using System.Globalization;

namespace RPAOtelRezervasyon.Domain.Models;

public static class RecommendationMetricFormatter
{
    public static string Distance(int meters) => meters < 1000
        ? string.Create(CultureInfo.InvariantCulture, $"{meters} m")
        : string.Create(CultureInfo.InvariantCulture, $"{meters / 1000.0:0.#} km");

    public static string RoadDuration(HotelReportRow row) => row.DistanceKind == DistanceKind.StraightLine
        ? "ölçülmedi"
        : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, row.DurationSeconds / 60)} dk");

    public static string WalkingDuration(RouteMetrics? metrics) => metrics is null
        ? "alınamadı"
        : string.Create(CultureInfo.InvariantCulture, $"{Math.Max(1, (int)Math.Round(metrics.DurationSeconds / 60.0))} dk");

    public static string Price(NightlyPrice price) =>
        string.Create(CultureInfo.InvariantCulture, $"{price.Amount:0.##} {price.Currency}");
}
