using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.UnitTests.Fakes;

/// <summary>MongoDB yerine kullanılan in-memory önbellek; ağ ve veritabanı gerektirmez.</summary>
public sealed class InMemoryGeocodeCache : IGeocodeCache
{
    private readonly Dictionary<string, CachedLocation> _entries = new(StringComparer.Ordinal);

    public int FindCount { get; private set; }

    public int StoreCount { get; private set; }

    public Task<CachedLocation?> FindAsync(string providerId, string placeName, CancellationToken cancellationToken)
    {
        FindCount++;

        _entries.TryGetValue(Key(providerId, placeName), out var cached);

        return Task.FromResult(cached);
    }

    public Task StoreAsync(
        string providerId,
        string placeName,
        GeoPoint location,
        string? displayName,
        CancellationToken cancellationToken)
    {
        StoreCount++;

        _entries[Key(providerId, placeName)] = new CachedLocation(
            location,
            providerId,
            displayName,
            new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero));

        return Task.CompletedTask;
    }

    private static string Key(string providerId, string placeName) => $"{providerId}:{placeName}";
}
