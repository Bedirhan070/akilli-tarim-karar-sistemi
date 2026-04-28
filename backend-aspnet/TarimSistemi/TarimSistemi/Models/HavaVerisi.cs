using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("HavaVerisi")]
    public class HavaVerisi
    {
        [Key]
        [Column("havaVeriId")]
        public int HavaVeriId { get; set; }

        [Column("lokasyonId")]
        public int LokasyonId { get; set; }

        [Column("tarih")]
        public DateTime Tarih { get; set; }

        [Column("sicaklikMax")]
        public decimal? SicaklikMax { get; set; }

        [Column("sicaklikMin")]
        public decimal? SicaklikMin { get; set; }

        [Column("nem")]
        public decimal? Nem { get; set; }

        [Column("yagis")]
        public decimal? Yagis { get; set; }

        [Column("ruzgarHizi")]
        public decimal? RuzgarHizi { get; set; }

        [Column("apiKaynagi")]
        public string? ApiKaynagi { get; set; }

        [Column("anomaliDurumu")]
        public bool AnomaliDurumu { get; set; } = false;

        [Column("kayitZamani")]
        public DateTime KayitZamani { get; set; } = DateTime.Now;

        // İlişki
        public Lokasyon Lokasyon { get; set; }
    }
}