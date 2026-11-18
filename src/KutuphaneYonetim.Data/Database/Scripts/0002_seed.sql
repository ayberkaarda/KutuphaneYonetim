-- Sample data. Only inserted into an empty database so existing data is never touched.

IF NOT EXISTS (SELECT 1 FROM dbo.Kitaplar) AND NOT EXISTS (SELECT 1 FROM dbo.Uyeler)
BEGIN
    INSERT INTO dbo.Kitaplar (ad, yazar, durum) VALUES
        (N'1984', N'George Orwell', 1),
        (N'Hayvan Çiftliği', N'George Orwell', 1),
        (N'Simyacı', N'Paulo Coelho', 1),
        (N'Kürk Mantolu Madonna', N'Sabahattin Ali', 1),
        (N'Suç ve Ceza', N'Fyodor Dostoyevski', 1),
        (N'Sefiller', N'Victor Hugo', 1),
        (N'Tutunamayanlar', N'Oğuz Atay', 1),
        (N'İnce Memed', N'Yaşar Kemal', 1);

    INSERT INTO dbo.Uyeler (ad, soyad) VALUES
        (N'Örnek', N'Üye'),
        (N'Deneme', N'Kullanıcı'),
        (N'Test', N'Okur');

    DECLARE @book1 INT = (SELECT MIN(kitap_id) FROM dbo.Kitaplar WHERE ad = N'1984');
    DECLARE @book2 INT = (SELECT MIN(kitap_id) FROM dbo.Kitaplar WHERE ad = N'Simyacı');
    DECLARE @member1 INT = (SELECT MIN(uye_id) FROM dbo.Uyeler WHERE ad = N'Örnek');
    DECLARE @member2 INT = (SELECT MIN(uye_id) FROM dbo.Uyeler WHERE ad = N'Deneme');

    -- One returned loan and one active loan to demonstrate date tracking.
    INSERT INTO dbo.OduncIslemleri (kitap_id, uye_id, odunc_tarihi, iade_tarihi) VALUES
        (@book1, @member1, DATEADD(DAY, -14, GETDATE()), DATEADD(DAY, -3, GETDATE())),
        (@book2, @member2, DATEADD(DAY, -2, GETDATE()), NULL);

    UPDATE dbo.Kitaplar SET durum = 0 WHERE kitap_id = @book2;
END
GO
