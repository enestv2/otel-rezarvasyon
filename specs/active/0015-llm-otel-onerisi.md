# Spec 0015 - LLM aciklamali otel onerisi
- Status: In progress
- Mode: lite
- Plan: `specs/plans/0015-plan.md`

## Intent
Rezervasyon birimi, deterministik olarak secilmis en uygun otelin neden secildigini PDF onerisi icinde acik ve okunabilir bir Turkce metin olarak gormek ister. Sistem butun otel onerisi sonucunu OpenAI uyumlu bir LLM'e gonderir; model secimi degistirmez, yalnizca izin verilen gerekce kodlarini stream olarak dondurur. Uygulama dogrulanmis kodlari kanonik metriklerle Turkce PDF metnine cevirir; serbest model metni PDF'ye eklenmez. LLM ayarlari gelistirici profilinde launchSettings uzerinden, production'da Rancher Secret/ortam degiskenleriyle verilir.

## Requirements
- Tum siralanmis otel sonuc ve metrikleri, secilen otel, secim gerekcesi ve cozumlenemeyen otel listesi LLM'e aciklama baglami olarak gonderilir.
- Otel secimi ve siralamasi mevcut deterministik is kurallarina gore yapilir; LLM bunlari degistiremez.
- LLM secilen otelin neden secildigini PDF'deki onerilen otel bolumunde Turkce metinle aciklar.
- LLM yaniti saglayicidan stream olarak okunur; secilen otel adi ve gerekce kodlari izin listesine karsi dogrulanir. Sunucu, PDF metnini yalnizca kanonik verilerden kurar; serbest model metni kabul edilmez.
- LLM erisilemez, zaman asimina ugrar veya cevabi dogrulamadan gecemezse PDF bilinen deterministik gerekceyle olusturulmaya devam eder.
- PDF metni serbest LLM cikisindan kurulmaz; girdi veya sonuc verisinde olmayan otel ozelligi, musaitlik, rezervasyon, ulasim secenegi veya fiyat etkisi PDF'ye giremez. Gecelik fiyatin mevcut siralamayi etkilemedigi kurali korunur.
- Personel sicil numarasi, ad/soyad, API anahtari ve saglayici kimlik bilgileri LLM istegine eklenmez.
- LLM endpoint/base URL, model, API key, timeout, output limiti ve sicaklik gibi ayarlar launchSettings profillerinde yapilandirilabilir; paylasilabilir ornek dosyada gercek sir bulunmaz.
- Mevcut JSON ve PDF endpoint sozlesmeleri geriye uyumlu kalir; JSON yanitinin yapisi degismez.

## Constraints & out of scope
- LLM tum sonuc verisini degerlendirip izinli gerekce kodu secer; PDF metni bu kod ve kanonik verilerden sunucuda uretilir. Modelin siralama, puanlama, konum/rota cagirma veya rezervasyon araci yoktur.
- Model cevabi deterministik secim sonucunun otoritesi degildir; PDF metrikleri ve secilen otel mevcut model/sonuc nesnesinden gelir.
- Konusma gecmisi veya LLM istem/cevaplari kalici saklanmaz ve loglanmaz.
- V1, istemciye SSE/WebSocket ile token aktarmaz; stream, LLM sağlayicisindan sunucu tarafinda okunur. PDF ancak tam yanit dogrulaninca uretilir.
- Gercek model saglayici/adi, kullanici belirlemedikce sabitlenmez; OpenAI uyumlu BaseUrl ve model ayariyla degistirilebilir.
- K8s manifesti/Helm/Rancher nesnesi olusturulmaz.

## Acceptance criteria
- [ ] AC-1 Basarili oneride LLM baglami secilen oteli, tum siralanmis otellerin mevcut metrik/fiyat verilerini ve cozumlenemeyen otelleri kapsar; personel/secret verileri kapsamaz.
- [ ] AC-2 LLM secilen oteli, otel siralamasini veya is kuralini degistiremez; dogru Turkce gerekce PDF'de onerilen otel bolumunde gorunur.
- [ ] AC-3 Saglayici stream'i parca parca okunur; [DONE] olmadan biten veya gecersiz secim/kod iceren cevap PDF gerekcesinde kullanilmaz.
- [ ] AC-4 Timeout, saglayici hatasi, bos/bozuk veya dogrulamadan gecmeyen cevap deterministik gerekce fallback'iyle sonuclanir. Istek iptali caller cancellation olarak yukari iletilir; tamamlanmamis model ciktisi PDF'ye alinmaz.
- [ ] AC-5 Modelden yalniz izinli gerekce kodlari alinir; PDF cumleleri sunucuda kanonik verilerle olusturulur. Boylece serbest model iddialari PDF'ye giremez ve fiyatin siralamayi etkiledigi soylenmez.
- [ ] AC-6 LLM konfigurasyonu `Llm__...` launch profile anahtarlariyla okunur; API key repoya girmez; ornek profil secretsizdir.
- [ ] AC-7 Mevcut JSON response semasi ve deterministik siralama davranisi degismez; saglayici adapter'i arayuz arkasindadir.

## Definition of Done
- [ ] Her kabul kriteri test veya tekrar edilebilir gozlemle kanitlanir.
- [ ] `scripts/check` yesildir.
- [ ] Bagimsiz inceleme yapilir, gercek bulgular giderilir.
- [ ] Guvenlik, mimari ve kullanim belgeleri guncellenir.
- [ ] Spec `specs/done/` konumuna tasinir.

## Recommendation
Onerim, modeli `IRecommendationExplanationProvider` arayuzu arkasinda OpenAI uyumlu stream adapter'iyle kullanip yalniz izinli gerekce kodlarini kabul etmek; PDF cumlelerini kanonik verilerden sunucuda olusturmak ve her LLM hatasinda deterministik gerekceye donmektir. Boylece okunabilir aciklama kazanilirken secim kurallari LLM'e devredilmez.

## Verification evidence (2026-09-27)
- `dotnet build backend/RPAOtelRezervasyon.sln --no-restore --nologo -warnaserror -m:1`: passed, 0 warnings/errors.
- `RPAOtelRezervasyon.UnitTests`: passed, 104/104.
- `RecommendationExplanationServiceTests` + `RecommendationReportServiceTests`: passed, 21/21.
- `OpenAiCompatibleRecommendationExplanationProviderTests`: passed, 3/3.
- `Post_pdf_renders_turkish_names_and_the_selected_hotel_without_corruption`: passed, 1/1.
- `git diff --check`: passed (Git reported only configured LF/CRLF notices).
- Local launch settings checked without printing values: both profiles contain all LLM keys and enable the provider; file is ignored by Git. Shareable example has LLM disabled and blank model/API-key fields.
- Independent read-only review rechecked the reason-code-only response and server-rendered rationale; the initial unsupported-claim concern was closed, with no further actionable findings.
- Broader run is not green: existing Geoapify map integration expectations fail (expected 2 map calls, got 1; fallback copy expects stale text), and one provider test cannot assert its validation exception because Windows Event Log permission failure wraps it in `AggregateException`. These are unrelated to the LLM adapter.
- `scripts/check` could not be launched in this Windows shell (`sh` unavailable; direct `bash scripts/check` is denied). Its configured build and test commands were run directly; full test suite still has the failures above, so DoD remains open.
