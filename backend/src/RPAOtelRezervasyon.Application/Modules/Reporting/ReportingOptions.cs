using System.ComponentModel.DataAnnotations;

namespace RPAOtelRezervasyon.Application.Modules.Reporting;

/// <summary>
/// Belge üretim sınırları ve şablon varsayılanları (R-1, kapsam notları). Kültürden bağımsızdır;
/// değerler yapılandırmadan bağlanır ve başlangıçta doğrulanır.
/// </summary>
public sealed class ReportingOptions
{
    public const string SectionName = "Reporting";

    /// <summary>Coğrafi görsel genişliği (piksel). Üst sınır <c>StaticMapRequest.MaxDimensionPx</c>.</summary>
    [Range(64, 2048)]
    public int MapWidthPx { get; set; } = 640;

    /// <summary>Coğrafi görsel yüksekliği (piksel). Üst sınır <c>StaticMapRequest.MaxDimensionPx</c>.</summary>
    [Range(64, 2048)]
    public int MapHeightPx { get; set; } = 360;

    /// <summary>Belge boyutu üst sınırı (bayt); aşılırsa belge üretilmez.</summary>
    [Range(1024, 20971520)]
    public int MaxDocumentBytes { get; set; } = 5242880;

    /// <summary>Belge başlığı (Türkçe şablon).</summary>
    [Required]
    public string DocumentTitle { get; set; } = "Otel Önerisi";
}