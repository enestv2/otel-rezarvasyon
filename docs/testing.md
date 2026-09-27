# Test

> Bootstrap ile dolduruldu (2026-09-25).

## Sözleşme
- Her kabul kriteri en az bir teste karşılık gelir (kriter ↔ test eşlemesi plan dosyasındadır).
- Testler **davranışı** doğrular; uygulama detayını veya yalnızca durum kodunu değil.
- Tüm paket `scripts/check` içinde çalışır — tek komut, her yerde.

## Framework ve yerleşim
- **xUnit** + **FluentAssertions**.
- API seviyesi: `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) ile uçtan uca testler.
- Test projeleri `backend/tests/` altındadır: `RPAOtelRezervasyon.UnitTests`,
  `RPAOtelRezervasyon.IntegrationTests`, `RPAOtelRezervasyon.ProviderTests`,
  `RPAOtelRezervasyon.ArchitectureTests`.
- Sağlayıcılar **fake** ile enjekte edilir; testler ağa çıkmaz (determinizm).
- Önbellek `IGeocodeCache` arayüzüdür; birim testleri **in-memory fake** kullanır (ağ ve DB yok).
- MongoDB entegrasyonu ayrı, **isteğe bağlı** testlerle doğrulanır (yerel MongoDB ya da
  `EphemeralMongo`). Makinede Docker yok; bu yüzden varsayılan `scripts/check` bu testlere bağlı değildir.
- Adaptör (sağlayıcı) testleri: `HttpMessageHandler` sahteleriyle birim testleri + **WireMock.Net** ile
  sözleşme testleri (kayıtlı fixture'lar). Canlı API'ye vuran testler **opt-in smoke** kategorisindedir
  (`Category=Live`); varsayılan `check` bunlara bağlı değildir.

## Neler test edilmeli
- Her iş kuralı: BR-1 … BR-6 (özellikle BR-1 kısmi başarı ve BR-3 determinizm).
- Gerçekten yaşanmış / olası sınır durumları: boş otel listesi, çözümlenemeyen etkinlik alanı,
  sağlayıcı timeout'u, aynı mesafede eşitlik (BR-6), yinelenen otel adları, sağlayıcı rate limit.
- Kritik akış: geocode → routing → sıralama → teslim uçtan uca.
- Önbellek davranışı: isabet → dış sağlayıcı **çağrılmaz**; kayıt servis yeniden başlayınca kalır.
- Sağlayıcı sözleşmesi: beklenen JSON şekli/alan adları değişirse sözleşme testleri kırılır.
- Yasak bağımlılıklar için mimari test (domain katmanında `System.Net.*` ve sağlayıcı adı geçmemesi).

## Korumalı testler
Testi yeşile çevirmek için assert zayıflatmak, silmek veya atlamak **yasaktır**. Kırmızı test
`prompts/recovery/red-test.md` (R-02) tetikler: önce neyin yanlış olduğuna karar verilir — kod mu,
test mi, spec mi.

## Determinizm
Flaky testler düzeltilir, yeniden denenmez veya atlanmaz — bkz. R-03. Düzeltmenin kanıtı: 5 ardışık
yeşil çalışma.
