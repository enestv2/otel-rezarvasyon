# ADR 0005 — Düz çizgi fallback için süre uydurulmaz; sıralama mesafe türüne göre yapılır

- Status: Accepted
- Date: 2026-09-25

## Context
Yol rotası bulunamadığında (sağlayıcı "rota yok" derse) düz çizgi mesafesi hesaplanır. Ama bu durumda
**süre ölçülemez**. Bootstrap'ta yazılan BR-6 sıralamayı süreye dayandırdığı için, fallback otelleri
sıralayabilmek adına 60 km/sa varsayımıyla **tahmini** bir süre üretiliyordu. Bu, sistemdeki tek
uydurma değerdi ve tahminin gerçek ölçümü geçmesi gibi yanıltıcı sonuçlar doğurabiliyordu.

## Decision
Ölçülmemiş süre için **tahmin üretilmez**. Düz çizgi fallback'te `RouteMetrics.DurationSeconds = 0`'dır
(0 = "ölçülmedi" anlamında; süre olarak yorumlanmaz). BR-6 sıralaması yeniden tanımlanır: **önce
mesafe türü** — ölçülmüş yol (`Road`) her zaman düz çizgi tahmininden (`StraightLine`) önce gelir.
Road grubunda süre → mesafe → otel adı; StraightLine grubunda mesafe → otel adı.

## Consequences
**Kazanç:** Hiçbir değer uydurulmaz; "ölçüm" ile "tahmin" net ayrılır; gerçek yol ölçümü daima
tahminden önce sıralanır. **Maliyet:** Yalnızca fallback'ten oluşan bir sonuç kümesinde sıralama
mesafeye dayanır (süre yok); `DurationSeconds = 0`'ın "ölçülmedi" anlamı ekipçe bilinmelidir ve
tüketiciye `DistanceKind` ile birlikte sunulmalıdır.

## Alternatives considered
- **Tahmini süre (60 km/sa):** en az değişiklik; ama uydurma değer ve yanıltıcı sıralama → reddedildi.
- **Fallback'i hiç kullanmama (rota yoksa ele):** en basit; ama rota verisi olmayan oteller tamamen
  düşer ve AC-11 anlamsızlaşır → reddedildi.
- **Süreyi nullable yapmak (`int?`):** en açık model; ama Rationale/API sözleşmesine yayılan tip
  değişikliği ve ek karmaşıklık getirir → ertelendi (gerekirse ayrı ADR).

## Revisit triggers
- Gerçek sağlayıcı bağlandıktan sonra fallback sık devreye girerse (rota verisi kalitesi sorunu).
- Süre tahmini gerçekten gerekli olursa (ör. kullanıcı süreye göre filtre isterse) — o zaman açıkça
  "tahmin" olarak işaretlenmiş ayrı bir alanla yapılır.
