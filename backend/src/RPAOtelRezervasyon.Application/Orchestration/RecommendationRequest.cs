using RPAOtelRezervasyon.Domain.Validation;
using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Application.Orchestration;

/// <summary>Servise gelen ham istek: etkinlik alanı adı + fiyatlı anlaşmalı otel girdileri (R-1).</summary>
public sealed record RecommendationRequest
{
    public string VenueName { get; }

    public IReadOnlyList<RequestedHotel> Hotels { get; }

    public PersonnelInfo Personnel { get; }

    public GeocodingContext GeocodingContext { get; }

    public RecommendationRequest(
        string venueName,
        IReadOnlyList<RequestedHotel> hotels,
        PersonnelInfo? personnel = null,
        GeocodingContext? geocodingContext = null)
    {
        ArgumentNullException.ThrowIfNull(venueName);
        ArgumentNullException.ThrowIfNull(hotels);

        VenueName = venueName;
        Hotels = hotels;
        Personnel = personnel ?? new PersonnelInfo(null, null, null);
        GeocodingContext = geocodingContext ?? global::RPAOtelRezervasyon.Domain.Models.GeocodingContext.Empty;
    }
}
