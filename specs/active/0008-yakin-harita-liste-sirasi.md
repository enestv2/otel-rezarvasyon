# Spec 0008 — Yakın haritada liste sırasını koruma

- Status: Draft
- Mode: lite
- Plan: `specs/plans/0008-plan.md`
- Supersedes: the walking-distance ordering in AC-26 of `specs/done/0006-oneri-pdf-belgesi.md`

## Intent
Raporu okuyan kişi, yakın haritadaki otelleri PDF listesindeki sırayla takip edebilmelidir. Yakın görünüm en fazla iki yürüme rotasını göstermeye devam eder; hangi otellerin seçileceğini yürüme mesafesi değil, rapor listesindeki konumları belirler. Yürüme mesafeleri tabloda bilgi olarak kalır.

## Requirements
- Yakın harita, kullanılabilir yürüme geometrisi olan otelleri PDF değerlendirme listesindeki sıraya göre ele alır ve ilk iki oteli gösterir.
- Yürüme mesafesi, yakın harita için otel seçme veya sıralama ölçütü değildir.
- Yürüme geometrisi bulunmayan otel yakın haritada gösterilmez; yürüme metrikleri PDF tablosunda mevcut davranışla gösterilir.
- Genel haritanın tüm otelleri, seçili otelin mevcut ana rotasını, PDF tablo sırasını ve öneri sırasını değiştirmez.
- Yakın harita paneli genel görünümün büyük bölümünü kaplamayacak ölçüde küçültülür; panel başlığı ve PDF lejantı gösterim sırasını doğru anlatır.
- Rota çizgileri, Geoapify'nin döndürdüğü yol geometrisini korur; çizgiler taban haritadaki yollarla hizalı gösterilir ve kopuk geometri parçaları arasında yapay bağlantı çizilmez.
- Rota çizgileri mevcut görünüme göre daha ince çizilir; etkinlik alanı, otel işaretleri ve rota ayrımı okunabilir kalır.

## Constraints & out of scope
- En fazla iki otel ve mevcut yakın harita boyut/sağlayıcı yedek davranışları korunur.
- Geoapify yürüme sorgusu uygunluk sınırı, yürüme metrikleri ve JSON davranışı değişmez.
- Yürüme mesafelerini tabloda sıralama veya öneri kararına katma kapsam dışıdır.

## Acceptance criteria
- [ ] AC-1 — Yakın haritada kullanılabilir yürüme geometrisi olan oteller arasından PDF listesindeki ilk en fazla iki otel, yürüme mesafelerinden bağımsız olarak gösterilir.
- [ ] AC-2 — İlk iki uygun otelin listede daha üstte olanı daha kısa yürüyüşe sahip olmasa bile önce gelir; geometrisi olmayan otel atlanır ve sonraki uygun otel varsa boşalan yer ona verilir.
- [ ] AC-3 — Değişiklik yürüme metriklerini PDF tablosunda, genel haritadaki tüm otelleri ve seçili ana rotayı, JSON sırasını veya öneri seçimini değiştirmez. Uygun geometri yoksa yakın görünüm oluşturulmaz.
- [ ] AC-4 — Replaced by `specs/active/0009-profesyonel-pdf-raporu.md` AC-2: the former 30%-size floating inset is superseded by separately titled, non-overlapping side-by-side panels.
- [x] AC-5 — Geoapify `MultiLineString` yol parçaları ayrı ayrı korunur ve harita sağlayıcısının kendi taban haritasıyla aynı projeksiyonda çizilir; parçalar arasına rota geometrisinde olmayan birleştirici çizgi eklenmez. Kanıt: `GeoapifyProviderTests.GetRouteAsync_preserves_disconnected_multiline_components_without_joining_them` ve `HostedStaticMapProviderTests.RenderAsync_requests_a_png_base_map_and_adds_the_overlay`.
- [x] AC-6 — Seçili ve diğer rota çizgilerinin görsel kalınlığı azaltılır; renk/desen ayrımı ve rota görünürlüğü korunur. Kanıt: hosted static map testinde API POST gövdesinin casing 5, seçili 4 ve diğer rota 2 genişliklerini doğrulaması.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique
- **Risk:** “liste sırası” yakın harita çizim sırasıyla karıştırılabilir. Önerim: seçim sırasını `RankedHotels`/PDF değerlendirme listesinin sırası olarak tanımlamak; çünkü kullanıcı “listedeki sırayla” dedi ve tabloda bu sıra zaten görünür.
- **Risk:** Uygun yürüme geometrisi olmayan yüksek sıradaki otel, görünümde boş bir yer bırakabilir. Önerim: geometri olmayanları atlayıp sonraki geometrili oteli dahil etmek; olmayan güzergâh çizilmediğinden bu, yanıltıcı çizgi üretmez ve iki rota kapasitesini korur.
