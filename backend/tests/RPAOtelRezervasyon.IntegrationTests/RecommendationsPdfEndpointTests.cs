using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RPAOtelRezervasyon.Api.Auth;
using RPAOtelRezervasyon.Domain.Models;
using RPAOtelRezervasyon.IntegrationTests.Support;
using UglyToad.PdfPig;
using System.Globalization;
using SkiaSharp;

namespace RPAOtelRezervasyon.IntegrationTests;

/// <summary>
/// PDF belge uç noktası sözleşmesi (0006): kimlik doğrulama (AC-10), PDF üretimi (AC-1),
/// Türkçe karakterler (AC-9), coğrafi görsel (AC-4), kısmi başarı (AC-6), hata eşlemesi (AC-7)
/// ve içerik tutarlılığı (AC-2, AC-5). Dış sağlayıcılar fake, belge/görsel üretimi gerçektir.
/// </summary>
public sealed class RecommendationsPdfEndpointTests
{
    private const string Venue = "İstanbul Kongre Merkezi";
    private const string NearHotel = "Şişli Oteli";
    private const string FarHotel = "Kadıköy Oteli";

    [Fact]
    public async Task Post_pdf_without_api_key_is_rejected_and_providers_are_not_called()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/recommendations/pdf", RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, factory.Geocoding.CallCount);
        Assert.Equal(0, factory.Routing.CallCount);
    }

    [Fact]
    public async Task Post_pdf_with_valid_request_returns_a_pdf_document()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(FarHotel, 800m, "EUR"), Hotel(NearHotel, 1250m, "TRY")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        Assert.NotNull(fileName);
        Assert.StartsWith("oneri-", fileName);
        Assert.EndsWith(".pdf", fileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000, $"Belge beklenenden küçük: {bytes.Length} bayt");
        Assert.Equal([0x25, 0x50, 0x44, 0x46, 0x2D], bytes.Take(5));

        var logs = string.Join(" ", factory.LogCapture.Messages);
        Assert.DoesNotContain("S12345", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Ada", logs, StringComparison.Ordinal);
        Assert.DoesNotContain("Yılmaz", logs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_shows_unavailable_for_a_failed_walking_route_without_failing()
    {
        using var factory = CreateFactory();
        factory.WalkingRouting.Handler = (_, _) => RouteOutcome.ProviderError();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations/pdf", RequestBody(Venue, Hotel(FarHotel)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, factory.WalkingRouting.CallCount);
        var text = ReadText(await response.Content.ReadAsByteArrayAsync());
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("YÜRÜME", normalized, StringComparison.Ordinal);
        Assert.Contains("Alınamadı", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_requires_personnel_and_event_city_country()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations/pdf", new
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
    public async Task Post_pdf_renders_turkish_names_and_the_selected_hotel_without_corruption()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(FarHotel, 800m, "EUR"), Hotel(NearHotel, 1250m, "TRY")));

        var bytes = await response.Content.ReadAsByteArrayAsync();
        var words = ReadWords(bytes);

        // AC-9: Türkçe karakterler bozulmadan görünür (İ, ş, ö).
        Assert.Contains("İstanbul", words);
        Assert.Contains("Şişli", words);

        // AC-2 / AC-5: seçilen otel belgede adıyla yer alır.
        Assert.Contains("Oteli", words);
        Assert.Contains(Venue, ReadText(bytes), StringComparison.Ordinal);

        // Fiyat ve metrikler invariant biçimde yazılır.
        var text = ReadText(bytes);
        Assert.Contains("1250 TRY", text, StringComparison.Ordinal);
        Assert.Contains("OTEL SEÇENEKLERİ", text, StringComparison.Ordinal);
        Assert.Contains("BÜTÇE ÖNCELİKLİ", text, StringComparison.Ordinal);
        Assert.Contains("ULAŞIM ÖNCELİKLİ", text, StringComparison.Ordinal);
        Assert.Contains("DENGE", text, StringComparison.Ordinal);
        Assert.Contains("rezervasyon teklifi veya teyidi değildir", text, StringComparison.Ordinal);
        Assert.Contains("S12345", text, StringComparison.Ordinal);
        Assert.Contains("Ada Yılmaz", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_shows_three_policy_options_and_price_transport_tradeoffs()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel, 950m, "TRY"), Hotel(FarHotel, 1000m, "TRY")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = ReadText(await response.Content.ReadAsByteArrayAsync());
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("BÜTÇE ÖNCELİKLİ", normalized, StringComparison.Ordinal);
        Assert.Contains("ULAŞIM ÖNCELİKLİ", normalized, StringComparison.Ordinal);
        Assert.Contains("DENGE", normalized, StringComparison.Ordinal);
        Assert.Contains(NearHotel, normalized, StringComparison.Ordinal);
        Assert.Contains(FarHotel, normalized, StringComparison.Ordinal);
        Assert.Contains("%5.3", normalized, StringComparison.Ordinal);
        Assert.Contains("daha uzundur", normalized, StringComparison.Ordinal);
        Assert.Contains("yürüyüş rotası ayrıca 800 m ve 10 dk", normalized, StringComparison.Ordinal);
        Assert.Contains("rezervasyon teklifi veya teyidi değildir", normalized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_includes_a_map_image()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel)));

        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var document = PdfDocument.Open(bytes);
        var page = document.GetPage(1);

        // AC-4: coğrafi görsel belgeye gömülür.
        Assert.True(page.NumberOfImages > 0, "Belgede coğrafi görsel (image) beklenir.");
        Assert.Contains("HARİTA GÖRÜNÜMÜ", ReadText(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_uses_geoapify_base_map_and_includes_attribution()
    {
        var handler = new StaticMapHttpHandler();
        using var factory = CreateFactory();
        factory.StaticMapHttpHandlerFactory = () => handler;
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Contains("apiKey=integration-map-key", handler.LastUri!.Query, StringComparison.Ordinal);
        var pdf = await response.Content.ReadAsByteArrayAsync();
        using var document = PdfDocument.Open(pdf);
        Assert.True(document.GetPage(1).NumberOfImages > 0);
        var text = ReadText(pdf);
        var normalizedText = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var words = ReadWords(pdf);
        Assert.Equal(1, CountOccurrences(text, "Geoapify"));
        Assert.Contains("HARİTA GÖRÜNÜMÜ", normalizedText, StringComparison.Ordinal);
        Assert.Contains("Numaralar", words);
        Assert.Contains("s\u0131ras\u0131n\u0131", normalizedText, StringComparison.Ordinal);
        Assert.Contains("ger\u00e7ek konumudur", normalizedText, StringComparison.Ordinal);
        Assert.Contains(NearHotel, normalizedText, StringComparison.Ordinal);
        Assert.Contains(FarHotel, normalizedText, StringComparison.Ordinal);
        Assert.Contains("Varış: etkinlik alanı", normalizedText, StringComparison.Ordinal);
        Assert.Contains("Ulaşım öncelikli rota", normalizedText, StringComparison.Ordinal);
        Assert.Contains("Rota rengi = otel işareti", normalizedText, StringComparison.Ordinal);
        Assert.Contains("YOL", normalizedText, StringComparison.Ordinal);
        Assert.Contains("YÜRÜME", normalizedText, StringComparison.Ordinal);
        Assert.Contains("GECELİK ÜCRET", normalizedText, StringComparison.Ordinal);
        Assert.Contains("\"attribution\":\"mandatory\"", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"scaleFactor\":2", handler.LastBody, StringComparison.Ordinal);
        Assert.Contains("\"geometries\"", handler.LastBody, StringComparison.Ordinal);
        Assert.DoesNotContain("integration-map-key", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_falls_back_to_schematic_map_when_geoapify_fails()
    {
        var handler = new StaticMapHttpHandler(HttpStatusCode.ServiceUnavailable);
        using var factory = CreateFactory();
        factory.StaticMapHttpHandlerFactory = () => handler;
        factory.Routing.Handler = (origin, destination) =>
        {
            var firstCut = new GeoPoint(origin.Latitude + ((destination.Latitude - origin.Latitude) * 0.4),
                origin.Longitude + ((destination.Longitude - origin.Longitude) * 0.4));
            var secondCut = new GeoPoint(origin.Latitude + ((destination.Latitude - origin.Latitude) * 0.6),
                origin.Longitude + ((destination.Longitude - origin.Longitude) * 0.6));
            IReadOnlyList<GeoPoint>[] parts = [[origin, firstCut], [secondCut, destination]];
            return RouteOutcome.Found(new RouteMetrics(1500, 900, DistanceKind.Road,
                [origin, firstCut, secondCut, destination], parts));
        };
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(handler.CallCount >= 1);
        var pdf = await response.Content.ReadAsByteArrayAsync();
        using var document = PdfDocument.Open(pdf);
        Assert.True(document.GetPage(1).NumberOfImages > 0);
        var text = ReadText(pdf);
        var normalizedText = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var words = ReadWords(pdf);
        Assert.Contains("Şematik görünüm", normalizedText, StringComparison.Ordinal);
        Assert.Contains("HARİTA GÖRÜNÜMÜ", normalizedText, StringComparison.Ordinal);
        Assert.Contains("Numaralar", words);
        Assert.Contains("s\u0131ras\u0131n\u0131", normalizedText, StringComparison.Ordinal);
        Assert.Contains("ger\u00e7ek konumudur", normalizedText, StringComparison.Ordinal);
        Assert.Contains(NearHotel, normalizedText, StringComparison.Ordinal);
        Assert.Contains(FarHotel, normalizedText, StringComparison.Ordinal);
        Assert.Contains("Varış: etkinlik alanı", normalizedText, StringComparison.Ordinal);
        Assert.Contains("Ulaşım öncelikli rota", normalizedText, StringComparison.Ordinal);
        Assert.Contains("Rota rengi = otel işareti", normalizedText, StringComparison.Ordinal);
        Assert.DoesNotContain("Powered by Geoapify", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_paginates_long_hotel_names_and_repeats_table_headers()
    {
        using var factory = CreateFactory();
        var hotels = Enumerable.Range(1, 20)
            .Select(index => $"Otel {index:00} — Uluslararası Kongre ve Konaklama Merkezi Uzun İsimli Şubesi")
            .ToArray();
        foreach (var (name, index) in hotels.Select((name, index) => (name, index)))
        {
            factory.Geocoding.Locations[name] = new GeoPoint(41.02 + index * 0.001, 28.98 + index * 0.001);
        }

        using var client = CreateAuthorizedClient(factory);
        var response = await client.PostAsJsonAsync("/api/recommendations/pdf",
            RequestBody(Venue, hotels.Select((name, index) => Hotel(name, 500m + index, "TRY")).Cast<object>().ToArray()));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pdf = await response.Content.ReadAsByteArrayAsync();
        using var document = PdfDocument.Open(pdf);
        Assert.True(document.NumberOfPages > 1, "Twenty long hotel names should paginate.");
        var pages = document.GetPages().ToArray();
        var lastPageText = pages[^1].Text;
        Assert.Contains("Otel 20", lastPageText, StringComparison.Ordinal);
        var normalizedReportText = string.Join(' ', pages.Select(page =>
            string.Join(' ', page.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))));
        Assert.Contains("Uluslararası Kongre ve Konaklama Merkezi Uzun İsimli Şubesi", normalizedReportText, StringComparison.Ordinal);
        Assert.Contains("Otel 01", normalizedReportText, StringComparison.Ordinal);
        Assert.Contains("Otel 20", normalizedReportText, StringComparison.Ordinal);
        Assert.Contains("OTEL", pages[1].Text, StringComparison.Ordinal);
        Assert.Contains("YÜRÜME", pages[1].Text, StringComparison.Ordinal);
        Assert.All(pages, page => Assert.All(page.GetWords(), word =>
        {
            Assert.True(word.BoundingBox.Left >= -0.1);
            Assert.True(word.BoundingBox.Right <= page.Width + 0.1);
            Assert.True(word.BoundingBox.Bottom >= -0.1);
            Assert.True(word.BoundingBox.Top <= page.Height + 0.1);
        }));
    }

    [Fact]
    public async Task Post_pdf_with_unresolved_hotel_still_returns_the_document()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel), Hotel("Bilinmeyen Otel")));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var text = ReadText(await response.Content.ReadAsByteArrayAsync());

        // AC-6: kısmi başarı korunur; çözümlenemeyen otel belgede listelenir.
        Assert.Contains("Çözümlenemeyen oteller", text, StringComparison.Ordinal);
        Assert.Contains("Bilinmeyen Otel", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_with_invalid_request_returns_validation_problem_instead_of_a_document()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel, -1m, "TRY")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, factory.Geocoding.CallCount);
    }

    [Fact]
    public async Task Post_pdf_when_the_venue_cannot_be_resolved_returns_422()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody("Bilinmeyen Etkinlik Alanı", Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_pdf_when_no_hotel_can_be_resolved_returns_422_with_unresolved_list()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
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

    [Fact]
    public async Task Post_pdf_when_the_provider_is_unavailable_returns_502()
    {
        using var factory = CreateFactory();
        factory.Geocoding.Locations.Remove(Venue);
        factory.Geocoding.MissingStatus = OutcomeStatus.TransientError;
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_pdf_with_wrong_api_key_is_rejected_and_providers_are_not_called()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, "wrong-api-key");

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, factory.Geocoding.CallCount);
        Assert.Equal(0, factory.Routing.CallCount);
    }

    [Fact]
    public async Task Post_pdf_marks_straight_line_fallback_rows_without_a_duration_estimate()
    {
        using var factory = CreateFactory();
        factory.Routing.Handler = (_, _) => RouteOutcome.NotFound();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync(
            "/api/recommendations/pdf",
            RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var text = ReadText(await response.Content.ReadAsByteArrayAsync());

        // AC-3/BR-6: düz çizgi fallback satırı işaretlenir ve süre "ölçülmedi" olarak yazılır.
        Assert.Contains("Düz çizgi", text, StringComparison.Ordinal);
        Assert.Contains("ölçülmedi", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_selection_matches_the_json_endpoint_for_the_same_request()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);
        var body = RequestBody(Venue, Hotel(FarHotel, 800m, "EUR"), Hotel(NearHotel, 1250m, "TRY"));

        var jsonResponse = await client.PostAsJsonAsync("/api/recommendations", body);
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        Assert.Equal(0, factory.WalkingRouting.CallCount);

        using var json = JsonDocument.Parse(await jsonResponse.Content.ReadAsStringAsync());
        var selected = json.RootElement.GetProperty("selectedHotel");
        var selectedName = selected.GetProperty("name").GetString()!;
        var selectedAmount = selected.GetProperty("price").GetProperty("amount").GetDecimal();
        var selectedCurrency = selected.GetProperty("price").GetProperty("currency").GetString()!;

        var pdfResponse = await client.PostAsJsonAsync("/api/recommendations/pdf", body);
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal(1, factory.WalkingRouting.CallCount);
        var text = ReadText(await pdfResponse.Content.ReadAsByteArrayAsync());
        var normalized = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("YÜRÜME", normalized, StringComparison.Ordinal);
        Assert.Contains("Mesafe", normalized, StringComparison.Ordinal);
        Assert.Contains("800 m", text, StringComparison.Ordinal);

        // JSON'un belirlenimci seçimi değişmez; PDF'de otel ve ulaşım öncelikli etiketi bulunur.
        Assert.Contains(selectedName, text, StringComparison.Ordinal);
        Assert.Contains("ULAŞIM ÖNCELİKLİ", text, StringComparison.Ordinal);

        var selectedDistance = selected.GetProperty("distanceMeters").GetInt32();
        var selectedDuration = selected.GetProperty("durationSeconds").GetInt32();
        var selectedKind = selected.GetProperty("distanceKind").GetString()!;

        // Beyaz boşluk farklarını yok sayarak seçilen otel kartının metrik satırını birebir karşılaştır.
        var expectedCardLine = string.Create(
            CultureInfo.InvariantCulture,
            $"Mesafe: {FormatDistance(selectedDistance)} · Süre: {FormatDuration(selectedDuration, selectedKind)} · Mesafe türü: {FormatKind(selectedKind)} · Gecelik ücret: {FormatPrice(selectedAmount, selectedCurrency)}");

        Assert.Contains(expectedCardLine, normalized, StringComparison.Ordinal);

    }

    [Fact]
    public async Task Post_pdf_without_a_map_still_returns_the_document_and_states_the_image_is_missing()
    {
        using var factory = CreateFactory();
        factory.StaticMapOverride = new FailingStaticMapProvider();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsJsonAsync("/api/recommendations/pdf", RequestBody(Venue, Hotel(NearHotel)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var text = ReadText(await response.Content.ReadAsByteArrayAsync());

        // AC-8: görsel üretilemese de belge üretilir ve eksikliği açıkça belirtilir.
        Assert.Contains("üretilemedi", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Etkinlik alanı ve oteller", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_pdf_writes_no_report_data_to_the_location_cache()
    {
        var body = RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel));

        using var jsonFactory = CreateFactory();
        using var jsonClient = CreateAuthorizedClient(jsonFactory);
        Assert.Equal(
            HttpStatusCode.OK,
            (await jsonClient.PostAsJsonAsync("/api/recommendations", body)).StatusCode);

        using var pdfFactory = CreateFactory();
        using var pdfClient = CreateAuthorizedClient(pdfFactory);
        Assert.Equal(
            HttpStatusCode.OK,
            (await pdfClient.PostAsJsonAsync("/api/recommendations/pdf", body)).StatusCode);

        // AC-11: belge üretimi konum önbelleğine ek/rapor verisi yazmaz; JSON yoluyla aynı kayıtlar (yalnızca konum).
        Assert.Equal(jsonFactory.Cache.Keys.Order(), pdfFactory.Cache.Keys.Order());
        Assert.Equal(3, pdfFactory.Cache.Keys.Count);
    }
    private static string FormatDistance(int meters) =>
        meters < 1000
            ? string.Create(CultureInfo.InvariantCulture, $"{meters} m")
            : string.Create(CultureInfo.InvariantCulture, $"{meters / 1000.0:0.#} km");

    private static string FormatDuration(int seconds, string distanceKind) =>
        distanceKind == nameof(DistanceKind.StraightLine)
            ? "ölçülmedi"
            : string.Create(CultureInfo.InvariantCulture, $"{seconds / 60} dk");

    private static string FormatKind(string distanceKind) =>
        distanceKind == nameof(DistanceKind.Road) ? "Yol" : "Düz çizgi";

    private static string FormatPrice(decimal amount, string currency) =>
        string.Create(CultureInfo.InvariantCulture, $"{amount:0.##} {currency}");
    [Fact]
    public async Task Post_pdf_does_not_make_an_extra_route_call_compared_to_json()
    {
        var body = RequestBody(Venue, Hotel(NearHotel), Hotel(FarHotel));

        using var jsonFactory = CreateFactory();
        using var jsonClient = CreateAuthorizedClient(jsonFactory);
        Assert.Equal(HttpStatusCode.OK, (await jsonClient.PostAsJsonAsync("/api/recommendations", body)).StatusCode);

        using var pdfFactory = CreateFactory();
        using var pdfClient = CreateAuthorizedClient(pdfFactory);
        Assert.Equal(HttpStatusCode.OK, (await pdfClient.PostAsJsonAsync("/api/recommendations/pdf", body)).StatusCode);

        // AC-15: belge üretimi ek rota çağrısı yapmaz; çağrı sayısı JSON yoluyla aynıdır (BR-9/R-12).
        Assert.Equal(jsonFactory.Routing.CallCount, pdfFactory.Routing.CallCount);
    }
    private static GeoPoint[] RoutePath(GeoPoint origin, GeoPoint destination) =>
    [
        origin,
        new GeoPoint(
            (origin.Latitude + destination.Latitude) / 2,
            ((origin.Longitude + destination.Longitude) / 2) + 0.005),
        destination,
    ];
    private static string ReadText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);

        return string.Join(' ', document.GetPages().Select(page => page.Text));
    }

    private static string[] ReadWords(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);

        return document.GetPages()
            .SelectMany(page => page.GetWords())
            .Select(word => word.Text)
            .ToArray();
    }

    private static int CountOccurrences(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    private sealed class StaticMapHttpHandler(HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Uri? LastUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastBody { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);
            LastUri = request.RequestUri;
            LastMethod = request.Method;
            LastBody = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            if (statusCode != HttpStatusCode.OK)
            {
                return Task.FromResult(new HttpResponseMessage(statusCode));
            }

            using var body = JsonDocument.Parse(LastBody!);
            var width = body.RootElement.GetProperty("width").GetInt32() * 2;
            var height = body.RootElement.GetProperty("height").GetInt32() * 2;
            using var surface = SKSurface.Create(new SKImageInfo(width, height));
            surface.Canvas.Clear(SKColors.White);
            using var image = surface.Snapshot();
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            var content = new ByteArrayContent(png!.ToArray());
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private static ApiTestFactory CreateFactory()
    {
        var factory = new ApiTestFactory();

        factory.Geocoding.Locations[Venue] = new GeoPoint(41.0082, 28.9784);
        factory.Geocoding.Locations[NearHotel] = new GeoPoint(41.0600, 28.9870);
        factory.Geocoding.Locations[FarHotel] = new GeoPoint(40.9900, 29.0300);

        factory.Routing.Handler = (origin, destination) =>
        {
            var kilometers = Math.Abs(destination.Latitude - origin.Latitude) * 111d;

            return RouteOutcome.Found(new RouteMetrics(
                (int)Math.Round(kilometers * 1000),
                (int)Math.Round(kilometers * 60),
                DistanceKind.Road,
                RoutePath(origin, destination)));
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
