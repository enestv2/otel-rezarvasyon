namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>
/// Sıralamaya girebilecek otel: konumu ve yol metrikleri **çözümlenmiş** olmalıdır.
/// Konumu ya da metriği olmayan otel aday olamaz (AC-7).
/// </summary>
public sealed record HotelCandidate
{
    public ContractedHotel Hotel { get; }

    public GeoPoint Location { get; }

    public RouteMetrics Metrics { get; }

    public HotelCandidate(ContractedHotel hotel, GeoPoint location, RouteMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(metrics);

        Hotel = hotel;
        Location = location;
        Metrics = metrics;
    }
}
