using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TarimSistemi.Models
{
    [Table("Kullanici")]
    public class Kullanici
    {
        [Key]
        public int KullaniciId { get; set; }
        public string AdSoyad { get; set; } = "";
        public string Email { get; set; } = "";
        public string SifreHash { get; set; } = "";
        public string? Telefon { get; set; }
        public DateTime KayitTarihi { get; set; } = DateTime.Now;
        public DateTime? SonGirisTarihi { get; set; }

        /// <summary>Kayıt / e-posta yenileme sonrası doğrulanana kadar false.</summary>
        public bool EmailOnayli { get; set; }

        public string? EmailOnayToken { get; set; }
        public DateTime? EmailOnayTokenSon { get; set; }

        /// <summary>E-posta ile onaylanacak yeni şifre özeti (BCrypt).</summary>
        public string? BekleyenSifreHash { get; set; }
        public string? SifreOnayToken { get; set; }
        public DateTime? SifreOnayTokenSon { get; set; }

        /// <summary>Şifremi unuttum e-postasındaki tek kullanımlık bağlantı.</summary>
        public string? SifreSifirlamaToken { get; set; }
        public DateTime? SifreSifirlamaTokenSon { get; set; }

        // İlişkiler
        public ICollection<Lokasyon> Lokasyonlar { get; set; }
        public ICollection<Oneri> Oneriler { get; set; }
    }
}