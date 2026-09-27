namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Dış sağlayıcı çağrısının beklenen sonuç durumları.</summary>
public enum OutcomeStatus
{
    Found = 0,
    NotFound = 1,
    TransientError = 2,
    ProviderError = 3,
}

/// <summary>Geocoding (ad → konum) sonucu. Beklenen durumlar istisna değil, sonuçtur.</summary>
public sealed record GeocodingOutcome
{
    public OutcomeStatus Status { get; }

    public string ProviderId { get; }

    public GeoPoint? Location { get; }

    public string? DisplayName { get; }

    private GeocodingOutcome(OutcomeStatus status, string providerId, GeoPoint? location, string? displayName)
    {
        Status = status;
        ProviderId = providerId;
        Location = location;
        DisplayName = displayName;
    }

    public static GeocodingOutcome Found(string providerId, GeoPoint location, string? displayName = null) =>
        new(OutcomeStatus.Found, providerId, location, displayName);

    public static GeocodingOutcome NotFound(string providerId) =>
        new(OutcomeStatus.NotFound, providerId, null, null);

    public static GeocodingOutcome TransientError(string providerId) =>
        new(OutcomeStatus.TransientError, providerId, null, null);

    /// <summary>Sağlayıcı isteği reddetti (ör. 403) veya beklenmeyen bir HTTP hatası döndürdü.</summary>
    public static GeocodingOutcome ProviderError(string providerId) =>
        new(OutcomeStatus.ProviderError, providerId, null, null);
}

/// <summary>Routing (konum çifti → mesafe/süre) sonucu.</summary>
public sealed record RouteOutcome
{
    public OutcomeStatus Status { get; }

    public RouteMetrics? Metrics { get; }

    private RouteOutcome(OutcomeStatus status, RouteMetrics? metrics)
    {
        Status = status;
        Metrics = metrics;
    }

    public static RouteOutcome Found(RouteMetrics metrics) => new(OutcomeStatus.Found, metrics);

    public static RouteOutcome NotFound() => new(OutcomeStatus.NotFound, null);

    public static RouteOutcome TransientError() => new(OutcomeStatus.TransientError, null);

    public static RouteOutcome ProviderError() => new(OutcomeStatus.ProviderError, null);
}
