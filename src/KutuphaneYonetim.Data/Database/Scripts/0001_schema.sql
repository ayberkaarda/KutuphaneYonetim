-- Library schema. Mirrors the original database (Kitaplar, Uyeler, OduncIslemleri)
-- and is idempotent so it can run against an existing, restored database.

IF OBJECT_ID(N'dbo.Kitaplar', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Kitaplar
    (
        kitap_id INT IDENTITY(1, 1) NOT NULL,
        ad       NVARCHAR(100)      NOT NULL,
        yazar    NVARCHAR(100)      NOT NULL,
        durum    BIT                NOT NULL CONSTRAINT DF_Kitaplar_durum DEFAULT (1),
        CONSTRAINT PK_Kitaplar PRIMARY KEY CLUSTERED (kitap_id)
    );
END
GO

IF OBJECT_ID(N'dbo.Uyeler', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Uyeler
    (
        uye_id INT IDENTITY(1, 1) NOT NULL,
        ad     NVARCHAR(50)       NOT NULL,
        soyad  NVARCHAR(50)       NOT NULL,
        CONSTRAINT PK_Uyeler PRIMARY KEY CLUSTERED (uye_id)
    );
END
GO

IF OBJECT_ID(N'dbo.OduncIslemleri', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OduncIslemleri
    (
        odunc_id     INT IDENTITY(1, 1) NOT NULL,
        kitap_id     INT                NOT NULL,
        uye_id       INT                NOT NULL,
        odunc_tarihi DATETIME           NOT NULL,
        iade_tarihi  DATETIME           NULL,
        CONSTRAINT PK_OduncIslemleri PRIMARY KEY CLUSTERED (odunc_id),
        CONSTRAINT FK_OduncIslemleri_Kitaplar FOREIGN KEY (kitap_id) REFERENCES dbo.Kitaplar (kitap_id),
        CONSTRAINT FK_OduncIslemleri_Uyeler FOREIGN KEY (uye_id) REFERENCES dbo.Uyeler (uye_id),
        CONSTRAINT CK_OduncIslemleri_iade_tarihi CHECK (iade_tarihi IS NULL OR iade_tarihi >= odunc_tarihi)
    );
END
GO

-- Lookup indexes for the foreign key columns.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OduncIslemleri_kitap_id' AND object_id = OBJECT_ID(N'dbo.OduncIslemleri'))
    CREATE NONCLUSTERED INDEX IX_OduncIslemleri_kitap_id ON dbo.OduncIslemleri (kitap_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OduncIslemleri_uye_id' AND object_id = OBJECT_ID(N'dbo.OduncIslemleri'))
    CREATE NONCLUSTERED INDEX IX_OduncIslemleri_uye_id ON dbo.OduncIslemleri (uye_id);
GO

-- A book can have at most one open (not yet returned) loan at a time.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_OduncIslemleri_active_kitap' AND object_id = OBJECT_ID(N'dbo.OduncIslemleri'))
    CREATE UNIQUE NONCLUSTERED INDEX UX_OduncIslemleri_active_kitap
        ON dbo.OduncIslemleri (kitap_id)
        WHERE iade_tarihi IS NULL;
GO
