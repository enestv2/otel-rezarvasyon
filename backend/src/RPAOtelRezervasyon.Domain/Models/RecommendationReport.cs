namespace RPAOtelRezervasyon.Domain.Models;

public enum RecommendationStrategy
{
    BudgetPriority,
    TransportPriority,
    Balanced,
}

public sealed record RecommendationReportOption(
    RecommendationStrategy Strategy,
    HotelReportRow? Hotel,
    string Explanation);

/// <summary>PDF satırı: temel rota/fiyat değerleri JSON ile aynıdır; yürüyüş ölçümleri yalnızca PDF'ye özeldir.</summary>
public sealed record HotelReportRow
{
    public string Name { get; }

    public int DistanceMeters { get; }

    public int DurationSeconds { get; }

    public DistanceKind DistanceKind { get; }

    public NightlyPrice Price { get; }

    public bool IsSelected { get; }

    public GeoPoint Location { get; }

    public RouteMetrics? WalkingMetrics { get; }

    public bool WalkingRouteRequested { get; }

    public HotelReportRow(
        string name,
        int distanceMeters,
        int durationSeconds,
        DistanceKind distanceKind,
        NightlyPrice price,
        bool isSelected,
        GeoPoint location,
        RouteMetrics? walkingMetrics = null,
        bool walkingRouteRequested = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(location);

        Name = name;
        DistanceMeters = distanceMeters;
        DurationSeconds = durationSeconds;
        DistanceKind = distanceKind;
        Price = price;
        IsSelected = isSelected;
        Location = location;
        WalkingMetrics = walkingMetrics;
        WalkingRouteRequested = walkingRouteRequested;
    }
}

/// <summary>
/// Belge içeriğinin deterministik modeli (R-1..R-9). Çekirdek, PDF veya görsel kütüphanesi bilmez;
/// renderer bu modeli belgeye çevirir. Değerler JSON öneriyle aynı kaynaktan gelir (R-5).
/// </summary>
public sealed record RecommendationReport
{
    public string Title { get; }

    public string VenueName { get; }

    public HotelReportRow SelectedHotel { get; }

    public IReadOnlyList<HotelReportRow> RankedHotels { get; }

    public IReadOnlyList<string> UnresolvedHotels { get; }

    public StaticMapImage? Map { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public PersonnelInfo? Personnel { get; }

    public string Explanation { get; }

    public IReadOnlyList<RecommendationReportOption> Options { get; }

    public RecommendationReport(
        string title,
        string venueName,
        HotelReportRow selectedHotel,
        IReadOnlyList<HotelReportRow> rankedHotels,
        IReadOnlyList<string> unresolvedHotels,
        StaticMapImage? map,
        DateTimeOffset generatedAtUtc,
        PersonnelInfo? personnel = null,
        string explanation = "",
        IReadOnlyList<RecommendationReportOption>? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(venueName);
        ArgumentNullException.ThrowIfNull(selectedHotel);
        ArgumentNullException.ThrowIfNull(rankedHotels);
        ArgumentNullException.ThrowIfNull(unresolvedHotels);

        if (rankedHotels.Count == 0)
        {
            throw new ArgumentException("Rapor listesi boş olamaz.", nameof(rankedHotels));
        }

        if (!rankedHotels.Any(row => string.Equals(row.Name, selectedHotel.Name, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Seçilen otel rapor listesinde bulunmalıdır.", nameof(selectedHotel));
        }

        Title = title;
        VenueName = venueName;
        SelectedHotel = selectedHotel;
        RankedHotels = rankedHotels;
        UnresolvedHotels = unresolvedHotels;
        Map = map;
        GeneratedAtUtc = generatedAtUtc;
        Personnel = personnel;
        Explanation = explanation;
        Options = options ?? [];
    }
}
