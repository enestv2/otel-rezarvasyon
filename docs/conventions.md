# Konvansiyonlar

> Bootstrap ile dolduruldu (2026-09-25). Burada yalnızca gerçek kurallar vardır: her kural ya
> araçlarla zorlanır (tercih edilen) ya da incelemede kontrol edilir. Temenniler buraya girmez.

## Dil ve framework sürümleri
- **.NET 10** (ASP.NET Core 10) — kurulu SDK: `10.0.201`. Hedef framework: `net10.0`.
- C# dili: C# 14 (SDK varsayılanı). `Nullable` **enable**, `ImplicitUsings` enable.
- `.NET Core 10` ifadesi kullanılmaz; doğru ad `.NET 10`'dur (Core markası .NET 5'ten beri yok).

## Adlandırma
- Tipler, metotlar, özellikler: `PascalCase`. Yerel değişkenler/parametreler: `camelCase`.
- Arayüzler `I` ile başlar (`IGeocodingProvider`). Async metotlar `Async` ile biter.
- Modül klasörleri: `VenueGeocoding`, `HotelGeocoding`, `DistanceRouting`, `Ranking`, `Delivery`,
  `Reporting`.
- Testler: `<Konu>Tests` sınıfı, `<Metot>_<Durum>_<Beklenen>` metot adı.
- MongoDB: veritabanı `rpaotelrezervasyon`, koleksiyon `geocode_cache`; doküman alanları `camelCase`.
- Branch: `feature/<spec-no>-<kısa-ad>` (bkz. `docs/git.md`).

## Hata yönetimi
- Tek desen: **`ProblemDetails`** (RFC 7807). Kullanıcıya stack trace, sağlayıcı ham yanıtı veya
  iç mesaj sızmaz.
- Doğrulama hataları → `400`; etkinlik alanı çözümlenemezse → `422`; hiçbir otel çözümlenemezse → `422`
  (AC-17); sağlayıcı erişilemezse → `502`; beklenmeyen → `500`.
- Kısmi başarı (BR-1) hata değildir: `200` + yanıt gövdesinde `unresolvedHotels`.

## API belgeleri (OpenAPI / Swagger)
- OpenAPI belgesi `/openapi/v1.json`, Swagger UI `/swagger` yalnızca **Development** ortamında
  sunulur; Development dışında 404 döner ve bu uçlar kimlik doğrulaması istemez (0002).
- Yetkisiz (`401`) yanıtı da tek hata desenine uyar: `application/problem+json` + `ProblemDetails`.

## Veri kuralları
- Koordinatlar: `double` enlem/boylam (WGS84). Mesafe: `int` metre. Süre: `int` saniye.
- Metinler kültürden bağımsız işlenir; `CultureInfo.InvariantCulture` kullanılır.
- Kültüre bağlı biçimlendirme (`,` ondalık) **yasak**.
- Gecelik ücret: tutar `decimal` (JSON sayısı), negatif olamaz, sıfır geçerlidir; para birimi kodu
  **üç büyük ASCII harf** (ISO 4217, ör. `TRY`, `EUR`). Kur dönüşümü yapılmaz. Fiyat sıralamaya
  girmez ve konum önbelleğine yazılmaz (BR-8).
- Önbellek `_id`: `<sağlayıcı>:<escape(şehir)>|<escape(ülke)>|<escape(normalize-ad)>`.
  Alanlar `name`/`normalizedName` tam sorgu anahtarını, `provider` sağlayıcıyı taşır. Bağlamsız eski
  kayıtlar yeni sorgularla eşleşmez. Alanlar:
  `name`, `normalizedName`, `provider`, `lat`, `lon`, `displayName`, `createdAt` (UTC).
- Sıralamada eşitlik bozucu otel adı karşılaştırması **ordinal**'dir (deterministik); dilsel/Türkçe
  alfabetik sıra beklenmez.
- MongoDB bağlantı dizesi **sırdır** (bkz. `docs/security.md`).

## Belge üretimi (PDF) ve coğrafi görsel
- Rapor içeriği çekirdekte **deterministik bir modele** dönüştürülür (`RecommendationReport`);
  PDF/görsel üretimi Infrastructure adaptörlerinde yapılır (ADR-0006). Çekirdek `QuestPDF`,
  `SkiaSharp` veya `UglyToad.*` tiplerine bağlanamaz (mimari test bunu doğrular).
- Belge uç noktası ayrıdır ve JSON uç noktasıyla **aynı** istek gövdesini ve hata eşlemesini kullanır;
  başarıda `application/pdf`, hatada `application/problem+json` (`ProblemDetails`) döner.
- Dosya adı kültürden bağımsız, ASCII-güvenli bir slug'dır: `oneri-<slug>.pdf` (ör.
  `oneri-istanbul-kongre-merkezi.pdf`).
- Belge metni `CultureInfo.InvariantCulture` ile biçimlendirilir (mesafe metre/km, süre dakika,
  ücret `amount currency`); Türkçe görünen metinler şablondadır.
- Coğrafi görselde etkinlik alanı, otel sıra rozetlerinden farklı, 1,1× boyutlu kırmızı varış piniyle gösterilir; otel adları PDF metin
  katmanında verilir. Gerçek ve şematik haritada 20 otele kadar kategorik renk paleti, rozet/rota renk eşleşmesi
  ve diğer rotalarda ek çizgi desenleri korunur. Rota çizgileri ince çizilir. Yakın rozetler çakışma yoksa ankorda kalır, gerekirse
  en yakın boş konuma lider çizgisiyle taşınır; koordinatlar değişmez. PDF lejantı varış pinini, otel rengiyle eşleşen
  rotaları, düz seçili rotayı ve kesikli diğer rotaları ayırt eder. Gerçek harita sağlayıcısı kullanılırsa zorunlu atıf belgeye yazılır.
- PDF haritası tek ve tam genişlikte genel görünüm sunar; tüm oteller ve kullanılabilir rota geometrileri aynı haritada yer alır. Yalnızca sağlayıcının döndürdüğü rota parçaları çizilir; geometri yoksa düz bağlantı uydurulmaz. Hosted Geoapify rotaları taban harita isteğinde çizer; şematik yedek her geometri parçasını ayrı çizer. PDF sıra numaraları ve otel renkleri sabit kalır; sağlayıcı atıfları korunur.
- Statik harita sağlayıcısı Geoapify'dir; rotalar Static Maps POST gövdesindeki `geometries` alanıyla taban haritayla aynı projeksiyonda çizilir. `StaticMap` seçenekleri başlangıçta doğrulanır, `ApiKey`
  `user-secrets`/ortam değişkeninde tutulur ve harita isteği URL'si loglanmaz. Görsel 2x piksel
  yoğunluğunda istenir; harita üzerindeki OSM/OpenMapTiles zorunlu atıfları korunur ve Geoapify kredisi
  PDF'de bir kez yazılır. Sağlayıcı hatasında belge şematik görsele döner.
- Sınırlar yapılandırmadan gelir (`Reporting` bölümü): `MapWidthPx`, `MapHeightPx`,
  `MaxDocumentBytes`, `DocumentTitle`; başlangıçta doğrulanır.

## Adaptör kuralları (Infrastructure: Providers / Persistence)
- Infrastructure tek projedir: `RPAOtelRezervasyon.Infrastructure`; altında `Providers/` ve
  `Persistence/` klasörleri (ADR-0004). Yeni adaptör bu iki gruptan birine girer.
- Dış dünyaya yalnızca adaptörden çıkılır; typed `HttpClient` + `IHttpClientFactory` kullanılır.
- `static HttpClient` veya her çağrıda `new HttpClient()` **yasak**.
- Sağlayıcı seçenekleri `IOptions<T>` ile bağlanır: `ValidateDataAnnotations().ValidateOnStart()`.
- Dayanıklılık politikaları adaptörde tanımlanır; **retry yalnızca idempotent GET** için, jitter'lı.
- Sağlayıcı DTO'ları `internal`; Domain tiplerine dönüşüm adaptörde yapılır.
- Beklenen hatalar sonuç tipiyle döner (`Found | NotFound | TransientError | ProviderError`); beklenmeyen durum için
  istisna yalnızca adaptör sınırında yakalanıp sonuç tipine çevrilir.
- Loglara sır, ham sağlayıcı yanıtı veya PII yazılmaz.
- Adaptör iş kuralı içermez (sıralama, eleme, iş kararı çekirdektedir).

## Araçlarla zorlananlar
- `TreatWarningsAsErrors` / `dotnet build -warnaserror` → uyarılar CI'ı kırar.
- `Nullable enable` → null güvenliği derleyici tarafından zorlanır.
- `.editorconfig` + `dotnet format --verify-no-changes` (lint adımı) → biçim.
- Analyzer'lar (`EnableNETAnalyzers`, `AnalysisLevel=latest`) → kod kokuları.
- Yeni kural mümkünse `scripts/check`'e bağlanır; metin tavsiyedir, araç kanundur.

## Bağımlılıklar (belge üretimi)
- `QuestPDF` — PDF üretimi; **topluluk lisansı** gelir eşiğine bağlıdır (ADR-0006; eşik uygun değilse
  MIT alternatifine geçilir).
- `SkiaSharp` (+ `SkiaSharp.NativeAssets.Linux.NoDependencies`) — şematik görsel çizimi (MIT).
- `PdfPig` (UglyToad, Apache-2.0) — **yalnızca test** projesinde, üretilen PDF metnini doğrulamak için.
## Etkinlik request alanları ve Geoapify (0007)
- JSON (`POST /api/recommendations`) ve PDF (`POST /api/recommendations/pdf`) ortak request DTO'sunu
  kullanır: `personnelRegistrationNumber`, `personnelFirstName`, `personnelLastName`, `venueName`,
  `eventCity`, `eventCountry`, `hotels`.
- Sicil 1-50, ad/soyad 1-100, etkinlik şehri/ülkesi 2-100 karakter olmalıdır. Herhangi biri yok/geçersizse
  iki endpoint de sağlayıcı çağırmadan aynı doğrulama `400` sonucunu verir.
- Geocoding şehir/ülke bağlamını kullanır. Cache kimliği `<provider>:<escape(city)>|<escape(country)>|<escape(name)>`;
  bağlamsız eski cache kayıtları eşleşmez ve yanlış konum olarak kullanılmaz.
- Geoapify geocoding/routing adapter'ları `IGeocodingProvider` ve `IRouteDistanceProvider` arayüzlerine
  doğrudan kaydedilir. Tek API anahtarı `Geoapify:ApiKey` ile sağlanır; gövde/route loglarında URL veya
  sorgu alanı tutulmaz. Yeni sağlayıcı aynı arayüzlere yeni bir adapter ile eklenir.
- Personel alanları PDF başlığında görünür; JSON başarı yanıtında ve loglarda görünmez. Uygulama bu bilgiyi
  kalıcılaştırmaz.
