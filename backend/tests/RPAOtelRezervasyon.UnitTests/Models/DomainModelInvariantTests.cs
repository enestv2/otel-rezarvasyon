using FluentAssertions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Models;

public class DomainModelInvariantTests
{
    [Theory]
    [InlineData(-91.0)]
    [InlineData(91.0)]
    [InlineData(double.NaN)]
    public void GeoPoint_rejects_invalid_latitude(double latitude)
    {
        var act = () => new GeoPoint(latitude, 29.0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RouteMetrics_rejects_negative_distance()
    {
        var act = () => new RouteMetrics(-1, 60, DistanceKind.Road);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RouteMetrics_rejects_negative_duration()
    {
        var act = () => new RouteMetrics(1000, -60, DistanceKind.Road);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Recommendation_rejects_empty_ranking()
    {
        var candidate = new HotelCandidate(
            new ContractedHotel("Grand Hotel", new NightlyPrice(1500m, "TRY")),
            new GeoPoint(41.0, 29.0),
            new RouteMetrics(1000, 600, DistanceKind.Road));

        var act = () => new Recommendation([], candidate);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ContractedHotel_DistinctByName_keeps_the_first_occurrence_and_ignores_case_and_padding()
    {
        var hotels = new[]
        {
            new ContractedHotel("Grand Hotel", new NightlyPrice(1500m, "TRY")),
            new ContractedHotel("grand hotel", new NightlyPrice(900m, "TRY")),
            new ContractedHotel("  Grand Hotel  ", new NightlyPrice(1200m, "TRY")),
        };

        var distinct = ContractedHotel.DistinctByName(hotels);

        distinct.Should().HaveCount(1);
        distinct[0].Name.Should().Be("Grand Hotel");
        distinct[0].Price.Should().Be(new NightlyPrice(1500m, "TRY"));
    }

    [Fact]
    public void NightlyPrice_accepts_zero_amount()
    {
        var price = new NightlyPrice(0m, "TRY");

        price.Amount.Should().Be(0m);
        price.Currency.Should().Be("TRY");
    }

    [Fact]
    public void NightlyPrice_rejects_negative_amount()
    {
        var act = () => new NightlyPrice(-0.01m, "TRY");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("try")]
    [InlineData("TR")]
    [InlineData("TRYY")]
    [InlineData("T1Y")]
    [InlineData("TÜR")]
    public void NightlyPrice_rejects_invalid_currency_code(string currency)
    {
        var act = () => new NightlyPrice(100m, currency);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("TRY")]
    [InlineData("EUR")]
    [InlineData("USD")]
    public void NightlyPrice_accepts_three_uppercase_ascii_letters(string currency)
    {
        var price = new NightlyPrice(100m, currency);

        price.Currency.Should().Be(currency);
    }
}
