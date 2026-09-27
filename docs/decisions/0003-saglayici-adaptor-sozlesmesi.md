# ADR 0003 — Sağlayıcı adaptör sözleşmesi ve dayanıklılık tek yerde

- Status: Superseded by ADR-0004
- Date: 2026-09-25

## Context
Nominatim ve OSRM dış servislerdir: yavaşlar, rate limit uygular, şemaları haber vermeden değişebilir
ve zamanla değiştirilebilirler. Bu kırılganlık iş koduna sızarsa her sağlayıcı olayı çekirdeği
etkiler. Ayrıca her adaptörün kendi timeout/retry/limit davranışını farklı biçimde uygulaması,
tutarsız ve test edilemez bir sistem üretir.

## Decision
Sağlayıcı erişimi **anti-corruption layer** olarak kurulur: arayüzler Domain'de, uygulamalar
Infrastructure'da (`Providers` = HTTP, `Persistence` = depolama). Beklenen durumlar sonuç tipiyle
(`Found | NotFound | TransientError`) döner; sağlayıcı DTO'ları `internal` kalır. Dayanıklılık
politikaları (rate limit → total timeout → retry → circuit breaker → attempt timeout) adaptörde,
tek ve tutarlı biçimde tanımlanır; her adaptör DI kaydını kendi uzantısıyla yapar.

## Consequences
**Kazanç:** Sağlayıcı değişimi/arızası çekirdeğe dokunmaz; hata davranışı öngörülebilir ve test
edilebilir; sır/başlık/limit yönetimi tek noktada; sağlayıcı geçişi shadow-run ile güvenli.
**Maliyet:** Daha fazla dosya/soyutlama ve ortak altyapı kodu (handler, resilience, options);
ekip için adaptör yazma disiplini gerekir; aşırı soyutlama riskine karşı adaptörler ince tutulmalı.

## Alternatives considered
- **İş kodunda doğrudan `HttpClient`:** en hızlı başlangıç; ama kırılganlık ve dağınık hata yönetimi →
  reddedildi (AGENTS.md yasak bağımlılık kuralı).
- **Polly'yi çağrı başına, iş kodunda uygulamak:** esneklik; ama tutarsızlık ve tekrar → reddedildi.
- **Sağlayıcı SDK'sı kullanmak:** daha az kod; ama sürüm/kilitlenme ve değiştirilebilirlik kaybı →
  `HttpClient` + arayüz tercih edildi (bkz. ADR-0001).

## Revisit triggers
- İkinci bir geocoding/routing sağlayıcısı aynı anda gerekirse (keyed services + seçim stratejisi).
- Sağlayıcı sözleşmeleri sık kırılırsa (sözleşme testleri + sürümleme stratejisi gözden geçirilir).
- Gecikme bütçesi sıkılaşırsa (bulkhead/paralellik politikaları yeniden değerlendirilir).
