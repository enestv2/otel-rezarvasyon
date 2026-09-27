using System.Collections.Concurrent;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.IntegrationTests.Support;

/// <summary>MongoDB olmadan önbellek davranışını (BR-4) sınamak için süreç içi sahte önbellek.</summary>
internal sealed class InMemoryGeocodeCache : IGeocodeCache
{
    private readonly ConcurrentDictionary<string, CachedLocation> _entries = new(StringComparer.Ordinal);

    /// <summary>Önbellekteki kayıtların anahtar kümesi; yalnızca test doğrulaması için (0006 AC-11).</summary>
    public ICollection<string> Keys => _entries.Keys;

    public Task<CachedLocation?> FindAsync(string providerId, string placeName, CancellationToken cancellationToken) =>
        Task.FromResult(_entries.TryGetValue(Key(providerId, placeName), out var entry) ? entry : null);

    public Task StoreAsync(
        string providerId,
        string placeName,
        GeoPoint location,
        string? displayName,
        CancellationToken cancellationToken)
    {
        _entries[Key(providerId, placeName)] = new CachedLocation(location, providerId, displayName, DateTimeOffset.UtcNow);

        return Task.CompletedTask;
    }

    private static string Key(string providerId, string placeName) => $"{providerId}:{placeName}";
}
