# Spec 0013 — Nominatim ve OSRM adaptörlerinin kaldırılması

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0013-plan.md`

## Intent
Uygulama bundan sonra Nominatim ve `router.project-osrm.org` sağlayıcılarıyla çalışmayacaktır. Bu sağlayıcılara özel uygulama, yapılandırma ve testler kaldırılarak bakım yükü ve yanlışlıkla eski sağlayıcıya dönme olasılığı azaltılır. Geocoding ve routing'in Domain arayüzleri korunur; böylece ileride yeni bir sağlayıcı aynı sözleşmelere adapter olarak eklenebilir. Mevcut Geoapify akışı ve kullanıcıya görünen davranış aynı kalır.

## Requirements
- Nominatim ve OSRM uygulama adaptörleri, kayıtları, yapılandırmaları ve yalnızca bu adaptörlere ait testler kaldırılır.
- Normal uygulama akışı Geoapify geocoding ve routing kullanmayı sürdürür.
- `IGeocodingProvider` ve `IRouteDistanceProvider` sözleşmeleri ile bunların Domain modelleri değiştirilmez.
- Cache, sağlayıcı health check'leri, raporlamadaki Geoapify/şematik fallback ve sahte sağlayıcı testleri korunur.
- Yeni bir sağlayıcı, korunan arayüzleri uygulayan yeni bir Infrastructure adapter'ı ve DI kaydı eklenerek bağlanabilir.

## Constraints & out of scope
- API istek/yanıt sözleşmesi, sıralama, rota metrikleri ve cache davranışında değişiklik yapılmaz.
- Geoapify, statik harita veya yürüyüş rotası entegrasyonları kaldırılmaz.
- Domain sağlayıcı arayüzleri yeniden adlandırılmaz, birleştirilmez veya sağlayıcı ayrıntısı alacak şekilde değiştirilmez.
- Kullanılmayan Legacy/Shadow geçiş yolları ve karşılaştırma altyapısı kaldırılabilir; buna bağlı dokümantasyon ve testler güncellenir.

## Acceptance criteria
- [x] AC-1 — Üretim kodunda Nominatim ve OSRM HTTP adapter'ları, DI/health-check kayıtları ve bu sağlayıcılara özel ayarlar bulunmaz.
- [x] AC-2 — Nominatim/OSRM adapter'larına ve sağlayıcı geçiş/karşılaştırma davranışına özel testler kaldırılır; Geoapify ve sağlayıcıdan bağımsız testler korunur.
- [x] AC-3 — `IGeocodingProvider`, `IRouteDistanceProvider` ve Domain sonuç modellerinin public sözleşmeleri değişmez; başka adapter yazmak için kullanılabilir kalır.
- [ ] AC-4 — Normal konfigürasyonla geocoding ve routing Geoapify üzerinden çalışır; API ve raporlama davranışında regresyon olmaz.
- [x] AC-5 — Dokümantasyon ve örnek konfigürasyon eski sağlayıcıları etkin seçenek gibi sunmaz.

## Definition of Done
- [ ] Her kabul ölçütü test veya yinelenebilir kanıta eşlenir.
- [ ] `scripts/check` yeşildir.
- [x] Bağımsız inceleme tamamlanır; gerçek bulgular giderilir (bulgu yok).
- [ ] Spec `specs/done/` konumuna taşınır.

## Recommendation
Önerim, yalnızca ismi geçen sağlayıcı adapter'larını değil, artık işlevsiz olan `Legacy`/`Shadow` seçim ve kıyaslama yollarını da kaldırmak; Geoapify'yi tek aktif sağlayıcı yapmak ve Domain arayüzlerini olduğu gibi bırakmaktır. Böylece gizli bir rollback yolu kalmaz, sonraki sağlayıcı entegrasyonunun sınırı da net kalır.
