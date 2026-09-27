namespace RPAOtelRezervasyon.Domain.Models;

/// <summary>
/// R-1: bir otelin **bir gecelik** ücreti. Tutar negatif olamaz (sıfır = ücretsiz konaklama geçerlidir);
/// para birimi üç büyük ASCII harfli ISO 4217 kodudur (R-5). Kur dönüşümü yapılmaz, tutar olduğu gibi taşınır.
/// </summary>
public sealed record NightlyPrice
{
    public decimal Amount { get; }

    public string Currency { get; }

    public NightlyPrice(decimal amount, string currency)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        if (!IsValidCurrencyCode(currency))
        {
            throw new ArgumentException("Para birimi kodu üç büyük ASCII harf olmalıdır.", nameof(currency));
        }

        Amount = amount;
        Currency = currency;
    }

    /// <summary>R-5: para birimi kodu tam olarak üç büyük ASCII harftir (ör. TRY, EUR).</summary>
    public static bool IsValidCurrencyCode(string? currency) =>
        currency is { Length: 3 } && currency.All(static character => character is >= 'A' and <= 'Z');
}

/// <summary>Kurumun anlaşmalı olduğu otel; ad ve bir gecelik ücret birlikte taşınır (R-1).</summary>
public sealed record ContractedHotel
{
    public string Name { get; }

    public NightlyPrice Price { get; }

    public ContractedHotel(string name, NightlyPrice price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(price);

        Name = name;
        Price = price;
    }

    /// <summary>
    /// AC-10 / G-3: yinelenen adları tekilleştirir. Büyük/küçük harf ve baş/son boşluk duyarsızdır;
    /// ilk görülen kayıt korunur (determinizm).
    /// </summary>
    public static IReadOnlyList<ContractedHotel> DistinctByName(IEnumerable<ContractedHotel> hotels)
    {
        ArgumentNullException.ThrowIfNull(hotels);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ContractedHotel>();

        foreach (var hotel in hotels)
        {
            if (seen.Add(hotel.Name.Trim()))
            {
                result.Add(hotel);
            }
        }

        return result;
    }
}
