using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("KullaniciGeriBildirim")]
    public class KullaniciGeriBildirim
    {
        [Key]
        [Column("geriBildirimId")]
        public int GeriBildirimId { get; set; }

        [Column("kullaniciId")]
        public int KullaniciId { get; set; }

        [Column("konu")]
        public string? Konu { get; set; }

        [Column("mesaj")]
        public string? Mesaj { get; set; }

        [Column("tarih")]
        public DateTime Tarih { get; set; } = DateTime.Now;

        [Column("durum")]
        public string? Durum { get; set; }

        // İlişki
        public Kullanici Kullanici { get; set; }
    }
}