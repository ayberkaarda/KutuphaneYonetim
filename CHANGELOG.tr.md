# Değişiklik Günlüğü

🇬🇧 [English](CHANGELOG.md) · 🇹🇷 **Türkçe**

Bu projedeki önemli değişiklikler bu dosyada belgelenir.
Biçim [Keep a Changelog](https://keepachangelog.com/tr-TR/1.1.0/) esas alınarak hazırlanmıştır.

## [Yayımlanmamış]

### Eklenenler

- Serilog ile `%LOCALAPPDATA%\KutuphaneYonetim\logs` altında günlük dönen dosya günlükleri (son 30 dosya saklanır).
  İstisna mesajları ve kişisel veriler günlüğe yazılmaz.
- Business katmanında kitap, üye ve ödünç girdileri için merkezi FluentValidation doğrulayıcıları.
- Business servisleri için Moq ile birim testleri: ödünç çakışması, iade, silme kuralları ve doğrulama.
- SQLite bellek içi veritabanında repository ve servis testleri.
- Türkçe hata mesajları ve günlük temizleme için testler.
- CI'da sürüm işi: `v*` etiketlerinde win-x64 çıktısı yayımlanır ve zip olarak yüklenir.
- Mimari diyagramlı İngilizce ve Türkçe README, tehdit modeli (`docs/THREAT_MODEL.md`) ve bu değişiklik günlüğü.

### Değişenler

- Elle yazılmış doğrulama yardımcısı doğrulayıcılarla değiştirildi; mesajlar ve kırpma davranışı aynı kaldı.

### Düzeltilenler

- Beklenmeyen hata pencereleri artık ham (çoğunlukla İngilizce) istisna mesajını göstermiyor; Türkçe bir mesaj
  gösterip günlük dosyasına yönlendiriyor.

## Katmanlı .NET 10 altyapısı - 2026-11-19 (#1)

### Eklenenler

- Katmanlı çözüm: `src/` altında `Domain`, `Business`, `Data` ve `UI` projeleri.
- Kurallı Business servisleri (`BookService`, `MemberService`, `LoanService`): bir kitap aynı anda iki kez ödünç
  verilemez; açık ödüncü ya da ödünç geçmişi olan kitap ve üye silinemez.
- Varlık yapılandırmaları, `InitialCreate` migration'ı ve örnek verilerle EF Core veri erişimi; migration'lar
  açılışta uygulanır.
- Özgün uygulamanın oluşturduğu veritabanlarının verisi korunarak yerinde yükseltilmesi.
- `appsettings.json`, user secrets ve `KUTUPHANE_CONNECTION` ortam değişkeninden yapılandırma; gerçek ayarlar
  yerine `appsettings.example.json` depoya eklendi.
- Türkçe mesajlı merkezi hata yönetimi.
- Örnek yapılandırma ve eski veritabanı yükseltmesi için test projeleri.
- Her push ve pull request'te derleyip test eden CI iş akışı.

### Değişenler

- Projeler .NET 10 hedefli, nullable başvuru türleri açık SDK biçimine dönüştürüldü.
- Formlar doğrudan SQL çalıştırmak yerine bağımlılık enjeksiyonuyla Business servislerini çağırıyor.
- README İngilizce olarak yeniden yazıldı, Türkçe README eklendi.

### Düzeltilenler

- Örnek bağlantı cümlesindeki ters eğik çizgi doğru kaçışla yazıldı.

### Kaldırılanlar

- Depoda izlenen derleme çıktıları, IDE önbellekleri ve veritabanı yedeği.
