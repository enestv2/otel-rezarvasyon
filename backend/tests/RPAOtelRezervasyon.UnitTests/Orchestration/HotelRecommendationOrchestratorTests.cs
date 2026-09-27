using FluentAssertions;
using RPAOtelRezervasyon.Application.Caching;
using RPAOtelRezervasyon.Application.Modules.Delivery;
using RPAOtelRezervasyon.Application.Modules.DistanceRouting;
using RPAOtelRezervasyon.Application.Modules.HotelGeocoding;
using RPAOtelRezervasyon.Application.Modules.Ranking;
using RPAOtelRezervasyon.Application.Modules.VenueGeocoding;
using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Validation;
using RPAOtelRezervasyon.UnitTests.Fakes;

namespace RPAOtelRezervasyon.UnitTests.Orchestration;

public class HotelRecommendationOrchestratorTests
{
    private const string VenueName = "Kongre Merkezi";
    private const string NearHotel = "Yakın Otel";
    private const string FarHotel = "Uzak Otel";

    private static readonly GeoPoint VenueLocation = new(41.0, 29.0);
    private static readonly GeoPoint NearLocation = new(41.1, 29.1);
    private static readonly GeoPoint FarLocation = new(41.2, 29.2);

    [Fact]
    public async Task Recommend_returns_ranked_list_and_picks_the_fastest_hotel()
    {
        var (orchestrator, _, _) = CreateOrchestrator();

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.Succeeded);

        var recommendation = result.Recommendation!;
        recommendation.RankedHotels.Select(candidate => candidate.Hotel.Name)
            .Should().ContainInOrder(NearHotel, FarHotel);
        recommendation.Selected.Hotel.Name.Should().Be(NearHotel);
        result.UnresolvedHotels.Should().BeEmpty();
        result.Rationale!.EvaluatedHotelCount.Should().Be(2);
    }

    [Fact]
    public async Task Recommend_requests_routes_from_each_hotel_to_the_venue()
    {
        var (orchestrator, _, router) = CreateOrchestrator();

        await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        router.Calls.Should().BeEquivalentTo(
            [(NearLocation, VenueLocation), (FarLocation, VenueLocation)]);
    }

    [Fact]
    public async Task Recommend_fails_when_the_venue_cannot_be_resolved()
    {
        var geocoder = new FakeGeocodingProvider()
            .With(NearHotel, NearLocation.Latitude, NearLocation.Longitude)
            .With(FarHotel, FarLocation.Latitude, FarLocation.Longitude);

        var (orchestrator, _, _) = CreateOrchestrator(geocoder: geocoder);

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.VenueNotResolved);
        result.Recommendation.Should().BeNull();
    }

    [Theory]
    [InlineData(OutcomeStatus.TransientError)]
    [InlineData(OutcomeStatus.ProviderError)]
    public async Task Recommend_reports_provider_unavailable_when_venue_provider_fails(OutcomeStatus status)
    {
        var geocoder = new FakeGeocodingProvider { MissingStatus = status };

        var (orchestrator, _, _) = CreateOrchestrator(geocoder: geocoder);

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.ProviderUnavailable);
        result.Recommendation.Should().BeNull();
    }

    [Fact]
    public async Task Recommend_reports_unresolved_hotels_without_failing_the_request()
    {
        var geocoder = new FakeGeocodingProvider()
            .With(VenueName, VenueLocation.Latitude, VenueLocation.Longitude)
            .With(NearHotel, NearLocation.Latitude, NearLocation.Longitude);

        var (orchestrator, _, _) = CreateOrchestrator(geocoder: geocoder);

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.Succeeded);
        result.UnresolvedHotels.Should().ContainSingle().Which.Should().Be(FarHotel);
        result.Recommendation!.RankedHotels.Should().HaveCount(1);
    }

    [Fact]
    public async Task Recommend_excludes_hotels_whose_route_cannot_be_measured()
    {
        var (orchestrator, _, router) = CreateOrchestrator();
        router.RouteResolver = (origin, _) => origin == FarLocation
            ? RouteOutcome.TransientError()
            : RouteOutcome.Found(new RouteMetrics(1000, 300, DistanceKind.Road));

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.Succeeded);
        result.UnresolvedHotels.Should().ContainSingle().Which.Should().Be(FarHotel);
        result.Recommendation!.RankedHotels.Should().OnlyContain(candidate => candidate.Hotel.Name == NearHotel);
    }

    [Fact]
    public async Task Recommend_marks_straight_line_fallback_when_no_road_route_exists()
    {
        var (orchestrator, _, router) = CreateOrchestrator();
        router.RouteResolver = (origin, _) => origin == FarLocation
            ? RouteOutcome.NotFound()
            : RouteOutcome.Found(new RouteMetrics(1000, 300, DistanceKind.Road));

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        var ranked = result.Recommendation!.RankedHotels;
        ranked.Select(candidate => candidate.Hotel.Name).Should().ContainInOrder(NearHotel, FarHotel);

        var fallback = ranked.Single(candidate => candidate.Hotel.Name == FarHotel);
        fallback.Metrics.Kind.Should().Be(DistanceKind.StraightLine);
        fallback.Metrics.DistanceMeters.Should().BePositive();
        fallback.Metrics.DurationSeconds.Should().Be(0);
        fallback.Metrics.Path.Should().BeEmpty();
    }

    [Fact]
    public async Task Recommend_is_deterministic_for_the_same_input()
    {
        var (first, _, _) = CreateOrchestrator();
        var (second, _, _) = CreateOrchestrator();

        var firstResult = await first.RecommendAsync(ValidRequest(), CancellationToken.None);
        var secondResult = await second.RecommendAsync(ValidRequest(), CancellationToken.None);

        firstResult.Recommendation!.RankedHotels.Select(candidate => candidate.Hotel.Name)
            .Should().ContainInOrder(secondResult.Recommendation!.RankedHotels.Select(candidate => candidate.Hotel.Name));
        firstResult.Recommendation.Selected.Hotel.Name.Should().Be(secondResult.Recommendation.Selected.Hotel.Name);
    }

    [Fact]
    public async Task Recommend_deduplicates_hotel_names_before_ranking()
    {
        var (orchestrator, _, _) = CreateOrchestrator();
        var request = new RecommendationRequest(
            VenueName,
            [Hotel(NearHotel, 100m), Hotel("yakın otel", 999m), Hotel(NearHotel, 300m)],
            ValidRequest().Personnel,
            ValidRequest().GeocodingContext);

        var result = await orchestrator.RecommendAsync(request, CancellationToken.None);

        result.Recommendation!.RankedHotels.Should().HaveCount(1);
        result.Recommendation.Selected.Hotel.Name.Should().Be(NearHotel);
        result.Recommendation.Selected.Hotel.Price.Amount.Should().Be(100m);
    }

    [Fact]
    public async Task Recommend_reports_no_candidates_when_no_hotel_can_be_resolved()
    {
        var geocoder = new FakeGeocodingProvider()
            .With(VenueName, VenueLocation.Latitude, VenueLocation.Longitude);

        var (orchestrator, _, _) = CreateOrchestrator(geocoder: geocoder);

        var result = await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.NoResolvedHotels);
        result.UnresolvedHotels.Should().BeEquivalentTo([NearHotel, FarHotel]);
    }

    [Fact]
    public async Task Recommend_rejects_an_invalid_request_before_calling_providers()
    {
        var (orchestrator, geocoder, router) = CreateOrchestrator();
        var request = new RecommendationRequest(VenueName, []);

        var result = await orchestrator.RecommendAsync(request, CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.InvalidRequest);
        result.Validation.IsValid.Should().BeFalse();
        geocoder.CallCount.Should().Be(0);
        router.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Recommend_carries_the_submitted_price_to_ranked_hotels_and_selection()
    {
        var (orchestrator, _, _) = CreateOrchestrator();
        var request = new RecommendationRequest(
            VenueName,
            [Hotel(NearHotel, 1250.50m, "EUR"), Hotel(FarHotel, 800m, "TRY")],
            ValidRequest().Personnel,
            ValidRequest().GeocodingContext);

        var result = await orchestrator.RecommendAsync(request, CancellationToken.None);

        var ranked = result.Recommendation!.RankedHotels;
        var near = ranked.Single(candidate => candidate.Hotel.Name == NearHotel);
        near.Hotel.Price.Should().Be(new NightlyPrice(1250.50m, "EUR"));

        result.Recommendation.Selected.Hotel.Name.Should().Be(NearHotel);
        result.Recommendation.Selected.Hotel.Price.Should().Be(new NightlyPrice(1250.50m, "EUR"));
    }

    [Fact]
    public async Task Recommend_ranking_is_unaffected_by_prices()
    {
        var cheapNear = new RecommendationRequest(
            VenueName,
            [Hotel(NearHotel, 0m, "TRY"), Hotel(FarHotel, 9_999_999m, "TRY")],
            ValidRequest().Personnel,
            ValidRequest().GeocodingContext);
        var priceyNear = new RecommendationRequest(
            VenueName,
            [Hotel(NearHotel, 9_999_999m, "TRY"), Hotel(FarHotel, 0m, "TRY")],
            ValidRequest().Personnel,
            ValidRequest().GeocodingContext);

        var (first, _, _) = CreateOrchestrator();
        var (second, _, _) = CreateOrchestrator();

        var firstResult = await first.RecommendAsync(cheapNear, CancellationToken.None);
        var secondResult = await second.RecommendAsync(priceyNear, CancellationToken.None);

        firstResult.Recommendation!.RankedHotels.Select(candidate => candidate.Hotel.Name)
            .Should().ContainInOrder(secondResult.Recommendation!.RankedHotels.Select(candidate => candidate.Hotel.Name));
        firstResult.Recommendation.Selected.Hotel.Name.Should().Be(NearHotel);
        secondResult.Recommendation.Selected.Hotel.Name.Should().Be(NearHotel);
    }

    [Fact]
    public async Task Recommend_uses_the_cache_on_a_repeated_request()
    {
        var (orchestrator, geocoder, _) = CreateOrchestrator(useCache: true);

        await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);
        var callCountAfterFirst = geocoder.CallCount;

        await orchestrator.RecommendAsync(ValidRequest(), CancellationToken.None);

        geocoder.CallCount.Should().Be(callCountAfterFirst);
    }

    [Fact]
    public async Task Recommend_passes_event_city_and_country_to_every_geocoding_query()
    {
        var (orchestrator, geocoder, _) = CreateOrchestrator();
        var context = new GeocodingContext("Ankara", "Turkey");
        var request = new RecommendationRequest(VenueName, [Hotel(NearHotel), Hotel(FarHotel)],
            new PersonnelInfo("S12345", "Ada", "Yılmaz"), context);

        await orchestrator.RecommendAsync(request, CancellationToken.None);

        Assert.Equal(3, geocoder.Contexts.Count);
        Assert.All(geocoder.Contexts, actual => Assert.Equal(context, actual));
    }

    private static RequestedHotel Hotel(string name, decimal amount = 1500m, string currency = "TRY") =>
        new(name, amount, currency);

    private static RecommendationRequest ValidRequest() => new(
        VenueName,
        [Hotel(NearHotel), Hotel(FarHotel)],
        new PersonnelInfo("S12345", "Ada", "Yılmaz"),
        new GeocodingContext("Ankara", "Turkey"));

    private static (HotelRecommendationOrchestrator Orchestrator, FakeGeocodingProvider Geocoder, FakeRouteDistanceProvider Router)
        CreateOrchestrator(FakeGeocodingProvider? geocoder = null, bool useCache = false)
    {
        geocoder ??= new FakeGeocodingProvider()
            .With(VenueName, VenueLocation.Latitude, VenueLocation.Longitude)
            .With(NearHotel, NearLocation.Latitude, NearLocation.Longitude)
            .With(FarHotel, FarLocation.Latitude, FarLocation.Longitude);

        var router = new FakeRouteDistanceProvider();
        router.RouteResolver = (origin, _) => RouteOutcome.Found(origin == FarLocation
            ? new RouteMetrics(9000, 900, DistanceKind.Road)
            : new RouteMetrics(1000, 300, DistanceKind.Road));

        IGeocodingProvider geocodingProvider = useCache
            ? new CachedGeocodingProvider(geocoder, new InMemoryGeocodeCache())
            : geocoder;

        var orchestrator = new HotelRecommendationOrchestrator(
            new VenueGeocodingService(geocodingProvider),
            new HotelGeocodingService(geocodingProvider),
            new DistanceRoutingService(router),
            new RankingService(),
            new RecommendationDeliveryService());

        return (orchestrator, geocoder, router);
    }
}
