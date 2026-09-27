namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Coğrafi görselde işaretlenecek tek konum: otel adı, konumu ve seçili işareti.</summary>
public sealed record StaticMapMarker
{
    private const int MinimumPathPoints = 2;

    public string Label { get; }

    public GeoPoint Location { get; }

    public bool IsSelected { get; }

    /// <summary>Genel görünümde kullanılacak ana rota geometrisi.</summary>
    public IReadOnlyList<GeoPoint> Path { get; }

    public IReadOnlyList<IReadOnlyList<GeoPoint>> PathSegments { get; }

    /// <summary>Varsa detay görünümünde kullanılacak yürüme metrikleri.</summary>
    public RouteMetrics? WalkingMetrics { get; }

    public StaticMapMarker(
        string label,
        GeoPoint location,
        bool isSelected,
        IReadOnlyList<GeoPoint>? path = null,
        RouteMetrics? walkingMetrics = null,
        IReadOnlyList<IReadOnlyList<GeoPoint>>? pathSegments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(location);

        Label = label;
        Location = location;
        IsSelected = isSelected;
        PathSegments = pathSegments is { Count: > 0 }
            ? pathSegments.Where(segment => segment.Count >= MinimumPathPoints).Select(segment => (IReadOnlyList<GeoPoint>)segment.ToArray()).ToArray()
            : path is { Count: >= MinimumPathPoints } ? [path.ToArray()] : [];
        Path = PathSegments.SelectMany(segment => segment).ToArray();
        WalkingMetrics = walkingMetrics;
    }
}

/// <summary>Coğrafi görsel isteği.</summary>
public sealed record StaticMapRequest
{
    /// <summary>Görsel boyutları için üst sınır; kaynak tükenmesini engeller.</summary>
    public const int MaxDimensionPx = 2048;

    public GeoPoint Venue { get; }

    public IReadOnlyList<StaticMapMarker> Markers { get; }

    public int WidthPx { get; }

    public int HeightPx { get; }

    public StaticMapRequest(GeoPoint venue, IReadOnlyList<StaticMapMarker> markers, int widthPx, int heightPx)
    {
        ArgumentNullException.ThrowIfNull(venue);
        ArgumentNullException.ThrowIfNull(markers);

        if (markers.Count == 0)
        {
            throw new ArgumentException("Coğrafi görsel en az bir işaret gerektirir.", nameof(markers));
        }

        if (widthPx is <= 0 or > MaxDimensionPx)
        {
            throw new ArgumentOutOfRangeException(nameof(widthPx), widthPx, $"Genişlik 1 ile {MaxDimensionPx} arasında olmalıdır.");
        }

        if (heightPx is <= 0 or > MaxDimensionPx)
        {
            throw new ArgumentOutOfRangeException(nameof(heightPx), heightPx, $"Yükseklik 1 ile {MaxDimensionPx} arasında olmalıdır.");
        }

        Venue = venue;
        Markers = markers;
        WidthPx = widthPx;
        HeightPx = heightPx;
    }
}

/// <summary>Rapor için üretilen harita görseli ve atıf bilgisi.</summary>
public sealed record StaticMapImage(
    ReadOnlyMemory<byte> Content,
    string ContentType,
    string? Attribution = null,
    bool IncludesDetailView = false);
