using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("Oneri")]
    public class Oneri
    {
        public int OneriId { get; set; }
        public int KullaniciId { get; set; }
        public int LokasyonId { get; set; }
        public int? UrunId { get; set; }
        public decimal? RiskSkoru { get; set; }
        public string? RiskTipi { get; set; }
        public string? TavsiyeMetni { get; set; }
        public DateTime OlusturulmaZamani { get; set; } = DateTime.Now;

        // İlişkiler
        public Kullanici Kullanici { get; set; }
        public Lokasyon Lokasyon { get; set; }
        public UrunBilgisi? UrunBilgisi { get; set; }
        public ICollection<KullaniciGeriBildirim> GeriBildirimler { get; set; }
    }
}