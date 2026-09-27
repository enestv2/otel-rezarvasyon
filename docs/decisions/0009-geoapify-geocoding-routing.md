# ADR 0009 — Geoapify geocoding ve routing geçişi

- Status: Accepted
- Date: 2026-09-26

## Context
Nominatim/OSRM ayrık sağlayıcıları etkinlik şehri/ülkesiyle arama yapmıyor ve farklı işletim/kalite
özelliklerine sahip. Statik PDF haritası zaten Geoapify kullanıyor. Her iki öneri endpoint'inin personel
bilgisi ve etkinlik şehir/ülke bağlamını alması, yer aramasını etkinlik lokasyonuna sabitlemeye yardım eder.

## Decision
- Nötr Domain provider sözleşmeleri korunur; Geoapify Geocoding ve Routing adapter'ları Infrastructure'da
  implement edilir. Geocoding structured `name/city/country`, routing araç modu `drive`, metre/saniye ve
  GeoJSON geometri kullanır.
- `ProviderMigration:Mode` geçiş kapısıdır; `Geoapify` normal varsayılandır, `Legacy` açık rollback
  override'ı, `Shadow` sonuç değiştirmeyen geçici karşılaştırma modudur. Shadow ilave API kredisi kullanır.
- Geocode cache kimliği provider ve normalize edilen sorgu bağlamının tüm bileşenlerini kapsar. Eski
  bağlamsız cache girdileri yeni isteklerle eşleşmez.
- Statik harita/geocoding/routing tek sunucu tarafı `Geoapify:ApiKey` secret'ını paylaşır. URL/query ve
  personel bilgileri loglanmaz. Eski `StaticMap:ApiKey` yalnız geçiş kolaylığı için fallback'tir.
- Sicil/ad/soyad aynı request DTO'sunda alınır, PDF rapor başlığında gösterilir; JSON yanıtına veya kalıcı
  depolamaya eklenmez.

## Consequences
**Kazanımlar:** Aynı konum sağlayıcısı geocoding+routing+statik haritayı kapsar; şehir/ülke araması
belirsiz yer adlarını sınırlar; provider değişikliği arayüz arkasında kalır.

**Maliyetler/riskler:** Ücretli/ücretsiz kredi tüketimi ve tek sağlayıcıya bağımlılık artar. Shadow mod iki
provider ailesine ilave çağrı ve latency ekler. Ülke/şehir isimlerinin Geoapify tarafından tanınması kaliteyi
etkiler; doğru sonuç garantisi değildir. Tek Geoapify kesintisi geocode ve routing'i birlikte etkiler.

## Revisit triggers
- Shadow farkları veya kullanıcı geri bildirimi Geoapify konum kalitesini doğrulamazsa.
- Kredi tüketimi, rate limit veya fiyatlandırma sürdürülemez hale gelirse.
- Sicil/ad/soyadın PDF'de görünmesi veri minimizasyonu/politika gereksinimleriyle çelişirse.


## Current implementation status
The Legacy and Shadow migration modes described above were removed by spec 0013. Geoapify remains the active provider, and future providers should implement the existing Domain interfaces through Infrastructure adapters.
