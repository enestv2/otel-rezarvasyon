# ADR 0002 — Konum sonuçları MongoDB'de kalıcı önbelleklenir

- Status: Accepted
- Date: 2026-09-25

## Context
Aynı etkinlik alanı ve otel adları tekrar tekrar sorulur. Nominatim gibi bir sağlayıcı saniyede en
çok 1 isteğe izin verir; her istekte dış servise gitmek hem yavaş hem de politika ihlali riskidir.
Önceki taslakta önbellek süreç belleğindeydi: servis yeniden başlayınca kaybolur ve çok örnekli
çalışmada tutarsız olur. Konum verisi (ad → koordinat) doğası gereği nadiren değişir.

## Decision
Konum sonuçları **MongoDB'de kalıcı olarak** saklanır ve `IGeocodeCache` arayüzü arkasında sunulur.
Geocoding'den önce önbellek kontrol edilir; **isabet varsa dış sağlayıcıya hiç gidilmez**. İlk
uygulama MongoDB'dir; arayüz sayesinde başka bir depo (ör. Redis, SQL) sonradan takılabilir.

## Consequences
**Kazanç:** Tekrarlı isimlerde dış çağrı sıfıra iner (hız + rate limit rahatlaması); kayıt servis
yeniden başlasa da kalır; sağlayıcı politikasına uyum kolaylaşır. **Maliyet:** Yeni bir altyapı
bileşeni ve bağlantı sırrı yönetimi gelir; testlerde Docker olmadığı için Mongo entegrasyonu
isteğe bağlı testlere ayrılır; önbellek şeması ve TTL gibi bakım yükleri doğar.

## Alternatives considered
- **Süreç içi (in-memory) önbellek:** en basit; ama yeniden başlatmada kaybolur, çok örnekli
  dağıtımda tutarsız → reddedildi.
- **Redis:** çok hızlı ve TTL hazır; ama ek altyapı ve kalıcılık yapılandırması gerekir → ertelendi.
- **İlişkisel veritabanı (SQL):** bilinen araçlar; ama şemasız ad→konum kaydı için gereğinden ağır →
  reddedildi.
- **Önbelleksiz (her istek dış servise):** basit; ama rate limit ve gecikme nedeniyle sürdürülemez →
  reddedildi.

## Revisit triggers
- Çok örnekli (yatay ölçekli) dağıtımda okuma gecikmesi sorun olursa (Redis önüne alınabilir).
- Veri hacmi/TTL politikası maliyeti artırırsa.
- Farklı bir depo standardı (ör. kurumsal SQL zorunluluğu) devreye girerse.
