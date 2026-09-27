# Spec 0002 — Swagger entegrasyonu

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0002-plan.md`

## Intent
Geliştiriciler, API'nin kullanılabilir uç noktalarını ve istek/yanıt biçimlerini uygulama çalışırken tarayıcıdan inceleyip deneyebilmelidir. OpenAPI belgesi, API anahtarı gereksinimini doğru yansıtmalı; Swagger UI bu belgeyi sunmalıdır. Dokümantasyon yalnızca Development ortamında erişilebilir olmalı; diğer ortamlarda kapalı kalmalıdır.

## Requirements
- API uç noktaları OpenAPI belgesinde açıklanır.
- Belge, yetkilendirme için kullanılan API anahtarı başlığını ve ilgili operasyonların yanıt durumlarını gösterir.
- Swagger UI, Development ortamında belgeyi yükler ve yetkili çağrı denemesine imkân verir.
- Development dışındaki ortamlarda OpenAPI belgesi ve Swagger UI sunulmaz.
- Dokümantasyon uçları, anahtar olmadan tarayıcıdan erişilebilir.

## Constraints & out of scope
- API'nin iş davranışları ve kimlik doğrulama biçimi değişmez.
- Üretimde API dokümantasyonu yayınlanması, alternatif UI'lar ve istemci kodu üretimi kapsam dışıdır.

## Acceptance criteria
- [x] AC-1 — Geliştirme ortamında OpenAPI belgesi başarılı yanıt verir ve API operasyonlarını içerir.
- [x] AC-2 — Swagger UI geliştirme ortamında yüklenir ve OpenAPI belgesine bağlıdır.
- [x] AC-3 — Belge API anahtarı başlık güvenlik şemasını ve korumalı operasyonların yetkilendirme gereksinimini gösterir.
- [x] AC-4 — Belge, öneri operasyonunun başarılı, doğrulama, yetkisiz ve iş kuralı yanıtlarını doğru tiplerle listeler.
- [x] AC-5 — OpenAPI ve Swagger UI, üretim ortamında sunulmaz.
- [x] AC-6 — OpenAPI ve Swagger UI uçları kimlik doğrulaması istemez; API iş uç noktalarının yetkilendirmesi etkilenmez.

## Definition of Done
- [x] Her kabul ölçütü kanıta (test veya tekrarlanabilir gözlem) bağlandı
- [x] `scripts/check` yeşil
- [x] Bağımsız inceleme yapıldı; gerçek bulgular giderildi, gürültü yazılı gerekçeyle reddedildi
- [x] Davranış veya mimari değiştiyse dokümanlar / ADR'ler güncellendi
- [x] Spec `specs/done/` konumuna taşındı (değiştirilemez olur)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 |
| Fix rounds | 1 |
| Review findings: real / noise | 1 / 0 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 |

## Ship notları (inceleme)
- Bağımsız inceleme (2026-09-26, salt-okunur ajan): 6/6 AC davranış düzeyinde kanıtlandı; başka bulgu yok (clean).
- Düşük önemli gözlem (kabul edildi): OpenAPI güvenlik gereksinimi yalnızca `[Authorize]` endpoint metadata'sından üretilir; ileride yalnızca fallback politikayla korunan bir uç eklenirse belgede yetkisiz görünebilir. Bugün tüm korumalı uçlar `[Authorize]` taşıdığı için risk yok.

## Öz eleştiri
- Ortam ayrımının hangi ayara bağlı olduğu belirsiz. Öneri: ASP.NET Core `Development` ortamında etkinleştir, `Production` ortamında kapalı tut; bu standart ve konfigürasyon sürprizini azaltır.
- OpenAPI belgesinin sürümleme ve başlık metaverisi ölçütlerde tanımlı değil. Öneri: mevcut API adını kullanıp tek belge (`v1`) üret; sürümleme politikası kapsam dışı kalsın.
