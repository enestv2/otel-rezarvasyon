using System.ComponentModel.DataAnnotations;

namespace RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB konum önbelleği ayarları. `Mongo` yapılandırma bölümünden bağlanır; bağlantı dizesi
/// sırdır ve `appsettings*.json` içine yazılmaz (docs/security.md).
/// </summary>
public sealed class MongoGeocodeCacheOptions
{
    public const string SectionName = "Mongo";

    [Required(ErrorMessage = "Mongo:ConnectionString zorunludur.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Mongo:Database zorunludur.")]
    public string Database { get; set; } = "rpaotelrezervasyon";

    [Required(AllowEmptyStrings = false, ErrorMessage = "Mongo:GeocodeCollection zorunludur.")]
    public string GeocodeCollection { get; set; } = "geocode_cache";

    /// <summary>TTL süresi (gün). BR-7: eski kayıtlar otomatik temizlenir.</summary>
    [Range(1, 3650, ErrorMessage = "Mongo:CacheTtlDays 1 ile 3650 arasında olmalıdır.")]
    public int CacheTtlDays { get; set; } = 180;
}
