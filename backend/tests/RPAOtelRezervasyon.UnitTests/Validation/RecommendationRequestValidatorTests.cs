using FluentAssertions;
using RPAOtelRezervasyon.Domain.Validation;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Validation;

public class RecommendationRequestValidatorTests
{
    private static RequestedHotel Hotel(string? name, decimal? amount = 1500m, string? currency = "TRY") =>
        new(name, amount, currency);

    [Fact]
    public void Validate_accepts_a_well_formed_request()
    {
        var result = RecommendationRequestValidator.Validate(
            "Ankara ATO Kongre Merkezi",
            [Hotel("Grand Hotel"), Hotel("Park Otel", 900m, "EUR")]);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_requires_personnel_and_event_location_context_for_api_requests()
    {
        var result = RecommendationRequestValidator.Validate(
            "Kongre Merkezi", [Hotel("Grand Hotel")], new PersonnelInfo(" ", "Ada", "Yılmaz"), "A", null);

        result.Issues.Select(issue => issue.Code).Should().Contain([
            "personnel-registration-required", "event-city-invalid", "event-country-invalid"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_rejects_blank_venue_name(string? venueName)
    {
        var result = RecommendationRequestValidator.Validate(venueName, [Hotel("Grand Hotel")]);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "venue-name-required");
    }

    [Fact]
    public void Validate_rejects_empty_hotel_list()
    {
        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", []);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "hotels-required");
    }

    [Fact]
    public void Validate_rejects_more_hotels_than_the_limit()
    {
        var hotels = Enumerable.Range(0, RecommendationRequestValidator.MaxHotels + 1)
            .Select(index => Hotel($"Otel {index}"))
            .ToList();

        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", hotels);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code == "hotels-too-many");
    }

    [Fact]
    public void Validate_rejects_venue_name_shorter_than_the_limit()
    {
        var result = RecommendationRequestValidator.Validate("ab", [Hotel("Grand Hotel")]);

        result.Issues.Should().Contain(issue => issue.Code == "venue-name-length");
    }

    [Fact]
    public void Validate_rejects_venue_name_longer_than_the_limit()
    {
        var longName = new string('a', RecommendationRequestValidator.MaxVenueNameLength + 1);

        var result = RecommendationRequestValidator.Validate(longName, [Hotel("Grand Hotel")]);

        result.Issues.Should().Contain(issue => issue.Code == "venue-name-length");
    }

    [Fact]
    public void Validate_rejects_blank_hotel_name()
    {
        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", [Hotel("Grand Hotel"), Hotel("  ")]);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code.StartsWith("hotel-name-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_hotel_name_longer_than_the_limit()
    {
        var longName = new string('b', RecommendationRequestValidator.MaxHotelNameLength + 1);

        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", [Hotel(longName)]);

        result.Issues.Should().Contain(issue => issue.Code.StartsWith("hotel-name-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_does_not_treat_duplicate_hotel_names_as_an_error()
    {
        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", [Hotel("Grand Hotel"), Hotel("Grand Hotel")]);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_accepts_zero_price()
    {
        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", [Hotel("Grand Hotel", 0m, "TRY")]);

        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public void Validate_rejects_negative_amount(double amount)
    {
        var result = RecommendationRequestValidator.Validate(
            "Kongre Merkezi",
            [Hotel("Grand Hotel", (decimal)amount, "TRY")]);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code.StartsWith("hotel-price-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rejects_missing_amount()
    {
        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", [Hotel("Grand Hotel", null, "TRY")]);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code.StartsWith("hotel-price-invalid", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("try")]
    [InlineData("TR")]
    [InlineData("TRYY")]
    [InlineData("T1Y")]
    [InlineData(null)]
    public void Validate_rejects_invalid_currency_code(string? currency)
    {
        var result = RecommendationRequestValidator.Validate("Kongre Merkezi", [Hotel("Grand Hotel", 1500m, currency)]);

        result.IsValid.Should().BeFalse();
        result.Issues.Should().Contain(issue => issue.Code.StartsWith("hotel-currency-invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_reports_the_index_of_the_invalid_hotel()
    {
        var result = RecommendationRequestValidator.Validate(
            "Kongre Merkezi",
            [Hotel("Grand Hotel"), Hotel("Park Otel", -5m, "TRY")]);

        result.Issues.Should().Contain(issue => issue.Code == "hotel-price-invalid[1]");
    }
}
