# ADR 0008 — Coğrafi görselin arka planı: barındırılan ücretsiz statik harita API'si

- Status: Accepted
- Date: 2026-09-26

## Context
ADR-0006 v1'de coğrafi görseli dış ağ kullanmayan **şematik** üreticiyle sınırlıyordu (taban harita
yoktu) ve gerçek statik harita sağlayıcısını ileri bir adıma bırakıyordu. ADR-0007 ile görsel gerçek
yol güzergâhını gösterse de arka plan düz renkli kalmıştı. Rezervasyon/seyahat birimi görseli **gerçek
harita** üzerinde okunabilir istedi. Kullanıcı bir karo (tile) servisini **self-host edemeyeceğini**
belirtti; bu yüzden **barındırılan, ücretsiz ve ticari kullanıma uygun** bir sağlayıcı gerekiyor.
Ayrıca etkinlik alanı ile seçilen otelin ayırt edilebilir olması (vurgu) ve zorunlu **atıf** kısıtı var.

## Decision
Belgedeki coğrafi görselin arka planı, yapılandırılmış **barındırılan statik harita API'sinden**
(gönderim: **Geoapify** ücretsiz planı) alınan gerçek harita görüntüsüdür. Harita görünümü `center`+`zoom`
ile sabitlenir; etkinlik alanı ve otel işaretleri yerelde SkiaSharp ile çizilir, yol güzergâhları statik
harita isteğinde taban haritayla birlikte çizilir. `IStaticMapProvider` arayüzü korunur: barındırılan
sağlayıcı **birincil**, şematik sağlayıcı **yedek** olur (`FallbackStaticMapProvider`); sağlayıcı
erişilemezse belge şematik görselle üretilir (R-15). Sağlayıcı anahtarı **sırdır** (`user-secrets`/
ortam değişkeni); zorunlu **atıf** korunur. Harita çıktısı 2x piksel yoğunluğunda alınır; OSM ve
OpenMapTiles atıfları görselde, Geoapify kredisi PDF metninde bir kez gösterilir. İşaretler sıralı otel
tablosuna bağlanan numaralı rozetlerdir; etkinlik alanı `E` ile işaretlenir.

### Uygulama güncellemesi (2026-09-27)

Rota çizgileri Geoapify Static Maps POST gövdesindeki `geometries` alanında taban haritayla birlikte
çizilir. Böylece rota ve taban harita aynı projeksiyonu kullanır. Routing yanıtındaki MultiLineString
bileşenleri ayrı polyline olarak korunur; aralarına bağlantı çizgisi eklenmez. Etkinlik ve otel işaretleri
SkiaSharp ile yerel olarak eklenmeye devam eder. Şematik yedek rotaları yerelde segment segment çizer.

## Consequences
**Kazanç:** Görsel gerçek harita üzerinde okunur; etkinlik/seçilen otel vurgusu yerelde tam kontrolle
yapılır; rotalar taban harita ile aynı projeksiyonda çizilir; sağlayıcı arızasında belge yine üretilir
(zarif bozulma); JSON/PDF sözleşmeleri ve şematik yedek korunur. **Maliyet:** Dış ağ bağımlılığı ve ücretsiz kota/rate limit; anahtar yönetimi (sır) ve
atıf uyumu; kota aşılırsa
görsel şematiğe düşer; sağlayıcı değişirse stil/atıf yapılandırması güncellenmelidir.

## Alternatives considered
- **Şematik taban (ADR-0006 durumu):** ağsız/deterministik ve ücretsiz; ama kullanıcı gerçek harita
  istedi → reddedildi (yedek olarak korunur).
- **Self-hosted karo servisi (OpenMapTiles/Protomaps vb.):** en yüksek kontrol ve limitsiz; ama
  kullanıcı işletemiyor, operasyon/önbellek yükü var → reddedildi.
- **LocationIQ ücretsiz planı:** ticari kullanıma açık ama belirgin geri-link zorunlu; atıf yükü daha
  ağır → Geoapify öne geçti.
- **MapTiler / Stadia / Mapbox / Thunderforest ücretsiz katmanları:** ticari/üretim kullanımına uygun
  değil → reddedildi.
- **OSMF public tile sunucuları:** üretim kullanımı politika dışı → reddedildi.
- **OpenFreeMap:** ücretsiz + ticari, ama yalnızca **vektör**; hazır statik PNG yok, vektör render
  motoru gerekir → v1 dışı.
- **Sağlayıcının işaret/rota parametreleriyle çizim:** daha az yerel kod; ama etkinlik/seçilen otel
  vurgusu ve güzergâh çizimi sağlayıcıya bağlı ve kısıtlı → yerel çizim seçildi.
- **Karo indirip mozaikleme + yerel render:** tam kontrol; ama çok istek/kota ve politika riski →
  statik harita uç noktası seçildi.

## Revisit triggers
- Ücretsiz kota/rate limit üretimde yetersiz kalırsa (→ ücretli plan, önbellek veya farklı sağlayıcı).
- Geoapify lisans/atıf koşulları değişirse (→ yeni sağlayıcı değerlendirmesi).
- Yerel çizim ile tile hizalanmasında görünür sapma gözlenirse (→ `area=rect` veya farklı projeksiyon).
- Vektör/harita kalitesi veya Türkçe yer adı gereksinimi doğarsa (→ vektör render seçeneği).
