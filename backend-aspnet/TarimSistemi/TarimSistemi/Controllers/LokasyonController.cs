using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TarimSistemi.Data;
using TarimSistemi.Models;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class LokasyonController : ControllerBase
    {
        private readonly TarimDbContext _context;

        public LokasyonController(TarimDbContext context)
        {
            _context = context;
        }

        // GET /api/Lokasyon
        [HttpGet]
        public async Task<IActionResult> GetLokasyonlar()
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var lokasyonlar = await _context.Lokasyonlar
                .Where(l => l.KullaniciId == kullaniciId)
                .OrderByDescending(l => l.KayitTarihi)
                .Select(l => new LokasyonResponseDto
                {
                    LokasyonId = l.LokasyonId,
                    Isim = l.Isim,
                    Sehir = l.Sehir,
                    Ilce = l.Ilce,
                    Enlem = l.Enlem,
                    Boylam = l.Boylam,
                    KayitTarihi = l.KayitTarihi,
                    UrunId = l.UrunId,
                    UrunAdi = l.UrunBilgisi != null ? l.UrunBilgisi.UrunAdi : null
                })
                .ToListAsync();

            return Ok(lokasyonlar);
        }

        // POST /api/Lokasyon
        [HttpPost]
        public async Task<IActionResult> LokasyonEkle([FromBody] LokasyonDto dto)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            if (dto.UrunId.HasValue)
            {
                var urunGecerli = await _context.UrunBilgileri.AnyAsync(u => u.UrunId == dto.UrunId.Value);
                if (!urunGecerli)
                    return BadRequest(new { message = "Geçersiz ürün seçimi" });
            }

            if (Math.Abs(dto.Enlem) < 0.0001m && Math.Abs(dto.Boylam) < 0.0001m)
                return BadRequest(new { message = "Koordinat eksik veya geçersiz. Tarlalarım haritasından il ve ilçe seçerek kaydedin." });

            // Kabaca Türkiye sınırları dışındaysa (yanlışlıkla 0 veya yurtdışı) uyar
            if (dto.Enlem < 35.5m || dto.Enlem > 42.5m || dto.Boylam < 25.5m || dto.Boylam > 45.5m)
                return BadRequest(new { message = "Koordinatlar Türkiye aralığında değil. Haritadan konum seçtiğinizden emin olun." });

            var lokasyon = new Lokasyon
            {
                KullaniciId = kullaniciId,
                Isim = dto.Isim,
                Sehir = dto.Sehir,
                Ilce = dto.Ilce,
                Enlem = dto.Enlem,
                Boylam = dto.Boylam,
                KayitTarihi = DateTime.Now,
                UrunId = dto.UrunId
            };

            _context.Lokasyonlar.Add(lokasyon);
            await _context.SaveChangesAsync();

            string? urunAdi = null;
            if (lokasyon.UrunId.HasValue)
            {
                urunAdi = await _context.UrunBilgileri
                    .Where(u => u.UrunId == lokasyon.UrunId.Value)
                    .Select(u => u.UrunAdi)
                    .FirstOrDefaultAsync();
            }

            return Ok(new LokasyonResponseDto
            {
                LokasyonId = lokasyon.LokasyonId,
                Isim = lokasyon.Isim,
                Sehir = lokasyon.Sehir,
                Ilce = lokasyon.Ilce,
                Enlem = lokasyon.Enlem,
                Boylam = lokasyon.Boylam,
                KayitTarihi = lokasyon.KayitTarihi,
                UrunId = lokasyon.UrunId,
                UrunAdi = urunAdi
            });
        }

        // PATCH /api/Lokasyon/{id}/urun
        [HttpPatch("{id}/urun")]
        public async Task<IActionResult> UrunAta(int id, [FromBody] LokasyonUrunPatchDto dto)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var lokasyon = await _context.Lokasyonlar.FirstOrDefaultAsync(l =>
                l.LokasyonId == id && l.KullaniciId == kullaniciId);

            if (lokasyon == null)
                return NotFound(new { message = "Lokasyon bulunamadı" });

            if (dto.UrunId.HasValue)
            {
                var urunGecerli = await _context.UrunBilgileri.AnyAsync(u => u.UrunId == dto.UrunId.Value);
                if (!urunGecerli)
                    return BadRequest(new { message = "Geçersiz ürün seçimi" });
            }

            lokasyon.UrunId = dto.UrunId;
            await _context.SaveChangesAsync();

            string? urunAdi = null;
            if (lokasyon.UrunId.HasValue)
            {
                urunAdi = await _context.UrunBilgileri
                    .Where(u => u.UrunId == lokasyon.UrunId.Value)
                    .Select(u => u.UrunAdi)
                    .FirstOrDefaultAsync();
            }

            return Ok(new LokasyonResponseDto
            {
                LokasyonId = lokasyon.LokasyonId,
                Isim = lokasyon.Isim,
                Sehir = lokasyon.Sehir,
                Ilce = lokasyon.Ilce,
                Enlem = lokasyon.Enlem,
                Boylam = lokasyon.Boylam,
                KayitTarihi = lokasyon.KayitTarihi,
                UrunId = lokasyon.UrunId,
                UrunAdi = urunAdi
            });
        }

        // DELETE /api/Lokasyon/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> LokasyonSil(int id)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var lokasyon = await _context.Lokasyonlar.FindAsync(id);

            if (lokasyon == null)
                return NotFound(new { message = "Lokasyon bulunamadı" });

            if (lokasyon.KullaniciId != kullaniciId)
                return Forbid();

            // FK kısıtlaması nedeniyle önce bağlı kayıtları sil
            var havaVerileri = await _context.HavaVerileri
                .Where(h => h.LokasyonId == id).ToListAsync();
            _context.HavaVerileri.RemoveRange(havaVerileri);

            var oneriler = await _context.Oneriler
                .Where(o => o.LokasyonId == id).ToListAsync();
            _context.Oneriler.RemoveRange(oneriler);

            _context.Lokasyonlar.Remove(lokasyon);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Lokasyon silindi" });
        }
    }

    public class LokasyonDto
    {
        public string? Isim { get; set; }
        public string Sehir { get; set; } = "";
        public string? Ilce { get; set; }
        public decimal Enlem { get; set; }
        public decimal Boylam { get; set; }
        public int? UrunId { get; set; }
    }

    public class LokasyonUrunPatchDto
    {
        public int? UrunId { get; set; }
    }

    public class LokasyonResponseDto
    {
        public int LokasyonId { get; set; }
        public string? Isim { get; set; }
        public string? Sehir { get; set; }
        public string? Ilce { get; set; }
        public decimal Enlem { get; set; }
        public decimal Boylam { get; set; }
        public DateTime KayitTarihi { get; set; }
        public int? UrunId { get; set; }
        public string? UrunAdi { get; set; }
    }
}