using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TarimSistemi.Data;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class OneriController : ControllerBase
    {
        private readonly TarimDbContext _context;

        public OneriController(TarimDbContext context)
        {
            _context = context;
        }

        // GET /api/Oneri — kullanıcının tüm önerileri
        [HttpGet]
        public async Task<IActionResult> GetOneriler()
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var oneriler = await _context.Oneriler
                .Include(o => o.Lokasyon)
                .Where(o => o.KullaniciId == kullaniciId)
                .OrderByDescending(o => o.OlusturulmaZamani)
                .Select(o => new
                {
                    o.OneriId,
                    o.RiskSkoru,
                    o.RiskTipi,
                    o.TavsiyeMetni,
                    o.OlusturulmaZamani,
                    o.UrunId,
                    UrunAdi = o.UrunBilgisi != null ? o.UrunBilgisi.UrunAdi : null,
                    LokasyonAdi = o.Lokasyon.Isim ?? o.Lokasyon.Sehir
                })
                .ToListAsync();

            return Ok(oneriler);
        }

        // GET /api/Oneri/son — son 5 öneri (dashboard için)
        [HttpGet("son")]
        public async Task<IActionResult> GetSonOneriler()
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var oneriler = await _context.Oneriler
                .Where(o => o.KullaniciId == kullaniciId)
                .OrderByDescending(o => o.OlusturulmaZamani)
                .Take(5)
                .Select(o => new
                {
                    o.OneriId,
                    o.RiskSkoru,
                    o.RiskTipi,
                    o.TavsiyeMetni,
                    o.OlusturulmaZamani,
                    o.UrunId,
                    UrunAdi = o.UrunBilgisi != null ? o.UrunBilgisi.UrunAdi : null
                })
                .ToListAsync();

            return Ok(oneriler);
        }
    }
}