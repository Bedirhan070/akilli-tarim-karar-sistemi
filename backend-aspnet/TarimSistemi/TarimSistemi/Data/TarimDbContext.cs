using Microsoft.EntityFrameworkCore;
using TarimSistemi.Models;

namespace TarimSistemi.Data
{
    public class TarimDbContext : DbContext
    {
        public TarimDbContext(DbContextOptions<TarimDbContext> options)
            : base(options)
        {
        }

        public DbSet<Kullanici> Kullanicilar { get; set; }
        public DbSet<Lokasyon> Lokasyonlar { get; set; }
        public DbSet<HavaVerisi> HavaVerileri { get; set; }
        public DbSet<UrunBilgisi> UrunBilgileri { get; set; }
        public DbSet<Oneri> Oneriler { get; set; }
        public DbSet<KullaniciGeriBildirim> KullaniciGeriBildirimler { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<HavaVerisi>()
                .HasIndex(h => new { h.LokasyonId, h.Tarih })
                .IsUnique();

            modelBuilder.Entity<Lokasyon>()
                .HasOne(l => l.UrunBilgisi)
                .WithMany(u => u.Lokasyonlar)
                .HasForeignKey(l => l.UrunId)
                .OnDelete(DeleteBehavior.SetNull);

            // Koordinatlar: 9 basamak, 6 ondalık (±180.000000 yeterli)
            modelBuilder.Entity<Lokasyon>()
                .Property(l => l.Enlem).HasPrecision(9, 6);
            modelBuilder.Entity<Lokasyon>()
                .Property(l => l.Boylam).HasPrecision(9, 6);

            // Hava verisi ölçümleri
            modelBuilder.Entity<HavaVerisi>()
                .Property(h => h.SicaklikMax).HasPrecision(5, 2);
            modelBuilder.Entity<HavaVerisi>()
                .Property(h => h.SicaklikMin).HasPrecision(5, 2);
            modelBuilder.Entity<HavaVerisi>()
                .Property(h => h.Nem).HasPrecision(5, 2);
            modelBuilder.Entity<HavaVerisi>()
                .Property(h => h.Yagis).HasPrecision(7, 2);
            modelBuilder.Entity<HavaVerisi>()
                .Property(h => h.RuzgarHizi).HasPrecision(6, 2);

            // Risk skoru: 0.00–1.00 arası
            modelBuilder.Entity<Oneri>()
                .Property(o => o.RiskSkoru).HasPrecision(5, 2);

            // Ürün idealleri: sıcaklık/nem değerleri
            modelBuilder.Entity<UrunBilgisi>()
                .Property(u => u.IdealSicaklikMin).HasPrecision(5, 2);
            modelBuilder.Entity<UrunBilgisi>()
                .Property(u => u.IdealSicaklikMax).HasPrecision(5, 2);
            modelBuilder.Entity<UrunBilgisi>()
                .Property(u => u.IdealNemMin).HasPrecision(5, 2);
            modelBuilder.Entity<UrunBilgisi>()
                .Property(u => u.IdealNemMax).HasPrecision(5, 2);
        }
    }
}