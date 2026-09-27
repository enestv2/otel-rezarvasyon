using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Validation;

namespace RPAOtelRezervasyon.Application.Orchestration;

public enum RecommendationStatus
{
    Succeeded = 0,
    InvalidRequest = 1,
    VenueNotResolved = 2,
    NoResolvedHotels = 3,
    ProviderUnavailable = 4,
}

/// <summary>İstek sonucu. Başarısızlık durumları istisna değil, açık bir statüdür.</summary>
public sealed record RecommendationResult
{
    public RecommendationStatus Status { get; }

    public Recommendation? Recommendation { get; }

    public Rationale? Rationale { get; }

    public IReadOnlyList<string> UnresolvedHotels { get; }

    public ValidationResult Validation { get; }

    /// <summary>Etkinlik alanı adı (kırpılmış). Belge başlığı ve bağlam için taşınır.</summary>
    public string? VenueName { get; }

    /// <summary>
    /// Etkinlik alanı konumu. Başarılı sonuçta çözülmüştür ve coğrafi görsel için gereklidir (R-4);
    /// kısmi başarı akışında ikinci bir geocoding çağrısı yapılmasını önler.
    /// </summary>
    public GeoPoint? VenueLocation { get; }

    private RecommendationResult(
        RecommendationStatus status,
        ValidationResult validation,
        Recommendation? recommendation,
        Rationale? rationale,
        IReadOnlyList<string> unresolvedHotels,
        string? venueName,
        GeoPoint? venueLocation)
    {
        Status = status;
        Validation = validation;
        Recommendation = recommendation;
        Rationale = rationale;
        UnresolvedHotels = unresolvedHotels;
        VenueName = venueName;
        VenueLocation = venueLocation;
    }

    public static RecommendationResult Succeeded(
        string venueName,
        GeoPoint venueLocation,
        Recommendation recommendation,
        Rationale rationale,
        IReadOnlyList<string> unresolvedHotels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(venueName);
        ArgumentNullException.ThrowIfNull(venueLocation);
        ArgumentNullException.ThrowIfNull(recommendation);
        ArgumentNullException.ThrowIfNull(rationale);
        ArgumentNullException.ThrowIfNull(unresolvedHotels);

        return new RecommendationResult(
            RecommendationStatus.Succeeded,
            ValidationResult.Ok(),
            recommendation,
            rationale,
            unresolvedHotels,
            venueName,
            venueLocation);
    }

    public static RecommendationResult Invalid(ValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        return new RecommendationResult(RecommendationStatus.InvalidRequest, validation, null, null, [], null, null);
    }

    public static RecommendationResult VenueNotResolved(string? venueName = null) =>
        new(RecommendationStatus.VenueNotResolved, ValidationResult.Ok(), null, null, [], venueName, null);

    public static RecommendationResult NoResolvedHotels(
        IReadOnlyList<string> unresolvedHotels,
        string? venueName = null)
    {
        ArgumentNullException.ThrowIfNull(unresolvedHotels);

        return new RecommendationResult(
            RecommendationStatus.NoResolvedHotels,
            ValidationResult.Ok(),
            null,
            null,
            unresolvedHotels,
            venueName,
            null);
    }

    /// <summary>Etkinlik alanı sağlayıcısına erişilemedi veya sağlayıcı isteği reddetti (502).</summary>
    public static RecommendationResult ProviderUnavailable(string? venueName = null) =>
        new(RecommendationStatus.ProviderUnavailable, ValidationResult.Ok(), null, null, [], venueName, null);
}