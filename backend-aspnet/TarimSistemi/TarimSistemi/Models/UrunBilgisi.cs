using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("UrunBilgisi")]
    public class UrunBilgisi
    {
        [Key]
        public int UrunId { get; set; }
        public string UrunAdi { get; set; }
        public decimal? IdealSicaklikMin { get; set; }
        public decimal? IdealSicaklikMax { get; set; }
        public decimal? IdealNemMin { get; set; }
        public decimal? IdealNemMax { get; set; }
        public string? EkimAylari { get; set; }
        public int? HasatSuresiGun { get; set; }
        public string? Aciklama { get; set; }

        // İlişki
        public ICollection<Oneri> Oneriler { get; set; }
    }
}