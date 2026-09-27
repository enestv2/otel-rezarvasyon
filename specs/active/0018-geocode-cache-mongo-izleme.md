# Spec 0018 — Geocode cache Mongo izleme

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0018-plan.md`

## Intent
Geliştirici ve operatör, konum çözümleme önbelleğinin MongoDB’ye erişip erişemediğini anlayabilmelidir. Şu an Mongo hataları önbellek işlemlerinde yakalanıyor ve istek sürerken yazma atlanabiliyor; Mongo için ayrı bir health kontrolü bulunmuyor. Başarılı geocode sonuçları kalıcı önbelleğe yazılmaya devam etmeli, Mongo erişilemediğinde ise asıl öneri akışı mevcut dayanıklı davranışını korumalıdır. Bu iş öneri veya rezervasyon verisi saklamaz.

## Requirements
- Operatör Mongo önbellek erişim durumunu servis sağlayıcı kontrollerinden ayrı görebilmelidir.
- Başarılı geocode sonuçları mevcut Mongo upsert akışıyla kalıcı önbelleğe yazılmalıdır.
- Önbellek yazma başarısı/başarısızlığı varsayılan uygulama log seviyesinde kişisel veya arama yeri bilgisi açığa çıkarmadan ayırt edilebilmelidir.
- Mongo erişim hatası öneri isteğinin mevcut fail-open davranışını değiştirmemelidir.

## Constraints & out of scope
- Yalnızca geocoding konum önbelleği kapsamdadır; öneri, otel, rezervasyon veya PDF saklanmaz.
- Mongo başarısızlık politikasının değiştirilmesi ve cache kullanım semantiğinin değiştirilmesi kapsam dışıdır.
- Bağlantı dizesi, kullanıcı adı, parola, koordinat ve arama adı health yanıtı/loglara eklenmez.

## Acceptance criteria
- [x] AC-1 — Mongo erişilebiliyorsa storage health kontrolü `Healthy`, erişilemiyorsa `Degraded` bildirir.
- [x] AC-2 — Storage health kontrolü MongoDB durumunu, provider health kontrolünden ayrı sunar.
- [x] AC-3 — Başarılı upsert denemesi tanınabilir bir information log üretir; hata warning log üretir; iki log da sorgu adını, çözümlenmiş konum adını, koordinatı veya bağlantı bilgisini içermez.
- [x] AC-4 — Mongo erişilemediğinde geocoding/öneri akışı hata vermez ve cache yazma hatası warning olarak kaydedilir.
- [ ] AC-5 — `scripts/check` ve bağımsız inceleme tamamlanır.

## Definition of Done
- [ ] Every acceptance criterion mapped to proof (test or reproducible observation)
- [ ] `scripts/check` green
- [ ] Independent review done; real findings fixed, noise rejected with written rationale
- [ ] Docs / ADRs updated if behavior or architecture changed
- [ ] Spec moved to `specs/done/` (it becomes immutable there)

## Scorecard (fill at ship — honest numbers make the process improvable)
| Metric | Value |
|---|---|
| Spec revisions | |
| Fix rounds | |
| Review findings: real / noise | |
| Regressions introduced | |
| Bugs escaped to production | |

## Verification evidence (2026-09-27)
- Mongo health-check unit tests pass (healthy and unreachable ping: 2/2).
- Storage endpoint separation and fail-open sanitized warning integration tests pass (3/3).
- Sanitized successful-upsert information log test passes (1/1). The optional live Mongo suite skips unless `MONGO_TEST_CONNECTION` is configured.
- Full solution build passes with `-warnaserror` (0 warnings, 0 errors).
- Full solution test run: UnitTests 106/106 and ArchitectureTests 5/5. ProviderTests had one Windows Event Log permission failure; IntegrationTests had one Geoapify call-count failure and 4 optional Mongo skips. Remaining tests passed.
- Independent read-only review completed; metadata, log level and display-name privacy findings were resolved.
- `git diff --check` passes. `scripts/check` returns `Access is denied` in this PowerShell environment.
