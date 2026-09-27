using FluentAssertions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Ranking;

namespace RPAOtelRezervasyon.UnitTests.Ranking;

public class HotelRankingPolicyTests
{
    private static HotelCandidate Candidate(
        string name,
        int durationSeconds,
        int distanceMeters,
        DistanceKind kind = DistanceKind.Road) =>
        new(
            new ContractedHotel(name, new NightlyPrice(1500m, "TRY")),
            new GeoPoint(41.0, 29.0),
            new RouteMetrics(distanceMeters, durationSeconds, kind));

    [Fact]
    public void Rank_orders_by_duration_ascending()
    {
        var slow = Candidate("Yavaş Otel", 900, 1000);
        var fast = Candidate("Hızlı Otel", 300, 5000);

        var ranked = HotelRankingPolicy.Rank([slow, fast]);

        ranked.Select(candidate => candidate.Hotel.Name).Should().ContainInOrder("Hızlı Otel", "Yavaş Otel");
    }

    [Fact]
    public void Rank_breaks_tie_by_distance_when_duration_is_equal()
    {
        var near = Candidate("Yakın Otel", 600, 1000);
        var far = Candidate("Uzak Otel", 600, 9000);

        var ranked = HotelRankingPolicy.Rank([far, near]);

        ranked.Select(candidate => candidate.Hotel.Name).Should().ContainInOrder("Yakın Otel", "Uzak Otel");
    }

    [Fact]
    public void Rank_breaks_tie_by_name_alphabetically_when_duration_and_distance_are_equal()
    {
        var zeta = Candidate("Zeta Otel", 600, 1000);
        var alfa = Candidate("Alfa Otel", 600, 1000);

        var ranked = HotelRankingPolicy.Rank([zeta, alfa]);

        ranked.Select(candidate => candidate.Hotel.Name).Should().ContainInOrder("Alfa Otel", "Zeta Otel");
    }

    [Fact]
    public void Rank_is_deterministic_regardless_of_input_order()
    {
        HotelCandidate[] candidates =
        [
            Candidate("C Otel", 500, 1000),
            Candidate("A Otel", 500, 1000),
            Candidate("B Otel", 500, 1000),
        ];

        var forward = HotelRankingPolicy.Rank(candidates).Select(candidate => candidate.Hotel.Name).ToList();
        var reversed = HotelRankingPolicy.Rank(candidates.Reverse()).Select(candidate => candidate.Hotel.Name).ToList();

        reversed.Should().ContainInOrder(forward);
    }

    [Fact]
    public void Rank_places_measured_road_candidates_before_straight_line_fallbacks()
    {
        var road = Candidate("Yol Oteli", 1800, 20_000);
        var fallback = Candidate("Tahmin Oteli", 0, 300, DistanceKind.StraightLine);

        var ranked = HotelRankingPolicy.Rank([fallback, road]);

        ranked.Select(candidate => candidate.Hotel.Name).Should().ContainInOrder("Yol Oteli", "Tahmin Oteli");
    }
}
