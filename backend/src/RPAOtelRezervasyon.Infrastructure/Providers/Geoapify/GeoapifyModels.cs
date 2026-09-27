using System.Text.Json.Serialization;

namespace RPAOtelRezervasyon.Infrastructure.Providers.Geoapify;

internal sealed class GeoapifyGeocodeResponse
{
    [JsonPropertyName("results")]
    public List<GeoapifyGeocodeResult>? Results { get; set; }
}

internal sealed class GeoapifyGeocodeResult
{
    [JsonPropertyName("lat")]
    public double Latitude { get; set; }

    [JsonPropertyName("lon")]
    public double Longitude { get; set; }

    [JsonPropertyName("formatted")]
    public string? Formatted { get; set; }
}

internal sealed class GeoapifyRouteResponse
{
    [JsonPropertyName("features")]
    public List<GeoapifyRouteFeature>? Features { get; set; }
}

internal sealed class GeoapifyRouteFeature
{
    [JsonPropertyName("properties")]
    public GeoapifyRouteProperties? Properties { get; set; }

    [JsonPropertyName("geometry")]
    public GeoapifyRouteGeometry? Geometry { get; set; }
}

internal sealed class GeoapifyRouteProperties
{
    [JsonPropertyName("distance")]
    public double Distance { get; set; }

    [JsonPropertyName("time")]
    public double Time { get; set; }
}

internal sealed class GeoapifyRouteGeometry
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("coordinates")]
    public List<List<double[]>>? MultiLineCoordinates { get; set; }
}
