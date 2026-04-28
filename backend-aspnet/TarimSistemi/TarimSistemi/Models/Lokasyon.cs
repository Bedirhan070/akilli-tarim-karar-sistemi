using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("Lokasyon")]
    public class Lokasyon
    {
        [Key]
        [Column("lokasyonId")]
        public int LokasyonId { get; set; }

        [Column("kullaniciId")]
        public int KullaniciId { get; set; }

        [Column("isim")]
        public string? Isim { get; set; }

        [Column("sehir")]
        public string Sehir { get; set; } = "";

        [Column("ilce")]
        public string? Ilce { get; set; }

        [Column("enlem")]
        public decimal Enlem { get; set; }

        [Column("boylam")]
        public decimal Boylam { get; set; }

        [Column("kayitTarihi")]
        public DateTime KayitTarihi { get; set; } = DateTime.Now;

        // İlişkiler
        public Kullanici Kullanici { get; set; }
        public ICollection<HavaVerisi> HavaVerileri { get; set; }
        public ICollection<Oneri> Oneriler { get; set; }
    }
}