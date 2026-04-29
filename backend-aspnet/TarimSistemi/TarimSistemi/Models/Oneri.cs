using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("Oneri")]
    public class Oneri
    {
        [Key]
        [Column("oneriId")]
        public int OneriId { get; set; }

        [Column("kullaniciId")]
        public int KullaniciId { get; set; }

        [Column("lokasyonId")]
        public int LokasyonId { get; set; }

        [Column("urunId")]
        public int? UrunId { get; set; }

        [Column("riskSkoru")]
        public decimal? RiskSkoru { get; set; }

        [Column("riskTipi")]
        public string? RiskTipi { get; set; }

        [Column("tavsiyeMetni")]
        public string? TavsiyeMetni { get; set; }

        public DateTime OlusturulmaZamani { get; set; } = DateTime.Now;

        // İlişkiler
        public Kullanici Kullanici { get; set; }
        public Lokasyon Lokasyon { get; set; }

        [ForeignKey("UrunId")]
        public UrunBilgisi? UrunBilgisi { get; set; }
    }
}