# Spec 0005 — Otel gecelik ücretinin öneri akışında taşınması

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0005-plan.md`

## Intent
Öneri servisini çağıran birim, her anlaşmalı otel için bir gecelik fiyatı da sağlayabilmelidir. Servis fiyatı ilgili otelle birlikte başarılı yanıtta geri verir; böylece tüketici ve ilerideki veri akışları konum/rota önerisini fiyat bilgisiyle eşleyebilir. Fiyat bu sürümde otel sıralamasını etkilemez ve MongoDB'de saklanmaz.

## Requirements
- **R-1** İstek, etkinlik alanı adı ile her otelin adını, bir gecelik tutarını ve para birimi kodunu taşıyabilir.
- **R-2** Başarılı yanıttaki her sıralanmış otel, istekteki bir gecelik tutar ve para birimi kodunu içerir.
- **R-3** Seçilen otelin yanıtındaki fiyat bilgisi de aynı otel için gönderilen değerlerle aynıdır.
- **R-4** Fiyat öneri sıralamasını, konum çözümlemesini veya rota hesaplamasını etkilemez.
- **R-5** Geçersiz/negatif tutar veya geçersiz para birimi kodu içeren istek doğrulama hatasıyla reddedilir.
- **R-6** Fiyat bilgisi MongoDB konum önbelleğine yazılmaz.

## Constraints & out of scope
- Fiyat, her otel için **bir gecelik** tutardır; tutar ve üç harfli ISO 4217 para birimi kodu ayrı alanlardır.
- Tutar JSON sayısı/decimal olarak taşınır; negatif olamaz. Ücretsiz konaklama için sıfır tutara izin verilir.
- Fiyatlar arasında dönüştürme/kur hesabı yapılmaz; sıralama mevcut mesafe/süre kurallarını kullanmaya devam eder.
- Rezervasyon süresi, vergi/ücret kırılımı, fiyat geçmişi ve kalıcı fiyat saklama kapsam dışıdır.

## Acceptance criteria
- [x] AC-1 — İstek her oteli ad ve `{ amount, currency }` fiyat nesnesiyle kabul eder.
- [x] AC-2 — Başarılı yanıtın `rankedHotels` listesindeki her otel kendi gönderilmiş fiyatını ve para birimini içerir.
- [x] AC-3 — `selectedHotel` fiyatı, seçilen otelin istek fiyatıyla aynıdır.
- [x] AC-4 — Fiyat sıralamayı değiştirmez; aynı konum/rota sonuçlarında fiyatlar değişse de mevcut sıralama aynı kalır.
- [x] AC-5 — Negatif, eksik veya sayısal olmayan tutar ile biçimi üç büyük ASCII harf olmayan para birimi kodu doğrulama hatası döndürür.
- [x] AC-6 — Sıfır fiyat kabul edilir.
- [x] AC-7 — Başarısız konum çözümlemesi/sıralama nedeniyle elenen otellerin mevcut kısmi başarı davranışı korunur.
- [x] AC-8 — Fiyat bilgisi MongoDB konum önbelleği kayıtlarına eklenmez.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique (hostile reader) — onaya açık
- **G-1** `hotelNames: string[]` biçimi artık nesne listesi olur. Önerim: yeni `hotels: [{ name, price: { amount, currency } }]` sözleşmesini zorunlu kılmak; bu uygulama için mevcut kullanımda geriye uyumluluk gereksinimi belirtilmedi. Mevcut dış istemciler varsa uyumluluk ayrıca kararlaştırılmalı.
- **G-2** Para birimi dönüşümü yapılmadığı için farklı para birimli fiyatlar karşılaştırılmaz. Önerim: tutarı olduğu gibi geri taşımak ve sıralamaya katmamak.
- **G-3** Aynı otel birden fazla farklı fiyatla gönderilirse mevcut ilk-görülen tekilleştirme kuralı ilk kaydın fiyatını korur. Önerim: mevcut determinizm kuralını korumak.

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 |
| Fix rounds | 2 |
| Review findings: real / noise | 3 / 0 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 |
