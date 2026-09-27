# Spec 0016 - Otel onerisi secenekleri ve aciklama kalitesi

- Status: In progress
- Mode: lite
- Plan: `specs/plans/0016-plan.md`

## Intent
Rezervasyon birimi otel seceneklerini fiyat ve etkinlik alanina ulasim acisindan karsilastirip kendi onceligine gore karar vermek ister. PDF, butce oncelikli, ulasim oncelikli ve dengeli secenekleri anlasilir trade-off aciklamalariyla sunar. LLM yalniz izinli gerekce kodlari secer; secim ve olgusal iddialar sunucu tarafinda belirlenir. Kullanici, girdi verisinde olmayan fiyat veya otel ozelliklerinin varsayilmadigini gorebilmelidir.

## Requirements
- PDF'de butce oncelikli, ulasim oncelikli ve denge secenekleri ayri ayri gosterilir; baglamsiz tek bir "en iyi" otel iddiasi kurulmaz.
- Butce oncelikli secenek, ayni para birimindeki en dusuk gecelik tutari secer; esit fiyatta ulasim verisiyle esittiebici yapilir.
- Ulasim oncelikli secenek, olculmus yol suresini birinci, yol mesafesini ikinci siralama olcutu alir. Yol rotasi yoksa mevcut duz cizgi verisi acikca bu sekilde etiketlenerek kullanilir.
- Denge secenegi, en dusuk ayni para birimli tutarin en fazla %10 uzerindeki adaylar arasindan ulasim suresi en iyi olani secer; sure esitliginde yol mesafesi kullanilir. Karsilastirilabilir tutar yoksa denge secenegi hesaplanmaz.
- Ulasim/yolculuk suresi ve mesafesi her secenekte dikkate alinir ve avantaj-dezavantaj aciklamasinda gercek olculerle belirtilir.
- Yurumeye ait olculmus sure/mesafe varsa alternatif ulasim bilgisi olarak aciklamada gosterilir. Yurumeyle yolculuk olculeri farkli modlar oldugu icin birbirlerinin yerine kullanilmaz veya ayni siralama olcutu gibi kiyaslanmaz.
- Fiyat farki en dusuk tutarin %10'u veya altindaysa kucuk; %10'u asiyorsa belirgin kabul edilir. Kucuk farkta ulasim avantaji, belirgin farkta ucuz secenegin ulasim dezavantaji acikca belirtilir.
- Fiyat tutarlari kullanici isteginde verilen gecelik miktarlardir. API bu tutarlarin kaynagini veya rezervasyon onayini dogrulamiyorsa PDF, bunlarin rezervasyon teklifi/garantisi olmadigini belirtir.
- Farkli para birimleri arasinda kur saglanmadikca butce ve denge secenegi olusturulmaz; fiyat farki veya ortak fiyat siralamasi iddia edilmez. Tutarlar kendi para birimiyle gosterilir.
- Otel ozelligi, musaitlik, rezervasyon, fiyat garantisi, trafik veya girdi/saglayici sonucunda bulunmayan baska bir olgu varsayilmaz. Olculmemis yurumeye uygun rota uretilmez.
- Aciklamadaki sure ve mesafe PDF tablosuyla ayni birim ve yuvarlama kuralini kullanir.
- LLM yalnizca izinli gerekce kodlarini secer. Otel adi, fiyat, para birimi, metrikler ve nihai aciklama sunucunun kanonik rapor verisinden uretilir; serbest, dogrulanmamis LLM metni PDF'ye girmez.

## Constraints & out of scope
- Oneri politikasi acik ve deterministik olur; LLM otel secimini/siralamayi degistiremez.
- Farkli para birimlerini cevirmek icin kur verisi veya harici kur saglayicisi eklemek kapsam disidir.
- Girdi sozlesmesinde fiyat kaynagi/rezervasyon onay durumu bulunmuyorsa tum fiyatlari rezervasyon onayi olmayan, kullanici tarafindan saglanmis tutar olarak belirt.
- PDF uc secenegi sunar; JSON API degisikligi ancak PDF icin gerekli olmadigi kanitlanirsa yapilmaz. API degisikligi zorunlu cikarsa kapsam ve geriye uyumluluk tekrar gozen gecirilir.
- Kullanici karari (2026-09-27): uc politika ayri olacak; butce fiyati, ulasim rotayi, denge ikisini oncelikler.
- Kullanici karari (2026-09-27): %10 ve alti fiyat farki kucuk kabul edilir; denge en dusuk fiyatin en fazla %10 uzerindeki adaylar arasindan secilir.

## Acceptance criteria
- [ ] AC-1 PDF butce oncelikli, ulasim oncelikli ve denge seceneklerini ayri adlarla sunar; tek bir evrensel "en iyi" secim iddiasinda bulunmaz.
- [ ] AC-2 Butce secenegi en dusuk karsilastirilabilir fiyatli oteli; ulasim secenegi en iyi olculmus yol suresi ve esitlikte yol mesafesine gore belirler.
- [ ] AC-3 Denge secenegi en dusuk fiyatin %10 dahilindeki bandinda en iyi ulasim suresini secer; %10 esik siniri test edilir.
- [ ] AC-4 Yurumeye iliskin mevcut gercek mesafe/sure secenek aciklamasinda alternatif ulasim verisi olarak yer alir; olmayan deger uretilmez ve modlar birbirine karistirilmaz.
- [ ] AC-5 Kucuk fiyat farkinda ulasim avantajlari; belirgin farkta ucuz secenegin ulasim dezavantajlari dogru belirtilir.
- [ ] AC-6 Farkli para birimleri arasinda ortak fiyat sirasi/fark/denge secenegi sunulmaz; her tutar kendi para birimiyle gosterilir.
- [ ] AC-7 PDF, tutarlarin rezervasyon onayi olmadigini belirtir; sistem verilen tutari rezervasyon fiyati olarak sunmaz.
- [ ] AC-8 Sure/mesafe gosterimi PDF tablosuyla tutarlidir ve LLM kapali/hata fallback'i dahil ayni formatta kalir.
- [ ] AC-9 LLM yalniz izinli kod dondurur, secim veya metrikleri degistiremez; serbest LLM iddialari PDF'ye eklenmez ve mevcut JSON API sozlesmesi degismez.
- [ ] AC-10 Olmayan otel ozelligi, musaitlik, trafik, rezervasyon veya ulasim bilgisi varsayilmaz; aciklama dogal, kisa Turkceyle trade-off'u anlatir.

## Definition of Done
- [ ] Her kabul kriteri test veya tekrar edilebilir gozlemle kanitlanir.
- [ ] `scripts/check` yesildir.
- [ ] Bagimsiz inceleme yapilir; gercek bulgular giderilir.
- [ ] Guvenlik/mimari ve API belgeleri gerekiyorsa guncellenir.
- [ ] Spec `specs/done/` konumuna tasinir.

## Recommendation
Onerim, uc acik politika kuralini LLM'den bagimsiz uygulamak; LLM'i yalnizca bu seceneklerin kanita dayali gerekcelerini secmekte kullanmak; tum tutar ve olculeri sunucudan gelen verilerle yazmaktir. %10 siniri butceyle ulasim arasinda anlasilir ve tekrar edilebilir bir denge verir. Kullanici tarafindan girilmis tutarlari rezervasyon teyidi gibi gostermemek guvenilirlik icin gereklidir.
