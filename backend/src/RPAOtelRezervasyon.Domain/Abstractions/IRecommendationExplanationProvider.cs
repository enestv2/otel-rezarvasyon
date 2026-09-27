using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Abstractions;

/// <summary>Generates a constrained explanation for an already-ranked recommendation.</summary>
public interface IRecommendationExplanationProvider
{
    Task<RecommendationExplanationOutcome> GenerateAsync(
        RecommendationExplanationRequest request,
        CancellationToken cancellationToken);
}
