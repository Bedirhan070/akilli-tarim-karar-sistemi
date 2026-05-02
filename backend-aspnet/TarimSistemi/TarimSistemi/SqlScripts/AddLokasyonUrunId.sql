-- Mevcut veritabanına tarla-ürün ilişkisi kolonu ekler. Bir kez çalıştırın.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Lokasyon]') AND name = N'urunId'
)
BEGIN
    ALTER TABLE [dbo].[Lokasyon] ADD [urunId] INT NULL;

    ALTER TABLE [dbo].[Lokasyon]
    ADD CONSTRAINT [FK_Lokasyon_UrunBilgisi]
    FOREIGN KEY ([urunId]) REFERENCES [dbo].[UrunBilgisi]([urunId]);
END
GO
