# Spec 0007 — Etkinlik kapsamı ve Geoapify konum servisleri

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0007-plan.md`

## Intent
Rezervasyon birimi, JSON ve PDF öneri uç noktalarına aynı personel ve etkinlik bilgilerini göndererek
tek ve tutarlı bir öneri almalıdır. Etkinlik alanı ve anlaşmalı oteller, etkinliğin şehir/ülke bilgisiyle
konumlandırılmalı; konum bulma ve araç rotası aynı Geoapify entegrasyonundan gelmelidir. Personel
bilgileri raporu ilişkilendirmek için istenir ancak gereksiz yere API yanıtlarına/loglara yayılmaz.

## Requirements
- Normal uygulama yapılandırmasında otel ve etkinlik alanı geocoding'i Geoapify kullanır; Nominatim/OSRM
  yalnızca açıkça `Legacy` seçildiğinde kullanılır. Shadow geçiş ölçümü için isteğe bağlı kalır.
- Her iki öneri uç noktası personelin sicil numarası, adı, soyadı ile etkinlik alanı adı, şehir ve ülke
  bilgilerini aynı request sözleşmesinde alır.
- Etkinlik alanı ve otel geocoding sorguları etkinlik şehir/ülke bağlamını kullanır.
- Etkinlik alanı-otel sürüş mesafesi, süresi ve rota geometrisi Geoapify Routing'den gelir.
- Sağlayıcı geçişi ölçülebilir ve geri alınabilir olur; önbellek farklı sağlayıcı ve coğrafi sorgu
  bağlamlarının sonuçlarını birbirine karıştırmaz.
- Sicil/ad/soyad PDF'de gösterilir; JSON yanıtına ve loglara eklenmez (önerilen gizlilik varsayımı).

## Constraints & out of scope
- JSON/PDF başarı ve hata davranışları ortak orkestrasyonda aynı kalır.
- API anahtarı sunucu tarafı gizli konfigürasyondan alınır; kaynak koda, örnek isteğe, yanıta ve loga
  yazılmaz. İstek URL'lerinde anahtar içerebileceğinden tam outbound URI loglanmaz.
- Rota başına bir Geoapify isteği mevcut paralel akışta sürdürülür; batch/matrix optimizasyonu,
  otel adresi toplama, personel verisini kalıcı saklama kapsam dışıdır.
- Mevcut PDF statik harita Geoapify anahtarıyla çalışmaya devam eder; servis ayarlarında aynı secret
  yapılandırmasının yeniden kullanımı planlanır.

## Acceptance criteria
- [x] AC-1 — JSON ve PDF uç noktaları sicil, ad, soyad, etkinlik adı, şehir ve ülke alanlarını kabul
  eder; geçersiz/eksik değerler ikisinde de aynı 400 doğrulama sözleşmesini üretir.
- [x] AC-2 — Şehir/ülke etkinlik geocoding isteğine ve otel konum sorgularına taşınır; Geoapify
  structured geocoding parametreleriyle uygun sorgu üretir.
- [x] AC-3 — Geoapify geocoding yanıtı koordinata, Geoapify sürüş rotası yanıtı mesafe/süre/GeoJSON
  geometrisine eşlenir; bulunamama/geçici hata/sağlayıcı hatası mevcut domain sonuçlarına çevrilir.
- [x] AC-4 — Geoapify konfigürasyonu tek sunucu secret'ı kullanır; anahtar istek query parametresinde
  gerekiyorsa dahi loglarda/çıktılarda görünmez ve boş/geçersiz anahtarla başlangıç doğrulaması yapılır.
- [x] AC-5 — Geocode cache key sağlayıcı kimliğinin yanında normalize ad + normalize şehir + normalize
  ülke bağlamını ayırt eder; eski bağlamsız kayıtlar yanlış eşleşme üretmez.
- [x] AC-6 — Normal uygulama yapılandırması Geoapify'yi geocoding ve routing için seçer; Nominatim/OSRM
  ancak `Legacy` açıkça seçilirse kullanılır. Shadow sonuçları kullanıcı sonucunu değiştirmez.
- [x] AC-9 — Geoapify varsayılan modunda API anahtarı yoksa uygulama başlangıçta anlaşılır konfigürasyon
  hatası verir; anahtar yapılandırıldığında etkinlik alanı ve her otel sorgusu event city/country bağlamıyla
  Geoapify'ye gider. Legacy override mevcut akışı çalıştırmaya devam eder.
- [x] AC-7 — Her iki endpoint'in PDF/JSON davranışı ve ortak hata eşlemesi regresyon testleriyle korunur;
  sicil/ad/soyad PDF metninde bulunur, JSON gövdesi ve loglarda bulunmaz.
- [x] AC-8 — Sağlayıcı dokümantasyonu/konvansiyonlar ve karar kaydı güncellenir; `scripts/check` yeşil
  olur ve her kriter bağımsız incelemede kanıtla eşleştirilir.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Değişiklik isteği (R-08, 2026-09-26)
İlk uygulama planı `Legacy` varsayılanını koruyordu. Kullanıcı, otel konumlarının Nominatim yerine
Geoapify'den event city/country bağlamıyla alınmasını istedi ve delta planını onayladı. Varsayılan mod
Bu sağlayıcılar ve Legacy/Shadow geçiş yolları daha sonraki 0013 spec kapsamıyla kaldırılmaktadır.

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise | |
| Regressions introduced | |
| Bugs escaped to production | |


> Current implementation note: spec 0013 later removed the Legacy/Shadow provider modes and their Nominatim/OSRM adapters.
