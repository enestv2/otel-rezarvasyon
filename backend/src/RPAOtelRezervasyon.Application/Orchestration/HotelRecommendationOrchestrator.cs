using RPAOtelRezervasyon.Application.Modules.Delivery;
using RPAOtelRezervasyon.Application.Modules.DistanceRouting;
using RPAOtelRezervasyon.Application.Modules.HotelGeocoding;
using RPAOtelRezervasyon.Application.Modules.Ranking;
using RPAOtelRezervasyon.Application.Modules.VenueGeocoding;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Domain.Validation;

namespace RPAOtelRezervasyon.Application.Orchestration;

/// <summary>
/// Uçtan uca akış: doğrula → otel adlarını tekilleştir → etkinlik alanını çözümle → otelleri
/// çözümle (paralel) → mesafeleri çöz (paralel) → sırala/seç → teslim et.
/// Kısmi hata yalıtılır: çözümlenemeyen/ölçülemeyen oteller sonucu düşürmez (BR-1).
/// </summary>
public sealed class HotelRecommendationOrchestrator(
    VenueGeocodingService venueGeocodingService,
    HotelGeocodingService hotelGeocodingService,
    DistanceRoutingService distanceRoutingService,
    RankingService rankingService,
    RecommendationDeliveryService deliveryService)
{
    public async Task<RecommendationResult> RecommendAsync(
        RecommendationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = RecommendationRequestValidator.Validate(
            request.VenueName,
            request.Hotels,
            request.Personnel,
            request.GeocodingContext.City,
            request.GeocodingContext.Country);
        if (!validation.IsValid)
        {
            return RecommendationResult.Invalid(validation);
        }

        var venueName = request.VenueName.Trim();

        var hotels = ContractedHotel.DistinctByName(
            request.Hotels.Select(ToContractedHotel));

        var venueOutcome = await venueGeocodingService
            .ResolveAsync(venueName, request.GeocodingContext, cancellationToken)
            .ConfigureAwait(false);

        if (venueOutcome.Status is OutcomeStatus.TransientError or OutcomeStatus.ProviderError)
        {
            return RecommendationResult.ProviderUnavailable(venueName);
        }

        if (venueOutcome.Status != OutcomeStatus.Found || venueOutcome.Location is null)
        {
            return RecommendationResult.VenueNotResolved(venueName);
        }

        var venueLocation = venueOutcome.Location;
        var hotelGeocoding = await hotelGeocodingService
            .ResolveAsync(hotels, request.GeocodingContext, cancellationToken)
            .ConfigureAwait(false);

        var routingTasks = hotelGeocoding.Resolved
            .Select(resolved => distanceRoutingService.ResolveMetricsAsync(
                resolved.Location,
                venueLocation,
                cancellationToken))
            .ToArray();

        var metrics = await Task.WhenAll(routingTasks).ConfigureAwait(false);

        var candidates = new List<HotelCandidate>();
        var unresolvedNames = new List<string>();

        foreach (var hotel in hotelGeocoding.Unresolved)
        {
            unresolvedNames.Add(hotel.Name);
        }

        for (var index = 0; index < hotelGeocoding.Resolved.Count; index++)
        {
            var resolved = hotelGeocoding.Resolved[index];
            var routeMetrics = metrics[index];

            if (routeMetrics is null)
            {
                unresolvedNames.Add(resolved.Hotel.Name);
                continue;
            }

            candidates.Add(new HotelCandidate(resolved.Hotel, resolved.Location, routeMetrics));
        }

        if (candidates.Count == 0)
        {
            return RecommendationResult.NoResolvedHotels(unresolvedNames, venueName);
        }

        var recommendation = rankingService.Select(candidates);

        return deliveryService.Deliver(venueName, venueLocation, recommendation, unresolvedNames);
    }

    /// <summary>
    /// Doğrulama geçtikten sonra çağrılır (RecommendationRequestValidator): ad, tutar ve para birimi
    /// bu noktada geçerlidir. Fiyat yalnızca otelle birlikte taşınır; sıralamaya katılmaz (R-4).
    /// </summary>
    private static ContractedHotel ToContractedHotel(RequestedHotel requested) =>
        new(requested.Name!.Trim(), new NightlyPrice(requested.Amount!.Value, requested.Currency!));
}
