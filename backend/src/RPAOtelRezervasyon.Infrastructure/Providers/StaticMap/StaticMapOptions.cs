using System.ComponentModel.DataAnnotations;

namespace RPAOtelRezervasyon.Infrastructure.Providers.StaticMap;

public sealed class StaticMapOptions
{
    public const string SectionName = "StaticMap";

    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://maps.geoapify.com/v1/staticmap";

    public string ApiKey { get; set; } = string.Empty;

    [Required]
    public string Style { get; set; } = "osm-bright";

    [Required]
    public string Attribution { get; set; } = "Powered by Geoapify";

    [Range(0.1, 5.0)]
    public double RequestsPerSecond { get; set; } = 1;

    [Range(1, 5)]
    public int RetryMaxAttempts { get; set; } = 2;

    [Range(1, 20)]
    public int MaxZoom { get; set; } = 18;
}
