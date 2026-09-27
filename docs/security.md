# Güvenlik

> Bootstrap ile dolduruldu (2026-09-25). Her plan ve incelemede ajanların uyacağı taban kurallar.

## Gizli bilgiler
- API anahtarları, sırlar repoya, spec'e, prompt'a veya sohbete **girmez**. `.env` gitignore'ludur;
  `gitignore` içinde `!.env.example` istisnası vardır → `.env.example` commit edilir.
- Anahtarlar yerelde `dotnet user-secrets`, CI'da ortam değişkeni ile verilir.
- Geliştirici profili için `backend/src/RPAOtelRezervasyon.Api/Properties/launchSettings.example.json` dosyası `launchSettings.json` olarak kopyalanır. Yerel gerçek `launchSettings.json` `.gitignore` ile Git dışında tutulur ve geliştiriciye ait secret içerebilir.
- Production ortam değişkenlerinin tamamı Rancher Secret üzerinden enjekte edilir; production secret değerleri repoya, örnek launch profiline veya `appsettings*.json` dosyalarına yazılmaz.
- LLM is disabled in the safe example profile. Configure `Llm__Enabled`, `Llm__BaseUrl`, `Llm__Model`, and `Llm__ApiKey` in the ignored local launch settings after validating the endpoint and model. Timeout, output token count, and temperature are bounded. Production receives these settings through Rancher Secret/environment variables.
- LLM'e etkinlik alanı ve otel öneri sonuçları (otel adları, koordinatlar, fiyatlar, rota metrikleri ve çözümlenemeyen adlar) gönderilir. Sicil numarası, personel adı/soyadı ve API anahtarları gönderilmez. İstek/yanıt gövdeleri loglanmaz veya kalıcı saklanmaz.
- Ajanlar hata ayıklarken bile sır değerlerini yazdırmaz; yalnızca "tanımlı/tanımsız" bilgisini verir.
- **MongoDB bağlantı dizesi** kullanıcı adı/parola içerebilir → sırdır; `user-secrets`/ortam değişkeni
  ile verilir, `appsettings*.json` içine yazılmaz.

Yerel API anahtari gerekiyorsa `Api:ApiKey` User Secrets'e kaydedilir:
`dotnet user-secrets set "Api:ApiKey" "<api-anahtari>" --project backend/src/RPAOtelRezervasyon.Api`.
Geoapify anahtari icin `Geoapify:ApiKey` kullanilir:
`dotnet user-secrets set "Geoapify:ApiKey" "<geoapify-anahtari>" --project backend/src/RPAOtelRezervasyon.Api`.
Bu degerleri repodaki launch settings ornegine veya `.env.example` dosyasina eklemeyin.
## Girdi ve çıktı
- Sınırda doğrulama: etkinlik alanı adı boş olamaz ve uzunluk sınırı vardır; otel listesi 1…N
  arasındadır (BR-5). Uzunluk/sayı sınırı kaynak tükenmesini (DoS) engeller.
- Dış sağlayıcı yanıtları **güvenilmez** kabul edilir: şema doğrulaması yapılır, ham yanıt kullanıcıya
  yansıtılmaz.
- Hata mesajlarında iç detay (stack trace, ham sağlayıcı yanıtı, dosya yolu) **sızmaz** (ProblemDetails).

## Dış servis politikaları
- Sağlayıcıya özgü kullanım politikaları adapter yapılandırmasında uygulanır; geocoding sonuçları
  önbelleklenir (BR-4).
- Giden çağrılara timeout, sınırlı retry (üstel geri çekilme) ve devre kesici uygulanır.
- MongoDB erişimi **en az yetkiyle**: yalnızca önbellek veritabanına `readWrite`; üretimde TLS zorunlu.

## AuthN / AuthZ
- Servis uç noktası varsayılan olarak **kapalıdır** (default-deny); API anahtarı veya JWT ile açılır.
- Yetkilendirme, girdi doğrulamadan **önce** çalışır.
- Rezervasyon birimine giden teslim de kimlik doğrulamalı bir kanaldan yapılır.

## Belge üretimi (PDF)
- Belge yalnızca doğrulanmış, başarılı öneri için üretilir; kullanıcı girdisi PDF'e **düz metin**
  olarak yazılır (gömülü dosya/zengin içerik/komut yoktur).
- Kaynak tükenmesine karşı sınırlar: istek gövdesi (≤ 64 KB), coğrafi görsel boyutu (≤ 2048 px kenar)
  ve üretilen belge boyutu (`Reporting__MaxDocumentBytes`). Aşılırsa belge üretilmez, hata döner.
- Belge kalıcı saklanmaz; konum önbelleğine belge/rapor verisi **yazılmaz**.
- Harita dış bir sağlayıcıdan gelirse atıf zorunludur ve sağlayıcı anahtarı sır olarak yönetilir
  (`user-secrets`/ortam değişkeni).
- Geoapify anahtarı `Geoapify__ApiKey` ile verilir; statik harita, geocoding ve routing aynı secret'ı
  kullanır. Eski `StaticMap__ApiKey` geçici uyumluluk girdisidir. Anahtar çıktılara/telemetry/loglara
  yazılmaz; query string anahtar içerdiği için Geoapify typed HttpClient logları kapalıdır.
- Yerelde yeni/rotate edilmiş anahtarı kaynak dosyaya koymadan şu şekilde kaydet:
  `dotnet user-secrets set "Geoapify:ApiKey" "<yeni-anahtar>" --project backend/src/RPAOtelRezervasyon.Api`
  ASP.NET Core ortam değişkeni eşlemesi de `Geoapify__ApiKey` adını `Geoapify:ApiKey` ayarına bağlar.
  `.env.example` içindeki değer bilerek boştur; gerçek anahtar eklenmez.

## Bağımlılıklar
- Yeni bağımlılık: insan onayı + lisans, bakım durumu ve bilinen CVE kontrolü. Sağlayıcı SDK'sı yerine
  mümkünse `HttpClient` + arayüz tercih edilir (değiştirilebilirlik).
- Belge üretimi bağımlılıkları (0006, ADR-0006): `QuestPDF` (topluluk lisansı), `SkiaSharp` (MIT),
  test-only `PdfPig` (Apache-2.0).

## İnceleme merceği
Güvenlik, her bağımsız incelemenin zorunlu bir boyutudur (bkz. `prompts/review.md`), ayrı bir son
aşama değildir.
## Geoapify ve personel verisi (0007)
- Geoapify anahtarı `Geoapify__ApiKey` / `Geoapify:ApiKey` ile verilir; statik harita, geocoding ve
  routing aynı secret'ı kullanır. Eski `StaticMap__ApiKey` geçici uyumluluk girdisidir. Anahtar
  `appsettings.json`, PDF/JSON çıktısı, telemetry ve loglara yazılmaz. Query string anahtarı içerdiği için
  Geoapify typed `HttpClient` istek logları kapalıdır; adapter'lar exception/URI yazdırmaz.
- Sicil numarası ve personel adı/soyadı istekte alınır ve yalnızca PDF başlığında gösterilir; JSON
  yanıtına, loglara ve MongoDB konum önbelleğine eklenmez. Hassas bir anahtar sohbet/issue gibi herkese
  açık veya saklanan bir kanalda paylaşılırsa iptal edilip yenilenmelidir.
