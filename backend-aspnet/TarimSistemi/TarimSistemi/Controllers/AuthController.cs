using Microsoft.AspNetCore.Mvc;
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
}