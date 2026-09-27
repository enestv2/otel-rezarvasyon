namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>
/// Coğrafi görsel üretiminin beklenen sonucu: sağlayıcı hatası istisna değil sonuçtur
/// (docs/architecture.md). Görsel üretilemezse belge görselsiz üretilir (R-8).
/// </summary>
public sealed record StaticMapOutcome
{
    public OutcomeStatus Status { get; }

    public string ProviderId { get; }

    public ReadOnlyMemory<byte>? Image { get; }

    public string? ContentType { get; }

    public string? Attribution { get; }

    public bool IncludesDetailView { get; }

    private StaticMapOutcome(
        OutcomeStatus status,
        string providerId,
        ReadOnlyMemory<byte>? image,
        string? contentType,
        string? attribution,
        bool includesDetailView)
    {
        Status = status;
        ProviderId = providerId;
        Image = image;
        ContentType = contentType;
        Attribution = attribution;
        IncludesDetailView = includesDetailView;
    }

    public static StaticMapOutcome Found(
        string providerId,
        ReadOnlyMemory<byte> image,
        string contentType,
        string? attribution = null,
        bool includesDetailView = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);

        if (image.IsEmpty)
        {
            throw new ArgumentException("Görsel içeriği boş olamaz.", nameof(image));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return new StaticMapOutcome(OutcomeStatus.Found, providerId, image, contentType, attribution, includesDetailView);
    }

    public static StaticMapOutcome NotFound(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return new StaticMapOutcome(OutcomeStatus.NotFound, providerId, null, null, null, false);
    }

    public static StaticMapOutcome TransientError(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return new StaticMapOutcome(OutcomeStatus.TransientError, providerId, null, null, null, false);
    }

    public static StaticMapOutcome ProviderError(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        return new StaticMapOutcome(OutcomeStatus.ProviderError, providerId, null, null, null, false);
    }
}
