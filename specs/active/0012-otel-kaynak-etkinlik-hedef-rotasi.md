# Spec 0012 — Otel kaynaklı etkinlik hedefli rota

- Status: Spec and plan proposed; awaiting human plan approval
- Mode: lite
- Plan: `specs/plans/0012-plan.md`
- Related: `specs/active/0011-rota-geometri-parcalarini-koru.md`

## Intent
Otel ile etkinlik alanı arasındaki rota bilgisi alınırken yolculuk otelde başlamalı ve etkinlik alanında bitmelidir. Böylece sağlayıcı isteğinin yönü iş anlamıyla tutarlı olur ve dönen rota geometrisi haritada otelden etkinliğe uzanır.

## Requirements
- Her anlaşmalı otelin araç rotası sorgusunda kaynak otelin konumu, hedef etkinlik alanının konumudur.
- Ek yürüyüş rotası sorgusu yapıldığında da kaynak otelin konumu, hedef etkinlik alanının konumudur.
- Dönen mesafe, süre ve rota geometrisi sağlayıcı yanıtından olduğu gibi kullanılır; sıralama kuralları ve otel önerisi değişmez.

## Constraints & out of scope
- Yeni rota çağrısı, veri kaynağı veya sağlayıcı eklenmez.
- Geocoding, sıralama, mesafe/süre birimleri ve fallback davranışı değişmez.
- Rota geometrisi yalnızca ters çevrilmez; sağlayıcıya otel → etkinlik yönüyle sorulur.

## Acceptance criteria
- [ ] AC-1 — Her otel için ana routing sağlayıcısına verilen `origin` otel koordinatı, `destination` etkinlik koordinatıdır.
- [ ] AC-2 — Yürüyüş rotası istenen her otel için yürüyüş sağlayıcısına verilen `origin` otel koordinatı, `destination` etkinlik koordinatıdır.
- [ ] AC-3 — İstek yönünün değiştirilmesi sıralama/öneri davranışını ya da sağlayıcıdan alınan mesafe, süre ve geometrinin değerlerini dönüştürmez.

## Definition of Done
- [ ] Her kabul ölçütü test veya yinelenebilir kanıta eşlenir.
- [ ] Bağımsız inceleme tamamlanır.
- [ ] `scripts/check` yeşildir.
- [ ] Dokümanlar güncellenir.
- [ ] Spec `specs/done/` konumuna taşınır.

## Self-critique
- Risk: Bazı rota sağlayıcıları başlangıç/hedef yönüne göre farklı mesafe, süre veya geometri döndürebilir. Öneri: İstenen yönü her iki rota türü için de açıkça sabitleyip mevcut sıralama kurallarına dokunmamak; bu, kullanıcının belirttiği otelden etkinliğe seyahat yönünü karşılar.
