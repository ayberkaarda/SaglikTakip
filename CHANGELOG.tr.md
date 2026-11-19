# Değişiklik Günlüğü

🇬🇧 [English](CHANGELOG.md) · 🇹🇷 **Türkçe**

Bu projedeki önemli değişiklikler bu dosyada belgelenir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) temel alınarak hazırlanmıştır.

## [Yayımlanmadı]

### Eklenenler

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
