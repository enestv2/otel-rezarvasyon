# ADR 0001 — Konum sağlayıcısı `IGeocodingProvider` arkasında, ilk uygulama Nominatim

- Status: Accepted
- Date: 2026-09-25

## Context
Servis, etkinlik alanı ve otel **adlarından** konum üretmek zorundadır. Sağlayıcı pazarı sık
değişir (ücret, kapsam, rate limit, veri kalitesi) ve sağlayıcıya doğrudan bağlanmak tüm çekirdeği
kırılgan yapar. Elimizde .NET 10, ağ erişimi ve OSM ekosistemiyle başlama tercihi var.

## Decision
Konum bulma, `IGeocodingProvider` arayüzünün arkasına alınır. **İlk uygulama Nominatim
(OpenStreetMap)** olur. Mesafe için `IRouteDistanceProvider` arayüzü ayrılır; önerilen ilk uygulama
**OSRM**'dir. Testlerde ve yerel geliştirmede fake sağlayıcılar kullanılır.

## Consequences
**Kazanç:** Sağlayıcı değişimi çekirdeğe dokunmaz; testler ağsız ve deterministiktir; anahtarsız
başlanabilir. **Maliyet:** Nominatim'in kullanım politikası (geçerli `User-Agent`, saniyede ≤1
istek) ve veri kalitesi sınırları vardır; bu yüzden önbellek (BR-4) zorunlu hale gelir. Ek bir
arayüz ve adaptör bakım yükü oluşur.

## Alternatives considered
- **Sağlayıcıya doğrudan bağlanma:** en hızlı yol; ama sağlayıcı değişiminde tüm çekirdek kırılır →
  reddedildi.
- **Google Maps Platform ile başlama:** daha iyi veri/coverage; ama anahtar + faturalandırma
  zorunlu, öğrenme aşamasında gereksiz maliyet → ertelendi.
- **Azure Maps:** geocoding + routing tek abonelikte; .NET dostu. Anahtar gerektirir → alternatif
  adaptör olarak saklandı, ilk uygulama seçilmedi.

## Revisit triggers
- Nominatim istek limiti/kalite sorunu ölçekte problem olursa.
- Ticari SLA veya yüksek doğruluk gerekirse (Google/Azure Maps adaptörü).
- Routing için OSRM yetersiz kalırsa (trafik verisi, gerçek zamanlı süre).
