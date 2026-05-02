namespace TarimSistemi.Models
{
    public class KullaniciProfilDto
    {
        public int KullaniciId { get; set; }
        public string AdSoyad { get; set; } = "";
        public string Email { get; set; } = "";
        public string? Telefon { get; set; }
        public DateTime KayitTarihi { get; set; }
        public DateTime? SonGirisTarihi { get; set; }
        public bool EmailOnayli { get; set; }

        /// <summary>Hesabım şifre değişikliği: e-posta onayı bekleniyor mu?</summary>
        public bool SifreDegisikligiOnayBekliyor { get; set; }

        /// <summary>Onay bağlantısının son geçerlilik zamanı (UTC).</summary>
        public DateTime? SifreOnaySonUtc { get; set; }
    }
}
