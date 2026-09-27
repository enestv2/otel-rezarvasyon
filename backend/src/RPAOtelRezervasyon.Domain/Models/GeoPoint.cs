namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>WGS84 koordinatı (enlem/boylam).</summary>
public sealed record GeoPoint
{
    public double Latitude { get; }

    public double Longitude { get; }

    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Enlem -90 ile 90 arasında olmalıdır.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Boylam -180 ile 180 arasında olmalıdır.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }
}
