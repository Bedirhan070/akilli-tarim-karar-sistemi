using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TarimSistemi.Services;
using TarimSistemi.Models;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TelegramService _telegramService;
        private readonly GunlukBildirimServisi _gunlukBildirim;
        private readonly IConfiguration _configuration;

        public AuthController(AuthService authService, TelegramService telegramService, GunlukBildirimServisi gunlukBildirim, IConfiguration configuration)
        {
            _authService = authService;
            _telegramService = telegramService;
            _gunlukBildirim = gunlukBildirim;
            _configuration = configuration;
        }

        // POST: api/Auth/kayit
        [HttpPost("kayit")]
        public async Task<IActionResult> KayitOl([FromBody] KayitDto kayit)
        {
            var sonuc = await _authService.KayitOl(kayit.AdSoyad, kayit.Email, kayit.Sifre, kayit.Telefon);

            if (!sonuc.Success)
            {
                if (sonuc.ZatenKayitli)
                    return Conflict(new { message = sonuc.Message });
                return BadRequest(new { message = sonuc.Message });
            }

            return Ok(new { message = sonuc.Message });
        }

        // POST: api/Auth/giris
        [HttpPost("giris")]
        public async Task<IActionResult> GirisYap([FromBody] GirisDto giris)
        {
            var sonuc = await _authService.GirisYap(giris.Email, giris.Sifre);

            if (!sonuc.Success)
                return Unauthorized(new { message = sonuc.Message });

            return Ok(new { token = sonuc.Token, message = sonuc.Message });
        }

        // GET: api/Auth/profil
        [Authorize]
        [HttpGet("profil")]
        public async Task<IActionResult> ProfilGet()
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var profil = await _authService.GetProfilAsync(kullaniciId);
            if (profil == null)
                return NotFound(new { message = "Kullanıcı bulunamadı" });
            return Ok(profil);
        }

        // PUT: api/Auth/profil
        [Authorize]
        [HttpPut("profil")]
        public async Task<IActionResult> ProfilGuncelle([FromBody] ProfilGuncelleDto body)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var (ok, message, token) = await _authService.GuncelleProfilAsync(kullaniciId, body.AdSoyad, body.Telefon);
            if (!ok)
                return BadRequest(new { message });
            return Ok(new { message, token });
        }

        // POST: api/Auth/sifre-degistir (e-posta onayı sonrası aktif olur)
        [Authorize]
        [HttpPost("sifre-degistir")]
        public async Task<IActionResult> SifreDegistir([FromBody] SifreDegistirDto body)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var (ok, message) = await _authService.SifreDegisikligiBaslatAsync(kullaniciId, body.MevcutSifre, body.YeniSifre);
            if (!ok)
                return BadRequest(new { message });
            return Ok(new { message });
        }

        // POST: api/Auth/sifre-degistir-onay-yenile — bekleyen şifre değişikliği için onay mailini tekrar gönderir
        [Authorize]
        [HttpPost("sifre-degistir-onay-yenile")]
        public async Task<IActionResult> SifreDegisiklikOnayYenile()
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var (ok, message) = await _authService.SifreDegisiklikOnayMailiYenileAsync(kullaniciId);
            if (!ok)
                return BadRequest(new { message });
            return Ok(new { message });
        }

        // POST: api/Auth/email-dogrulama-yenile
        [HttpPost("email-dogrulama-yenile")]
        public async Task<IActionResult> EmailDogrulamaYenile([FromBody] EmailDogrulamaYenileDto body)
        {
            var (ok, message) = await _authService.EmailDogrulamaYenidenGonder(body.Email ?? "");
            if (!ok) return BadRequest(new { message });
            return Ok(new { message });
        }

        // POST: api/Auth/sifremi-unuttum
        [HttpPost("sifremi-unuttum")]
        public async Task<IActionResult> SifremiUnuttum([FromBody] SifremiUnuttumDto body)
        {
            var (ok, message) = await _authService.SifremiUnuttumIstekAsync(body.Email ?? "");
            if (!ok) return BadRequest(new { message });
            return Ok(new { message });
        }

        // POST: api/Auth/email-dogrula
        [HttpPost("email-dogrula")]
        public async Task<IActionResult> EmailDogrula([FromBody] EmailDogrulaDto body)
        {
            var (ok, msg) = await _authService.OnaylaKayitEmailiAsync(body.Email ?? "", body.Kod ?? "");
            if (!ok)
                return BadRequest(new { message = msg });
            return Ok(new { message = msg });
        }

        // POST: api/Auth/sifre-sifirla
        [HttpPost("sifre-sifirla")]
        public async Task<IActionResult> SifreSifirla([FromBody] SifreSifirlaDto body)
        {
            var (ok, msg) = await _authService.SifreSifirlaKaydetAsync(body.Email ?? "", body.Kod ?? "", body.YeniSifre ?? "");
            if (!ok)
                return BadRequest(new { message = msg });
            return Ok(new { message = msg });
        }

        // GET: api/Auth/telegram/baglama-linki — Deep-link üret (30 dk geçerli)
        [Authorize]
        [HttpGet("telegram/baglama-linki")]
        public async Task<IActionResult> TelegramBaglamaLinki()
        {
            if (!_telegramService.Aktif)
                return BadRequest(new { message = "Telegram botu henüz yapılandırılmamış. BotToken eksik." });

            // BotUsername config'de yoksa getMe ile otomatik çek
            var botUsername = await _telegramService.GetBotUsernameAsync();
            if (string.IsNullOrWhiteSpace(botUsername))
                return BadRequest(new { message = "Bot kullanıcı adı alınamadı. BotToken'ı kontrol edin." });

            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var (ok, token, message) = await _authService.UretTelegramBaglamaTokenu(kullaniciId);
            if (!ok) return BadRequest(new { message });

            var link = $"https://t.me/{botUsername}?start={token}";
            return Ok(new { link, message = "Bu link 30 dakika geçerlidir. Açtıktan sonra Telegram'da 'Başlat' butonuna basın." });
        }

        // PUT: api/Auth/telegram — Telegram chat ID kaydet / kaldır
        [Authorize]
        [HttpPut("telegram")]
        public async Task<IActionResult> TelegramKaydet([FromBody] TelegramDto body)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var (ok, message) = await _authService.KaydetTelegramAsync(kullaniciId, body.ChatId);
            if (!ok)
                return BadRequest(new { message });
            return Ok(new { message });
        }

        // POST: api/Auth/telegram/test-gunluk — Günlük tarım raporunu şimdi gönder
        [Authorize]
        [HttpPost("telegram/test-gunluk")]
        public async Task<IActionResult> TelegramTestGunluk()
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var (ok, message) = await _gunlukBildirim.TekKullaniciGonder(kullaniciId);
            if (!ok) return BadRequest(new { message });
            return Ok(new { message });
        }

        // POST: api/Auth/telegram/test — Kayıtlı chat ID'ye test mesajı gönder
        [Authorize]
        [HttpPost("telegram/test")]
        public async Task<IActionResult> TelegramTest()
        {
            if (!_telegramService.Aktif)
                return BadRequest(new { message = "Telegram botu henüz yapılandırılmamış. Sistem yöneticisine bildirin." });

            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            var profil = await _authService.GetProfilAsync(kullaniciId);

            if (string.IsNullOrWhiteSpace(profil?.TelegramChatId))
                return BadRequest(new { message = "Önce Telegram chat ID girip kaydedin." });

            var basarili = await _telegramService.MesajGonderAsync(
                profil.TelegramChatId,
                $"✅ <b>Akıllı Tarım Sistemi</b>\n\nMerhaba {System.Net.WebUtility.HtmlEncode(profil.AdSoyad)}! " +
                "Telegram bildirimleri aktif. Tarlalarınızda kritik risk oluştuğunda buradan haber alacaksınız.");

            if (!basarili)
                return BadRequest(new { message = "Mesaj gönderilemedi. Chat ID'yi kontrol edin ve botu başlattığınızdan emin olun." });

            return Ok(new { message = "Test mesajı Telegram'a gönderildi!" });
        }
    }

    public class KayitDto
    {
        public string AdSoyad { get; set; } = "";
        public string Email { get; set; } = "";
        public string Sifre { get; set; } = "";
        public string? Telefon { get; set; }
    }

    public class GirisDto
    {
        public string Email { get; set; } = "";
        public string Sifre { get; set; } = "";
    }

    public class ProfilGuncelleDto
    {
        public string AdSoyad { get; set; } = "";
        public string? Telefon { get; set; }
    }

    public class SifreDegistirDto
    {
        public string MevcutSifre { get; set; } = "";
        public string YeniSifre { get; set; } = "";
    }

    public class EmailDogrulamaYenileDto
    {
        public string Email { get; set; } = "";
    }

    public class SifremiUnuttumDto
    {
        public string Email { get; set; } = "";
    }

    public class EmailDogrulaDto
    {
        public string Email { get; set; } = "";
        public string Kod { get; set; } = "";
    }

    public class SifreSifirlaDto
    {
        public string Email { get; set; } = "";
        public string Kod { get; set; } = "";
        public string YeniSifre { get; set; } = "";
    }

    public class TelegramDto
    {
        public string? ChatId { get; set; }
    }
}