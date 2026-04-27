namespace TarimSistemi.Models
{
    public class KullaniciGeriBildirim
    {
        public int GeribildirimId { get; set; }
        public int OneriId { get; set; }
        public int KullaniciId { get; set; }
        public int? Puan { get; set; }
        public string? Yorum { get; set; }
        public DateTime KayitZamani { get; set; } = DateTime.Now;

        // İlişkiler
        public Oneri Oneri { get; set; }
        public Kullanici Kullanici { get; set; }
    }
}