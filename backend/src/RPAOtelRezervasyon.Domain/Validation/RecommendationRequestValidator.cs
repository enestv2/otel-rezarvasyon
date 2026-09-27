using RPAOtelRezervasyon.Domain.Models;

namespace RPAOtelRezervasyon.Domain.Validation;

/// <summary>
/// R-1: doğrulanmamış otel girdisi (ad + bir gecelik tutar ve para birimi kodu). Geçersiz değerler
/// istisna yerine doğrulama sorunu olarak raporlanır; alanlar bu yüzden nullable'dır.
/// </summary>
public sealed record RequestedHotel(string? Name, decimal? Amount, string? Currency);

public sealed record ValidationIssue(string Code, string Message);

public sealed record ValidationResult
{
    public IReadOnlyList<ValidationIssue> Issues { get; }

    public bool IsValid => Issues.Count == 0;

    public ValidationResult(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues;
    }

    public static ValidationResult Ok() => new([]);
}

/// <summary>BR-5: girdi sınırları. Yinelenen otel adı hata değildir (AC-10'da tekilleştirilir).</summary>
public static class RecommendationRequestValidator
{
    public const int MaxHotels = 20;
    public const int MinVenueNameLength = 3;
    public const int MaxVenueNameLength = 200;
    public const int MinHotelNameLength = 2;
    public const int MaxHotelNameLength = 200;

    public static ValidationResult Validate(string? venueName, IReadOnlyList<RequestedHotel>? hotels) =>
        ValidateCore(venueName, hotels, null, null, null, requireRequestContext: false);

    public static ValidationResult Validate(
        string? venueName,
        IReadOnlyList<RequestedHotel>? hotels,
        PersonnelInfo? personnel,
        string? city,
        string? country) =>
        ValidateCore(venueName, hotels, personnel, city, country, requireRequestContext: true);

    private static ValidationResult ValidateCore(
        string? venueName,
        IReadOnlyList<RequestedHotel>? hotels,
        PersonnelInfo? personnel,
        string? city,
        string? country,
        bool requireRequestContext)
    {
        var issues = new List<ValidationIssue>();

        if (requireRequestContext)
        {
            ValidateRequiredText(personnel?.RegistrationNumber, "personnel-registration-required", "Personel sicil numarası zorunludur.", 1, 50, issues);
            ValidateRequiredText(personnel?.FirstName, "personnel-first-name-invalid", "Personel adı 1-100 karakter olmalıdır.", 1, 100, issues);
            ValidateRequiredText(personnel?.LastName, "personnel-last-name-invalid", "Personel soyadı 1-100 karakter olmalıdır.", 1, 100, issues);
            ValidateRequiredText(city, "event-city-invalid", "Etkinlik şehri 2-100 karakter olmalıdır.", 2, 100, issues);
            ValidateRequiredText(country, "event-country-invalid", "Etkinlik ülkesi 2-100 karakter olmalıdır.", 2, 100, issues);
        }

        if (string.IsNullOrWhiteSpace(venueName))
        {
            issues.Add(new ValidationIssue("venue-name-required", "Etkinlik alanı adı zorunludur."));
        }
        else if (venueName.Trim().Length is < MinVenueNameLength or > MaxVenueNameLength)
        {
            issues.Add(new ValidationIssue(
                "venue-name-length",
                $"Etkinlik alanı adı {MinVenueNameLength}-{MaxVenueNameLength} karakter olmalıdır."));
        }

        if (hotels is null || hotels.Count == 0)
        {
            issues.Add(new ValidationIssue("hotels-required", "En az bir otel adı gereklidir."));
            return new ValidationResult(issues);
        }

        if (hotels.Count > MaxHotels)
        {
            // Üst sınır aşıldığında per-otel hata üretmeyi durdur: N adet sorun üretmek
            // doğrulama çıktısını girdi boyutuyla büyütür (DoS yükseltmesi).
            issues.Add(new ValidationIssue("hotels-too-many", $"En fazla {MaxHotels} otel verilebilir."));
            return new ValidationResult(issues);
        }

        for (var i = 0; i < hotels.Count; i++)
        {
            var hotel = hotels[i];
            var trimmedLength = hotel.Name?.Trim().Length ?? 0;

            if (string.IsNullOrWhiteSpace(hotel.Name) || trimmedLength is < MinHotelNameLength or > MaxHotelNameLength)
            {
                issues.Add(new ValidationIssue(
                    $"hotel-name-invalid[{i}]",
                    $"Otel adı {MinHotelNameLength}-{MaxHotelNameLength} karakter olmalıdır."));
            }

            if (hotel.Amount is null or < 0)
            {
                issues.Add(new ValidationIssue(
                    $"hotel-price-invalid[{i}]",
                    "Gecelik tutar sıfır veya pozitif bir sayı olmalıdır."));
            }

            if (!NightlyPrice.IsValidCurrencyCode(hotel.Currency))
            {
                issues.Add(new ValidationIssue(
                    $"hotel-currency-invalid[{i}]",
                    "Para birimi kodu üç büyük ASCII harf olmalıdır."));
            }
        }

        return new ValidationResult(issues);
    }

    private static void ValidateRequiredText(
        string? value,
        string code,
        string message,
        int minLength,
        int maxLength,
        ICollection<ValidationIssue> issues)
    {
        var length = value?.Trim().Length ?? 0;
        if (length < minLength || length > maxLength)
        {
            issues.Add(new ValidationIssue(code, message));
        }
    }
}
