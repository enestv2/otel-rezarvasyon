using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using RPAOtelRezervasyon.Api.Auth;
using RPAOtelRezervasyon.Api.Contracts;
using RPAOtelRezervasyon.Domain.Abstractions;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.Infrastructure.Persistence.Mongo;
using RPAOtelRezervasyon.IntegrationTests.Support;

namespace RPAOtelRezervasyon.IntegrationTests;

/// <summary>
/// Uç nokta sözleşmesi uçtan uca: kimlik doğrulama (AC-9), doğrulama (AC-5, AC-6), başarılı
/// sıralama ve fiyat taşıma (AC-1, AC-2, AC-3), çözümlenemeyen etkinlik alanı, fallback işareti
/// (AC-11), önbellek isabeti (AC-14), hiç otel çözümlenememesi (AC-17), kısmi başarı (AC-7) ve
/// fiyatın konum önbelleğine yazılmaması (AC-8).
/// </summary>
public sealed class RecommendationsEndpointTests
{
    private const string Venue = "Ankara Kongre Merkezi";
    private const string NearHotel = "Yakin Otel";
    private const string FarHotel = "Uzak Otel";
    private const string VeryFarHotel = "Cok Uzak Otel";

    [Fact]
    public async Task Post_without_api_key_is_rejected_and_providers_are_not_called()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, factory.Geocoding.CallCount);
        Assert.Equal(0, factory.Routing.CallCount);
    }

    [Fact]
    public async Task Post_with_wrong_api_key_is_rejected_and_providers_are_not_called()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, "wrong-api-key");

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
        Assert.Equal(0, factory.Routing.CallCount);
    }

    [Fact]
    public async Task Post_with_valid_request_returns_ranked_recommendation_with_prices_and_rationale()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            RequestBody(Venue, Hotel(FarHotel, 800m, "EUR"), Hotel(NearHotel, 1250m, "TRY")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseJson = await response.Content.ReadAsStringAsync();
        var payload = JsonSerializer.Deserialize<RecommendationResponseDto>(responseJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(payload);
        Assert.DoesNotContain("S12345", responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", responseJson, StringComparison.Ordinal);
        Assert.Equal("Succeeded", payload.Status);
        Assert.Equal(NearHotel, payload.SelectedHotel.Name);
        Assert.Equal([NearHotel, FarHotel], payload.RankedHotels.Select(hotel => hotel.Name));
        Assert.All(payload.RankedHotels, hotel => Assert.Equal("Road", hotel.DistanceKind));
        Assert.Equal(NearHotel, payload.Rationale.HotelName);
        Assert.Equal(2, payload.Rationale.EvaluatedHotelCount);
        Assert.Empty(payload.UnresolvedHotels);

        // AC-2: her sıralanmış otel kendi gönderilen fiyatını taşır.
        var near = payload.RankedHotels.Single(hotel => hotel.Name == NearHotel);
        var far = payload.RankedHotels.Single(hotel => hotel.Name == FarHotel);
        Assert.Equal(new PriceDto(1250m, "TRY"), near.Price);
        Assert.Equal(new PriceDto(800m, "EUR"), far.Price);

        // AC-3: seçilen otelin fiyatı aynı otelin istek fiyatıyla birebir aynıdır.
        Assert.Equal(near.Price, payload.SelectedHotel.Price);

        // 0001 AC-1: ölçülmüş yol metriği mesafe ve süre değerleriyle döner.
        Assert.True(near.DistanceMeters > 0);
        Assert.True(near.DurationSeconds > 0);

        // 0001 AC-2: gerekçe, seçilen otelin metrikleriyle birebir tutarlıdır.
        Assert.Equal(payload.SelectedHotel.DistanceMeters, payload.Rationale.DistanceMeters);
        Assert.Equal(payload.SelectedHotel.DurationSeconds, payload.Rationale.DurationSeconds);
        Assert.Equal(payload.SelectedHotel.DistanceKind, payload.Rationale.DistanceKind);
    }

    [Fact]
    public async Task Post_uses_default_geoapify_and_applies_event_city_country_to_venue_and_hotel_queries()
    {
        using var factory = new ApiTestFactory { ReplaceRecommendationProviders = false };
        factory.GeoapifyHttpHandlerFactory = () => new GeoapifyStubHttpMessageHandler(factory.GeoapifyRequestUris);
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue, Hotel(NearHotel)));

        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"Geoapify request failed: {response.StatusCode} {await response.Content.ReadAsStringAsync()}; requests: {string.Join(",", factory.GeoapifyRequestUris)}");
        var geocodingRequests = factory.GeoapifyRequestUris
            .Where(uri => uri.AbsolutePath.EndsWith("/geocode/search", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, geocodingRequests.Length);
        Assert.Contains(geocodingRequests, uri => Uri.UnescapeDataString(uri.Query).Contains(Venue, StringComparison.Ordinal));
        Assert.Contains(geocodingRequests, uri => Uri.UnescapeDataString(uri.Query).Contains(NearHotel, StringComparison.Ordinal));
        Assert.Contains(factory.GeoapifyRequestUris, uri => uri.AbsolutePath.EndsWith("/routing", StringComparison.Ordinal));
        Assert.All(geocodingRequests, uri =>
        {
            var query = Uri.UnescapeDataString(uri.Query);
            Assert.Contains("city=Ankara", query, StringComparison.Ordinal);
            Assert.Contains("country=Turkey", query, StringComparison.Ordinal);
            Assert.Contains("apiKey=integration-geoapify-key", query, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task Post_does_not_write_personnel_fields_to_application_logs()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue, Hotel(NearHotel)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var logs = string.Join(" ", factory.LogCapture.Messages);
        Assert.DoesNotContain("S12345", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("YÄ±lmaz", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_requires_personnel_and_event_city_country()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", new
        {
            venueName = Venue,
            eventCity = "Ankara",
            hotels = new[] { Hotel(NearHotel) },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("personnel-registration-required", body, StringComparison.Ordinal);
        Assert.Contains("event-country-invalid", body, StringComparison.Ordinal);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_accepts_a_zero_price()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            RequestBody(Venue, Hotel(NearHotel, 0m, "TRY")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(new PriceDto(0m, "TRY"), payload.SelectedHotel.Price);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task Post_with_negative_price_is_rejected(double amount)
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            new
            {
                venueName = Venue,
                hotels = new[] { new { name = NearHotel, price = new { amount, currency = "TRY" } } },
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_with_missing_price_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            new { venueName = Venue, hotels = new[] { new { name = NearHotel } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_with_explicit_null_price_is_rejected_by_the_validator()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        const string body = """
            { "venueName": "Ankara Kongre Merkezi", "hotels": [ { "name": "Yakin Otel", "price": null } ] }
            """;
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/recommendations", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("errors", out _), "Doğrulama hatası (ValidationProblemDetails) beklenir.");
    }

    [Fact]
    public async Task Post_with_non_numeric_amount_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        const string body = """
            { "venueName": "Ankara Kongre Merkezi", "hotels": [ { "name": "Yakin Otel", "price": { "amount": "abc", "currency": "TRY" } } ] }
            """;
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/recommendations", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Theory]
    [InlineData("try")]
    [InlineData("TR")]
    [InlineData("TRYY")]
    [InlineData("T1Y")]
    public async Task Post_with_invalid_currency_code_is_rejected(string currency)
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            RequestBody(Venue, Hotel(NearHotel, 1500m, currency)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_with_blank_venue_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody("   ", Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_with_empty_hotel_list_is_rejected()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_when_venue_cannot_be_resolved_returns_422()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody("Bilinmeyen Etkinlik Alani", Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("Etkinlik alanı çözümlenemedi", problem.Title);
        Assert.Equal(0, factory.Routing.CallCount);
    }

    [Fact]
    public async Task Post_when_geocoding_provider_rejects_returns_502_without_upstream_details()
    {
        using var factory = CreateFactory();
        factory.Geocoding.Locations.Remove(Venue);
        factory.Geocoding.MissingStatus = OutcomeStatus.ProviderError;
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(502, problem.Status!.Value);
        Assert.Equal("Konum sağlayıcısına erişilemedi", problem.Title);
        Assert.Equal(0, factory.Routing.CallCount);

        // AC-4: hata gövdesi sabit/genel kalır; ham sağlayıcı ayrıntısı (extensions) taşımaz.
        var raw = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        Assert.False(
            document.RootElement.TryGetProperty("extensions", out _),
            "Sağlayıcı hatası yanıtı ham upstream ayrıntısı (extensions) taşımamalı.");
    }

    [Fact]
    public async Task Post_when_route_missing_marks_straight_line_and_orders_after_road()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations", RequestBody(Venue, Hotel(VeryFarHotel), Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(NearHotel, payload.SelectedHotel.Name);

        var fallback = payload.RankedHotels[^1];
        Assert.Equal(VeryFarHotel, fallback.Name);
        Assert.Equal("StraightLine", fallback.DistanceKind);
        Assert.Equal(0, fallback.DurationSeconds);
        Assert.Equal(new PriceDto(1500m, "TRY"), fallback.Price);
    }

    [Fact]
    public async Task Post_same_request_twice_uses_cache_and_does_not_call_provider_again()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);
        var body = RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel));

        var first = await client.PostAsJsonAsync("/api/recommendations", body);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var callsAfterFirst = factory.Geocoding.CallCount;
        Assert.Equal(3, callsAfterFirst);

        var second = await client.PostAsJsonAsync("/api/recommendations", body);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        Assert.Equal(callsAfterFirst, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_with_a_partially_unresolved_list_returns_partial_success()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            RequestBody(Venue, Hotel(NearHotel, 1250m, "TRY"), Hotel("Bilinmeyen Otel")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RecommendationResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal(NearHotel, payload.SelectedHotel.Name);
        Assert.Single(payload.RankedHotels);
        Assert.Equal(["Bilinmeyen Otel"], payload.UnresolvedHotels);
        Assert.Equal(new PriceDto(1250m, "TRY"), payload.SelectedHotel.Price);
    }

    [Fact]
    public async Task Post_when_no_hotel_can_be_resolved_returns_422_with_unresolved_list()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations",
            RequestBody(Venue, Hotel("Bilinmeyen Otel Bir"), Hotel("Bilinmeyen Otel Iki")));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var unresolved = document.RootElement
            .GetProperty("unresolvedHotels")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .ToArray();

        Assert.Equal(["Bilinmeyen Otel Bir", "Bilinmeyen Otel Iki"], unresolved);
    }

    /// <summary>
    /// AC-8: fiyat bilgisi kalıcı konum önbelleğine yazılmaz. Kanıt: izin listesi (allow-list) —
    /// önbellek portunun imzası ve önbellek dokümanı/okuma modeli yalnızca konum alanlarını taşır;
    /// adı ne olursa olsun yeni bir alan (ör. Fee, Tariff) eklenirse test kırılır.
    /// </summary>
    [Fact]
    public void Cache_contract_and_document_expose_only_location_fields()
    {
        Assert.Equal(
            ["CreatedAt", "DisplayName", "Id", "Latitude", "Longitude", "Name", "NormalizedName", "ProviderId"],
            typeof(GeocodeCacheDocument).GetProperties().Select(property => property.Name).Order().ToArray());

        Assert.Equal(
            ["CreatedAt", "DisplayName", "Location", "ProviderId"],
            typeof(CachedLocation).GetProperties().Select(property => property.Name).Order().ToArray());

        var storeAsync = typeof(IGeocodeCache).GetMethod(nameof(IGeocodeCache.StoreAsync));
        Assert.NotNull(storeAsync);
        Assert.Equal(
            ["cancellationToken", "displayName", "location", "placeName", "providerId"],
            storeAsync.GetParameters().Select(parameter => parameter.Name!).Order().ToArray());
    }

    /// <summary>
    /// Swagger/OpenAPI girdi şeması: `hotels[].price` alanı `null` değil, `amount` + `currency`
    /// taşıyan `HotelPriceRequestDto` nesnesine çözümlenmelidir (R-1 sözleşmesi).
    /// </summary>
    [Fact]
    public async Task OpenApi_request_schema_documents_the_price_object()
    {
        await using var factory = new ApiTestFactory("Development");
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(
            await (await client.GetAsync("/openapi/v1.json")).Content.ReadAsStringAsync());
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        var price = schemas.GetProperty("HotelRequestDto").GetProperty("properties").GetProperty("price");
        Assert.True(price.TryGetProperty("$ref", out var reference), $"price şeması: {price.GetRawText()}");
        Assert.Equal("#/components/schemas/HotelPriceRequestDto", reference.GetString());

        var priceProperties = schemas.GetProperty("HotelPriceRequestDto").GetProperty("properties");
        Assert.True(priceProperties.TryGetProperty("amount", out _));
        Assert.True(priceProperties.TryGetProperty("currency", out _));
    }

    private static ApiTestFactory CreateFactory()
    {
        var factory = new ApiTestFactory();

        factory.Geocoding.Locations[Venue] = new GeoPoint(39.9208, 32.8541);
        factory.Geocoding.Locations[NearHotel] = new GeoPoint(39.9300, 32.8600);
        factory.Geocoding.Locations[FarHotel] = new GeoPoint(40.1000, 32.9000);
        factory.Geocoding.Locations[VeryFarHotel] = new GeoPoint(41.5000, 33.5000);

        factory.Routing.Handler = (origin, destination) =>
        {
            var kilometers = Math.Abs(destination.Latitude - origin.Latitude) * 111d;

            return kilometers > 50
                ? RouteOutcome.NotFound()
                : RouteOutcome.Found(new RouteMetrics(
                    (int)Math.Round(kilometers * 1000),
                    (int)Math.Round(kilometers * 60),
                    DistanceKind.Road));
        };

        return factory;
    }

    private static HttpClient CreateAuthorizedClient(ApiTestFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiTestFactory.ApiKey);

        return client;
    }

    private static object Hotel(string name, decimal amount = 1500m, string currency = "TRY") =>
        new { name, price = new { amount, currency } };

    private static object RequestBody(string venueName, params object[] hotels) => new
    {
        personnelRegistrationNumber = "S12345",
        personnelFirstName = "Ada",
        personnelLastName = "Yılmaz",
        venueName,
        eventCity = "Ankara",
        eventCountry = "Turkey",
        hotels,
    };
}
