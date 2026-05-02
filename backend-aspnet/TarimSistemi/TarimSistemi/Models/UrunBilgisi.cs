using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("UrunBilgisi")]
    public class UrunBilgisi
    {
        [Key]
        [Column("urunId")]
        public int UrunId { get; set; }

        [Column("urunAdi")]
        public string UrunAdi { get; set; } = "";

        [Column("idealSicaklikMax")]
        public decimal? IdealSicaklikMax { get; set; }

        [Column("idealSicaklikMin")]
        public decimal? IdealSicaklikMin { get; set; }

        [Column("idealNemMax")]
        public decimal? IdealNemMax { get; set; }

        [Column("idealNemMin")]
        public decimal? IdealNemMin { get; set; }

        [Column("ekimAylari")]
        public string? EkimAylari { get; set; }

        [Column("hasatSuresiGun")]
        public int? HasatSuresiGun { get; set; }

        [Column("aciklama")]
        public string? Aciklama { get; set; }

        // İlişki
        public ICollection<Oneri> Oneriler { get; set; }
        public ICollection<Lokasyon> Lokasyonlar { get; set; } = new List<Lokasyon>();
    }
}