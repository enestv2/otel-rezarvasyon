using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Ranking;

/// <summary>
/// BR-6 (ADR-0005): önce **mesafe türü** — ölçülmüş yol (<see cref="DistanceKind.Road"/>) her zaman
/// düz çizgi tahmininden (<see cref="DistanceKind.StraightLine"/>) önce gelir. Road grubunda süre →
/// mesafe → otel adı; StraightLine grubunda (süre 0 = ölçülmedi) mesafe → otel adı. Sıralama
/// tamamen deterministiktir (AC-8, AC-12).
/// </summary>
public static class HotelRankingPolicy
{
    public static IReadOnlyList<HotelCandidate> Rank(IEnumerable<HotelCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .OrderBy(candidate => candidate.Metrics.Kind)
            .ThenBy(candidate => candidate.Metrics.DurationSeconds)
            .ThenBy(candidate => candidate.Metrics.DistanceMeters)
            .ThenBy(candidate => candidate.Hotel.Name, StringComparer.Ordinal)
            .ToList();
    }
}
