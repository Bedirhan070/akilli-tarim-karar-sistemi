using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TarimSistemi.Data;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UrunController : ControllerBase
    {
        private readonly TarimDbContext _context;

        public UrunController(TarimDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetUrunler()
        {
            var list = await _context.UrunBilgileri
                .AsNoTracking()
                .OrderBy(u => u.UrunAdi)
                .Select(u => new UrunListeDto
                {
                    UrunId = u.UrunId,
                    UrunAdi = u.UrunAdi,
                    IdealSicaklikMin = u.IdealSicaklikMin,
                    IdealSicaklikMax = u.IdealSicaklikMax,
                    IdealNemMin = u.IdealNemMin,
                    IdealNemMax = u.IdealNemMax,
                    EkimAylari = u.EkimAylari,
                    HasatSuresiGun = u.HasatSuresiGun,
                    Aciklama = u.Aciklama
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUrun(int id)
        {
            var u = await _context.UrunBilgileri.AsNoTracking()
                .Where(x => x.UrunId == id)
                .Select(x => new UrunListeDto
                {
                    UrunId = x.UrunId,
                    UrunAdi = x.UrunAdi,
                    IdealSicaklikMin = x.IdealSicaklikMin,
                    IdealSicaklikMax = x.IdealSicaklikMax,
                    IdealNemMin = x.IdealNemMin,
                    IdealNemMax = x.IdealNemMax,
                    EkimAylari = x.EkimAylari,
                    HasatSuresiGun = x.HasatSuresiGun,
                    Aciklama = x.Aciklama
                })
                .FirstOrDefaultAsync();

            if (u == null)
                return NotFound(new { message = "Ürün bulunamadı" });

            return Ok(u);
        }
    }

    public class UrunListeDto
    {
        public int UrunId { get; set; }
        public string UrunAdi { get; set; } = "";
        public decimal? IdealSicaklikMin { get; set; }
        public decimal? IdealSicaklikMax { get; set; }
        public decimal? IdealNemMin { get; set; }
        public decimal? IdealNemMax { get; set; }
        public string? EkimAylari { get; set; }
        public int? HasatSuresiGun { get; set; }
        public string? Aciklama { get; set; }
    }
}
