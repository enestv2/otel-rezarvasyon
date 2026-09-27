using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Orchestration;

/// <summary>
/// AC-2 / G-2: seçim gerekçesi serbest metin değil, yapılandırılmış alandır:
/// seçilen otelin süresi, mesafesi, mesafe türü ve kaç otel arasından seçildiği.
/// </summary>
public sealed record Rationale
{
    public string HotelName { get; }

    public int DurationSeconds { get; }

    public int DistanceMeters { get; }

    public DistanceKind DistanceKind { get; }

    public int EvaluatedHotelCount { get; }

    public Rationale(
        string hotelName,
        int durationSeconds,
        int distanceMeters,
        DistanceKind distanceKind,
        int evaluatedHotelCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hotelName);
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(evaluatedHotelCount);

        HotelName = hotelName;
        DurationSeconds = durationSeconds;
        DistanceMeters = distanceMeters;
        DistanceKind = distanceKind;
        EvaluatedHotelCount = evaluatedHotelCount;
    }

    public static Rationale For(HotelCandidate selected, int evaluatedHotelCount)
    {
        ArgumentNullException.ThrowIfNull(selected);

        return new Rationale(
            selected.Hotel.Name,
            selected.Metrics.DurationSeconds,
            selected.Metrics.DistanceMeters,
            selected.Metrics.Kind,
            evaluatedHotelCount);
    }
}
