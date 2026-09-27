# Domain

> Bootstrap ile dolduruldu (2026-09-25). İş, insan ve ajan arasındaki ortak dil. Bir terim burada
> yoksa ajan ona kendi anlamını uydurur.

## Ubiquitous language
| Terim | Anlam | Karıştırılmaması gereken |
|---|---|---|
| EtkinlikAlanı | Etkinliğin yapılacağı yer; adı verilir, konumu geocoding ile bulunur. | Otel |
| AnlaşmalıOtel (ContractedHotel) | Kurumun anlaşmalı olduğu otel; adı ve bir gecelik ücretiyle birlikte gelir. | Etkinlik alanı |
| GecelikÜcret (NightlyPrice) | Bir otelin **bir gecelik** konaklama tutarı ve üç büyük harfli ISO 4217 para birimi kodu. | Rezervasyon toplamı (süre/vergi dahil) |
| Konum (GeoPoint) | WGS84 enlem/boylam çifti. | Adres metni |
| Mesafe/Süre (RouteMetrics) | Routing sağlayıcısının yol mesafesi (metre) ve süresi (saniye). | Düz çizgi (haversine) |
| Öneri (Recommendation) | Sıralanmış otel listesi + seçilen en uygun otel + gerekçe. | Rezervasyon |
| RezervasyonBirimi | Öneriyi tüketen hedef birim/servis. | Öneri servisi |
| Çözümleme (Resolution) | Ad → Konum eşlemesinin başarı/başarısızlık durumu. | — |
| Konum Önbelleği (GeocodeCache) | Normalize edilmiş ad → konum eşlemesi; MongoDB'de kalıcı. | Canlı konum (otorite sağlayıcıdadır) |
| Öneri Belgesi (RecommendationReport) | Önerinin paylaşılabilir PDF çıktısının deterministik modeli. | JSON yanıtı (aynı veriyi taşır, farklı biçim) |
| Coğrafi Görsel (StaticMap) | Etkinlik alanı ve otelleri gerçek lat/lon oranıyla, aralarındaki yol güzergâhını çizgi olarak gösteren şematik görünüm. | Gerçek harita (taban harita/yol adı içermez) |

## Business rules
- **BR-1 — Kısmi başarı:** Bir otelin adı çözümlenemezse istek hata vermez; otel elenir ve yanıttaki
  "çözümlenemeyenler" listesinde döner.
- **BR-2 — Yol mesafesi:** Mesafe/süre routing sağlayıcısından alınır; düz çizgi mesafesi yalnızca
  fallback'dir ve yanıtta bu durum işaretlenir.
- **BR-3 — Determinizm:** Aynı girdi ve aynı sağlayıcı yanıtları için sıralama birebir aynıdır.
- **BR-4 — Önbellek:** Aynı sorgu bağlamı için konum, **MongoDB'deki kalıcı önbellekten** kullanılır; **isabet
  varsa dış sağlayıcıya istek yapılmaz.** Anahtar: sağlayıcı kimliği + kırpılmış, küçük harfe indirgenmiş ve ardışık
  boşlukları tek boşluğa indirgenmiş ad, şehir ve ülke. Sağlayıcı rate limit'ine uyulur
  (sağlayıcı limitleri adapter tarafından uygulanır).
- **BR-7 — Önbellek kaydı:** Her kayıt normalize adı, koordinatı, kaynak sağlayıcıyı ve çözümleme
  zamanını taşır; servis yeniden başlasa da kayıt kalır. Başarısız çözümlemeler kalıcı yazılmaz (G-8).
- **BR-5 — Doğrulama:** Etkinlik alanı adı boş olamaz; otel listesi en az 1, en çok tanımlı üst sınır
  kadar olmalıdır. Her otelin gecelik tutarı negatif olamaz (sıfır = ücretsiz konaklama geçerlidir) ve
  para birimi kodu tam olarak üç büyük ASCII harf olmalıdır.
- **BR-8 — Fiyat taşıma:** Gecelik ücret otelle birlikte taşınır ve başarılı yanıtta aynen geri döner;
  sıralamayı, konum çözümlemesini ve rota hesabını **etkilemez** (R-4). Tutar olduğu gibi taşınır, kur
  dönüşümü yapılmaz ve fiyat MongoDB konum önbelleğine **yazılmaz** (R-6).
- **BR-6 — Sıralama (ADR-0005):** Önce **mesafe türü**: ölçülmüş yol (`Road`) her zaman düz çizgi
  tahmininden (`StraightLine`) önce gelir. Road grubunda süre → mesafe → otel adı (A→Z); StraightLine
  grubunda mesafe → otel adı. Düz çizgi fallback'te süre ölçülmediği için `DurationSeconds = 0`'dır
  ve süre olarak yorumlanmaz.
- **BR-9 — Belge sadakati:** Öneri belgesi, aynı girdiyle üretilen JSON öneriyle **aynı** seçilen oteli
  ve aynı öneri mesafesi/süresi/fiyat değerlerini taşır. PDF, ölçülmüş yol mesafesi 3.000 metrenin
  altındaki oteller için ayrı Geoapify yürüme mesafesi/süresi alabilir; bu ek ölçümler JSON'u veya
  öneri sırasını değiştirmez.
- **BR-12 — PDF tercih seçenekleri:** PDF, JSON sıralamasını değiştirmeden bütçe öncelikli, ulaşım öncelikli
  ve dengeli alternatifleri ayrıca gösterir. Bütçe en düşük aynı para birimli tutarı, ulaşım ölçülmüş
  yol süresini (eşitlikte yol mesafesini), denge ise en düşük tutarın en fazla %10 üzerindeki seçenekler
  içinden en iyi ulaşım süresini seçer. Fiyat para birimleri karışık ise bütçe/denge karşılaştırması
  yapılmaz. İstekle gelen fiyat, doğrulanmış rezervasyon teklifi olarak sunulmaz. Yürüme ölçüleri mevcutsa
  alternatif ulaşım bilgisi olarak açıklanır; yol ve yürüyüş modları birbirinin yerine sıralanmaz.
- **BR-10 — Zarif bozulma (belge):** Coğrafi görsel üretilemezse belge **görselsiz** üretilir ve bu
  durum belgede açıkça yazılır; görsel eksikliği isteği düşürmez. Doğrulama/çözümleme/sağlayıcı
  hatalarında ise belge hiç üretilmez (JSON uç noktasıyla aynı hata sonucu döner).
- **BR-11 — Güzergâh geometrisi:** Belgedeki coğrafi görsel, etkinlik alanı ile otel arasındaki **yol
  güzergâhını** routing sağlayıcısının döndürdüğü geometriyle (GeoJSON LineString/MultiLineString) çizgi olarak gösterir.
  Rota sorguları otel konumunu kaynak, etkinlik alanı konumunu hedef alır.
  Çok parçalı geometri bileşenleri ayrı tutulur; aralarına bağlantı çizgisi eklenmez. Hosted Geoapify haritası
  rotayı taban haritayla aynı projeksiyonda API tarafında çizer; şematik yedek rota geometrisini yerelde çizer.
  Geometri yoksa veya alınamazsa (düz çizgi fallback) düz çizgi gösterilir. Mevcut rota geometrisi için
  ikinci çağrı yapılmaz; PDF'ye özel yürüme geometrisi BR-9 kapsamındaki ek yürüyüş çağrısından gelir ve
  JSON yanıtına eklenmez.

## Key domain invariants
- PDF yakın haritası, kullanılabilir yürüme geometrisi olan oteller arasından değerlendirme listesindeki ilk en fazla ikisini gösterir; yürüme mesafesi seçim sırasını belirlemez. Geometrisi olmayan otel atlanır.
- Koordinatı çözümlenmemiş hiçbir otel öneri listesine giremez.
- Gecelik tutar negatif olamaz; para birimi kodu üç büyük ASCII harftir. Fiyat konum önbelleğine yazılmaz.
- Etkinlik alanı konumu çözümlenemezse sonuç üretilmez; açık hata döner. Hiçbir otel çözümlenemezse
  de öneri üretilmez (AC-17).
- Mesafe ve süre negatif olamaz.
- Öneri listesi her zaman deterministik sırada döner.
- Önbellekten dönen konum, kaynağı (sağlayıcı + zaman) bilinmeden kullanılmaz.
- Ölçülmemiş süre için **tahmin üretilmez**; düz çizgi fallback'te süre 0'dır ve sıralamada süre
  ölçütü olarak kullanılmaz.
- Belge yalnızca başarılı (en az bir otel çözümlenmiş) öneri için üretilir; belge içeriği JSON
  sonucuyla birebir tutarlıdır.
- Yol güzergâhı geometrisi yalnızca sağlayıcı ölçümüyle gelir; **tahmin edilmez**. Düz çizgi fallback'te
  ve geometri alınamadığında geometri boştur.
- PDF haritası Geoapify taban haritası üzerinde Geoapify tarafından hizalanmış rota çizgileri ve yerel etkinlik/otel işaretleri gösterir;
  harita alınamazsa rapor şematik görselle üretilir ve harita atfı PDF'de yer alır.
## Recommendation request context (0007)
- İstek personel sicil/ad/soyadını ve etkinlik adı/şehri/ülkesini taşır; JSON ile PDF endpoint'i aynı
  request DTO ve aynı iş doğrulamasını kullanır.
- Etkinlik şehri/ülkesi venue ve otel geocoding'ine aktarılır; otellerin farklı şehirde bulunması V1'de
  desteklenmez (otel listesi etkinlik şehrine göre aranır).
- Personel kimliği önerinin sıralama/konum hesabına katılmaz. PDF'de rapor sahibini göstermek için
  kullanılır; JSON yanıtı ve kalıcı depolama kapsamı dışındadır.
- Geocoding cache eşitliği provider + normalize ad + normalize şehir + normalize ülke üzerinden
  sağlanır; farklı şehir/ülkelerdeki aynı ad farklı konumlardır.
