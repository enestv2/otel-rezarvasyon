using MongoDB.Bson.Serialization.Attributes;

namespace RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB'deki konum önbelleği dokümanı (ADR-0002). Alan adları kural gereği `camelCase`'tir
/// (docs/conventions.md); `_id` = `&lt;sağlayıcı&gt;:&lt;normalize-ad&gt;`.
/// </summary>
public sealed class GeocodeCacheDocument
{
    [BsonId]
    [BsonElement("_id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("normalizedName")]
    public string NormalizedName { get; set; } = string.Empty;

    [BsonElement("provider")]
    public string ProviderId { get; set; } = string.Empty;

    [BsonElement("lat")]
    public double Latitude { get; set; }

    [BsonElement("lon")]
    public double Longitude { get; set; }

    [BsonElement("displayName")]
    public string? DisplayName { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}
