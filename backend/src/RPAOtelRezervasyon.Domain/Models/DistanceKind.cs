namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Mesafenin nasıl elde edildiği (BR-2).</summary>
public enum DistanceKind
{
    /// <summary>Routing sağlayıcısından gelen yol mesafesi/süresi.</summary>
    Road = 0,

    /// <summary>Düz çizgi (haversine) fallback'i; yanıtta işaretlenir.</summary>
    StraightLine = 1,
}
