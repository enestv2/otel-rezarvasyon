namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>Sıralı öneri listesi ve seçilen en uygun otel (AC-1, AC-2).</summary>
public sealed record Recommendation
{
    public IReadOnlyList<HotelCandidate> RankedHotels { get; }

    public HotelCandidate Selected { get; }

    public Recommendation(IReadOnlyList<HotelCandidate> rankedHotels, HotelCandidate selected)
    {
        ArgumentNullException.ThrowIfNull(rankedHotels);
        ArgumentNullException.ThrowIfNull(selected);

        if (rankedHotels.Count == 0)
        {
            throw new ArgumentException("Öneri listesi boş olamaz.", nameof(rankedHotels));
        }

        RankedHotels = rankedHotels;
        Selected = selected;
    }
}
