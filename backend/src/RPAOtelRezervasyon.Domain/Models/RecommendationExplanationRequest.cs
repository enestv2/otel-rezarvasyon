namespace RPAOtelRezervasyon.Domain.Models;

public sealed record RecommendationExplanationHotel(
    int Rank,
    string Name,
    decimal PriceAmount,
    string Currency,
    int DistanceMeters,
    int DurationSeconds,
    DistanceKind DistanceKind,
    double Latitude,
    double Longitude,
    bool WalkingRouteRequested,
    int? WalkingDistanceMeters,
    int? WalkingDurationSeconds);

public sealed record RecommendationExplanationPolicyOption(
    RecommendationStrategy Strategy,
    string SelectedHotelName,
    IReadOnlyList<string> AllowedReasonCodes);

/// <summary>Only recommendation evidence; personnel and provider credentials are deliberately absent.</summary>
public sealed record RecommendationExplanationRequest(
    string VenueName,
    IReadOnlyList<RecommendationExplanationHotel> RankedHotels,
    IReadOnlyList<string> UnresolvedHotels,
    IReadOnlyList<RecommendationExplanationPolicyOption> Options);

public sealed record RecommendationExplanationPolicyOutcome(
    RecommendationStrategy Strategy,
    IReadOnlyList<string> ReasonCodes);

public sealed record RecommendationExplanationOutcome
{
    public bool IsSuccess { get; }

    public IReadOnlyList<RecommendationExplanationPolicyOutcome> Options { get; }

    private RecommendationExplanationOutcome(
        bool isSuccess,
        IReadOnlyList<RecommendationExplanationPolicyOutcome> options)
    {
        IsSuccess = isSuccess;
        Options = options;
    }

    public static RecommendationExplanationOutcome Success(
        IReadOnlyList<RecommendationExplanationPolicyOutcome> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(true, options);
    }

    public static RecommendationExplanationOutcome Failure() => new(false, []);
}
