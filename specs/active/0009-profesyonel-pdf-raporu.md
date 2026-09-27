# Spec 0009 — Profesyonel PDF rapor düzeni

- Status: Draft
- Mode: lite
- Plan: `specs/plans/0009-plan.md`
- Supersedes: the 30%-size floating detail inset requirement in AC-4 of `specs/active/0008-yakin-harita-liste-sirasi.md`

## Intent
Rezervasyon ekibi, PDF'yi ilk bakışta tarayabilmeli; önerilen oteli, tüm otelleri ve harita işaretlerini birbirine karıştırmadan anlayabilmelidir. Belge düzenli, dengeli ve A4 ekranda/çıktıda okunaklı görünmelidir. Harita genel görünümü ve yakın planı ayrı alanlarda, başlıkları ve kısa bir görsel açıklamasıyla sunmalıdır. Otel sırası, rota seçimi, fiyatlar ve mevcut rapor verileri aynı kalır.

## Requirements
- Önerilen otel, raporun en belirgin bilgi kartında sunulur; otel adı ve karar için önemli mesafe, süre ve fiyat kolayca taranır.
- Genel harita ve yakın plan ayrı panellerde gösterilir; yakın plan genel haritanın üstünü kapatmaz.
- Her harita panelinin başlığı ne gösterdiğini doğru açıklar. Yakın plan, listedeki ilk en fazla iki uygun otel ve yürüme rotalarını gösterir.
- Harita işaretleri ve PDF tablo sıraları eşleşir. Seçili otel ile diğer oteller renk ve çizgi örüntüsüyle ayırt edilir.
- Harita açıklaması kısa ve görsel anahtarla anlaşılır; uzun, sıkışık paragraf haritanın altında kullanılmaz.
- Otel tablosu rahat taranır; yol ölçümleri, yürüme ölçümleri ve gecelik fiyat görsel olarak gruplanır.
- Belgedeki tüm mevcut rapor bilgileri, atıflar, erişilebilir harita yedeği ve sayfa numaraları korunur.

## Constraints & out of scope
- Öneri sırası, otel seçimi, yürüyüş uygunluk kuralı, mesafe/süre/fiyat değerleri ve JSON sözleşmesi değişmez.
- Yeni harita sağlayıcısı veya yeni veri kaynağı eklenmez.
- Marka kılavuzu verilmediği için mevcut kurumsal lacivert/turuncu renk paleti korunur.
- Tasarım A4 dikey PDF ve mevcut Türkçe içerik için yapılır.

## Acceptance criteria
- [ ] AC-1 — PDF'de başlık, etkinlik alanı, önerilen otel, haritalar ve değerlendirme tablosu belirgin bir görsel sırada sunulur; önerilen otel ilk bakışta ayırt edilir.
- [ ] AC-2 — Genel görünüm ve yakın plan ayrı, başlıklı panellerde yer alır; hiçbir harita diğerinin üstünü kapatmaz ve panel başlıkları içerikle eşleşir.
- [ ] AC-3 — Rapor sıra numaraları harita rozetleriyle eşleşir; seçili otel/rota ile diğer oteller/rotalar açıklamadaki renk ve örüntüyle ayırt edilir.
- [ ] AC-4 — Otel tablosu A4 sayfa genişliğinde başlık ve değerleri kırpmadan gösterir; yol, yürüme ve fiyat sütunları kolay taranır.
- [ ] AC-5 — Mevcut rapor verileri, PDF/JSON öneri eşitliği, atıflar, yedek harita ve sayfa numaraları korunur.
- [ ] AC-6 — Üretilen PDF'nin gerçek sayfa görüntüsü incelenir ve metin/harita örtüşmesi, okunamayan başlık veya kesilen içerik bulunmadığı kanıtlanır.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique
- **Risk:** Yan yana harita panelleri genel görünümün yatay alanını daraltabilir. Önerim: birleşik harita genişliğinin %60'ını genel görünüme, %40'ını yakın plana ayırmak; yakın plan dikey boyunu koruduğu için ayrıntı ve rota takibi mevcut küçük iç pencereden daha rahat olur. Harita projeksiyonları her panelin kendi boyutuna göre yeniden sığdırılır.
- **Risk:** Uzun otel adları ve sütun başlıkları tabloyu daraltabilir. Önerim: tablo sütunlarını gruplamak ve otel adı sütununa en büyük esnek payı vermek; tüm fiyat/rota değerleri korunur.
- **Risk:** Görsel kalite yalnızca metin çıkarımıyla doğrulanamaz. Önerim: test kanıtına ek olarak PDF sayfasının render edilmiş görüntüsünü incelemek ve bu kanıtı AC-6'ya bağlamak.
