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
        }
    }
}