# Git

> Bootstrap ile dolduruldu (2026-09-25). Varsayılanlar güvenlidir; gevşetmek bilinçli bir karar olur.

## Dallanma
- `feature/<spec-no>-<kısa-ad>` — **spec olmadan branch yok.**
- Düzeltmeler: `fix/<spec-no>-<kısa-ad>`; olaylar: `incident/<tarih>-<kısa-ad>`.
- Bootstrap/workspace kurulum işleri (ilk spec öncesi) doğrudan `main` üzerinde yapılabilir; ilk
  spec'ten itibaren dallanma zorunludur.

## Commit'ler
- Conventional Commits + plan referansı: `feat(routing): mesafe sağlayıcısı [plan 0001/3]`.
- Ajan commit'leri de aynı standarda uyar: mesajı ajan yazar, insan onaylar.
- Commit mesajları **İngilizce**; kod içi yorumlar ve dokümanlar Türkçe olabilir.
- Gerçek yerel değerler içeren `backend/src/RPAOtelRezervasyon.Api/Properties/launchSettings.json` ignore edilir; yalnız secret'sız `launchSettings.example.json` paylaşılır.

## Yasaklar
- Varsayılan dala doğrudan commit (ilk spec'ten sonra).
- Force push; paylaşılan dallarda geçmişi yeniden yazma. Geri alma = `git revert` (bkz. R-11).

## Pull request
- PR şablonu doldurulur; CI'da `scripts/check` yeşil; squash-merge.
- Her PR'da bir spec bağlantısı olur (AGENTS.md kural 1).
