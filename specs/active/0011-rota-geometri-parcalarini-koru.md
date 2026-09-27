# Spec 0011 — Rota geometri parçalarını haritaya kadar koruma

- Status: Overview-only amendment implemented; tests in progress; visual evidence and canonical check outstanding
- Mode: lite
- Plan: `specs/plans/0011-plan.md`
- Related: `specs/done/0010-etkinlik-varis-ve-renkli-rotalar.md`

## Intent
PDF haritasındaki her renkli çizgi, Geoapify'nin o otel için döndürdüğü gerçek yol parçalarını izlemelidir. Bir rota birden fazla ayrık geometri parçası içeriyorsa bu parçalar arasında gerçekte bulunmayan düz bağlantılar çizilmemelidir. Böylece otel ile etkinlik alanı arasındaki çizim, harita üzerinde yanıltıcı kesmeler oluşturmadan gerçek yolu anlatır.

## Requirements
- Ana rota geometrisinin parça sınırları, öneri sonucundan rapor harita isteğine ve harita sağlayıcıya kadar korunur.
- Rota çizimleri her geometri parçasını bağımsız çizer; parçalar arasında birleştirici çizgi oluşturmaz.
- Varış pini mevcut boyutunun 1,1 katı olur ve mavi yerine kırmızı gösterilir; otel rota renkleriyle karıştırılmaz.
- Otel sırası, rota renkleri, varış ikonu, mesafe/süre bilgileri ve öneri seçimi değişmez.

## Constraints & out of scope
- Yeni rota sorgusu, veri kaynağı veya sağlayıcı eklenmez.
- Geometri noktaları yeniden sıralanmaz, sadeleştirilmez veya interpolasyonla birleştirilmez.
- Yalnızca harita çizimine aktarılan geometri yapısı düzeltilir.

## Acceptance criteria
- [x] AC-1 — İki ayrı yol parçası olan `RouteMetrics`, rapor servisi tarafından `StaticMapRequest` içine iki ayrı parça olarak aktarılır. Kanıt: `RecommendationReportServiceTests.CreateAsync_preserves_disconnected_main_route_segments_for_the_map`.
- [x] AC-2 — Geoapify Static Maps isteğinde iki parça ayrı geometri olarak bulunur; ilk parçanın sonu ile ikinci parçanın başı arasında yapay çizgi üretilmez. Kanıt: `HostedStaticMapProviderTests.RenderAsync_requests_a_png_base_map_and_adds_the_overlay` asserts separate component endpoints; schematic renderer draws each component with a separate `MoveTo`.
- [x] AC-3 — Varış pini iki harita panelinde de mevcut boyutunun 1,1 katı ve kırmızı olarak görünür; PDF lejantı bu rengi açıklar. Kanıt: provider color/scale assertions and `RecommendationsPdfEndpointTests` legend assertion.
- [x] AC-4 — Tek parçalı rotalar ve otel sıra/renk eşleşmesi mevcut davranışı korur. Kanıt: provider, unit, and PDF/JSON parity suites.
- [x] AC-5 — Raporun render edilmiş haritası incelenir; ayrık parça sınırlarında düz bağlantı görülmez ve büyütülmüş kırmızı varış pini ile rotalar okunaklı kalır. Kanıt: `specs/evidence/0011-route-segments-preview.png` and source PDF.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique
- Risk: Her parça arasındaki boşluk kesinti gibi görünebilir. Öneri: gerçek sağlayıcı geometrisini olduğu gibi göstermek ve haritada yapay bir geçiş çizmemek; bu boşluk, yol ağındaki ayrıklığı doğru yansıtır.
- Risk: Kırmızı pin önerilen otelin turuncu rotasıyla karıştırılabilir. Öneri: rotanın turuncu rengini koruyup pin için ayrı, daha koyu kırmızı kullanmak; pin formunu ve lejantını da korumak.

## Approved amendment

- Destination pin scale changes from 1.5× to 1.1×; its red color remains unchanged.
- When a walking route was requested and returned for a hotel, both overview and detail use that same walking-route geometry for that hotel. Other hotels retain their existing route geometry. The overview continues to show every available hotel route, and detail continues to take the first two eligible hotels in ranked-list order.
- No route is reordered by walking distance. Distances, durations, hotel ranks, and route colors remain unchanged.
- Proof must include a report-service test that confirms the map marker carries the walking path when available, and composition coverage that confirms detail still selects the first two eligible markers in ranked-list order.
- Visual review must compare one hotel's route between overview and detail and confirm both use the same walking geometry.
## Amendment implementation status

- User approved the amendment on 2026-09-27.
- Report markers now use returned walking geometry for both overview and detail; when no walking route is returned, the original route geometry remains.
- Destination pin scale is now 1.1× and remains red.
- Regression reproduction: failed before the mapping change, passed afterward. UnitTests: 96 passed. ProviderTests: 76 passed.
- `scripts/check` remains blocked at solution build by API process PID 21280 locking Application/Infrastructure DLLs. Independent review and refreshed visual evidence remain outstanding.
## Approved amendment — overview-only map and route geometry integrity

- Render one full-width general overview map; remove the nearby detail panel.
- Keep every ranked hotel marker and every route geometry actually returned by the routing provider.
- Do not synthesize a straight venue-to-hotel line when a marker has no route geometry. Such a line is not a road route and can misrepresent the path.
- Preserve multipart segment boundaries, ranked marker order, route colors, and the approved 1.1× red destination pin.
- Proof: hosted and schematic provider tests assert that a geometry-less marker creates no route polyline; provider output contains only a single full-width panel; a rendered PDF is visually reviewed for complete overview and genuine route lines.
## Latest amendment implementation status

- The hosted and schematic providers now render one full-width general overview and retain all ranked hotel markers.
- Both drawing paths now omit route strokes for markers without provider-returned route geometry; disconnected returned components remain independent.
- Focused provider tests: 79 passed. Unit tests: 96 passed.
- Canonical `scripts/check`, refreshed PDF visual review, and independent review remain outstanding.