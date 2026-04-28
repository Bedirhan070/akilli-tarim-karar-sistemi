using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TarimSistemi.Data;
using TarimSistemi.Models;
using TarimSistemi.Services;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AnalizController : ControllerBase
    {
        private readonly TarimDbContext _context;
        private readonly HavaService _havaService;
        private readonly IConfiguration _config;

        public AnalizController(TarimDbContext context, HavaService havaService, IConfiguration config)
        {
            _context = context;
            _havaService = havaService;
            _config = config;
        }

        // POST /api/Analiz/anlik/{lokasyonId}
        [HttpPost("anlik/{lokasyonId}")]
        public async Task<IActionResult> AnlikAnaliz(int lokasyonId)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            // Lokasyon kullanıcıya ait mi?
            var lokasyon = await _context.Lokasyonlar
                .FirstOrDefaultAsync(l => l.LokasyonId == lokasyonId && l.KullaniciId == kullaniciId);

            if (lokasyon == null)
                return NotFound(new { message = "Lokasyon bulunamadı" });

            // Hava verisini çek (cache veya API)
            var hava = await _havaService.GetBugunHavasi(lokasyonId);

            if (hava == null)
                return StatusCode(503, new { message = "Hava durumu verisi alınamadı. Lütfen tekrar deneyin." });

            // ML servisini çağır (FastAPI hazır olunca burada devreye girer)
            var mlSonuc = await MlServisiniCagir(hava, lokasyon);

            // Öneriyi kaydet
            var oneri = new Oneri
            {
                KullaniciId = kullaniciId,
                LokasyonId = lokasyonId,
                RiskSkoru = mlSonuc.RiskSkoru,
                RiskTipi = mlSonuc.RiskTipi,
                TavsiyeMetni = mlSonuc.TavsiyeMetni,
                OlusturulmaZamani = DateTime.Now
            };

            _context.Oneriler.Add(oneri);
            await _context.SaveChangesAsync();

            return Ok(new AnalizSonucDto
            {
                RiskSkoru = mlSonuc.RiskSkoru,
                RiskTipi = mlSonuc.RiskTipi,
                TavsiyeMetni = mlSonuc.TavsiyeMetni,
                AnomaliBulundu = mlSonuc.AnomaliBulundu,
                HavaVerisi = new HavaDto
                {
                    SicaklikMax = hava.SicaklikMax,
                    SicaklikMin = hava.SicaklikMin,
                    Nem = hava.Nem,
                    Yagis = hava.Yagis,
                    RuzgarHizi = hava.RuzgarHizi,
                    ApiKaynagi = hava.ApiKaynagi
                }
            });
        }

        // POST /api/Analiz/tahmin/{lokasyonId}  (LSTM - 7 günlük)
        [HttpPost("tahmin/{lokasyonId}")]
        public async Task<IActionResult> GelecekTahmini(int lokasyonId)
        {
            var kullaniciId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

            var lokasyon = await _context.Lokasyonlar
                .FirstOrDefaultAsync(l => l.LokasyonId == lokasyonId && l.KullaniciId == kullaniciId);

            if (lokasyon == null)
                return NotFound(new { message = "Lokasyon bulunamadı" });

            var tahminVerisi = await _havaService.GetYediGunlukTahmin(lokasyonId);

            if (!tahminVerisi.Any())
                return StatusCode(503, new { message = "Tahmin verisi alınamadı." });

            // LSTM entegrasyonu hazır olunca buraya gelecek
            // Şimdilik hava verisini döner
            var sonuc = tahminVerisi.Select(h => new
            {
                tarih = h.Tarih.ToString("yyyy-MM-dd"),
                sicaklikMax = h.SicaklikMax,
                sicaklikMin = h.SicaklikMin,
                nem = h.Nem,
                yagis = h.Yagis,
                ruzgar = h.RuzgarHizi
            });

            return Ok(new { tahminler = sonuc });
        }

        // ML servisine HTTP isteği atar — FastAPI hazır değilse kural tabanlı fallback çalışır
        private Task<MlSonuc> MlServisiniCagir(HavaVerisi hava, Lokasyon lokasyon)
        {
            var fastApiUrl = _config["FastApi:BaseUrl"];

            if (!string.IsNullOrEmpty(fastApiUrl))
            {
                try
                {
                    // FastAPI hazır olduğunda Kasım bu kısmı dolduracak
                    // var httpClient = ...
                    // var response = await httpClient.PostAsJsonAsync($"{fastApiUrl}/predict", payload);
                }
                catch { }
            }

            // FastAPI hazır değilken çalışan kural tabanlı hesaplama
            return Task.FromResult(KuralTabanliHesapla(hava));
        }

        private static MlSonuc KuralTabanliHesapla(HavaVerisi hava)
        {
            decimal risk = 0;
            var riskler = new List<string>();

            // Don riski
            if (hava.SicaklikMin.HasValue && hava.SicaklikMin < 2)
            {
                risk += 0.5m;
                riskler.Add("don riski");
            }

            // Aşırı sıcaklık
            if (hava.SicaklikMax.HasValue && hava.SicaklikMax > 38)
            {
                risk += 0.3m;
                riskler.Add("aşırı sıcaklık");
            }

            // Yüksek yağış
            if (hava.Yagis.HasValue && hava.Yagis > 30)
            {
                risk += 0.25m;
                riskler.Add("yoğun yağış");
            }

            // Kuraklık (düşük nem + yağışsız)
            if (hava.Nem.HasValue && hava.Nem < 30 && (hava.Yagis == null || hava.Yagis < 1))
            {
                risk += 0.2m;
                riskler.Add("kuraklık");
            }

            // Şiddetli rüzgar
            if (hava.RuzgarHizi.HasValue && hava.RuzgarHizi > 60)
            {
                risk += 0.2m;
                riskler.Add("şiddetli rüzgar");
            }

            risk = Math.Min(risk, 1.0m);

            string tavsiye;
            string riskTipi;

            if (risk > 0.70m)
            {
                riskTipi = "Kritik";
                tavsiye = riskler.Contains("don riski")
                    ? $"KRİTİK: Tarlanızda don riski mevcut (Min: {hava.SicaklikMin}°C). Ekim işlemlerini erteleyin ve koruma önlemi alın."
                    : $"KRİTİK: Yüksek risk tespit edildi ({string.Join(", ", riskler)}). Tarla operasyonlarını durdurun.";
            }
            else if (risk > 0.40m)
            {
                riskTipi = "Uyarı";
                tavsiye = riskler.Contains("kuraklık")
                    ? $"UYARI: Kuraklık belirtileri var (Nem: %{hava.Nem}). Sulama sıklığını artırın."
                    : $"UYARI: Orta düzey risk ({string.Join(", ", riskler)}). Takibi artırın.";
            }
            else
            {
                riskTipi = "Güvenli";
                tavsiye = $"Tarımsal koşullar normal. Sıcaklık: {hava.SicaklikMax}°C, Nem: %{hava.Nem}. Normal operasyonlara devam edebilirsiniz.";
            }

            return new MlSonuc
            {
                RiskSkoru = Math.Round(risk, 2),
                RiskTipi = riskTipi,
                TavsiyeMetni = tavsiye,
                AnomaliBulundu = false
            };
        }
    }

    // --- DTO'lar ---

    public class AnalizSonucDto
    {
        public decimal RiskSkoru { get; set; }
        public string? RiskTipi { get; set; }
        public string? TavsiyeMetni { get; set; }
        public bool AnomaliBulundu { get; set; }
        public HavaDto? HavaVerisi { get; set; }
    }

    public class HavaDto
    {
        public decimal? SicaklikMax { get; set; }
        public decimal? SicaklikMin { get; set; }
        public decimal? Nem { get; set; }
        public decimal? Yagis { get; set; }
        public decimal? RuzgarHizi { get; set; }
        public string? ApiKaynagi { get; set; }
    }

    public class MlSonuc
    {
        public decimal RiskSkoru { get; set; }
        public string? RiskTipi { get; set; }
        public string? TavsiyeMetni { get; set; }
        public bool AnomaliBulundu { get; set; }
    }
}