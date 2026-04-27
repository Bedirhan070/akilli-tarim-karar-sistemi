namespace TarimSistemi.Models
{
    public class Kullanici
    {
        public int KullaniciId { get; set; }
        public string AdSoyad { get; set; }
        public string Email { get; set; }
        public string SifreHash { get; set; }
        public string? Telefon { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public DateTime? SonGirisTarihi { get; set; }

        // İlişkiler
        public ICollection<Lokasyon> Lokasyonlar { get; set; }
        public ICollection<Oneri> Oneriler { get; set; }
    }
}