using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Modules.VenueGeocoding;

/// <summary>Etkinlik alanı adını konuma çevirir.</summary>
public sealed class VenueGeocodingService(IGeocodingProvider geocodingProvider)
{
    public Task<GeocodingOutcome> ResolveAsync(string venueName, CancellationToken cancellationToken) =>
        geocodingProvider.GeocodeAsync(venueName, cancellationToken);

    public Task<GeocodingOutcome> ResolveAsync(
        string venueName,
        GeocodingContext context,
        CancellationToken cancellationToken) =>
        geocodingProvider.GeocodeAsync(venueName, context, cancellationToken);
}
