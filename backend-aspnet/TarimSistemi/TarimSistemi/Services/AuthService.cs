using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TarimSistemi.Configuration;
using TarimSistemi.Data;
using TarimSistemi.Models;

namespace TarimSistemi.Services
{
    public class AuthService
    {
        private readonly TarimDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailGonderici _emailGonderici;
        private readonly EmailAyarlari _emailAyarlari;

        public AuthService(
            TarimDbContext context,
            IConfiguration configuration,
            IEmailGonderici emailGonderici,
            IOptions<EmailAyarlari> emailAyarlari)
        {
            _context = context;
            _configuration = configuration;
            _emailGonderici = emailGonderici;
            _emailAyarlari = emailAyarlari.Value;
        }

        private string PublicBaseUrl
        {
            get
            {
                var u = (_emailAyarlari.PublicBaseUrl ?? "").Trim().TrimEnd('/');
                return string.IsNullOrEmpty(u) ? "https://localhost" : u;
            }
        }

        private static string UretUrlToken() =>
            WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        public async Task<(bool Success, string Message)> KayitOl(string adSoyad, string email, string sifre, string? telefon)
        {
            var emailNorm = email.Trim().ToLowerInvariant();
            var mevcut = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Email.ToLower() == emailNorm);

            if (mevcut != null)
            {
                if (mevcut.EmailOnayli)
                    return (false, "Bu e-posta adresi zaten kayıtlı.");

                mevcut.AdSoyad = adSoyad.Trim();
                mevcut.SifreHash = BCrypt.Net.BCrypt.HashPassword(sifre);
                mevcut.Telefon = string.IsNullOrWhiteSpace(telefon) ? null : telefon.Trim();
                mevcut.EmailOnayToken = UretUrlToken();
                mevcut.EmailOnayTokenSon = DateTime.UtcNow.AddHours(48);
                await _context.SaveChangesAsync();
                await KayitOnayMailiGonder(mevcut);
                return (true, "Bu e-posta ile doğrulanmamış bir kayıt vardı; bilgiler güncellendi ve doğrulama bağlantısı e-postanıza gönderildi.");
            }

            var kullanici = new Kullanici
            {
                AdSoyad = adSoyad.Trim(),
                Email = emailNorm,
                SifreHash = BCrypt.Net.BCrypt.HashPassword(sifre),
                Telefon = string.IsNullOrWhiteSpace(telefon) ? null : telefon.Trim(),
                KayitTarihi = DateTime.Now,
                EmailOnayli = false,
                EmailOnayToken = UretUrlToken(),
                EmailOnayTokenSon = DateTime.UtcNow.AddHours(48)
            };

            _context.Kullanicilar.Add(kullanici);
            await _context.SaveChangesAsync();
            await KayitOnayMailiGonder(kullanici);

            return (true, "Kayıt oluşturuldu. E-postanıza gönderilen doğrulama bağlantısına tıklayarak hesabınızı aktifleştirin.");
        }

        private async Task KayitOnayMailiGonder(Kullanici k)
        {
            var link = $"{PublicBaseUrl}/Home/EmailOnay?token={Uri.EscapeDataString(k.EmailOnayToken!)}";
            var html = $@"
<p>Merhaba {System.Net.WebUtility.HtmlEncode(k.AdSoyad)},</p>
<p>Akıllı Tarım hesabınızı doğrulamak için aşağıdaki bağlantıya tıklayın:</p>
<p><a href=""{link}"">{link}</a></p>
<p>Bu bağlantı 48 saat geçerlidir. Siz istemediyseniz bu e-postayı yok sayabilirsiniz.</p>";

            await _emailGonderici.GonderAsync(k.Email, "E-posta doğrulama — Akıllı Tarım", html);
        }

        public async Task<(bool Ok, string Message)> OnaylaKayitEmailiAsync(string token)
        {
            token = Uri.UnescapeDataString(token ?? "").Trim();
            if (string.IsNullOrEmpty(token))
                return (false, "Geçersiz doğrulama bağlantısı.");

            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.EmailOnayToken == token);
            if (k == null)
                return (false, "Bağlantı geçersiz veya zaten kullanılmış.");

            if (k.EmailOnayTokenSon == null || k.EmailOnayTokenSon < DateTime.UtcNow)
                return (false, "Doğrulama süresi dolmuş. Giriş sayfasından yeni doğrulama e-postası isteyin.");

            k.EmailOnayli = true;
            k.EmailOnayToken = null;
            k.EmailOnayTokenSon = null;
            await _context.SaveChangesAsync();
            return (true, "E-posta adresiniz doğrulandı. Şimdi giriş yapabilirsiniz.");
        }

        public async Task<string> EmailDogrulamaYenidenGonder(string email)
        {
            var emailNorm = email.Trim().ToLowerInvariant();
            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email.ToLower() == emailNorm);
            if (k == null || k.EmailOnayli)
                return "İsteğiniz alındı. Bu adrese ait doğrulanmamış hesap varsa yeni bir e-posta gönderilir.";

            k.EmailOnayToken = UretUrlToken();
            k.EmailOnayTokenSon = DateTime.UtcNow.AddHours(48);
            await _context.SaveChangesAsync();
            await KayitOnayMailiGonder(k);
            return "Doğrulama e-postası gönderildi. Gelen kutunuzu ve spam klasörünü kontrol edin.";
        }

        public async Task<(bool Success, string? Token, string Message)> GirisYap(string email, string sifre)
        {
            var emailNorm = email.Trim().ToLowerInvariant();
            var kullanici = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Email.ToLower() == emailNorm);

            if (kullanici == null || !BCrypt.Net.BCrypt.Verify(sifre, kullanici.SifreHash))
                return (false, null, "E-posta veya şifre hatalı.");

            if (!kullanici.EmailOnayli)
                return (false, null, "E-posta adresiniz henüz doğrulanmadı. Kayıt sırasında gönderilen bağlantıya tıklayın veya giriş sayfasından doğrulama e-postasını yeniden isteyin.");

            kullanici.SonGirisTarihi = DateTime.Now;
            await _context.SaveChangesAsync();

            return (true, BuildJwtToken(kullanici), "Giriş başarılı");
        }

        public async Task<KullaniciProfilDto?> GetProfilAsync(int kullaniciId)
        {
            return await _context.Kullanicilar.AsNoTracking()
                .Where(k => k.KullaniciId == kullaniciId)
                .Select(k => new KullaniciProfilDto
                {
                    KullaniciId = k.KullaniciId,
                    AdSoyad = k.AdSoyad,
                    Email = k.Email,
                    Telefon = k.Telefon,
                    KayitTarihi = k.KayitTarihi,
                    SonGirisTarihi = k.SonGirisTarihi,
                    EmailOnayli = k.EmailOnayli,
                    SifreDegisikligiOnayBekliyor = k.BekleyenSifreHash != null && k.SifreOnayToken != null,
                    SifreOnaySonUtc = k.SifreOnayTokenSon
                })
                .FirstOrDefaultAsync();
        }

        public async Task<(bool Ok, string Message, string? NewToken)> GuncelleProfilAsync(int kullaniciId, string adSoyad, string? telefon)
        {
            adSoyad = adSoyad?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(adSoyad))
                return (false, "Ad soyad boş olamaz", null);

            var kullanici = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.KullaniciId == kullaniciId);
            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı", null);

            kullanici.AdSoyad = adSoyad;
            kullanici.Telefon = string.IsNullOrWhiteSpace(telefon) ? null : telefon.Trim();
            await _context.SaveChangesAsync();

            return (true, "Bilgileriniz güncellendi", BuildJwtToken(kullanici));
        }

        /// <summary>Mevcut şifreyi doğrular, yeni şifreyi bekleyen alana yazar ve onay e-postası gönderir.</summary>
        public async Task<(bool Ok, string Message)> SifreDegisikligiBaslatAsync(int kullaniciId, string mevcutSifre, string yeniSifre)
        {
            if (string.IsNullOrEmpty(yeniSifre) || yeniSifre.Length < 6)
                return (false, "Yeni şifre en az 6 karakter olmalıdır");

            var kullanici = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.KullaniciId == kullaniciId);
            if (kullanici == null)
                return (false, "Kullanıcı bulunamadı");

            if (!BCrypt.Net.BCrypt.Verify(mevcutSifre, kullanici.SifreHash))
                return (false, "Mevcut şifre hatalı");

            kullanici.BekleyenSifreHash = BCrypt.Net.BCrypt.HashPassword(yeniSifre);
            kullanici.SifreOnayToken = UretUrlToken();
            kullanici.SifreOnayTokenSon = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();

            await SifreDegisiklikOnayMailiGonder(kullanici);

            return (true, "E-postanıza onay bağlantısı gönderildi. Mesajı açıp bağlantıya tıklayana kadar mevcut şifreniz geçerlidir. Görmezseniz spam klasörüne bakın; Hesabım sayfasından e-postayı yeniden gönderebilirsiniz.");
        }

        private async Task SifreDegisiklikOnayMailiGonder(Kullanici kullanici)
        {
            var link = $"{PublicBaseUrl}/Home/SifreEmailOnay?token={Uri.EscapeDataString(kullanici.SifreOnayToken!)}";
            var html = $@"
<p>Merhaba {System.Net.WebUtility.HtmlEncode(kullanici.AdSoyad)},</p>
<p>Hesabınızda şifre değişikliği talep edildi. Onaylamak için aşağıdaki bağlantıya tıklayın:</p>
<p><a href=""{link}"">{link}</a></p>
<p>Bu bağlantı 1 saat geçerlidir. Siz bu işlemi başlatmadıysanız şifrenizi değiştirmeyin ve bu e-postayı yok sayın.</p>";

            await _emailGonderici.GonderAsync(kullanici.Email, "Şifre değişikliğini onaylayın — Akıllı Tarım", html);
        }

        /// <summary>Bekleyen şifre değişikliği için onay e-postasını yeniden gönderir (süre 1 saat yenilenir).</summary>
        public async Task<(bool Ok, string Message)> SifreDegisiklikOnayMailiYenileAsync(int kullaniciId)
        {
            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.KullaniciId == kullaniciId);
            if (k == null)
                return (false, "Kullanıcı bulunamadı.");

            if (string.IsNullOrEmpty(k.BekleyenSifreHash) || string.IsNullOrEmpty(k.SifreOnayToken))
                return (false, "Bekleyen şifre değişikliği yok. Önce yeni şifre talebi oluşturun.");

            k.SifreOnayTokenSon = DateTime.UtcNow.AddHours(1);
            await _context.SaveChangesAsync();
            await SifreDegisiklikOnayMailiGonder(k);
            return (true, "Onay e-postası yeniden gönderildi. Gelen kutusu ve spam klasörünü kontrol edin. Bağlantı 1 saat geçerlidir.");
        }

        public async Task<(bool Ok, string Message)> OnaylaSifreDegisikligiAsync(string token)
        {
            token = Uri.UnescapeDataString(token ?? "").Trim();
            if (string.IsNullOrEmpty(token))
                return (false, "Geçersiz bağlantı.");

            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.SifreOnayToken == token);
            if (k == null || string.IsNullOrEmpty(k.BekleyenSifreHash))
                return (false, "Bağlantı geçersiz veya zaten kullanılmış.");

            if (k.SifreOnayTokenSon == null || k.SifreOnayTokenSon < DateTime.UtcNow)
                return (false, "Onay süresi dolmuş. Hesabım sayfasından işlemi yeniden başlatın.");

            k.SifreHash = k.BekleyenSifreHash;
            k.BekleyenSifreHash = null;
            k.SifreOnayToken = null;
            k.SifreOnayTokenSon = null;
            await _context.SaveChangesAsync();
            return (true, "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.");
        }

        /// <summary>E-posta bilinen hesaba şifre sıfırlama bağlantısı gönderir (hesap yoksa da genel mesaj).</summary>
        public async Task<string> SifremiUnuttumIstekAsync(string email)
        {
            var emailNorm = email.Trim().ToLowerInvariant();
            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.Email.ToLower() == emailNorm);
            if (k == null)
                return "İsteğiniz alındı. Bu adrese kayıtlı hesap varsa şifre sıfırlama bağlantısı e-posta ile gönderilir.";

            k.SifreSifirlamaToken = UretUrlToken();
            k.SifreSifirlamaTokenSon = DateTime.UtcNow.AddHours(2);
            k.BekleyenSifreHash = null;
            k.SifreOnayToken = null;
            k.SifreOnayTokenSon = null;
            await _context.SaveChangesAsync();

            var link = $"{PublicBaseUrl}/Home/SifreSifirla?token={Uri.EscapeDataString(k.SifreSifirlamaToken)}";
            var html = $@"
<p>Merhaba {System.Net.WebUtility.HtmlEncode(k.AdSoyad)},</p>
<p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın ve yeni şifrenizi belirleyin:</p>
<p><a href=""{link}"">{link}</a></p>
<p>Bu bağlantı 2 saat geçerlidir. Siz bu isteği yapmadıysanız bu e-postayı yok sayın; mevcut şifreniz değişmez.</p>";

            await _emailGonderici.GonderAsync(k.Email, "Şifre sıfırlama — Akıllı Tarım", html);
            return "Şifre sıfırlama bağlantısı e-postanıza gönderildi. Gelen kutusu ve spam klasörünü kontrol edin.";
        }

        public async Task<(bool Ok, string Message)> SifreSifirlaKaydetAsync(string token, string yeniSifre)
        {
            token = Uri.UnescapeDataString(token ?? "").Trim();
            if (string.IsNullOrEmpty(token))
                return (false, "Geçersiz bağlantı.");

            if (string.IsNullOrEmpty(yeniSifre) || yeniSifre.Length < 6)
                return (false, "Yeni şifre en az 6 karakter olmalıdır.");

            var k = await _context.Kullanicilar.FirstOrDefaultAsync(x => x.SifreSifirlamaToken == token);
            if (k == null)
                return (false, "Bağlantı geçersiz veya zaten kullanılmış.");

            if (k.SifreSifirlamaTokenSon == null || k.SifreSifirlamaTokenSon < DateTime.UtcNow)
                return (false, "Sıfırlama süresi dolmuş. Giriş sayfasından yeni istek gönderin.");

            k.SifreHash = BCrypt.Net.BCrypt.HashPassword(yeniSifre);
            k.SifreSifirlamaToken = null;
            k.SifreSifirlamaTokenSon = null;
            await _context.SaveChangesAsync();
            return (true, "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.");
        }

        private string BuildJwtToken(Kullanici kullanici)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, kullanici.KullaniciId.ToString()),
                new Claim(ClaimTypes.Email,          kullanici.Email),
                new Claim(ClaimTypes.Name,           kullanici.AdSoyad)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
