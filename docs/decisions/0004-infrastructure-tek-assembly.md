# ADR 0004 — Infrastructure tek assembly'dir (Providers + Persistence)

- Status: Accepted
- Date: 2026-09-25

## Context
ADR-0003, "Infrastructure"ı yalnızca bir **kavram** olarak tanımlayıp fiziksel olarak iki assembly'ye
(`Providers`, `Persistence`) bölmüştü. Uygulamada bu, "Infrastructure" adının hiçbir projede
görünmemesine ve "Providers mı, Infrastructure mı?" belirsizliğine yol açtı. İki adaptör grubu da
yalnızca Api tarafından tüketiliyor; derleme düzeyinde yalıtımın getirisi, isim karışıklığı ve
fazladan proje yükünün yanında küçük kaldı.

## Decision
Infrastructure **tek assembly**'dir: `RPAOtelRezervasyon.Infrastructure`. İçinde iki klasör/namespace
vardır: `Providers/` (outbound HTTP: Nominatim, OSRM) ve `Persistence/` (MongoDB konum önbelleği).
Yalıtım derleme yerine **namespace + mimari test** ile korunur.

## Consequences
**Kazanç:** "Infrastructure" somut bir proje olur; isim belirsizliği biter; proje sayısı azalır.
**Maliyet:** Yalıtımı artık derleyici değil mimari test zorlar; yanlış `using` derlemede değil testte
yakalanır. Bu yüzden mimari testler (Domain'de `System.Net.*`/`MongoDB.Driver` yok; `Persistence.*`
`HttpClient` kullanmaz) zorunlu hâle gelir.

## Alternatives considered
- **İki assembly (ADR-0003 hâli):** daha güçlü derleme yalıtımı; ama isim karışıklığı ve fazladan
  proje → terk edildi.
- **Providers'ı Infrastructure olarak adlandırıp Persistence'ı ayrı bırakmak:** "Infrastructure" ve
  "Persistence" iki farklı düzeyi ima eder, karışıklık sürer → reddedildi.

## Revisit triggers
- Persistence ayrı paketlenip dağıtılması gerekirse (ör. ayrı NuGet/servis).
- Mimari testler yalıtımı koruyamaz hâle gelirse (sık namespace ihlali).
