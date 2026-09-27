using FluentAssertions;
using RPAOtelRezervasyon.Application.Modules.Reporting;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Reporting;

public sealed class RecommendationExplanationServiceTests
{
    [Fact]
    public async Task ExplainAsync_returns_budget_transport_and_balanced_choices_from_server_rules()
    {
        var provider = new RecordingProvider(ValidOutcome());
        var options = await CreateService(provider).ExplainAsync("Etkinlik Alanı", Rows(), ["Konum Yok"], CancellationToken.None);

        options.Should().HaveCount(3);
        HotelFor(options, RecommendationStrategy.BudgetPriority).Name.Should().Be("Otel B");
        HotelFor(options, RecommendationStrategy.TransportPriority).Name.Should().Be("Otel A");
        HotelFor(options, RecommendationStrategy.Balanced).Name.Should().Be("Otel B");
        options.Single(option => option.Strategy == RecommendationStrategy.BudgetPriority).Explanation.Should().Contain("tutarı en düşük seçenek");
        options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation.Should().Contain("%33.3");
        provider.Request!.RankedHotels.Should().HaveCount(2);
        provider.Request.UnresolvedHotels.Should().ContainSingle().Which.Should().Be("Konum Yok");
        provider.Request.Options.Select(option => option.Strategy).Should().Contain([RecommendationStrategy.BudgetPriority, RecommendationStrategy.TransportPriority, RecommendationStrategy.Balanced]);
        provider.Request.Options.Select(option => option.SelectedHotelName).Should().Contain(["Otel A", "Otel B"]);
        typeof(RecommendationExplanationRequest).GetProperties().Select(property => property.Name)
            .Should().NotContain("Personnel");
    }

    [Fact]
    public async Task ExplainAsync_includes_only_measured_walking_metrics()
    {
        var options = await CreateService(new RecordingProvider(RecommendationExplanationOutcome.Failure()))
            .ExplainAsync("Etkinlik Alanı", Rows(withWalking: true), [], CancellationToken.None);

        options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation
            .Should().Contain("Ölçülmüş yürüyüş rotası ayrıca 850 m ve 9 dk sürüyor");
        options.Single(option => option.Strategy == RecommendationStrategy.BudgetPriority).Explanation
            .Should().NotContain("ölçülemedi");
    }

    [Fact]
    public async Task ExplainAsync_includes_exact_ten_percent_price_premium_in_balanced_choice()
    {
        var rows = new[]
        {
            Row("En Ucuz", 900m, 1200, 1200),
            Row("Denge", 990m, 300, 500),
            Row("Esik Üstü", 991m, 60, 100),
        };

        var options = await CreateService(new RecordingProvider(RecommendationExplanationOutcome.Failure()))
            .ExplainAsync("Etkinlik Alanı", rows, [], CancellationToken.None);

        HotelFor(options, RecommendationStrategy.Balanced).Name.Should().Be("Denge");
        options.Single(option => option.Strategy == RecommendationStrategy.Balanced).Explanation
            .Should().Contain("%10 üzerindeki bütçe aralığında");
    }

    [Fact]
    public async Task ExplainAsync_keeps_distance_tiebreak_reason_even_if_model_omits_optional_code()
    {
        var rows = new[]
        {
            Row("Otel A", 1200m, 600, 1000),
            Row("Otel B", 900m, 600, 5000),
        };
        var incompleteButAllowlisted = RecommendationExplanationOutcome.Success(
        [
            new(RecommendationStrategy.BudgetPriority, ["lowest-price", "significant-price-gap"]),
            new(RecommendationStrategy.TransportPriority, ["shortest-road-time"]),
            new(RecommendationStrategy.Balanced, ["within-ten-percent-budget-band", "significant-price-gap"]),
        ]);

        var options = await CreateService(new RecordingProvider(incompleteButAllowlisted))
            .ExplainAsync("Etkinlik Alanı", rows, [], CancellationToken.None);

        options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation
            .Should().Contain("Tahmini yol süreleri eşit olduğundan daha kısa yol mesafesi belirleyici oldu");
    }

    [Fact]
    public async Task ExplainAsync_scopes_distance_tiebreak_to_each_policy_candidate_set()
    {
        var rows = new[]
        {
            Row("Daha ucuz", 900m, 600, 1000),
            Row("Daha uzak", 1000m, 600, 5000),
        };

        var options = await CreateService(new RecordingProvider(RecommendationExplanationOutcome.Failure()))
            .ExplainAsync("Etkinlik Alanı", rows, [], CancellationToken.None);

        options.Single(option => option.Strategy == RecommendationStrategy.BudgetPriority).Explanation
            .Should().NotContain("daha kısa yol mesafesi belirleyici oldu");
        options.Single(option => option.Strategy == RecommendationStrategy.Balanced).Explanation
            .Should().NotContain("daha kısa yol mesafesi belirleyici oldu");
        options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation
            .Should().Contain("daha kısa yol mesafesi belirleyici oldu");
    }
    [Fact]
    public async Task ExplainAsync_does_not_compare_prices_across_currencies()
    {
        var rows = new[]
        {
            Row("TRY Oteli", 1000m, 1000, 1000, "TRY"),
            Row("EUR Oteli", 900m, 500, 500, "EUR"),
        };

        var options = await CreateService(new RecordingProvider(RecommendationExplanationOutcome.Failure()))
            .ExplainAsync("Etkinlik Alanı", rows, [], CancellationToken.None);

        options.Single(option => option.Strategy == RecommendationStrategy.BudgetPriority).Hotel.Should().BeNull();
        options.Single(option => option.Strategy == RecommendationStrategy.Balanced).Hotel.Should().BeNull();
        options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Hotel!.Name.Should().Be("EUR Oteli");
        options.Single(option => option.Strategy == RecommendationStrategy.BudgetPriority).Explanation
            .Should().Contain("para birimlerindeki tutarlar ortak bir fiyat sıralaması");
    }

    [Fact]
    public async Task ExplainAsync_rejects_unapproved_reason_codes_and_keeps_server_computed_selection()
    {
        var invalid = RecommendationExplanationOutcome.Success(
        [
            new(RecommendationStrategy.BudgetPriority, ["lowest-price", "hotel-amenities"]),
            new(RecommendationStrategy.TransportPriority, ["shortest-road-time"]),
            new(RecommendationStrategy.Balanced, ["within-ten-percent-budget-band"]),
        ]);
        var options = await CreateService(new RecordingProvider(invalid))
            .ExplainAsync("Etkinlik Alanı", Rows(), [], CancellationToken.None);

        HotelFor(options, RecommendationStrategy.TransportPriority).Name.Should().Be("Otel A");
        options.Single(option => option.Strategy == RecommendationStrategy.TransportPriority).Explanation
            .Should().Contain("En ucuz seçenek Otel B");
    }

    [Fact]
    public async Task ExplainAsync_does_not_call_provider_when_disabled()
    {
        var provider = new RecordingProvider(RecommendationExplanationOutcome.Failure());
        await CreateService(provider, enabled: false).ExplainAsync("Etkinlik Alanı", Rows(), [], CancellationToken.None);
        provider.CallCount.Should().Be(0);
    }

    private static RecommendationExplanationService CreateService(RecordingProvider provider, bool enabled = true) =>
        new(provider, new RecommendationExplanationOptions(enabled));

    private static HotelReportRow HotelFor(IReadOnlyList<RecommendationReportOption> options, RecommendationStrategy strategy) =>
        options.Single(option => option.Strategy == strategy).Hotel!;

    private static IReadOnlyList<HotelReportRow> Rows(bool withWalking = false) =>
    [
        Row("Otel A", 1200m, 600, 1000, walking: withWalking ? new RouteMetrics(850, 540, DistanceKind.Road) : null),
        Row("Otel B", 900m, 1500, 5000),
    ];

    private static HotelReportRow Row(
        string name,
        decimal price,
        int duration,
        int distance,
        string currency = "TRY",
        RouteMetrics? walking = null) =>
        new(name, distance, duration, DistanceKind.Road, new NightlyPrice(price, currency), false, new GeoPoint(41.1, 29.1), walking);

    private static RecommendationExplanationOutcome ValidOutcome() => RecommendationExplanationOutcome.Success(
    [
        new(RecommendationStrategy.BudgetPriority, ["lowest-price", "significant-price-gap"]),
        new(RecommendationStrategy.TransportPriority, ["shortest-road-time", "significant-price-gap"]),
        new(RecommendationStrategy.Balanced, ["within-ten-percent-budget-band", "significant-price-gap"]),
    ]);

    private sealed class RecordingProvider(RecommendationExplanationOutcome outcome) : IRecommendationExplanationProvider
    {
        public RecommendationExplanationRequest? Request { get; private set; }

        public int CallCount { get; private set; }

        public Task<RecommendationExplanationOutcome> GenerateAsync(
            RecommendationExplanationRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Request = request;
            return Task.FromResult(outcome);
        }
    }
}
