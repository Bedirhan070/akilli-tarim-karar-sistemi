using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
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

            var lokasyon = await _context.Lokasyonlar
                .Include(l => l.UrunBilgisi)
                .FirstOrDefaultAsync(l => l.LokasyonId == lokasyonId && l.KullaniciId == kullaniciId);

            if (lokasyon == null)
                return NotFound(new { message = "Lokasyon bulunamadı" });

            if (!lokasyon.UrunId.HasValue || lokasyon.UrunBilgisi == null)
                return BadRequest(new
                {
                    message = "Bu tarla için ürün seçilmedi. Tarlalarım sayfasından tarlaya bir ürün atayın."
                });

            var hava = await _havaService.GetBugunHavasi(lokasyonId);

            if (hava == null)
                return StatusCode(503, new { message = "Hava durumu verisi alınamadı. Lütfen tekrar deneyin." });

            var urun = lokasyon.UrunBilgisi;
            var mlSonuc = await MlServisiniCagir(hava, lokasyon, urun);

            var oneri = new Oneri
            {
                KullaniciId = kullaniciId,
                LokasyonId = lokasyonId,
                UrunId = lokasyon.UrunId,
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
                RiskEtkenleri = mlSonuc.RiskEtkenleri,
                UrunId = urun.UrunId,
                UrunAdi = urun.UrunAdi,
                UrunOzeti = UrunOzetiMetni(urun),
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
                .Include(l => l.UrunBilgisi)
                .FirstOrDefaultAsync(l => l.LokasyonId == lokasyonId && l.KullaniciId == kullaniciId);

            if (lokasyon == null)
                return NotFound(new { message = "Lokasyon bulunamadı" });

            var tahminVerisi = await _havaService.GetYediGunlukTahmin(lokasyonId);

            if (!tahminVerisi.Any())
                return StatusCode(503, new { message = "Tahmin verisi alınamadı." });

            var urun = lokasyon.UrunBilgisi;
            var sonuc = tahminVerisi.OrderBy(h => h.Tarih).Select(h =>
            {
                var (risk, _) = HesaplaRiskDetay(h, urun, h.Tarih);
                risk = Math.Min(risk, 1.0m);
                var skor = Math.Round(risk, 2);
                var ozet = skor > 0.70m ? "Kritik" : skor > 0.40m ? "Uyarı" : "Düşük";
                return new
                {
                    tarih = h.Tarih.ToString("yyyy-MM-dd"),
                    gunAdi = h.Tarih.ToString("ddd", new CultureInfo("tr-TR")),
                    sicaklikMax = h.SicaklikMax,
                    sicaklikMin = h.SicaklikMin,
                    nem = h.Nem,
                    yagis = h.Yagis,
                    ruzgar = h.RuzgarHizi,
                    riskSkoru = skor,
                    riskOzeti = ozet
                };
            });

            return Ok(new
            {
                tahminler = sonuc,
                urunId = lokasyon.UrunId,
                urunAdi = urun?.UrunAdi,
                aciklama = urun == null
                    ? "Ürün atanmadı; günlük risk skoru yalnızca hava etkenlerine göre hesaplanır."
                    : "Günlük skor, aynı kural motoru ile tahmin edilen hava + ürün ideal aralıklarına göre özetlenir. LSTM ile seri tahmin eklendiğinde bu grafik model çıktısıyla güçlendirilecek."
            });
        }

        private Task<MlSonuc> MlServisiniCagir(HavaVerisi hava, Lokasyon lokasyon, UrunBilgisi urun)
        {
            var fastApiUrl = _config["FastApi:BaseUrl"];

            if (!string.IsNullOrEmpty(fastApiUrl))
            {
                try
                {
                    // FastAPI: POST {BaseUrl}/predict — gövde: lokasyonId, enlem, boylam,
                    // urun { urunId, urunAdi, ideal*, ekimAylari, hasatSuresiGun }, hava { sicaklik*, nem, yagis, ruzgarHizi }
                }
                catch { }
            }

            return Task.FromResult(KuralTabanliHesapla(hava, urun));
        }

        private static string UrunOzetiMetni(UrunBilgisi u)
        {
            var parcalar = new List<string>();
            if (u.IdealSicaklikMin.HasValue && u.IdealSicaklikMax.HasValue)
                parcalar.Add($"İdeal sıcaklık {u.IdealSicaklikMin}–{u.IdealSicaklikMax}°C");
            if (u.IdealNemMin.HasValue && u.IdealNemMax.HasValue)
                parcalar.Add($"nem %{u.IdealNemMin}–{u.IdealNemMax}");
            if (!string.IsNullOrWhiteSpace(u.EkimAylari))
                parcalar.Add($"ekim ayları: {u.EkimAylari}");
            return parcalar.Count == 0 ? u.UrunAdi : string.Join(" · ", parcalar);
        }

        private static List<int> EkimAylariniOku(string? ekimAylari)
        {
            var sonuc = new List<int>();
            if (string.IsNullOrWhiteSpace(ekimAylari))
                return sonuc;

            foreach (var parca in ekimAylari.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(parca, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ay)
                    && ay >= 1 && ay <= 12)
                    sonuc.Add(ay);
            }

            return sonuc;
        }

        /// <summary>Anlık veya günlük tahmin satırı için risk skoru ve etken listesi (ürün opsiyonel).</summary>
        private static (decimal risk, List<string> riskler) HesaplaRiskDetay(
            HavaVerisi hava, UrunBilgisi? urun, DateTime referansGun)
        {
            decimal risk = 0;
            var riskler = new List<string>();

            if (hava.SicaklikMin.HasValue && hava.SicaklikMin < 2)
            {
                risk += 0.5m;
                riskler.Add("don riski");
            }

            if (hava.SicaklikMax.HasValue && hava.SicaklikMax > 38)
            {
                risk += 0.3m;
                riskler.Add("aşırı sıcaklık");
            }

            if (hava.Yagis.HasValue && hava.Yagis > 30)
            {
                risk += 0.25m;
                riskler.Add("yoğun yağış");
            }

            if (hava.Nem.HasValue && hava.Nem < 30 && (hava.Yagis == null || hava.Yagis < 1))
            {
                risk += 0.2m;
                riskler.Add("kuraklık");
            }

            if (hava.RuzgarHizi.HasValue && hava.RuzgarHizi > 60)
            {
                risk += 0.2m;
                riskler.Add("şiddetli rüzgar");
            }

            if (urun != null)
            {
                if (urun.IdealSicaklikMax.HasValue && hava.SicaklikMax.HasValue
                    && hava.SicaklikMax > urun.IdealSicaklikMax + 3)
                {
                    risk += 0.22m;
                    riskler.Add($"{urun.UrunAdi} için sıcaklık üst idealin üzerinde");
                }

                if (urun.IdealSicaklikMin.HasValue && hava.SicaklikMin.HasValue
                    && hava.SicaklikMin < urun.IdealSicaklikMin - 2)
                {
                    risk += 0.2m;
                    riskler.Add($"{urun.UrunAdi} için sıcaklık alt idealin altında");
                }

                if (urun.IdealNemMin.HasValue && hava.Nem.HasValue && hava.Nem < urun.IdealNemMin - 10)
                {
                    risk += 0.15m;
                    riskler.Add($"{urun.UrunAdi} için nem düşük");
                }

                if (urun.IdealNemMax.HasValue && hava.Nem.HasValue && hava.Nem > urun.IdealNemMax + 15)
                {
                    risk += 0.12m;
                    riskler.Add($"{urun.UrunAdi} için nem yüksek");
                }

                var ekimAylari = EkimAylariniOku(urun.EkimAylari);
                if (ekimAylari.Count > 0 && !ekimAylari.Contains(referansGun.Month))
                {
                    risk += 0.08m;
                    riskler.Add("tipik ekim ayı dışı (mevsimsel dikkat)");
                }
            }

            return (risk, riskler);
        }

        private static MlSonuc KuralTabanliHesapla(HavaVerisi hava, UrunBilgisi urun)
        {
            var (risk, riskler) = HesaplaRiskDetay(hava, urun, DateTime.Today);
            risk = Math.Min(risk, 1.0m);

            string tavsiye;
            string riskTipi;

            if (risk > 0.70m)
            {
                riskTipi = "Kritik";
                tavsiye = riskler.Contains("don riski")
                    ? $"KRİTİK: {urun.UrunAdi} için don riski (Min: {hava.SicaklikMin}°C). Koruma ve erteleme değerlendirin."
                    : $"KRİTİK ({urun.UrunAdi}): {string.Join(", ", riskler)}. Operasyonları gözden geçirin.";
            }
            else if (risk > 0.40m)
            {
                riskTipi = "Uyarı";
                tavsiye = riskler.Any(r => r.Contains("nem düşük", StringComparison.OrdinalIgnoreCase)
                    || r.Contains("kuraklık", StringComparison.OrdinalIgnoreCase))
                    ? $"UYARI ({urun.UrunAdi}): Nem/kuraklık riski (Nem: %{hava.Nem}). Sulama planını güncelleyin."
                    : $"UYARI ({urun.UrunAdi}): {string.Join(", ", riskler)}. Takibi artırın.";
            }
            else
            {
                riskTipi = "Güvenli";
                tavsiye =
                    $"Tarımsal koşullar {urun.UrunAdi} için genel olarak uygun. Sıcaklık: {hava.SicaklikMax}°C, nem: %{hava.Nem}. {UrunOzetiMetni(urun)}";
            }

            return new MlSonuc
            {
                RiskSkoru = Math.Round(risk, 2),
                RiskTipi = riskTipi,
                TavsiyeMetni = tavsiye,
                AnomaliBulundu = false,
                RiskEtkenleri = riskler.Count > 0 ? riskler : new List<string> { "Belirgin risk etkeni yok; rutin izlemeye devam edin." }
            };
        }
    }

    public class AnalizSonucDto
    {
        public decimal RiskSkoru { get; set; }
        public string? RiskTipi { get; set; }
        public string? TavsiyeMetni { get; set; }
        public bool AnomaliBulundu { get; set; }
        public List<string>? RiskEtkenleri { get; set; }
        public int? UrunId { get; set; }
        public string? UrunAdi { get; set; }
        public string? UrunOzeti { get; set; }
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
        public List<string> RiskEtkenleri { get; set; } = new();
    }
}
