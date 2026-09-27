namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>
/// İki konum arasındaki yol mesafesi (metre) ve süresi (saniye). Negatif olamaz. Sağlayıcı yol
/// güzergâhı (geometri) döndürdüyse <see cref="Path"/> bu güzergâhı taşır (R-11).
/// </summary>
public sealed record RouteMetrics
{
    private const int MinimumPathPoints = 2;

    public int DistanceMeters { get; }

    /// <summary>
    /// Yol süresi (saniye). <see cref="DistanceKind.StraightLine"/> fallback'inde süre ölçülemediği
    /// için <c>0</c>'dır ("ölçülmedi") ve sıralamada süre ölçütü olarak kullanılmaz (ADR-0005).
    /// </summary>
    public int DurationSeconds { get; }

    public DistanceKind Kind { get; }

    /// <summary>
    /// Sağlayıcının döndürdüğü yol güzergâhı (etkinlik alanı → otel sırasıyla). Düz çizgi
    /// fallback'inde ve geometri alınamadığında boştur; iki noktadan az güzergâh yok sayılır.
    /// </summary>
    public IReadOnlyList<GeoPoint> Path { get; }

    /// <summary>Yol geometrisinin ayrı MultiLineString bileşenleri; bileşenler arasında bağlantı çizilmez.</summary>
    public IReadOnlyList<IReadOnlyList<GeoPoint>> PathSegments { get; }

    public RouteMetrics(
        int distanceMeters,
        int durationSeconds,
        DistanceKind kind,
        IReadOnlyList<GeoPoint>? path = null,
        IReadOnlyList<IReadOnlyList<GeoPoint>>? pathSegments = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(distanceMeters);
        ArgumentOutOfRangeException.ThrowIfNegative(durationSeconds);

        DistanceMeters = distanceMeters;
        DurationSeconds = durationSeconds;
        Kind = kind;
        PathSegments = pathSegments is { Count: > 0 }
            ? pathSegments.Where(segment => segment.Count >= MinimumPathPoints).Select(segment => (IReadOnlyList<GeoPoint>)segment.ToArray()).ToArray()
            : path is { Count: >= MinimumPathPoints } ? [path.ToArray()] : [];
        Path = PathSegments.SelectMany(segment => segment).ToArray();
    }

    /// <summary>
    /// `Path` bir koleksiyon olduğundan record'un ürettiği varsayılan eşitlik yanıltıcı olurdu;
    /// değer eşitliğini açıkça güzergâh sırasına göre tanımlıyoruz.
    /// </summary>
    public bool Equals(RouteMetrics? other) =>
        other is not null
        && DistanceMeters == other.DistanceMeters
        && DurationSeconds == other.DurationSeconds
        && Kind == other.Kind
        && PathSegments.Count == other.PathSegments.Count
        && PathSegments.Zip(other.PathSegments).All(pair => pair.First.SequenceEqual(pair.Second));

    public override int GetHashCode() => HashCode.Combine(DistanceMeters, DurationSeconds, Kind, PathSegments.Count, Path.Count);
}
