# Kütüphane Yönetim Sistemi

.NET 10 ve SQL Server ile geliştirilmiş bir Windows Forms kütüphane yönetim uygulaması. Kitapları,
üyeleri ve ödünç işlemlerini (ödünç verme / iade, tarihleriyle birlikte) takip eder.

English: [README.md](README.md)

## Özellikler

- **Kitaplar**: kitapları listeleme, ekleme ve silme.
- **Üyeler**: üyeleri listeleme, ekleme ve silme.
- **Ödünç**: üyeye kitap ödünç verme, iade alma, ödünç ve iade tarihlerini görme.
- İş kuralları: aynı kitap aynı anda iki üyeye ödünç verilemez; açık ödüncü (veya ödünç geçmişi)
  olan kitap ya da üye silinemez.

## Mimari

```
src/
  KutuphaneYonetim.Domain    Varlıklar ve repository arayüzleri
  KutuphaneYonetim.Business  BookService, MemberService, LoanService (iş kuralları)
  KutuphaneYonetim.Data      EF Core DbContext, varlık yapılandırmaları, migration'lar, repository'ler
  KutuphaneYonetim.UI        WinForms ekranları; Program.cs bağımlılıkları kurar (DI)
```

- Veri erişimi EF Core (SQL Server) ile yapılır; tüm erişim asenkron ve parametrelidir.
- Uygulama açılışta bekleyen migration'ları uygular, veritabanı yoksa oluşturur.
- Beklenmeyen hatalar merkezi olarak yakalanır ve kullanıcıya anlaşılır bir mesajla gösterilir.

## Kurulum

1. Bağlantı cümlesini aşağıdakilerden biriyle verin (sonraki öncekini ezer):
   - `src/KutuphaneYonetim.UI/appsettings.example.json` dosyasını aynı klasöre `appsettings.json`
     olarak kopyalayıp `ConnectionStrings:Kutuphane` değerini düzenleyin (`appsettings.json` git'e girmez).
   - User secrets:
     `dotnet user-secrets set "ConnectionStrings:Kutuphane" "<bağlantı cümlesi>" --project src/KutuphaneYonetim.UI`
   - `KUTUPHANE_CONNECTION` ortam değişkeni.
2. Derleyip çalıştırın:
   ```
   dotnet build -c Release
   dotnet run --project src/KutuphaneYonetim.UI
   ```
   İlk açılışta veritabanı ve örnek veriler oluşturulur.

## Lisans

Kişisel ve eğitimsel kullanım için ücretsizdir. Ticari amaçla kullanılması uygun değildir.
