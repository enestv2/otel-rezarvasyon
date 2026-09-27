using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.HotelGeocoding;

public sealed record ResolvedHotel(ContractedHotel Hotel, GeoPoint Location);

public sealed record HotelGeocodingResult
{
    public IReadOnlyList<ResolvedHotel> Resolved { get; }

    public IReadOnlyList<ContractedHotel> Unresolved { get; }

    public HotelGeocodingResult(IReadOnlyList<ResolvedHotel> resolved, IReadOnlyList<ContractedHotel> unresolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        ArgumentNullException.ThrowIfNull(unresolved);

        Resolved = resolved;
        Unresolved = unresolved;
    }
}

/// <summary>
/// Her anlaşmalı otel adını konuma çevirir. Çağrılar **paralel** yürütülür; sonuç sırası girdi
/// sırasını korur (determinizm). Çözümlenemeyen oteller isteği düşürmez (BR-1, AC-4).
/// </summary>
public sealed class HotelGeocodingService(IGeocodingProvider geocodingProvider)
{
    public async Task<HotelGeocodingResult> ResolveAsync(
        IReadOnlyList<ContractedHotel> hotels,
        CancellationToken cancellationToken)
        => await ResolveAsync(hotels, GeocodingContext.Empty, cancellationToken).ConfigureAwait(false);

    public async Task<HotelGeocodingResult> ResolveAsync(
        IReadOnlyList<ContractedHotel> hotels,
        GeocodingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hotels);

        var tasks = hotels.Select(hotel => ResolveOneAsync(hotel, context, cancellationToken)).ToArray();
        var locations = await Task.WhenAll(tasks).ConfigureAwait(false);

        var resolved = new List<ResolvedHotel>();
        var unresolved = new List<ContractedHotel>();

        for (var index = 0; index < hotels.Count; index++)
        {
            var location = locations[index];
            if (location is null)
            {
                unresolved.Add(hotels[index]);
            }
            else
            {
                resolved.Add(new ResolvedHotel(hotels[index], location));
            }
        }

        return new HotelGeocodingResult(resolved, unresolved);
    }

    private async Task<GeoPoint?> ResolveOneAsync(
        ContractedHotel hotel,
        GeocodingContext context,
        CancellationToken cancellationToken)
    {
        var outcome = await geocodingProvider.GeocodeAsync(hotel.Name, context, cancellationToken).ConfigureAwait(false);

        return outcome.Status == OutcomeStatus.Found ? outcome.Location : null;
    }
}
