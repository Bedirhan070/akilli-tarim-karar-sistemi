using Microsoft.EntityFrameworkCore;
using TarimSistemi.Models;

namespace TarimSistemi.Data
{
    public static class DbInitializer
    {
        public static void SeedUrunler(TarimDbContext context)
        {
            if (context.UrunBilgileri.AsNoTracking().Any())
                return;

            context.UrunBilgileri.AddRange(
                new UrunBilgisi
                {
                    UrunAdi = "Buğday",
                    IdealSicaklikMin = 5,
                    IdealSicaklikMax = 28,
                    IdealNemMin = 40,
                    IdealNemMax = 75,
                    EkimAylari = "10,11",
                    HasatSuresiGun = 240,
                    Aciklama = "Kışlık/ılıman iklim buğdayı için tipik aralıklar."
                },
                new UrunBilgisi
                {
                    UrunAdi = "Mısır",
                    IdealSicaklikMin = 10,
                    IdealSicaklikMax = 35,
                    IdealNemMin = 50,
                    IdealNemMax = 85,
                    EkimAylari = "4,5",
                    HasatSuresiGun = 120,
                    Aciklama = "Sıcak sezon ürünü; yüksek sıcaklıkta sulama önemlidir."
                },
                new UrunBilgisi
                {
                    UrunAdi = "Ayçiçeği",
                    IdealSicaklikMin = 8,
                    IdealSicaklikMax = 32,
                    IdealNemMin = 45,
                    IdealNemMax = 80,
                    EkimAylari = "3,4",
                    HasatSuresiGun = 110,
                    Aciklama = "Ilıman ilkbahar ekimi; kuraklığa karşı nem takibi."
                },
                new UrunBilgisi
                {
                    UrunAdi = "Pamuk",
                    IdealSicaklikMin = 15,
                    IdealSicaklikMax = 38,
                    IdealNemMin = 45,
                    IdealNemMax = 70,
                    EkimAylari = "4,5",
                    HasatSuresiGun = 150,
                    Aciklama = "Sıcak ve orta nem; aşırı yağış hasadı olumsuz etkiler."
                },
                new UrunBilgisi
                {
                    UrunAdi = "Arpa",
                    IdealSicaklikMin = 4,
                    IdealSicaklikMax = 26,
                    IdealNemMin = 40,
                    IdealNemMax = 75,
                    EkimAylari = "10,11",
                    HasatSuresiGun = 220,
                    Aciklama = "Buğdaya benzer; don riskine duyarlı."
                },
                new UrunBilgisi
                {
                    UrunAdi = "Domates (Sera)",
                    IdealSicaklikMin = 12,
                    IdealSicaklikMax = 30,
                    IdealNemMin = 55,
                    IdealNemMax = 85,
                    EkimAylari = "2,3,4",
                    HasatSuresiGun = 90,
                    Aciklama = "Sera veya erken dışarı; nem ve havalandırma kritik."
                });

            context.SaveChanges();
        }
    }
}
