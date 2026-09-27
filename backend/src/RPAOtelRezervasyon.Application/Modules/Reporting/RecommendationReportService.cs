using System.Globalization;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.Reporting;

/// <summary>
/// Başarılı öneriyi rapor modeline dönüştürür, uygun oteller için ek yürüyüş metriklerini alır ve harita/PDF üretimini yürütür.
/// Ek yürüyüş değerleri mevcut JSON öneri metriklerini değiştirmez; harita üretilemese de belge oluşturulur.
/// </summary>
public sealed class RecommendationReportService(
    IStaticMapProvider staticMapProvider,
    IRecommendationReportRenderer renderer,
    IWalkingRouteDistanceProvider walkingRouteDistanceProvider,
    RecommendationExplanationService explanationService,
    ReportingOptions options)
{
    public async Task<ReportDocument> CreateAsync(
        string venueName,
        RecommendationResult result,
        CancellationToken cancellationToken) =>
        await CreateAsync(venueName, result, null, cancellationToken).ConfigureAwait(false);

    public async Task<ReportDocument> CreateAsync(
        string venueName,
        RecommendationResult result,
        PersonnelInfo? personnel,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(venueName);
        ArgumentNullException.ThrowIfNull(result);

        var recommendation = result.Recommendation
            ?? throw new InvalidOperationException("Belge yalnızca başarılı bir öneri için üretilebilir.");
        var venueLocation = result.VenueLocation
            ?? throw new InvalidOperationException("Başarılı sonuçta etkinlik alanı konumu bulunmalıdır.");

        var walkingRoutes = await GetWalkingRoutesAsync(recommendation.RankedHotels, venueLocation, cancellationToken).ConfigureAwait(false);
        var selectedName = recommendation.Selected.Hotel.Name;
        var rows = recommendation.RankedHotels
            .Select((candidate, index) => ToRow(candidate, selectedName, walkingRoutes[index].Metrics, walkingRoutes[index].Requested))
            .ToArray();
        var selectedRow = rows.First(row => row.IsSelected);

        var map = await TryRenderMapAsync(venueLocation, recommendation.RankedHotels, selectedName, walkingRoutes, cancellationToken).ConfigureAwait(false);
        var explanations = await explanationService.ExplainAsync(
            venueName.Trim(),
            rows,
            result.UnresolvedHotels,
            cancellationToken).ConfigureAwait(false);
        var transportExplanation = explanations.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation;

        var report = new RecommendationReport(
            options.DocumentTitle,
            venueName.Trim(),
            selectedRow,
            rows,
            result.UnresolvedHotels,
            map,
            DateTimeOffset.UtcNow,
            personnel,
            transportExplanation,
            explanations);

        var document = await renderer.RenderAsync(report, cancellationToken).ConfigureAwait(false);

        if (document.Content.Length > options.MaxDocumentBytes)
        {
            throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"Üretilen belge boyut sınırını aştı: {document.Content.Length} > {options.MaxDocumentBytes} bayt."));
        }

        return document;
    }

    private async Task<(RouteMetrics? Metrics, bool Requested)[]> GetWalkingRoutesAsync(
        IReadOnlyList<HotelCandidate> rankedHotels,
        GeoPoint venueLocation,
        CancellationToken cancellationToken)
    {
        var outcomes = await Task.WhenAll(rankedHotels.Select(async (candidate, index) =>
        {
            if (candidate.Metrics.Kind != DistanceKind.Road || candidate.Metrics.DistanceMeters >= 3000)
            {
                return (index, metrics: (RouteMetrics?)null, requested: false);
            }

            var route = await walkingRouteDistanceProvider
                .GetWalkingRouteAsync(candidate.Location, venueLocation, cancellationToken)
                .ConfigureAwait(false);
            return (index, metrics: route.Status == OutcomeStatus.Found ? route.Metrics : null, requested: true);
        })).ConfigureAwait(false);

        var result = new (RouteMetrics? Metrics, bool Requested)[rankedHotels.Count];
        foreach (var outcome in outcomes)
        {
            result[outcome.index] = (outcome.metrics, outcome.requested);
        }

        return result;
    }

    /// <summary>Görsel üretilemezse <c>null</c> döner; belge görselsiz üretilir (R-8).</summary>
    private async Task<StaticMapImage?> TryRenderMapAsync(
        GeoPoint venueLocation,
        IReadOnlyList<HotelCandidate> rankedHotels,
        string selectedName,
        IReadOnlyList<(RouteMetrics? Metrics, bool Requested)> walkingRoutes,
        CancellationToken cancellationToken)
    {
        var markers = rankedHotels
            .Select((candidate, index) =>
            {
                var walkingMetrics = walkingRoutes[index].Metrics;
                var mapMetrics = walkingMetrics ?? candidate.Metrics;
                return new StaticMapMarker(
                    candidate.Hotel.Name,
                    candidate.Location,
                    string.Equals(candidate.Hotel.Name, selectedName, StringComparison.Ordinal),
                    mapMetrics.Path,
                    walkingMetrics,
                    mapMetrics.PathSegments);
            })
            .ToArray();
        var request = new StaticMapRequest(venueLocation, markers, options.MapWidthPx, options.MapHeightPx);

        var outcome = await staticMapProvider.RenderAsync(request, cancellationToken).ConfigureAwait(false);

        return outcome.Status == OutcomeStatus.Found && outcome.Image is { IsEmpty: false } image
            ? new StaticMapImage(image, outcome.ContentType ?? "image/png", outcome.Attribution, outcome.IncludesDetailView)
            : null;
    }

    private static HotelReportRow ToRow(HotelCandidate candidate, string selectedName, RouteMetrics? walkingMetrics, bool walkingRouteRequested) =>
        new(
            candidate.Hotel.Name,
            candidate.Metrics.DistanceMeters,
            candidate.Metrics.DurationSeconds,
            candidate.Metrics.Kind,
            candidate.Hotel.Price,
            string.Equals(candidate.Hotel.Name, selectedName, StringComparison.Ordinal),
            candidate.Location,
            walkingMetrics,
            walkingRouteRequested);
}
