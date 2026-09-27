# Spec 0010 — Etkinlik alanı varış işareti ve renkli rotalar

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0010-plan.md`
- Related: `specs/active/0009-profesyonel-pdf-raporu.md`
- Supersedes: overview-only-selected-route wording in 0009's plan/conventions; the overview now shows every available hotel route.

## Intent
Raporu kullanan kişi haritada etkinlik alanını varış noktası olarak hemen tanıyabilmeli ve her otelin bu noktaya hangi güzergâhtan ulaştığını kendi harita işaretinin rengiyle izleyebilmelidir. Etkinlik alanı numaralı otel işaretlerinden görsel olarak ayrışır. Rota çizgileri yol geometrisini izler ve haritanın ayrıntılarını kapatmayacak kadar ince görünür.

## Requirements
- Etkinlik alanı, otel numara dairelerinden farklı bir varış noktası ikonu ile genel görünümde ve yakın planda gösterilir.
- İkon ve lejant, bu noktanın etkinlik alanı/varış yeri olduğunu açıkça anlatır.
- Rota çizgilerinin renkleri ilgili otel işaretinin rengiyle eşleşir; seçili otelin rengi de bu eşleşmenin içindedir.
- Genel görünüm, kullanılabilir yol geometrisi bulunan tüm otellerin varış rotalarını birlikte gösterir; yakın görünüm mevcut en fazla iki uygun yürüyüş rotasını gösterir.
- Kullanılabilir yol geometrileri ince çizgilerle gösterilir. Ayrık geometri parçaları arasına yapay bağlantı eklenmez.
- Çizgi kalınlığı, desen ve varış ikonu birlikte kullanıldığında yollar, otel numaraları ve harita ayrıntıları okunabilir kalır.

## Constraints & out of scope
- Otel sırası, rota seçimi, mesafe/süre değerleri, sağlayıcı geometrisi, GeoJSON/JSON sözleşmesi ve öneri davranışı değişmez.
- Yeni harita sağlayıcısı veya harita verisi eklenmez.
- Geometrisi bulunmayan rota için yol güzergâhı uydurulmaz; mevcut fallback davranışı korunur.

## Acceptance criteria
- [x] AC-1 — Etkinlik alanı iki harita panelinde de otel dairelerinden farklı bir varış ikonu olarak görünür ve lejantta varış noktası diye açıklanır. Kanıt: `SchematicStaticMapProviderTests.RenderAsync_distinguishes_nearby_markers_and_keeps_the_venue_moderate`; rendered preview.
- [x] AC-2 — Her rota çizgisi ilgili otel işaretinin rengiyle eşleşir; seçili rota dahil çizgiler harita ayrıntısını kapatmayacak incelikte kalır. Kanıt: `HostedStaticMapProviderTests.RenderAsync_requests_a_png_base_map_and_adds_the_overlay`; `SchematicStaticMapProviderTests.Overlay_renders_matching_colored_routes_with_solid_selected_and_dashed_other_routes`.
- [x] AC-3 — Rota çizgileri sağlayıcının yol geometrisini ve ayrık parçalarını aynen izler; parçalar arasına yapay düz bağlantı çizilmez. Kanıt: hosted map test asserts separate path segment payloads; `GeoapifyProviderTests.GetRouteAsync_preserves_disconnected_multiline_components_without_joining_them`.
- [x] AC-4 — Sağlayıcı ve PDF entegrasyon testleri ikon, renk eşleşmesi, ince çizgi ve mevcut sıralama/veri davranışının korunduğunu kanıtlar. Kanıt: provider suite 76 passed; integration suite 52 passed / 4 skipped; PDF/JSON parity test remains green.
- [x] AC-5 — Render edilmiş PDF sayfası incelenerek etkinlik varış ikonunun, rota renklerinin ve lejantın açıkça okunabildiği doğrulanır. Kanıt: `specs/evidence/0010-arrival-routes-preview.png` and source PDF.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Recommendation
Etkinlik alanı için otel numaralarından farklı, küçük bir varış bayrağı/pin işareti kullanılsın; lejantta “Varış: etkinlik alanı” olarak açıklansın. Her rota, bağlı olduğu otelin mevcut rengiyle çizilsin. Seçili rota dahil tüm rota çizgileri inceltilsin; seçili/diğer rota ayrımı mevcut dolu/kesikli desenle korunsun. Rota geometrileri ve diğer işaretler değişmesin.

## Self-critique
- Risk: Bütün rotaların renkli çizilmesi yakın bölgelerde üst üste gelebilir. Öneri: ince çizgi ve mevcut farklı desenlerle ayrımı korumak; harita ölçeğini veya rota geometrisini değiştirmemek.
- Risk: Yeni varış ikonu otel rozetleriyle karışabilir. Öneri: numara içermeyen bayrak/pin şekli ve lejant açıklaması kullanmak.
