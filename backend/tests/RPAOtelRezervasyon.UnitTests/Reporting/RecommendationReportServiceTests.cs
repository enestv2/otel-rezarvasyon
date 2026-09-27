using FluentAssertions;
using RPAOtelRezervasyon.Application.Modules.Reporting;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.UnitTests.Fakes;

namespace RPAOtelRezervasyon.UnitTests.Reporting;

public class RecommendationReportServiceTests
{
    private const string VenueName = "Kongre Merkezi";
    private const string NearHotel = "Yakın Otel";
    private const string FarHotel = "Uzak Otel";

    private static readonly GeoPoint VenueLocation = new(41.0, 29.0);

    private static readonly GeoPoint[] NearPath =
    [
        new(41.05, 29.05),
        new(41.1, 29.1),
        new(41.0, 29.0),
    ];

    [Fact]
    public async Task CreateAsync_maps_ranked_hotels_to_rows_and_marks_the_selected_one()
    {
        var (service, renderer, _, walkingProvider) = CreateService();
        var result = SucceededResult(800m, "EUR", unresolved: []);

        var document = await service.CreateAsync(VenueName, result, CancellationToken.None);

        document.Should().BeSameAs(renderer.Document);
        document.IsPdf.Should().BeTrue();
        renderer.CallCount.Should().Be(1);

        var report = renderer.LastReport!;
        report.Title.Should().Be("Otel Önerisi");
        report.Options.Should().HaveCount(3);
        report.Options.Single(option => option.Strategy == RecommendationStrategy.BudgetPriority).Hotel.Should().BeNull();
        report.Options.Single(option => option.Strategy == RecommendationStrategy.Balanced).Hotel.Should().BeNull();
        report.Options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Hotel!.Name.Should().Be(NearHotel);
        report.Explanation.Should().Contain("10 dk");
        report.Explanation.Should().Contain("850 m");
        report.VenueName.Should().Be(VenueName);
        report.RankedHotels.Select(row => row.Name).Should().ContainInOrder(NearHotel, FarHotel);

        // AC-2 / AC-3: satırlardaki metrik ve fiyatlar JSON yanıtındaki değerlerle aynıdır.
        var near = report.RankedHotels.Single(row => row.Name == NearHotel);
        near.DistanceMeters.Should().Be(1000);
        near.DurationSeconds.Should().Be(600);
        near.DistanceKind.Should().Be(DistanceKind.Road);
        near.Price.Should().Be(new NightlyPrice(1250m, "TRY"));
        near.IsSelected.Should().BeTrue();

        var far = report.RankedHotels.Single(row => row.Name == FarHotel);
        far.Price.Should().Be(new NightlyPrice(800m, "EUR"));
        far.IsSelected.Should().BeFalse();
        near.WalkingMetrics.Should().BeEquivalentTo(new RouteMetrics(850, 620, DistanceKind.Road, [near.Location, VenueLocation]));
        near.WalkingRouteRequested.Should().BeTrue();
        far.WalkingRouteRequested.Should().BeFalse();
        walkingProvider.Calls.Should().ContainSingle().Which.Should().Be((near.Location, VenueLocation));

        // AC-5: seçilen otel, sıralamanın ilkiyle birebir aynıdır.
        report.SelectedHotel.Name.Should().Be(result.Recommendation!.Selected.Hotel.Name);
        report.SelectedHotel.Price.Should().Be(result.Recommendation.Selected.Hotel.Price);
        report.SelectedHotel.DistanceMeters.Should().Be(result.Recommendation.Selected.Metrics.DistanceMeters);
    }

    [Fact]
    public async Task CreateAsync_adds_server_rendered_validated_llm_reasons_to_the_pdf_report_model()
    {
        var renderer = new RecordingReportRenderer();
        var provider = new FakeExplanationProvider(RecommendationExplanationOutcome.Success(
            [new RecommendationExplanationPolicyOutcome(
                RecommendationStrategy.TransportPriority,
                ["shortest-road-time", "walking-route-available"])]));
        var service = new RecommendationReportService(
            new FakeStaticMapProvider(),
            renderer,
            new FakeWalkingRouteDistanceProvider(),
            new RecommendationExplanationService(provider, new RecommendationExplanationOptions(true)),
            new ReportingOptions());

        await service.CreateAsync(VenueName, SucceededResult(800m, "EUR", unresolved: []), CancellationToken.None);

        renderer.LastReport!.Options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation
            .Should().Contain("ölçülen yol rotaları içinde en kısa tahmini yolculuk süresine sahip");
        renderer.LastReport.Options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation
            .Should().Contain("yürüyüş rotası ayrıca 850 m ve 10 dk");
    }

    [Fact]
    public async Task CreateAsync_lists_unresolved_hotels_and_still_produces_the_document()
    {
        var (service, renderer, _, _) = CreateService();
        var result = SucceededResult(800m, "EUR", unresolved: [FarHotel]);

        var document = await service.CreateAsync(VenueName, result, CancellationToken.None);

        document.IsPdf.Should().BeTrue();
        renderer.LastReport!.UnresolvedHotels.Should().ContainInOrder(FarHotel);
    }

    [Fact]
    public async Task CreateAsync_requests_the_map_with_the_venue_and_every_ranked_hotel()
    {
        var (service, renderer, mapProvider, _) = CreateService();

        await service.CreateAsync(VenueName, SucceededResult(800m, "EUR", unresolved: []), CancellationToken.None);

        mapProvider.CallCount.Should().Be(1);
        var request = mapProvider.LastRequest!;
        request.Venue.Should().Be(VenueLocation);
        request.Markers.Select(marker => marker.Label).Should().ContainInOrder(NearHotel, FarHotel);
        request.Markers.Single(marker => marker.Label == NearHotel).IsSelected.Should().BeTrue();
        request.Markers.Single(marker => marker.Label == FarHotel).IsSelected.Should().BeFalse();
        request.Markers.Single(marker => marker.Label == NearHotel).Path.Should().Equal(NearPath);
        request.Markers.Single(marker => marker.Label == FarHotel).Path.Should().BeEmpty();
        request.WidthPx.Should().Be(640);
        request.HeightPx.Should().Be(360);

        // AC-4: görsel başarılıysa rapor modelinde taşınır; atıf korunur.
        var map = renderer.LastReport!.Map;
        map.Should().NotBeNull();
        map!.Content.ToArray().Should().Equal(1, 2, 3, 4);
        map.ContentType.Should().Be("image/png");
        map.Attribution.Should().Be("Şematik görünüm (test)");
    }

    [Fact]
    public async Task CreateAsync_preserves_disconnected_main_route_segments_for_the_map()
    {
        var (service, _, mapProvider, _) = CreateService();
        IReadOnlyList<GeoPoint>[] segments =
        [
            [VenueLocation, new GeoPoint(41.01, 29.01)],
            [new GeoPoint(41.08, 29.08), new GeoPoint(41.1, 29.1)],
        ];
        var near = Candidate(NearHotel, 41.1, 29.1, 3000, 600, 1250m, "TRY", pathSegments: segments);
        var far = Candidate(FarHotel, 41.2, 29.2, 8000, 1800, 800m, "EUR");
        var result = RecommendationResult.Succeeded(
            VenueName, VenueLocation, new Recommendation([near, far], near), Rationale.For(near, 2), []);

        await service.CreateAsync(VenueName, result, CancellationToken.None);

        var marker = mapProvider.LastRequest!.Markers.Single(item => item.Label == NearHotel);
        marker.PathSegments.Should().HaveCount(2);
        marker.PathSegments[0].Should().Equal(segments[0]);
        marker.PathSegments[1].Should().Equal(segments[1]);
    }

    [Fact]
    public async Task CreateAsync_keeps_the_road_route_on_the_map_and_walking_metrics_separate()
    {
        var (service, _, mapProvider, walkingProvider) = CreateService();
        GeoPoint[] walkingPath = [VenueLocation, new GeoPoint(41.02, 29.03), new GeoPoint(41.1, 29.1)];
        walkingProvider.RouteResolver = (_, _) => RouteOutcome.Found(
            new RouteMetrics(1200, 900, DistanceKind.Road, walkingPath));
        var result = SucceededResult(800m, "EUR", unresolved: []);

        await service.CreateAsync(VenueName, result, CancellationToken.None);

        var marker = mapProvider.LastRequest!.Markers.Single(item => item.Label == NearHotel);
        marker.Path.Should().Equal(NearPath);
        marker.PathSegments.Should().ContainSingle().Which.Should().Equal(NearPath);
        marker.WalkingMetrics!.Path.Should().Equal(walkingPath);
        marker.Path.Should().NotEqual(walkingPath);
    }

    [Theory]
    [InlineData(OutcomeStatus.NotFound)]
    [InlineData(OutcomeStatus.TransientError)]
    [InlineData(OutcomeStatus.ProviderError)]
    public async Task CreateAsync_omits_the_map_when_the_provider_cannot_render(OutcomeStatus status)
    {
        var (service, renderer, mapProvider, _) = CreateService();
        mapProvider.Status = status;

        var document = await service.CreateAsync(VenueName, SucceededResult(800m, "EUR", unresolved: []), CancellationToken.None);

        // AC-8: görsel üretilemese de belge üretilir.
        document.IsPdf.Should().BeTrue();
        renderer.LastReport!.Map.Should().BeNull();
    }


    [Fact]
    public async Task CreateAsync_produces_deterministic_rows_for_the_same_result()
    {
        var (service, renderer, _, _) = CreateService();
        var result = SucceededResult(800m, "EUR", unresolved: []);

        await service.CreateAsync(VenueName, result, CancellationToken.None);
        var first = renderer.LastReport!;

        await service.CreateAsync(VenueName, result, CancellationToken.None);
        var second = renderer.LastReport!;

        second.RankedHotels.Should().Equal(first.RankedHotels);
        second.SelectedHotel.Should().Be(first.SelectedHotel);
        second.VenueName.Should().Be(first.VenueName);
    }

    [Fact]
    public async Task CreateAsync_carries_straight_line_fallback_rows_without_a_duration_estimate()
    {
        var (service, renderer, _, _) = CreateService();
        var near = Candidate(NearHotel, 41.1, 29.1, 1000, 600, 1250m, "TRY", path: NearPath);
        var far = Candidate(FarHotel, 41.2, 29.2, 4000, 0, 800m, "EUR", DistanceKind.StraightLine);
        var recommendation = new Recommendation([near, far], near);
        var result = RecommendationResult.Succeeded(VenueName, VenueLocation, recommendation, Rationale.For(near, 2), []);

        await service.CreateAsync(VenueName, result, CancellationToken.None);

        // AC-3/AC-5: düz çizgi fallback satırı mesafeyi taşır, süre ölçülmedi (0) olarak korunur (BR-6).
        var row = renderer.LastReport!.RankedHotels.Single(candidate => candidate.Name == FarHotel);
        row.DistanceKind.Should().Be(DistanceKind.StraightLine);
        row.DurationSeconds.Should().Be(0);
        row.DistanceMeters.Should().Be(4000);
    }

    [Fact]
    public async Task CreateAsync_requests_walking_routes_only_for_measured_drive_distances_below_three_kilometers()
    {
        var (service, renderer, _, walkingProvider) = CreateService();
        var under = Candidate("Under", 41.01, 29.01, 2999, 200, 100m, "TRY");
        var boundary = Candidate("Boundary", 41.02, 29.02, 3000, 250, 200m, "TRY");
        var fallback = Candidate("Fallback", 41.03, 29.03, 1000, 0, 300m, "TRY", DistanceKind.StraightLine);
        var recommendation = new Recommendation([under, boundary, fallback], under);
        var result = RecommendationResult.Succeeded(VenueName, VenueLocation, recommendation, Rationale.For(under, 3), []);
        walkingProvider.RouteResolver = (_, _) => RouteOutcome.ProviderError();

        await service.CreateAsync(VenueName, result, CancellationToken.None);

        walkingProvider.Calls.Should().ContainSingle().Which.Should().Be((under.Location, VenueLocation));
        var rows = renderer.LastReport!.RankedHotels;
        rows.Single(row => row.Name == "Under").WalkingRouteRequested.Should().BeTrue();
        rows.Single(row => row.Name == "Under").WalkingMetrics.Should().BeNull();
        rows.Single(row => row.Name == "Boundary").WalkingRouteRequested.Should().BeFalse();
        rows.Single(row => row.Name == "Fallback").WalkingRouteRequested.Should().BeFalse();
        renderer.LastReport.Map!.IncludesDetailView.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_throws_when_the_document_exceeds_the_size_limit()
    {
        var renderer = new RecordingReportRenderer
        {
            Document = new ReportDocument(new byte[64], ReportDocument.PdfContentType, "oneri.pdf"),
        };
        var service = new RecommendationReportService(
            new FakeStaticMapProvider(),
            renderer,
            new FakeWalkingRouteDistanceProvider(),
            new RecommendationExplanationService(new FakeExplanationProvider(), new RecommendationExplanationOptions(false)),
            new ReportingOptions { MaxDocumentBytes = 16 });

        var act = async () => await service.CreateAsync(VenueName, SucceededResult(800m, "EUR", unresolved: []), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_throws_when_the_result_is_not_successful()
    {
        var (service, _, _, _) = CreateService();
        var failed = RecommendationResult.VenueNotResolved(VenueName);

        var act = async () => await service.CreateAsync(VenueName, failed, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_rejects_blank_venue_name()
    {
        var (service, _, _, _) = CreateService();

        var act = async () => await service.CreateAsync("   ", SucceededResult(800m, "EUR", unresolved: []), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static (RecommendationReportService Service, RecordingReportRenderer Renderer, FakeStaticMapProvider MapProvider, FakeWalkingRouteDistanceProvider WalkingProvider) CreateService()
    {
        var renderer = new RecordingReportRenderer();
        var mapProvider = new FakeStaticMapProvider();
        var options = new ReportingOptions();
        var walkingProvider = new FakeWalkingRouteDistanceProvider();
        var explanationService = new RecommendationExplanationService(
            new FakeExplanationProvider(),
            new RecommendationExplanationOptions(false));

        return (new RecommendationReportService(mapProvider, renderer, walkingProvider, explanationService, options), renderer, mapProvider, walkingProvider);
    }

    private static RecommendationResult SucceededResult(
        decimal farAmount,
        string farCurrency,
        IReadOnlyList<string> unresolved)
    {
        var near = Candidate(NearHotel, 41.1, 29.1, 1000, 600, 1250m, "TRY", path: NearPath);
        var far = Candidate(FarHotel, 41.2, 29.2, 8000, 1800, farAmount, farCurrency);
        var recommendation = new Recommendation([near, far], near);

        return RecommendationResult.Succeeded(
            VenueName,
            VenueLocation,
            recommendation,
            Rationale.For(near, 2),
            unresolved);
    }

    private static HotelCandidate Candidate(
        string name,
        double latitude,
        double longitude,
        int distanceMeters,
        int durationSeconds,
        decimal amount,
        string currency,
        DistanceKind kind = DistanceKind.Road,
        IReadOnlyList<GeoPoint>? path = null,
        IReadOnlyList<IReadOnlyList<GeoPoint>>? pathSegments = null) =>
        new(
            new ContractedHotel(name, new NightlyPrice(amount, currency)),
            new GeoPoint(latitude, longitude),
            new RouteMetrics(distanceMeters, durationSeconds, kind, path, pathSegments));

    private sealed class FakeExplanationProvider(
        RecommendationExplanationOutcome? configuredOutcome = null) : IRecommendationExplanationProvider
    {
        public Task<RecommendationExplanationOutcome> GenerateAsync(
            RecommendationExplanationRequest request,
            CancellationToken cancellationToken) => Task.FromResult(configuredOutcome ?? RecommendationExplanationOutcome.Failure());
    }
}
