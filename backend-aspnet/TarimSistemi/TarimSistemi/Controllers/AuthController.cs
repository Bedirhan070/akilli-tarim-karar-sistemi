using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TarimSistemi.Services;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        // POST: api/Auth/kayit
        [HttpPost("kayit")]
        public async Task<IActionResult> KayitOl([FromBody] KayitDto kayit)
        {
            var sonuc = await _authService.KayitOl(kayit.AdSoyad, kayit.Email, kayit.Sifre, kayit.Telefon);

            if (!sonuc.Success)
                return BadRequest(new { message = sonuc.Message });

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
            var message = await _authService.EmailDogrulamaYenidenGonder(body.Email ?? "");
            return Ok(new { message });
        }

        // POST: api/Auth/sifremi-unuttum
        [HttpPost("sifremi-unuttum")]
        public async Task<IActionResult> SifremiUnuttum([FromBody] SifremiUnuttumDto body)
        {
            var message = await _authService.SifremiUnuttumIstekAsync(body.Email ?? "");
            return Ok(new { message });
        }

        // POST: api/Auth/sifre-sifirla
        [HttpPost("sifre-sifirla")]
        public async Task<IActionResult> SifreSifirla([FromBody] SifreSifirlaDto body)
        {
            var (ok, msg) = await _authService.SifreSifirlaKaydetAsync(body.Token ?? "", body.YeniSifre ?? "");
            if (!ok)
                return BadRequest(new { message = msg });
            return Ok(new { message = msg });
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

    public class SifreSifirlaDto
    {
        public string Token { get; set; } = "";
        public string YeniSifre { get; set; } = "";
    }
}