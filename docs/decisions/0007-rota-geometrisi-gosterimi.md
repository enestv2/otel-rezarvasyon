# ADR 0007 — Coğrafi görselde gerçek yol güzergâhının çizilmesi

- Status: Accepted
- Date: 2026-09-26

## Context
ADR-0006 şematik coğrafi görseli etkinlik alanı ↔ otel arasında **düz çizgi** bağlantılarla
tanımlıyordu. Rezervasyon birimi görselde "nasıl gidileceğini" yani **yol güzergâhını** istedi.
Routing sağlayıcısı (OSRM) zaten mesafe/süre ölçüyor; ancak `overview=false` istendiğinden geometri
alınmıyordu. Geometri belge için sonradan çekilirse BR-9/R-12'yi (ikinci rota çağrısı yok) ihlal
ederdi; bu yüzden geometrinin ölçümle birlikte taşınması gerekir.

## Decision
OSRM `/route` çağrısı `geometries=geojson&overview=simplified` ile yapılır; dönen `LineString`
koordinatları (`[boylam, enlem]`) **Domain**'de `RouteMetrics.Path` olarak taşınır (en fazla 512
nokta; kırpılırken iki uç korunur). Şematik sağlayıcı güzergâh varsa **polyline**, yoksa (düz çizgi
fallback / geometri alınamadı) **düz çizgi** çizer; görsel ölçeği güzergâhın tüm noktalarını kapsar.
JSON yanıt sözleşmesi değişmez (geometri yalnızca belge/görsel tüketir).

## Consequences
**Kazanç:** Görsel gerçek yol güzergâhını gösterir; taban harita/atıf/anahtar gerekmez; ikinci rota
çağrısı yoktur; görsel ağsız ve deterministik üretilir. **Maliyet:** `RouteMetrics` geometri taşıdığı
için bellek/yanıt boyutu büyür ve record değer-eşitliği **yapısal** olarak elle tanımlanır; geometri
"simplified" olduğundan çizgi yaklaşıktır; geometri yokluğunda görsel düz çizgiye düşer (zarif
bozulma); 512 nokta tavanı çok uzun rotalarda ayrıntıyı azaltır.

## Alternatives considered
- **Düz çizgi bağlantıda kalmak (ADR-0006 hâli):** en ucuz; ama kullanıcı gerçek güzergâh istedi →
  reddedildi.
- **Gerçek statik harita (tile + güzergâh):** tanıdık görünüm; anahtar/politika/atıf ve dış ağ
  bağımlılığı v1 kapsamı dışı → reddedildi (arayüz arkasında mümkün).
- **`overview=full`:** daha ayrıntılı; 640×360 görsel için gereksiz büyük ve yavaş → `simplified`
  seçildi.
- **Belge üretiminde ikinci rota çağrısı:** BR-9/R-12'yi ihlal eder ve tutarsızlık riski → reddedildi.

## Revisit triggers
- Güzergâh boyutu/gecikmesi ölçülebilir bir sorun olursa (→ downsample / önbellek / tavan ayarı).
- Taban haritalı gerçek görünüm iş gereği zorunlu olursa (→ statik harita adaptörü + atıf).
- Adım adım tarif veya çok ayaklı rota gerekirse (→ yeni sağlayıcı sözleşmesi).