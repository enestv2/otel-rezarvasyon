# ADR 0006 — Öneri raporu: PDF üretimi ve coğrafi görsel kaynağı

- Status: Accepted
- Date: 2026-09-26

## Context
Rezervasyon birimi, JSON öneriye ek olarak aynı girdiyle paylaşılabilir bir belge (PDF) istiyor.
Belge, seçilen oteli ve etkinlik–otel konum ilişkisini küçük bir coğrafi görselle göstermeli. Bu iki
yeni yetenek (belge üretimi ve görsel üretimi) bugüne kadar çekirdekte yoktu; mimari "dış dünyaya
yalnızca adaptörden çıkılır" ve "yeni bağımlılık = insan onayı + lisans kontrolü" kurallarını uygular.
Harita için anahtar/politika/atıf yükü olan dış statik harita sağlayıcıları mümkündür; PDF için
ticari/AGPL ve MIT lisanslı seçenekler vardır.

## Decision
Rapor içeriği çekirdekte (Application) **deterministik bir rapor modeline** dönüştürülür; belgeye
çevirme `IRecommendationReportRenderer` arayüzü arkasında **Infrastructure**'da **QuestPDF** ile
yapılır. Coğrafi görsel `IStaticMapProvider` arayüzü arkasındadır; ilk uygulama **dış ağ kullanmayan
şematik** üreticidir (gerçek lat/lon ölçekli; QuestPDF'in getirdiği SkiaSharp ile çizim). Belge,
mevcut JSON uç noktasından **ayrı** bir uç noktayla, aynı istek gövdesiyle sunulur.

## Consequences
**Kazanç:** Belge/görsel çekirdeğe sızmaz (Domain/Application PDF veya HTTP tipi bilmez); testler
ağsız ve deterministiktir; harita sağlayıcısı sonradan gerçek statik harita ile değiştirilebilir;
mevcut istemciler kırılmaz. **Maliyet:** İki yeni paket (QuestPDF + SkiaSharp) ve bakım/lisans
takibi; QuestPDF topluluk lisansı gelir eşiğine bağlıdır; şematik görsel gerçek harita değildir
(yol/yer adı yok) ve PDF görsel kanıtı sınırlıdır.

## Alternatives considered
- **Gerçek statik harita sağlayıcısı (ilk uygulama):** daha tanıdık görsel; ama anahtar/politika/atıf
  ve dış ağ bağımlılığı, testlerde kırılganlık → şematik seçildi, gerçek sağlayıcı arayüz arkasında
  bırakıldı.
- **PDFsharp/MigraDoc (MIT):** lisans riski yok; ama yerleşim/tablo/font işini elle yapmak gerekir ve
  gömülü font dosyası ekler → QuestPDF seçildi; MIT zorunlu olursa geri dönülebilir.
- **iText:** AGPL/ticari lisans çekirdek dağıtımına uygun değil → reddedildi.
- **Aynı uçta `Accept: application/pdf` içerik anlaşması:** mevcut sözleşmeyi karmaşıklaştırır →
  reddedildi.

## Revisit triggers
- Coğrafi görselin arka planı ve sağlayıcısı ADR-0008 ile güncellendi; bu ADR'nin şematik görsel
  kararı artık yalnızca yedek sağlayıcı için geçerlidir.
- QuestPDF lisans eşiği kurum için sorun olursa (→ MIT alternatifine geçiş).
- Gerçek harita görünümü iş gereği zorunlu olursa (→ statik harita adaptörü + atıf/anahtar yönetimi).
- Belge üretimi yük/gecikme darboğazı olursa (→ önbellek/kuyruk; şimdilik durumsuz).
