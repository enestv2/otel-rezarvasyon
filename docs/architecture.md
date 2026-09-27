# Mimari

> Bootstrap ile dolduruldu (2026-09-25). Kararları ve sınırları tanımlar; ajanlar plan yapmadan
> önce bu dosyayı okur. Belirsizlik varsa varsayım yapılmaz (R-05/R-12).

## Sistem özeti
RPAOtelRezervasyon, bir **etkinlik alanı adı** ve kurumun **anlaşmalı otel adları** listesini girdi
alır. Konum bulma (geocoding) sağlayıcısıyla her adı koordinata çevirir, routing sağlayıcısıyla
etkinlik alanı–otel yol mesafesi/süresini hesaplar, adayları sıralar ve en uygun oteli rezervasyon
birimine önerir. Tek deployable olarak **modüler monolit** biçimindedir; gerekçe: solo geliştirme ve
düşük operasyonel maliyet (ADR-0001). Servis **stateless**'tır; kalıcı durumu yalnızca **konum
önbelleğidir** (MongoDB, ADR-0002) — iş verisi (rezervasyon, otel, oda) saklanmaz. Geocoding
sonuçları bu önbellekten okunur/yazılır; dış sağlayıcılar arayüz arkasındadır ve değiştirilebilir.

## Depo yerleşimi
- **`backend/`** — uygulama kodu: solution, `src/`, `tests/`, `Directory.Build.props`, `.editorconfig`.
  .NET ile ilgili her şey bu klasördedir.
- **Kök dizin** — ANEW iş akışı dosyaları: `AGENTS.md`, `docs/`, `specs/`, `workflows/`, `prompts/`,
  `scripts/`, `adapters/`.
- `scripts/check` kökten çalışır ve `backend/RPAOtelRezervasyon.sln`'i derler.

## Modüller ve sahiplik
| Modül | Tek sorumluluk | Sahip olduğu veri |
|---|---|---|
| VenueGeocoding | Etkinlik alanı adını koordinata çevirir | Etkinlik alanı konumu |
| HotelGeocoding | Her anlaşmalı otel adını koordinata çevirir | Otel konumları |
| DistanceRouting | Konum çiftleri için yol mesafesi/süresi | Mesafe ve süre sonuçları |
| Ranking | Adayları puanlar, en uygun oteli seçer | Sıralama kuralı |
| Delivery | Öneriyi rezervasyon birimine iletir | Öneri çıktısı |
| Reporting | Öneriyi coğrafi görselli PDF belgesine çevirir | Belge çıktısı (kalıcı değil) |
| LocationCache | Ad → Konum eşlemesini kalıcı önbellekte tutar | Konum önbelleği (MongoDB) |

## Sağlayıcı soyutlamaları
- `IGeocodingProvider` — mevcut uygulaması **Geoapify** olan sağlayıcı sözleşmesi. Yeni sağlayıcı
  Infrastructure adapter'ı olarak eklenebilir; çekirdek yalnızca arayüze bağlıdır.
- `IRouteDistanceProvider` — mevcut uygulaması **Geoapify Routing** olan sağlayıcı sözleşmesi.
- `IGeocodeCache` — kalıcı konum önbelleği; ilk uygulama **MongoDB** (ADR-0002). Çekirdek yalnızca
  arayüze bağlıdır; testlerde in-memory fake kullanılır.
- `IStaticMapProvider` — belge için coğrafi görsel; birincil uygulama Geoapify Statik Harita API'sini
  rota geometrilerini aynı projeksiyonda çizmek için kullanır, yerel SkiaSharp etkinlik/otel işaretleri ekler;
  hata durumunda şematik yedek rota geometrilerini yerelde çizer (ADR-0008).
- `IRecommendationReportRenderer` — rapor modelini belgeye çevirir; uygulama **Infrastructure**'da
  **QuestPDF** iledir (ADR-0006). Çekirdek PDF kütüphanesini bilmez.
- `IRecommendationExplanationProvider` — tüm otel verisi ile sunucuda belirlenmiş bütçe/ulaşım/denge
  seçenekleri için izinli gerekçe kodlarını streaming olarak alan OpenAI uyumlu adapter. LLM otel seçmez
  veya serbest metin üretmez; gerekçeler doğrulanıp sunucuda raporlanır.
- Testler ve yerel geliştirme **fake (sahte) sağlayıcılar** kullanır; ağ çağrısı yapılmaz.

## Sağlayıcı adaptör sözleşmesi
- Arayüzler **Domain**'de; uygulamalar **Infrastructure**'da. Infrastructure **tek assembly**'dir:
  `RPAOtelRezervasyon.Infrastructure` — `Providers/` (outbound HTTP) ve `Persistence/` (depolama)
  namespace'leri (ADR-0004). Yalıtım derleme yerine namespace + mimari test ile korunur.
- Beklenen durumlar istisna değil **sonuç tipidir**: `Found | NotFound | TransientError | ProviderError`. Sağlayıcı
  istisnası veya HTTP durum kodu çekirdeğe sızmaz; hata çevirisi adaptörde yapılır.
- Sağlayıcı DTO'ları Infrastructure adapter'larında kalır ve Domain'e **sızmaz**; dönüşüm
  adaptör mapper'ında yapılır (anti-corruption layer).
- Her adaptör `CancellationToken` alır ve dış isteğe geçirir.
- Dayanıklılık yalnızca adaptörde ve tutarlı biçimde uygulanır: `rate limiter → total timeout → retry
  (yalnızca idempotent GET, jitter'lı) → circuit breaker → attempt timeout`.
- Sağlayıcıya özgü başlık ve limitler adaptörde toplanır; iş kodunda sağlayıcı detayı bulunmaz.
- Mevcut Geoapify adapter'ları Domain sağlayıcı arayüzlerine doğrudan kaydedilir. Yeni bir sağlayıcı,
  aynı arayüzleri uygulayan adapter ve DI kaydı eklenerek bağlanır.
- Önbellek anahtarı **sağlayıcı kimliği + yer adı + şehir + ülke bağlamını** içerir; farklı coğrafi
  sorguların koordinatları karışmaz (BR-4).

## Gözlemlenebilirlik
- Sağlayıcı-başı metrikler: gecikme, hata oranı, retry/circuit-breaker sayacı ve **önbellek isabet oranı**.
- Sağlayıcı erişilebilirliği ve MongoDB ayrı **health check**'lerle raporlanır.
- Loglar yapılandırılmıştır; sır ve PII yazılmaz (yalnızca "tanımlı/tanımsız" bilgisi).

## İletişim kuralları
- Modüller yalnızca public arayüzlerden çağrılır; modül içi tipler dışarı açılmaz.
- Tüm dış API çağrıları yalnızca sağlayıcı adaptörleri üzerinden yapılır.
- Geocoding çağrısından **önce** konum önbelleği (MongoDB) kontrol edilir; **isabet varsa dış
  sağlayıcı hiç çağrılmaz** (BR-4).
- Otel bazlı geocoding ve mesafe çağrıları **paralel** yürütülür.
- Kısmi hata yalıtılır: bir otel çözümlenemezse istek tümden düşmez (BR-1).

## Yasak bağımlılıklar (bir mimari test bunları doğrulayabilir)
- Çekirdek/domain katmanı hiçbir HTTP altyapısına (`HttpClient`, sağlayıcı SDK'sı, JSON
  serileştirme) bağımlı olamaz.
- Bir modül başka bir modülün internal tipine veya depolamasına erişemez; yalnızca public arayüz.
- Sağlayıcı adaptörleri iş kuralı içermez.
- Domain katmanı `System.Net.*` veya Infrastructure adapter'ı sağlayıcı markalarını içermez.
- Domain katmanı MongoDB sürücüsüne (`MongoDB.Driver`) bağımlı olamaz; yalnızca `IGeocodeCache`
  arayüzünü bilir.
- `Infrastructure.Persistence.*` namespace'i `HttpClient` kullanmaz; `Infrastructure.Providers.*`
  namespace'i `MongoDB.Driver` kullanmaz (mimari test).

## Uçtan uca akış
`İstek (etkinlik alanı + otel listesi) → Konum Önbelleği (MongoDB) → [isabetsizler]
VenueGeocoding / HotelGeocoding (paralel) → DistanceRouting (paralel) → Ranking → Delivery`

PDF belgesi akışı (aynı orkestrasyon sonucu; yalnızca ölçülmüş yol mesafesi 3 km altındaki oteller için
ayrı Geoapify yürüme rotası ölçümü yapılır):
`RecommendationResult → Reporting (+ IWalkingRouteDistanceProvider, PDF-only eligible hotels) →
IRecommendationExplanationProvider (stream, validate, deterministic fallback) → IStaticMapProvider →
IRecommendationReportRenderer (QuestPDF) → application/pdf`

## Bilinçli kapsam dışı
- Rezervasyon kaydı/CRUD, oda envanteri, fiyat/kapasite yönetimi.
- Kullanıcı arayüzü.
- İş verisinin (rezervasyon, otel, oda) kalıcı saklanması; MongoDB yalnızca konum önbelleğidir.
- Çoklu-otel zinciri optimizasyonu; toplu (batch) işleme.
- Belge arşivleme/saklama, belge teslimi (e-posta/kuyruk), imzalama; belge kalıcı tutulmaz.
## Etkinlik bağlamı ve Geoapify sağlayıcıları
- Her iki öneri endpoint'i sicil/ad/soyad ve etkinlik adı/şehri/ülkesini aynı request DTO'sunda alır.
- Şehir/ülke hem etkinlik alanı hem otel geocoding çağrılarına taşınır; provider adaptörü girdiyi kendi
  arama alanlarına eşler. Geocode cache kimliği sağlayıcı + normalize yer adı + normalize şehir/ülkedir.
- `Geoapify` adapter'ları `IGeocodingProvider` ve `IRouteDistanceProvider` sözleşmelerini uygular; routing
  mesafe, süre ve GeoJSON yol geometrisi döndürür. `Geoapify:ApiKey` statik harita, geocoding ve routing
  tarafından ortak gizli değer olarak kullanılır.
- Geoapify geocoding ve routing mevcut aktif sağlayıcılardır. Sağlayıcı seçimi için migration modu
  bulunmaz; yeni sağlayıcı ekleneceğinde ayrı adapter ve DI kaydı tanımlanır.
- Personel bilgisi PDF rapor başlığında gösterilir; JSON öneri yanıtı ve loglara eklenmez, kalıcı saklanmaz.
