using RPAOtelRezervasyon.Application.Orchestration;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.Delivery;

/// <summary>
/// V1'de teslim, çağırana dönen sonuçtur (REST gövdesi). İleride kuyruk/olay eklenirse burası genişler.
/// </summary>
public sealed class RecommendationDeliveryService
{
    public RecommendationResult Deliver(
        string venueName,
        GeoPoint venueLocation,
        Recommendation recommendation,
        IReadOnlyList<string> unresolvedHotels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(venueName);
        ArgumentNullException.ThrowIfNull(venueLocation);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(unresolvedHotels);

        var rationale = Rationale.For(recommendation.Selected, recommendation.RankedHotels.Count);

        return RecommendationResult.Succeeded(venueName, venueLocation, recommendation, rationale, unresolvedHotels);
    }
}