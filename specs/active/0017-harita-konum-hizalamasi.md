# Spec 0017 - Harita konum hizalamasi

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0017-plan.md`

## Intent
Etkinlik alanini ve otel konumlarini gosteren PDF haritasinda isaretciler ile taban haritadaki gercek yerler ayni konumda gorunmelidir. Rota cizgileri, rota geometrileri ve konum isaretcileri taban haritanin kullandigi koordinat olcegiyle hizalanmalidir.

## Requirements
- Etkinlik alani pini, verilen etkinlik koordinatini haritada dogru noktada gostermelidir.
- Otel isaretcileri ve varsa rota geometrileri ayni harita projeksiyonu/zoom olcegine gore konumlanmalidir.
- Farkli enlem ve boylam degerleri hem dogu-bati hem kuzey-guney yonunde beklenen piksel farkini olusturmalidir.
- Koordinat duzeltmesi hem hosted Geoapify haritasinda hem de sematik fallback haritasinda dogru calismalidir.

## Constraints & out of scope
- Geocoding veya routing saglayicilarinin koordinat cevabi degistirilmez.
- Harita saglayicisi, geocoding kapsam kalitesi ve rota secim politikasi degismez.
- Saglayicinin zoom projeksiyon olcegi degistirilmez; gercek konum isareti ve lejant belirginlestirilir.

## Acceptance criteria
- [ ] AC-1 Etkinlik pini verilen koordinatta kalir; PDF rozeti ile gercek otel konumu baglantisini aciklar.
- [ ] AC-2 Otel gercek konum noktasi, numara rozeti baska yere tasindiginda yeterince gorunur kalir.
- [ ] AC-3 Otel numara rozeti baglantisi konum noktasinda biter ve lejant bunu gercek konum olarak aciklar.
- [ ] AC-4 Walking route geometry does not replace road route geometry on the overview map.

## Definition of Done
- [ ] Her kabul kriteri test veya tekrar edilebilir gozlemle kanitlanir.
- [ ] `scripts/check` yesildir.
- [ ] Bagimsiz inceleme yapilir; gercek bulgular giderilir.
- [ ] Spec `specs/done/` konumuna tasinir.

## Diagnosis status
Goruntudeki PDF haritasi, varsa yurumeye ait rota geometrisini ana rota olarak secip road route lejantiyle gosteriyordu. Bu, yol metriyle rota cizgisinin farkli hedef/ulasilabilir noktalara gitmesine ve otel pininin kaymis gorunmesine yol acabilir. Ana harita her zaman siralama ve tabloda kullanilan yol geometrisini gostermeli. Ayrica numara rozeti cakisma onleme nedeniyle tasindiginda gercek koordinat ucu daha gorunur yapilmali.
## Verification evidence (2026-09-27)
- The supplied PDF screenshot exposed that the overview can use walking geometry while the map legend describes a road route.
- Added unit regression `CreateAsync_keeps_the_road_route_on_the_map_and_walking_metrics_separate`; it passes.
- Added render regression for a displaced hotel badge; it verifies the exact-coordinate anchor remains visible and passes.
- The PDF integration test verifies the updated legend text and passes.
- Full solution build succeeds with `-warnaserror` (0 warnings, 0 errors).
- Focused road-geometry unit tests pass (2/2); schematic-fallback PDF integration passes (1/1).
- Full test run: UnitTests 106/106, ArchitectureTests 5/5, ProviderTests 44/45, IntegrationTests 51/52 (4 skipped). The provider failure is Windows Event Log permission denied; the remaining integration failure expects 2 Geoapify HTTP calls but receives 1 in `Post_pdf_uses_geoapify_base_map_and_includes_attribution`.
- Independent read-only review found no remaining actionable issues.
- `scripts/check` could not execute in this PowerShell environment (`bash scripts/check` returned Access is denied); `git diff --check` passes (only line-ending normalization warnings).
