using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Ranking;

namespace RPAOtelRezervasyon.Application.Modules.Ranking;

/// <summary>Adayları BR-6'ya göre sıralar ve en uygun oteli seçer (AC-1, AC-2).</summary>
public sealed class RankingService
{
    public Recommendation Select(IReadOnlyList<HotelCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var ranked = HotelRankingPolicy.Rank(candidates);

        return new Recommendation(ranked, ranked[0]);
    }
}
