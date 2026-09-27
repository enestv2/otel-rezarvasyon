# Spec 0017 - Harita konum hizalamasi

- Status: Draft
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
- Harita saglayicisi, PDF yerlesimi, geocoding kapsam kalitesi ve rota secim politikasi degismez.
- Duzeltme taban haritanin zoom ve piksel yogunlugu ile kullandigi Web Mercator olcegi dogrulanarak yapilir.

## Acceptance criteria
- [ ] AC-1 Hosted harita projeksiyonu referans lat/lon noktalarini taban harita piksel koordinatlariyla ayni yere donusturur.
- [ ] AC-2 Fit zoom secimi etkinlik, otel ve rota geometrilerinin tamamini cizim alanina sigdirir.
- [ ] AC-3 Sematik harita etkinlik ve otel isaretcilerini verilen koordinatlara gore dogru yerlestirir.
- [ ] AC-4 Etkinlik pini, otel pinleri ve rota katmani arasinda koordinat donusum kaynakli kayma kalmaz.

## Definition of Done
- [ ] Her kabul kriteri test veya tekrar edilebilir gozlemle kanitlanir.
- [ ] `scripts/check` yesildir.
- [ ] Bagimsiz inceleme yapilir; gercek bulgular giderilir.
- [ ] Spec `specs/done/` konumuna tasinir.

## Diagnosis status
- The initial 512-pixel hypothesis is not confirmed. Geoapify documents `scaleFactor` as output pixel density and standard map tiles as 256 x 256, but its Static Maps endpoint reference does not specify world-pixel scale for `zoom`.
- The experimental test expecting 512 pixels per world at zoom 0 was assumption-based; it was removed along with the provisional code change.
- No PDF/image or known coordinates from the affected map were supplied, so the issue cannot yet be reproduced against the rendered basemap. No production source change remains from this investigation.
- `scripts/check` previously returned `Access is denied`; a running API process also prevented replacement of its build output.