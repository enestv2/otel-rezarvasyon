# Spec 0001 — Otel öneri akışı

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0001-plan.md`

## Intent
Kurumlar, etkinliklerini anlaşmalı otellerinde konaklatan birimlerdir; hangi otelin etkinlik alanına
en uygun olduğunu elle, haritalardan bakarak bulurlar — yavaş ve tutarsız. Bu iş, bir etkinlik alanı
adı ve anlaşmalı otel adları listesini alıp konaklama birimine **en uygun oteli gerekçesiyle**
önererek o kararı hızlandırmayı hedefler. Başarı: kullanıcı tek istekte, mesafeye göre sıralı ve
gerekçeli bir öneri alır; tekrar eden konum adları **MongoDB'deki kalıcı önbellekten** okunur ve dış
servis yalnızca ilk çözümlemede çağrılır. Bilinçli olarak yapılmayan: rezervasyon oluşturma/iptal, oda envanteri,
fiyat karşılaştırması, kullanıcı arayüzü.

## Requirements
- **R-1** Servis, bir etkinlik alanı adı ve bir otel adı listesi alır.
- **R-2** Her ad için konum bulunur; bulunan konum etkinlik alanı ile karşılaştırılır.
- **R-3** Her otel için etkinlik alanına olan yol mesafesi ve tahmini yol süresi hesaplanır.
- **R-4** Oteller en uygun olandan en uzağa sıralanır; önce süre, eşitlikte mesafe, hâlâ eşitlikte
  otel adı (A→Z) esas alınır.
- **R-5** Yanıt, seçilen en uygun oteli ve seçim gerekçesini içerir.
- **R-6** Konumu bulunamayan oteller isteği düşürmez; sonuç dışında ayrıca bildirilir.
- **R-7** Etkinlik alanının konumu bulunamazsa öneri üretilmez ve kullanıcıya anlaşılır bir hata döner.
- **R-8** Aynı girdi için sonuç her seferinde aynıdır.
- **R-9** Yol mesafesi kullanılamayıp düz çizgi mesafesine düşülürse, bu durum yanıtta açıkça işaretlenir.
- **R-10** Servis yalnızca yetkili çağrılara yanıt verir.
- **R-11** Her konum adı (etkinlik alanı ve oteller) MongoDB'deki kalıcı önbellekte tutulur; aynı ad
  tekrar geldiğinde konum önbellekten okunur ve dış sağlayıcıya istek yapılmaz.
- **R-12** Hiçbir otel çözümlenemezse (etkinlik alanı çözümlense bile) öneri üretilmez; çözümlenemeyen
  oteller ve durum kullanıcıya bildirilir.

## Constraints & out of scope
- **Kapsam dışı:** rezervasyon kaydı/CRUD, oda envanteri, fiyat/kapasite, kullanıcı arayüzü, iş
  verisinin kalıcı saklanması (MongoDB yalnızca konum önbelleğidir), toplu (batch) işleme, çoklu
  etkinlik alanı.
- **Kalıcılık:** Konum önbelleği için MongoDB (ADR-0002). MongoDB erişilemezse servis önbelleksiz
  çalışmaya devam eder (bkz. G-6).
- **Sınırlar (V1):** en fazla **20** otel adı (G-4 onaylandı); etkinlik alanı adı 3–200 karakter;
  otel adı 2–200 karakter.
- **Hedef:** sağlayıcı gecikmesi hariç tipik istek (etkinlik + ≤20 otel) < 2 sn.
- Dış sağlayıcı politikalarına uyulur (rate limit); anahtarsız başlangıç (Nominatim/OSRM).

## Acceptance criteria
- [x] AC-1 — Etkinlik alanı adı ve en az bir otel adı verildiğinde, yanıt her otel için mesafe ve
  süre içeren, en uygun otelden en uzağa sıralı bir öneri listesi döner.
- [x] AC-2 — Yanıt, sıralamada ilk sıradaki oteli "önerilen otel" olarak ve seçim gerekçesini içerir.
- [x] AC-3 — Etkinlik alanı adı çözümlenemezse istek sonuç üretmez; boş öneri ve anlaşılır hata döner.
- [x] AC-4 — Bir otel adı çözümlenemezse istek başarılı kalır; o otel öneri listesine girmez ve
  "çözümlenemeyen oteller" altında bildirilir.
- [x] AC-5 — Otel listesi boşsa istek doğrulama hatasıyla reddedilir.
- [x] AC-6 — Etkinlik alanı adı boş veya yalnızca boşluksa istek doğrulama hatasıyla reddedilir.
- [x] AC-7 — Bir otel için mesafe/süre elde edilemezse o otel "ölçülemedi" sayılır; varsayılan bir
  değerle sıralamaya sokulmaz, öneri listesine mesafesiz girmez.
- [x] AC-8 — Aynı girdiyle ardışık iki istek birebir aynı sıralamayı ve aynı önerilen oteli döner.
- [x] AC-9 — Kimlik doğrulaması olmayan istek reddedilir; hiçbir iş mantığı çalışmaz.
- [x] AC-10 — Listede yinelenen otel adı tekilleştirilir; öneri listesinde bir kez görünür.
- [x] AC-11 — Yol rotası bulunamayıp düz çizgi fallback'i kullanıldıysa ilgili otel `StraightLine`
  olarak işaretlenir, süresi 0 (ölçülmedi) olur ve sıralamada ölçülmüş (Road) otellerin tamamından
  sonra gelir; süresi için tahmin üretilmez.
- [x] AC-12 — Eşit süre ve mesafede oteller otel adına göre A→Z sıralanır.
- [x] AC-13 — 20'den fazla otel adı veya uzunluk sınırını aşan ad istek doğrulama hatasıyla reddedilir.
- [x] AC-14 — Aynı ad daha önce çözümlenmişse konum MongoDB'den okunur; bu istekte dış geocoding
  sağlayıcısına **çağrı yapılmaz**.
- [x] AC-15 — Önbellek kalıcıdır: servis yeniden başlatıldıktan sonra aynı ad için dış çağrı yapılmaz.
  - *Kanıt notu:* Gerçek MongoDB gerektirir. Test, `MONGO_TEST_CONNECTION` ortam değişkeni tanımlıysa
    koşar; değilse `skip` raporlanır. Varsayılan `scripts/check` bu teste bağlı değildir (opt-in
    doğrulama; bkz. plan 0001 madde 8 ve R-07 A-6).
- [x] AC-16 — Önbellek anahtarı normalizasyonu: büyük/küçük harf ve baş/son boşluk farkı aynı kaydı
  kullanır.
- [x] AC-17 — Etkinlik alanı çözümlenip hiçbir otel çözümlenemezse öneri üretilmez; çözümlenemeyen
  oteller bildirilir ve kullanıcıya anlaşılır hata döner.

## Definition of Done
- [x] Every acceptance criterion mapped to proof (test or reproducible observation)
- [x] `scripts/check` green
- [x] Independent review done; real findings fixed, noise rejected with written rationale
- [x] Docs / ADRs updated if behavior or architecture changed
- [x] Spec moved to `specs/done/` (it becomes immutable there)

## Self-critique (hostile reader) — onaya açık
- **G-1** (çözüldü: ADR-0005) "Rota yok" → düz çizgi fallback (işaretli, süre=0, ölçülmüş yolların
  ardında); sağlayıcı geçici hatası → otel ölçülemedi sayılıp elenir (AC-7).
- **G-2** (çözüldü) Gerekçe yapılandırılmış alandır (`Rationale`): seçilen otelin süresi, mesafesi,
  mesafe türü ve kaç otel arasından seçildiği.
- **G-3** (çözüldü) Yinelenen otel adlarında ilk görülen kayıt korunur (harf/boşluk duyarsız).
- **G-9** Etkinlik alanı çözümlenip hiç otel çözümlenemezse ne olur? Karar: öneri üretilmez, ayrı
  statü ve `422`; çözümlenemeyen oteller bildirilir (AC-17).
- **G-4** (onaylandı: 20) Otel listesi üst sınırı sağlayıcı rate limit'i ile çelişebilir (Nominatim
  1 istek/sn). Önbellek + hız sınırlama ile birlikte V1 sınırı **20** olarak belirlendi.
- **G-5** Kimlik doğrulama yöntemi (API anahtarı mı JWT mi) spec'te yok. Önerim: V1'de API anahtarı;
  JWT ileride. Onayını beklerim.
- **G-6** MongoDB erişilemezse ne olur? Önerim: istek düşmez; önbellek devre dışı kalır ve istek dış
  sağlayıcıdan çözülür (durum loglanır). Alternatif: 503 dönmek — önermiyorum; önbellek hızlandırmadır,
  zorunlu bağımlılık değildir.
- **G-7** Normalizasyon kuralı belirsiz. Önerim: `trim` + `ToLowerInvariant` + ardışık boşlukları tek
  boşluğa indirgeme. Aksan/transliterasyon V1'de yapılmaz.
- **G-8** Başarısız (negatif) çözümlemeler önbelleğe yazılsın mı? Önerim: **yazılmasın**; çözümleme
  sonradan düzelirse kalıcı hatalı kayıt oluşur. Negatif önbellek gerekirse kısa TTL'li ayrı kayıt
  olarak sonraki sürümde değerlendirilir.

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 |
| Fix rounds | 2 |
| Review findings: real / noise | 3 / 0 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 |

## Ship notları (kanıt sınırları)
- **AC-15** yalnızca opt-in Mongo testiyle kanıtlanır; varsayılan `scripts/check` kapsamı dışındadır (spec kararı).
- **AC-12/AC-13** birim (ranking/validator) düzeyinde kanıtlanmıştır; uçtan uca endpoint kanıtı yoktur (düşük risk).
- Bağımsız inceleme (2026-09-26, salt-okunur ajan): 17 AC'nin 14'ü bozulmaya duyarlı kanıtla doğrulandı; kalan sınırlar yukarıdadır.
- Kabul edilen inceleme gözlemleri (spec ihlali değil): son eşitlik kırıcı `StringComparer.Ordinal` kültür-duyarlı değil; çözümlenemeyen ve ölçülemeyen oteller aynı `unresolvedHotels` listesinde birleşiyor.
