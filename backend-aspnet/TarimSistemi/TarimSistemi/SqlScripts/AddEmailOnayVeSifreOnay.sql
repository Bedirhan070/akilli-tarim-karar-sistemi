-- Kullanici: e-posta doğrulama ve şifre değişikliği e-posta onayı alanları
-- Mevcut kullanıcılar doğrulanmış sayılır (EmailOnayli = 1).

IF COL_LENGTH('dbo.Kullanici', 'EmailOnayli') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD EmailOnayli BIT NOT NULL
        CONSTRAINT DF_Kullanici_EmailOnayli DEFAULT (1);
END
GO

IF COL_LENGTH('dbo.Kullanici', 'EmailOnayToken') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD EmailOnayToken NVARCHAR(128) NULL;
END
GO

IF COL_LENGTH('dbo.Kullanici', 'EmailOnayTokenSon') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD EmailOnayTokenSon DATETIME2 NULL;
END
GO

IF COL_LENGTH('dbo.Kullanici', 'BekleyenSifreHash') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD BekleyenSifreHash NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH('dbo.Kullanici', 'SifreOnayToken') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD SifreOnayToken NVARCHAR(128) NULL;
END
GO

IF COL_LENGTH('dbo.Kullanici', 'SifreOnayTokenSon') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD SifreOnayTokenSon DATETIME2 NULL;
END
GO
