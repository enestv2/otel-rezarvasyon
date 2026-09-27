# Spec 0006 — Önerinin coğrafi görselli PDF belgesi olarak sunulması

- Status: Complete
- Mode: lite
- Plan: `specs/plans/0006-plan.md`; walking-route delta: `specs/plans/0006-r12-walking-distance-detail.md`

## Intent
Rezervasyon/seyahat birimi, mevcut JSON önerisine ek olarak **aynı girdiyle** (etkinlik alanı adı +
fiyatlı anlaşmalı otel listesi) tek bir paylaşılabilir **PDF belgesi** almak ister. Belge, seyahat
birimi için seçilen en uygun oteli öne çıkarır; etkinlik alanı ile oteller arasındaki mesafe/süre
bilgisini listeler ve bu konumları etkinlik alanına göre **küçük bir coğrafi görsel** üzerinde
gösterir. Amaç, önerinin insanlara iletilebilir/arşivlenebilir bir çıktı olarak da verilmesidir.
Başarı, aynı girdiyle üretilen JSON öneriyle **birebir tutarlı** içerikte, indirilebilir bir belge
üretilmesidir. Belgenin kalıcı saklanması, e-posta/başka kanaldan teslimi veya resmî/imzalı belge
niteliği bu işin kapsamı dışındadır.

## Requirements
- **R-1** Aynı öneri girdisiyle (etkinlik alanı adı + fiyatlı otel listesi) sistem indirilebilir bir
  PDF belgesi üretebilir.
- **R-2** Belge, seçilen (en uygun) oteli adı, mesafesi, süresi, mesafe türü ve gecelik fiyatıyla
  öne çıkarır.
- **R-3** Belge, çözümlenen tüm otelleri sıralı biçimde ad, mesafe, süre, mesafe türü ve gecelik
  fiyatla listeler.
- **R-4** Belge, etkinlik alanı ve otel konumlarını, etkinlik alanına göre küçük bir coğrafi görsel
  üzerinde gösterir.
- **R-5** Belge içeriği aynı girdiyle üretilen JSON öneriyle tutarlıdır: seçilen otel ve tüm
  metrik/fiyat değerleri JSON yanıtındakilerle aynıdır.
- **R-6** Çözümlenemeyen oteller belgede ayrıca listelenir; en az bir otel çözümlendiyse belge yine
  üretilir (kısmi başarı, BR-1).
- **R-7** Doğrulama hatası, çözümlenemeyen etkinlik alanı, hiç otel çözümlenememesi veya sağlayıcı
  erişilemezliğinde belge üretilmez; JSON öneri davranışıyla **aynı** hata sonucu döner.
- **R-8** Coğrafi görsel üretilemezse belge yine üretilir; görselin eksik olduğu belgede açıkça
  belirtilir (zarif bozulma).
- **R-9** Belge, girdideki etkinlik alanı ve otel adlarını Türkçe karakterler dahil bozulmadan
  gösterir.
- **R-10** Belge üretimi kalıcı depolama veya belge önbelleği gerektirmez; servis durumsuz kalır.
- **R-11** Belgedeki coğrafi görsel, etkinlik alanı ile her çözümlenen otel arasındaki **yol güzergâhını**
  (routing sağlayıcısının ölçtüğü yol) çizgi olarak gösterir; yalnızca düz çizgi tahmini olan oteller için
  düz çizgi gösterilir.
- **R-12** Belge, mevcut öneri metriklerinin güzergâh geometrisi için ek rota/konum çağrısı yapmaz; bu geometri JSON uç noktasıyla aynı ölçümden taşınır. AC-25 kapsamındaki yürüme rotası çağrıları yalnızca PDF oluşturulurken yapılır ve JSON öneri sonucunu değiştirmez.
- **R-13** Belgedeki coğrafi görselin arka planı **gerçek harita** görüntüsüdür; yapılandırılmış, **ücretsiz
  ve ticari kullanıma uygun** bir statik harita sağlayıcısından alınır ve zorunlu **atıf** belgede yer alır.
- **R-14** Görselde **etkinlik alanı** belirgin bir işaretle öne çıkar ve **seçilen otel** diğer otellerden
  açıkça ayırt edilir (daha büyük/vurgulu işaret ve kalın güzergâh); bu ayrımın anlamı belgede
  (lejant/altyazı) yazılır.
- **R-15** Statik harita sağlayıcısına erişilemezse belge **şematik** görsel ile yine üretilir (zarif
  bozulma, BR-10).
- **R-16** Gerçek harita ve şematik yedek dahil her harita, etkinlik alanını otellerden ayırt edilebilir
  ve ölçülü boyutta bir işaretle gösterir; çözümlenen her oteli belgedeki sırasıyla eşleşen bir numarayla
  gösterir. Otel işaretleri ve güzergâhları kendi aralarında renk ile ayırt edilir. Seçilen otel ve onun
  güzergâhı belirgin kalır. Yakın konumlu işaretler okunabilirliği koruyacak şekilde çizilir; coğrafi
  konumları yanıltıcı biçimde değiştirilmez.
- **R-17** Harita, PDF'de gösterildiği boyutta net okunur. Sağlayıcı ve harita verisi atıfları görünür
  kalır; aynı sağlayıcı atfı harita görseli ve açıklama satırında gereksiz yere yinelenmez.
- **R-18** Otel numara rozetleri yalnızca gerekli çakışma azaltımı kadar gerçek konumlarından ayrılır;
  bağlantı çizgisi gerçek noktayı göstermeye devam eder. En fazla 20 otel için renkler ve rota stilleri
  birbirinden hızlı ayırt edilir. Rozetler/etkinlik işareti harita ayrıntılarını örtmeyecek ölçüde kompakt
  kalır; etkinlik `E` işareti şekil ve boyutla otellerden ayrılır. Lejant rozet-rota renk eşleşmesini açıklar.
- **R-19** Coğrafi görsel, aynı PDF görseli içinde tüm otel/etkinlik alanını gösteren bir genel görünüm ve
  etkinlik çevresindeki yakın otelleri daha büyük ölçekte gösteren bir detay görünümü sunar. Genel görünüm
  kapsamı ve seçili otelin tam rotasını korur; yakın otel kümesinin rotaları detay görünümünde gösterilir.
  İki görünümde numara/renk eşleşmesi aynıdır. Öneri turuncusu normal otel renklerinden açıkça ayrıdır ve
  başka otel rozetlerinde kullanılmaz. PDF lejantı genel/detay görünümünü ve turuncu seçili rota ile diğer
  rotalardaki çizgi deseni kodlamasını açıklar.

- **R-21** Mevcut ölçülmüş yol mesafesi 3.000 metrenin altında olan oteller için PDF üretimi Geoapify yürüme rotası mesafesi ve süresini alır; bunları mevcut öneri mesafesi/süresinden ayrı gösterir. Yürüme verileri JSON yanıtını, öneri sırasını veya seçilen oteli değiştirmez.
- **R-22** Detay görünümü, yürüme rotası alınabilen uygun oteller arasından yürüyüş mesafesi en kısa en fazla iki oteli ve bu yürüyüş rotalarının geometrisini gösterir; eşit mesafede öneri listesindeki sıra belirleyicidir. Genel görünüm mevcut tüm otelleri ve seçili otelin ölçülmüş tam rotasını korur.

## Constraints & out of scope
- **R-20** Yürüme metrikleri alınamayan uygun otel için bu değerler “Alınamadı” olarak gösterilir. Yürüme geometrisi yoksa metrikler tabloda korunur fakat detay haritasında tahmini düz çizgi çizilmez; detay yalnızca geometrisi alınan rotaları içerir. Hiçbir geometri alınamazsa genel görünüm tek başına kalır.
- Belge şablonu yalnızca **Türkçe**'dir; çok dilli şablon v1 dışıdır.
- Coğrafi görsel **küçük ve tekil**dir (v1): etkileşim, yakınlaştırma, döşeme (tile) taraması yok.
  Statik harita sağlayıcısı **ücretsiz ve ticari kullanıma uygun** olmalıdır (ör. Geoapify ücretsiz
  planı); zorunlu **atıf** belgeye yazılır. Görsel, sağlayıcıdan alınan gerçek harita arka planı (tile)
  üzerine çizilir; işaretler ve yol güzergâhı yerelde işlenir (sokak adı etiketi, adım adım tarif yok).
- Karo servisi **işletilmez/barındırılmaz**; yalnızca barındırılan sağlayıcı API'si kullanılır.
- Belge boyutu ve üretim süresi için üst sınır uygulanır; kesin değerler plan'da sabitlenir.
- Belge arşivleme/saklama, e-posta veya başka kanaldan teslim, imzalama, marka/tema özelleştirmesi
  kapsam dışıdır.
- Konaklama süresi toplamı, vergi/indirim hesabı yoktur; fiyat **tek gecelik** olarak taşınır (BR-8).
- Harita görselinin yüksek çözünürlüklü/vektörel olması kapsam dışıdır.
- Harita etkileşimi ve otel adlarının harita üstünde yazılması kapsam dışıdır; sıra numaraları PDF'deki
  otel listesine bağlanır.
- Adım adım yol tarifi, uydu görüntüsü, etkileşim, karo servisi işletimi/önbelleği ve sağlayıcı hesap
  yönetimi kapsam dışıdır.

## Acceptance criteria
- [x] AC-1 — Aynı girdiyle belge isteği, PDF içerik türünde indirilebilir bir belge döner.
- [x] AC-2 — Belge, seçilen otelin adını, mesafesini, süresini, mesafe türünü ve fiyatını içerir.
- [x] AC-3 — Belge, çözümlenen tüm otelleri sıralı olarak mesafe/süre/tür/fiyat ile listeler.
- [x] AC-4 — Belge, etkinlik alanı ve otel konumlarını gösteren bir coğrafi görsel içerir.
- [x] AC-5 — Belgedeki seçilen otel ve değerleri, aynı girdiyle alınan JSON yanıtındaki seçilen
  otelle birebir aynıdır.
- [x] AC-6 — Çözümlenemeyen oteller belgede listelenir ve belge yine üretilir (başarı durumu).
- [x] AC-7 — Doğrulama/etkinlik alanı çözümlenemedi/hiç otel çözümlenemedi/sağlayıcı erişilemedi
  durumlarında belge üretilmez; JSON ile aynı durum kodu ve gövdesi döner.
- [x] AC-8 — Coğrafi görsel üretilemediğinde belge metin içerikle üretilir ve görselin eksik olduğunu
  belirtir.
- [x] AC-9 — Türkçe karakterli adlar (ör. "Şişli", "İstanbul", "Ğ") belgede bozulmadan görünür.
- [x] AC-10 — Kimlik doğrulaması olmayan veya yanlış anahtarlı belge isteği reddedilir ve dış
  sağlayıcı çağrılmaz.
- [x] AC-11 — Belge üretimi konum önbelleğine belge/rapor verisi yazmaz.
- [x] AC-12 — Aynı girdi ve aynı sağlayıcı yanıtları için belgedeki seçim ve metrikler deterministiktir.
- [x] AC-13 — Coğrafi görsel, ölçülmüş yol (`Road`) otelleri için düz çizgi değil, sağlayıcının
  döndürdüğü **yol güzergâhı** boyunca çizgi gösterir.
- [x] AC-14 — Düz çizgi (`StraightLine`) fallback otelleri için görselde düz çizgi gösterilir; bu, JSON
  `distanceKind` ile tutarlıdır.
- [x] AC-15 — Belge üretimi ek rota çağrısı yapmaz (çağrı sayısı JSON ile aynı) ve geometri alınamazsa
  belge yine üretilir.
- [x] AC-16 — Coğrafi görselin arka planı gerçek harita görüntüsüdür (düz şematik zemin değil) ve zorunlu
  sağlayıcı atfını içerir.
- [x] AC-17 — Etkinlik alanı ile seçilen otel görselde ayırt edilebilir (boyut/şekil/vurgu) ve belgedeki
  lejant/altyazı bu ayrımı açıklar.
- [x] AC-18 — Statik harita sağlayıcısı erişilemez/hatalı olduğunda belge yine üretilir (şematik yedek)
  ve istek başarısız olmaz.
- [x] AC-19 — Harita anahtarı sır olarak yönetilir; belge/JSON çıktısına veya hata mesajına sızmaz.
- [x] AC-20 — Gerçek ve şematik her haritada etkinlik alanı `E` ile ölçülü boyutta, oteller PDF listesindeki
  sıralarıyla numaralandırılarak gösterilir; otel işaretleri/rotaları birbirinden renk ile ayrılır, seçilen
  otel/güzergâh belirgin kalır ve yakın işaretler okunabilirliğini korur.
- [x] AC-21 — Harita PDF'de net görünür; Geoapify ve harita verisi atıfları okunur ve Geoapify atfı
  harita üzerinde ve altındaki açıklamada yinelenmez.
- [x] AC-22 — Rozetler çakışma yokken gerçek noktanın yakınında kalır; çakışan/çok yakın rozetler yalnızca
  gerektiği kadar kaydırılıp gerçek noktaya lider çizgisiyle bağlanır. En fazla 20 otelin renkleri ve rota
  stilleri birbirinden ayrılır; turuncu seçim ve mavi `E` korunur. Rozetler ve etkinlik işareti kompakt
  boyut sınırlarında kalır; PDF lejantı aynı renkli otel-rota eşleşmesini açıklar.
- [x] AC-23 — Tek bir PDF harita görselinde tüm otel/etkinlik işaretleri ve seçili tam rota genel görünümde;
  etkinlik çevresindeki yakın oteller ve yerel rotaları daha büyük ölçekte detay görünümünde gösterilir.
  Numara/renk eşleşmesi iki görünümde aynıdır, etkinlik ve otel işaretleri çakışmadan okunur. Normal otel
  paletinde öneri turuncusuna/kırmızıya yakın renk yoktur. Lejant genel ve detay görünümünü, turuncu seçili
  rotayı ve diğer rotaların çizgi deseni kodlamasını açıklar. Detay harita sağlayıcısı erişilemezse belge
  genel görünümle üretilebilir; sağlayıcı tümden erişilemezse şematik yedek aynı iki görünümü üretir.

- [x] AC-25 — Mevcut ölçülmüş yol mesafesi 3.000 metrenin altında olan oteller için PDF üretiminde Geoapify yürüme mesafesi/süresi alınır ve ayrı gösterilir; diğer oteller için yürüme sorgusu yapılmaz. Yürüme çağrıları JSON uç noktasını ve öneri sırasını değiştirmez. Kanıt: `RecommendationReportServiceTests`, `RecommendationsPdfEndpointTests`.
- [x] AC-26 — Detay haritası, yürüme rotası başarıyla alınan uygun otellerden yürüme mesafesi en kısa en fazla ikisini ve yürüme güzergâhlarını gösterir; eşitlikte öneri sırası korunur. Genel harita tüm otelleri ve seçili otelin mevcut tam rotasını korur. Yürüme metriği hatasında değer “Alınamadı” olur; geometri yoksa yürüme değerleri tabloda kalır ama detayda tahmini düz çizgi gösterilmez. Hiçbir yürüme geometrisi yoksa belge genel harita ile üretilir. Kanıt: `SchematicStaticMapProviderTests`, `HostedStaticMapProviderTests`, `RecommendationsPdfEndpointTests`.

## Definition of Done
- [x] AC-24 — (R-11 ile teslim edilen eski davranış; bu spec değişikliğiyle AC-26 tarafından geçersiz kılındı.) Detay görünümünde kuş uçuşu mesafesine göre en yakın en fazla iki otel bulunur; eşit mesafede
  girdi sırası korunur. Bu sınır genel görünümün tüm otelleri göstermesini etkilemez.
- [x] Every acceptance criterion mapped to proof (test or reproducible observation); see delta plan criterion map.
- [x] `scripts/check` green: 2026-09-26, build 0 warnings/errors; 222 passed, 4 skipped, 0 failed.
- [x] Independent review done; no findings (2026-09-26).
- [x] Docs / ADRs updated if behavior or architecture changed: `docs/domain.md`, `docs/architecture.md`.
- [x] Spec moved to `specs/done/` (it becomes immutable there).

## Self-critique (hostile reader) — onaya açık
- **G-1 — Coğrafi görselin kaynağı belirsiz.** Belge bir "geo görünü" içermeli; ama bu görüntünün
  nereden geldiği requirements'a yazılamaz (teknik çözüm). İki yol: (a) dış bir statik harita
  sağlayıcısı (anahtar/politika/atıf/rate limit, CI'da ağ bağımlılığı), (b) dış bağımlılıksız
  **şematik** görsel (gerçek lat/lon ölçekli, etkinlik + otel işaretleri). **Önerim: (b)**, sağlayıcı
  arayüzü (`IStaticMapProvider`) arkasında; determinist, ağsız test edilebilir ve politika riski yok.
  (a) seçilirse atıf zorunlu ve sağlayıcı sözleşmesi/anahtar yönetimi gerekir.
- **G-2 — PDF üretimi yeni bir bağımlılık gerektirir; lisans onayı gerekir** (docs/security.md).
  **Önerim: QuestPDF** (hızlı yerleşim, Türkçe/font ve tablo desteği iyi). Lisansı topluluk
  eşiğine bağlıdır; bu eşik kurum için uygun değilse **MIT lisanslı PDFsharp/MigraDoc** alternatiftir
  (daha fazla el emeği + gömülü font dosyası). Karar sizde.
- **G-3 — Belge uç noktasının şekli.** **Önerim: mevcut JSON uç noktasından ayrı bir belge uç noktası
  (aynı istek gövdesi)**; böylece mevcut istemciler kırılmaz ve OpenAPI net kalır. Alternatif: aynı
  uçta `Accept: application/pdf` içerik anlaşması — mevcut sözleşmeyi karmaşıklaştırır.
- **G-4 — Hata gövdesi biçimi.** **Önerim:** başarıda PDF; hata durumlarında JSON `ProblemDetails`
  (JSON uç noktasıyla aynı kod/gövde). Böylece tek hata deseni korunur.
- **G-5 — Görsel üretilemezse ne olur?** **Önerim:** belge yine üretilir ve "coğrafi görsel
  üretilemedi" notu düşer (R-8); 502 ile tümden reddetmek BR-1'in kısmi başarı ruhuna aykırı olurdu.
- **G-6 — Belgede kaç otel yer alır?** **Önerim:** çözümlenen tüm oteller (girdi zaten en fazla 20).
  Sıralama JSON ile aynıdır; seçilen otel işaretlenir.
- **G-7 — AC-4 nasıl kanıtlanır?** **Önerim:** görselin varlığı rapor modelinde birim testle,
  PDF'te görsel nesnesi (`/Subtype /Image`) ve artan boyutla entegrasyon testinde doğrulanır; PDF
  üretim zamanı içerdiği için bayt eşitliği **kullanılmaz** (determinizm metin/seçim üzerinden).

- **G-8 — Görseldeki bağlantı düz çizgi mi, gerçek yol güzergâhı mı?** Şematik görünüm v1'de düz çizgi
  çiziyordu. Kullanıcı, "harita üzerinde nasıl gidileceğinin yol çizimi" olarak **gerçek yol güzergâhını**
  istedi (karar: 2026-09-26). **Karar/öneri: (b) gerçek geometri.** Gerekçe: güzergâh anlam taşır ve
  routing sağlayıcısı ölçümü zaten yapıyor. Etki: `RouteMetrics`'e (toplu/opsiyonel) geometri eklenir,
  OSRM `geometries=geojson&overview=full` ister, görsel sınırları güzergâhı kapsayacak şekilde genişler ve
  nokta sayısı sınırlanır. Düz çizgi yalnızca `StraightLine` fallback ve geometri yokluğunda kalır
  (R-11/R-12). JSON sözleşmesi değişmez.
- **G-9 — Görselin arka planı nasıl gelir?** Kullanıcı gerçek harita arka planı istedi ve **yerel servis
  işletemeyeceğini** belirtti. Doğrulanan seçenekler: Geoapify ücretsiz planı (3.000 kredi/gün, kredi
  kartı yok, "free plan in commercial projects? Yes ... including in production", atıf zorunlu);
  LocationIQ ücretsiz planı (ticari, ama belirgin geri-link); MapTiler/Stadia/Mapbox/Thunderforest
  ücretsiz katmanları (ticari değil); OSMF public tile (üretim için politika dışı); OpenFreeMap
  (ücretsiz+ticari ama yalnızca vektör, hazır PNG yok). **Karar: (b) barındırılan ücretsiz statik harita
  API'si (varsayılan Geoapify), anahtar sır olarak.** Alternatif: self-hosted karo servisi (kullanıcı
  işletemiyor → reddedildi).
## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise | |
| Regressions introduced | |
| Bugs escaped to production | |
