# Değişiklik Günlüğü

🇬🇧 [English](CHANGELOG.md) · 🇹🇷 **Türkçe**

Bu projedeki önemli değişiklikler bu dosyada belgelenir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) temel alınarak hazırlanmıştır.

## [Yayımlanmadı]

### Eklenenler

- Arayüz için merkezi tema: renk, yazı, boşluk ve köşe yarıçapı değerleri; varsayılan açık palet, isteğe bağlı koyu
  palet ya da Windows uygulama modu (`Appearance:Theme` = `Light`, `Dark` veya `System`); Windows yüksek karşıtlık
  açıkken sistem renkleri değiştirilmeden kullanılır.
- Özgün uygulama logosu ve simgesi (`assets/logo.svg`, `assets/app.ico` olarak rasterleştirilir).
- `tools/Screenshots`: README görüntülerini uydurma örnek verilerle, pencere açmadan 1x ve 2x olarak üretir; simgeyi
  logodan oluşturur.
- Palet karşıtlığı (WCAG), değer ölçekleri, tema seçimi, her ekranın gösterilmeden oluşturulması, yerleşimin pencere
  içinde kalması, sekme sırası ve pencere içi mesajlar için testler.
- Kilo ve nabız için bir tarih aralığında GDI+ ile çizilen trend grafikleri ("Trend Grafikleri"); ölçüm sayısı,
  en düşük, en yüksek, ortalama ve geriye dönük hareketli ortalama (1-90 gün); aynı gündeki ölçümler birleştirilir.
- Hedefler ve uyarı eşikleri ("Hedefler"): hedef ve başlangıç kilosu, kilo ve nabız aralıkları (sınırlar dahil),
  kaydedilen ölçüm sınır dışındaysa uyarı ve hedef kiloya ilerleme. Yeni `Hedefler` tablosu (`AddHealthGoals`
  migration'ı).
- Hatırlatıcılar ("Hatırlatıcılar"): saklanan dönemsel ölçüm ve egzersiz hatırlatıcıları, uygulama açıkken bildirim
  alanından balon (dakikada bir denetlenir, hiçbir pencere öne getirilmez) ve kapalıyken kaçırılanların açılışta
  listelenmesi. Yeni `Hatirlaticilar` tablosu (`AddReminders` migration'ı).
- CSV dışa aktarmalı (BOM'lu UTF-8, `;`, formül enjeksiyonu koruması) ve "PDF olarak kaydet" için yazdırılabilir,
  tek dosyalık HTML raporlu haftalık / aylık dönem raporu ("Dönem Raporu"); dosyalar geçici bir dosyaya yazılıp
  yerine taşınır.
- Hareketli ortalama pencereleri, boş ve tek günlük aralıklar, aynı gün ölçümleri, tam sınırdaki değerler, hedef
  ilerlemesi, hatırlatıcı zamanları, CSV kaçışları, bölünmez dışa aktarma, yeni tabloların SQLite ve eski veritabanı
  yükseltmesi ile yeni ekranlarda son isteğin kazandığı yükleme ve çift tıklama koruması için testler.
- `%LOCALAPPDATA%\SaglikTakip\logs` altında Serilog ile günlük dönen dosya günlükleri (son 30 dosya saklanır).
  Sağlık verileri (kilo, nabız, notlar), kullanıcı adları ve istisna mesajları günlüğe yazılmaz.
- Business katmanında kullanıcı, sağlık kaydı ve egzersiz girdileri için merkezi FluentValidation doğrulayıcıları.
- Doğrulayıcı sınır testleri (yaş 0/150, kilo 1/200, nabız 1/250, ileri tarih, süre, tekrar, uzunluklar) ve sıkı
  Moq mock'larıyla servis testleri.
- Check kısıtları ve yabancı anahtarlar dahil, bellek içi SQLite üzerinde repository ve servis testleri.
- Günlük dosyalarında sağlık ve kişisel veri bulunmadığını denetleyen testler.
- CI'da sürüm işi: `v*` etiketlerinde win-x64 derlemesi yayımlanır ve zip çıktısı olarak yüklenir.
- Mimari diyagramlı İngilizce ve Türkçe README, tehdit modeli (`docs/THREAT_MODEL.md`) ve bu değişiklik günlüğü.

### Değişenler

- Nötr "Sağlık Takip" kimliğiyle yeni tasarım: resmi kurum izlenimi veren ad, arka plan görseli ve logo kaldırıldı.
  Tüm ekranlar aynı çerçeveyi (başlık, pencere içi mesaj şeridi, içerik kartları, altta eylemler) paylaşır; standart,
  boyutlandırılabilir pencere, en küçük boyut, monitör başına DPI ölçekleme, erişim tuşları ve Esc ile kapatma.
- Ana menü ekranları "Kayıtlar", "Takip ve analiz" ve "Planlama" başlıkları altında kutucuklar olarak gruplar.
- Trend grafiği tema renklerini, renk körlüğünde de ayırt edilen bir seri çiftini, hedef aralığı için bir bandı,
  yuvarlak eksen adımlarını, işaretli sınır dışı ölçümleri, son değer etiketini ve yükleniyor/boş durumlarını kullanır.
- Sonuç ve doğrulama mesajları ileti kutusu yerine pencerenin içinde gösterilir ve odak düzeltilecek alana taşınır;
  onaylar ve beklenmeyen hatalar yine iletişim kutusuyla sorulur.
- Rapor ekranı, önceden pencerenin dışında kalan kayıt listelerini ve son değerleri kartlar halinde gösterir.
- Yeni, kebab-case adlı ekran görüntüleri.
- Ana menüde yeni ekranlar için dört düğme daha var.
- Sağlık kaydı kaydedilirken kullanıcının hedef sınırları da denetlenir.
- Elle yazılmış doğrulama yardımcısı doğrulayıcılarla değiştirildi; mesajlar, kırpma ve denetim sırası aynıdır.
- Trace çıktısına yazılan hatalar artık günlük dosyasına yazılır.

### Düzeltilenler

- Cinsiyet check kısıtı SQL Server dışında sağlayıcıdan bağımsız değişmezler kullanır, böylece şema SQLite
  üzerinde oluşturulabilir. Migration'lar değişmedi.

## Katmanlı .NET 10 temeli - 2026-11-19 (#1)

### Eklenenler

- Katmanlı çözüm: `src/` altında `Domain`, `Business`, `Data` ve `UI` projeleri.
- Kullanıcı, sağlık kaydı, egzersiz ve rapor için doğrulama kurallı Business servisleri.
- Varlık yapılandırmaları, `InitialCreate` migration'ı ve kurgusal örnek verilerle EF Core veri erişimi;
  migration'lar açılışta uygulanır.
- Eski uygulamanın oluşturduğu veritabanlarının veriler korunarak yerinde yükseltilmesi; `sp_getapplock` kilidi
  vermezse yükseltme durur.
- `appsettings.json`, user secrets ve `SAGLIKTAKIP_CONNECTION` ortam değişkeninden yapılandırma; gerçek ayarlar
  yerine `appsettings.example.json` depoya eklenir.
- Türkçe mesajlarla genel hata yönetimi.
- Servisler, yapılandırma ve eski veritabanı yükseltmesi için test projeleri.
- Her push ve pull request'te derleyip test eden CI iş akışı.

### Değişenler

- Nullable referans türleriyle .NET 10 hedefleyen SDK tarzı projelere geçildi.
- Formlar doğrudan SQL çalıştırmak yerine bağımlılık enjeksiyonuyla Business servislerini çağırır.

### Düzeltilenler

- "Kaydet" düğmesine tekrar tıklamak artık yinelenen kayıt başlatmaz.
- Rapor ekranı yalnızca en son seçilen kullanıcının raporunu gösterir.
- Hata pencereleri sabit bir mesaj gösterir; yalnızca istisna türleri ve metotlar günlüğe yazılır.

### Kaldırılanlar

- Depoda izlenen derleme çıktısı, paketler ve veritabanı yedeği.
