using System.ComponentModel.DataAnnotations;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

public sealed class GeoapifyOptions
{
    public const string SectionName = "Geoapify";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://api.geoapify.com";

    public string ApiKey { get; set; } = string.Empty;

    [Range(0.1, 10.0)]
    public double RequestsPerSecond { get; set; } = 2;

    [Range(1, 5)]
    public int RetryMaxAttempts { get; set; } = 2;

    [Required, RegularExpression("^[a-z_]+$")]
    public string RoutingMode { get; set; } = "drive";
}

