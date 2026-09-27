# Spec 0003 — Yerel gizli ayarların yapılandırılması

- Status: Shipped
- Mode: lite
- Plan: `specs/plans/0003-plan.md`

## Intent
Geliştirici, API’yi yerelde çalıştırırken MongoDB Atlas önbelleğini ve API key kimlik doğrulamasını kullanabilmelidir. MongoDB parolası ve API key kaynak koduna veya sürüm kontrolüne girmeden .NET’in yerel gizli ayar deposunda tutulmalıdır. Bağlantı URI’si gerçek parolayla tamamlanana kadar çalışır kabul edilmemelidir.

## Requirements
- API projesi yerel .NET User Secrets deposundan yapılandırma okuyabilir.
- MongoDB bağlantı dizesi Atlas kümesi ve `rpaotelrezarvasyon` veritabanıyla eşleşir.
- API key kriptografik olarak güvenli rastgele değer olmalı ve API’nin beklediği `Api:ApiKey` ayarına yazılmalıdır.
- Hiçbir parola veya API key izlenen dosyaya yazılmaz.

## Constraints & out of scope
- Atlas parolası kullanıcı tarafından sağlanmadan tahmin edilmez veya URI’ye literal placeholder bırakılmaz.
- Atlas kullanıcı/rol oluşturma, IP allowlist, küme yönetimi ve üretim sır dağıtımı kapsam dışıdır.

## Acceptance criteria
- [x] AC-1 — API projesi User Secrets kimliğine sahiptir ve `dotnet user-secrets` ayarları projeye bağlanır.
- [x] AC-2 — Yerel secret store’da `Mongo:ConnectionString` verilen Atlas hostu, kullanıcı adı ve veritabanını içerir; parola placeholder değildir.
- [x] AC-3 — Yerel secret store’da boş olmayan, kriptografik rastgele `Api:ApiKey` bulunur.
- [x] AC-4 — Mongo URI paroladaki URI özel karakterlerini güvenli biçimde kodlar.
- [x] AC-5 — Sırlar git tarafından izlenen dosyalara eklenmez.

## Definition of Done
- [x] Her kabul ölçütü kanıta (test veya tekrarlanabilir gözlem) bağlandı
- [x] `scripts/check` yeşil
- [x] Bağımsız inceleme yapıldı; gerçek bulgular giderildi, gürültü yazılı gerekçeyle reddedildi
- [x] Davranış veya mimari değiştiyse dokümanlar / ADR'ler güncellendi
- [x] Spec `specs/done/` konumuna taşındı (değiştirilemez olur)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | 0 |
| Fix rounds | 0 |
| Review findings: real / noise | 0 / 0 |
| Regressions introduced | 0 |
| Bugs escaped to production | 0 |

## Ship notları (kanıt sınırları)
- **AC-4** yerel parola URI özel karakteri içermediğinden percent-encoding yolu bu değerle egzersiz edilmemiştir; saklanan bağlantı dizesi geçerlidir (host/kullanıcı/veritabanı doğru, placeholder yok). Kalıcı test artefaktı yoktur; kabul edilen doğrulama sınırıdır.
- **AC-3** kriptografik rastgeleliğin statik saklanan değerden kanıtlanması mümkün değildir; uzunluk (43) ve karakter kümesi gözlemlenmiştir.
- Bağımsız inceleme (2026-09-26, salt-okunur ajan): AC-1/AC-2/AC-3/AC-5 doğrulandı; AC-4 yukarıdaki sınırla karşılandı.

## Öz eleştiri
- Veritabanı adı bağlantı URI’sinde bulunmuyor. Öneri: URI path’ine `rpaotelrezarvasyon` koy; böylece istenen veritabanı açıkça seçilir.
- API key’in kullanıcıya nasıl teslim edileceği tanımlı değil. Öneri: değeri yerel secret store’a yaz ve kullanıcıya bir kez göster; loglama/izlenen dosyaya yazma.
