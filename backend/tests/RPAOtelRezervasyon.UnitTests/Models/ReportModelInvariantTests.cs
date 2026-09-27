using FluentAssertions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Models;

public class ReportModelInvariantTests
{
    private static readonly GeoPoint Venue = new(41.0, 29.0);

    [Fact]
    public void StaticMapMarker_discards_a_single_point_path()
    {
        var marker = new StaticMapMarker("Otel", Venue, false, [new GeoPoint(41.0, 29.0)]);

        marker.Path.Should().BeEmpty();
    }

    [Fact]
    public void StaticMapMarker_keeps_a_multi_point_path()
    {
        var path = new[] { new GeoPoint(41.0, 29.0), new GeoPoint(41.1, 29.1) };

        var marker = new StaticMapMarker("Otel", Venue, false, path);

        marker.Path.Should().Equal(path);
    }
    [Fact]
    public void RouteMetrics_uses_structural_path_equality()
    {
        var first = new RouteMetrics(1000, 600, DistanceKind.Road, [new GeoPoint(41.0, 29.0), new GeoPoint(41.1, 29.1)]);
        var second = new RouteMetrics(1000, 600, DistanceKind.Road, [new GeoPoint(41.0, 29.0), new GeoPoint(41.1, 29.1)]);
        var differentPath = new RouteMetrics(1000, 600, DistanceKind.Road, [new GeoPoint(41.0, 29.0), new GeoPoint(41.2, 29.2)]);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
        first.Should().NotBe(differentPath);
    }
    private static StaticMapMarker Marker(string label = "Otel") => new(label, new GeoPoint(41.1, 29.1), false);

    [Fact]
    public void StaticMapMarker_rejects_blank_label()
    {
        var act = () => new StaticMapMarker("  ", Venue, false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StaticMapRequest_rejects_empty_markers()
    {
        var act = () => new StaticMapRequest(Venue, [], 640, 360);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0, 360)]
    [InlineData(-1, 360)]
    [InlineData(640, 0)]
    [InlineData(StaticMapRequest.MaxDimensionPx + 1, 360)]
    public void StaticMapRequest_rejects_out_of_range_dimensions(int width, int height)
    {
        var act = () => new StaticMapRequest(Venue, [Marker()], width, height);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void StaticMapOutcome_Found_rejects_empty_image()
    {
        var act = () => StaticMapOutcome.Found("fake", ReadOnlyMemory<byte>.Empty, "image/png");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReportDocument_rejects_empty_content()
    {
        var act = () => new ReportDocument(ReadOnlyMemory<byte>.Empty, ReportDocument.PdfContentType, "oneri.pdf");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReportDocument_rejects_blank_file_name()
    {
        var act = () => new ReportDocument(new byte[] { 1 }, ReportDocument.PdfContentType, " ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HotelReportRow_rejects_negative_distance()
    {
        var act = () => new HotelReportRow("Otel", -1, 60, DistanceKind.Road, new NightlyPrice(1m, "TRY"), false, Venue);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RecommendationReport_rejects_empty_ranking()
    {
        var row = new HotelReportRow("Otel", 1000, 600, DistanceKind.Road, new NightlyPrice(1m, "TRY"), true, Venue);

        var act = () => new RecommendationReport("Başlık", "Kongre", row, [], [], null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RecommendationReport_rejects_selected_hotel_outside_the_ranking()
    {
        var ranked = new HotelReportRow("Otel", 1000, 600, DistanceKind.Road, new NightlyPrice(1m, "TRY"), false, Venue);
        var selected = new HotelReportRow("Diğer", 2000, 900, DistanceKind.Road, new NightlyPrice(1m, "TRY"), true, Venue);

        var act = () => new RecommendationReport("Başlık", "Kongre", selected, [ranked], [], null, DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }
}