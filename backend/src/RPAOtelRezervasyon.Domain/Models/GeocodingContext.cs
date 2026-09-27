namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Coğrafi sorgunun idari bağlamı; sağlayıcı adaptörleri bunu arama alanlarına eşler.</summary>
public sealed record GeocodingContext(string? City, string? Country)
{
    public static GeocodingContext Empty { get; } = new(null, null);
}
