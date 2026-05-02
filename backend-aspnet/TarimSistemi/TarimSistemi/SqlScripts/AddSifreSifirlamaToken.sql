-- Şifremi unuttum: e-posta ile sıfırlama token alanları

IF COL_LENGTH('dbo.Kullanici', 'SifreSifirlamaToken') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD SifreSifirlamaToken NVARCHAR(128) NULL;
END
GO

IF COL_LENGTH('dbo.Kullanici', 'SifreSifirlamaTokenSon') IS NULL
BEGIN
    ALTER TABLE dbo.Kullanici ADD SifreSifirlamaTokenSon DATETIME2 NULL;
END
GO
