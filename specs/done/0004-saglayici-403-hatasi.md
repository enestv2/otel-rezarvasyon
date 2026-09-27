# Spec 0004 — Geocoding sağlayıcısı HTTP hatalarının doğru raporlanması

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0004-plan.md`

## Intent
Bir geocoding sağlayıcısı isteği reddettiğinde veya başarısız olduğunda, etkinlik alanı gerçekten bulunamadı gibi gösterilmemelidir. Kullanıcı, adres araması sonuçsuz kaldığında bu durumu; sağlayıcıya erişim veya politika hatası olduğunda ise hizmet hatasını ayırt edebilmelidir. Kimlik doğrulama hataları da bu iki durumdan ayrı kalmalıdır.

## Requirements
- Sağlayıcının başarılı aramasında boş sonuç, konum bulunamadı olarak değerlendirilir.
- Sağlayıcı HTTP isteğini reddederse veya beklenmeyen HTTP hata durumu döndürürse uygulama bunu bulunamayan konum olarak raporlamaz.
- Sağlayıcı hatasında istemciye sır veya upstream yanıt gövdesi sızdırmadan bir hizmet hatası döner.
- API anahtarı geçersiz/eksik olduğunda istek iş mantığına ve sağlayıcıya ulaşmaz.

## Constraints & out of scope
- Nominatim erişim engelinin nedeni varsayılmaz; gerçek User-Agent/Referer, kullanım sınırı ve olası sağlayıcı kısıtları ayrıca kontrol edilir.
- Nominatim’in 403 engelini aşmaya yönelik retry, proxy rotasyonu veya kimlik taklidi yapılmaz.
- Nominatim hesabı/erişim politikası değişikliği ve sağlayıcı değiştirme kapsam dışıdır.

- **Kapsam notu (0004):** Sağlayıcı hata sınıflandırması tüm geocoding çağrıları için geçerlidir (`ProviderError`). Otel bazlı sağlayıcı hatasında istek düşmez; 0001'in kısmi başarı kuralı gereği otel "çözümlenemedi" olarak raporlanır. Etkinlik alanı sağlayıcı hatasında öneri üretilmez ve `502` döner (bu spec'in odağı). Otel sağlayıcı hatalarının `502`'ye taşınması ayrı bir spec konusudur.

## Acceptance criteria
- [x] AC-1 — Başarılı HTTP yanıtında boş arama sonucu “konum bulunamadı” olarak kalır.
- [x] AC-2 — Nominatim 403 yanıtı “konum bulunamadı”ya dönüşmez; API güvenli bir hizmet hatası döner.
- [x] AC-3 — Diğer beklenmeyen sağlayıcı HTTP hata durumları yanlışlıkla bulunamayan konum sayılmaz.
- [x] AC-4 — Sağlayıcı hata yanıtı istemciye ham gövde veya hassas ayrıntı sızdırmaz; durum kodu loglanır.
- [x] AC-5 — Eksik/yanlış API key 401 üretir ve geocoding sağlayıcısı çağrılmaz.

## Definition of Done
- [x] Her kabul ölçütü kanıta (test veya tekrarlanabilir gözlem) bağlandı
- [x] `scripts/check` yeşil
- [x] Bağımsız inceleme yapıldı; gerçek bulgular giderildi, gürültü yazılı gerekçeyle reddedildi
- [x] Davranış veya mimari değiştiyse dokümanlar / ADR'ler güncellendi
- [x] Spec `specs/done/` konumuna taşındı (değiştirilemez olur)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 1 |
| Fix rounds | 1 |
| Review findings: real / noise | 3 / 1 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 |

## Öz eleştiri
- Hizmet hatasının HTTP kodu belirtilmedi. Öneri: upstream sağlayıcı başarısızlığını 502 Bad Gateway ile dön; istemci hatasıyla ayırt edilir ve sağlayıcı teknik ayrıntısı açıklanmaz.
- Nominatim’in boş 200 sonucu dışında bir HTTP 404 döndürmesi olasılığı belirsiz. Öneri: yalnızca başarılı boş arama sonucunu kesin `NotFound` say; HTTP hatalarını sağlayıcı hatası kabul et.

## Ship notları (kanıt ve kapsam)
- **AC-2/AC-3:** `NominatimGeocodingProviderTests.GeocodeAsync_when_http_error_maps_to_provider_error` (400/403/404 → `ProviderError`) ve `GeocodeAsync_when_provider_rejects_request_does_not_report_not_found`; entegrasyon `Post_when_geocoding_provider_rejects_returns_502_without_upstream_details` (502 + `application/problem+json`).
- **AC-4:** `GeocodeAsync_when_provider_rejects_request_logs_status_without_body` gerçek HTTP gövdesi modelleyip logda "403" olduğunu ve ham gövdenin sızmadığını doğrular; 502 yanıtı `extensions`/ham ayrıntı taşımaz.
- **AC-5:** Eksik ve yanlış API anahtarı için 401 + sağlayıcı çağrılmadı testleri mevcuttur (`Post_without_api_key_...`, `Post_with_wrong_api_key_...`).
- **Kapsam (F1):** Otel bazlı sağlayıcı hataları 0001'in kısmi başarı kuralıyla "çözümlenemedi" olarak raporlanır; 502'ye taşınması ayrı bir spec konusudur (Constraint'e not eklendi).
- **Doğrulama notu:** Çalışan API örneği (PID 20024) `Api/bin` çıktısını kilitlediğinden `scripts/check` düz çalıştırılamadı; eşdeğer build (`-warnaserror`) ve testler geçici `BaseOutputPath` ile çalıştırıldı — 0 uyarı/0 hata, 125 geçti, 4 opt-in Mongo skip.