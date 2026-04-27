using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("Lokasyon")]
    public class Lokasyon
    {
        public int LokasyonId { get; set; }
        public int KullaniciId { get; set; }
        public string? Isim { get; set; }
        public string Sehir { get; set; }
        public string? Ilce { get; set; }
        public decimal Enlem { get; set; }
        public decimal Boylam { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        // İlişkiler
        public Kullanici Kullanici { get; set; }
        public ICollection<HavaVerisi> HavaVerileri { get; set; }
        public ICollection<Oneri> Oneriler { get; set; }
    }
}