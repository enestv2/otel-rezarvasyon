namespace RPAOtelRezervasyon.Api.Contracts;

/// <summary>
/// Servisin girdi sözleşmesi (R-1): etkinlik alanı adı + fiyatlı anlaşmalı otel listesi. İş
/// doğrulaması (boşluk/uzunluk/tutar/para birimi) domain doğrulayıcısında yapılır (BR-5); burada
/// yalnızca bağlama (binding) vardır.
/// </summary>
public sealed class RecommendationRequestDto
{
    public string? PersonnelRegistrationNumber { get; init; }

    public string? PersonnelFirstName { get; init; }

    public string? PersonnelLastName { get; init; }

    public string? VenueName { get; init; }

    public string? EventCity { get; init; }

    public string? EventCountry { get; init; }

    public IReadOnlyList<HotelRequestDto?>? Hotels { get; init; }
}

/// <summary>Tek otel girdisi: ad ve bir gecelik fiyat (AC-1).</summary>
public sealed class HotelRequestDto
{
    public string? Name { get; init; }

    /// <summary>
    /// Nullable değil: OpenAPI/Swagger şeması `null` yerine `amount` + `currency` nesnesini gösterir.
    /// Eksik/boş fiyat varsayılan (alanları null) örnek olarak gelir ve domain doğrulamasında reddedilir.
    /// </summary>
    public HotelPriceRequestDto Price { get; init; } = new();
}

/// <summary>
/// Bir gecelik fiyat girdisi (R-1, R-5): tutar JSON sayısı/decimal, para birimi üç büyük ASCII
/// harfli koddur. Eksik tutarı sıfırdan ayırt edebilmek için <see cref="Amount"/> nullable'dır.
/// </summary>
public sealed class HotelPriceRequestDto
{
    public decimal? Amount { get; init; }

    public string? Currency { get; init; }
}
