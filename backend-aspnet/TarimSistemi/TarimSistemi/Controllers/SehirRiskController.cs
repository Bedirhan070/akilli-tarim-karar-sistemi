using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text.Json;
using TarimSistemi.Models;
using TarimSistemi.Services;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SehirRiskController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly MlService _mlService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SehirRiskController> _logger;

        // Türkiye'nin 81 ili
        private static readonly List<(string Sehir, double Enlem, double Boylam)> Iller = new()
        {
            ("Adana",           37.00, 35.32),
            ("Adıyaman",        37.76, 38.28),
            ("Afyonkarahisar",  38.76, 30.54),
            ("Ağrı",            39.72, 43.05),
            ("Amasya",          40.65, 35.83),
            ("Ankara",          39.92, 32.85),
            ("Antalya",         36.90, 30.70),
            ("Artvin",          41.18, 41.82),
            ("Aydın",           37.84, 27.84),
            ("Balıkesir",       39.65, 27.89),
            ("Bilecik",         40.15, 29.98),
            ("Bingöl",          38.89, 40.50),
            ("Bitlis",          38.40, 42.11),
            ("Bolu",            40.74, 31.61),
            ("Burdur",          37.72, 30.29),
            ("Bursa",           40.18, 29.06),
            ("Çanakkale",       40.15, 26.41),
            ("Çankırı",         40.60, 33.61),
            ("Çorum",           40.55, 34.95),
            ("Denizli",         37.77, 29.09),
            ("Diyarbakır",      37.91, 40.23),
            ("Edirne",          41.67, 26.56),
            ("Elazığ",          38.68, 39.22),
            ("Erzincan",        39.75, 39.50),
            ("Erzurum",         39.90, 41.27),
            ("Eskişehir",       39.77, 30.52),
            ("Gaziantep",       37.07, 37.38),
            ("Giresun",         40.91, 38.39),
            ("Gümüşhane",       40.46, 39.48),
            ("Hakkari",         37.57, 43.74),
            ("Hatay",           36.40, 36.34),
            ("Isparta",         37.76, 30.55),
            ("Mersin",          36.80, 34.64),
            ("İstanbul",        41.01, 28.97),
            ("İzmir",           38.42, 27.14),
            ("Kars",            40.61, 43.10),
            ("Kastamonu",       41.37, 33.78),
            ("Kayseri",         38.73, 35.49),
            ("Kırklareli",      41.73, 27.22),
            ("Kırşehir",        39.14, 34.16),
            ("Kocaeli",         40.85, 29.88),
            ("Konya",           37.87, 32.48),
            ("Kütahya",         39.42, 29.98),
            ("Malatya",         38.35, 38.32),
            ("Manisa",          38.62, 27.43),
            ("Kahramanmaraş",   37.59, 36.92),
            ("Mardin",          37.32, 40.73),
            ("Muğla",           37.21, 28.37),
            ("Muş",             38.73, 41.49),
            ("Nevşehir",        38.62, 34.71),
            ("Niğde",           37.97, 34.68),
            ("Ordu",            40.98, 37.88),
            ("Rize",            41.02, 40.52),
            ("Sakarya",         40.69, 30.43),
            ("Samsun",          41.29, 36.33),
            ("Siirt",           37.93, 41.95),
            ("Sinop",           42.03, 35.15),
            ("Sivas",           39.74, 37.01),
            ("Tekirdağ",        40.97, 27.50),
            ("Tokat",           40.31, 36.55),
            ("Trabzon",         41.00, 39.72),
            ("Tunceli",         39.11, 39.55),
            ("Şanlıurfa",       37.16, 38.79),
            ("Uşak",            38.68, 29.41),
            ("Van",             38.50, 43.38),
            ("Yozgat",          39.82, 34.80),
            ("Zonguldak",       41.45, 31.80),
            ("Aksaray",         38.37, 34.03),
            ("Bayburt",         40.26, 40.23),
            ("Karaman",         37.18, 33.22),
            ("Kırıkkale",       39.84, 33.52),
            ("Batman",          37.88, 41.13),
            ("Şırnak",          37.52, 42.46),
            ("Bartın",          41.63, 32.34),
            ("Ardahan",         41.11, 42.70),
            ("Iğdır",           39.92, 44.05),
            ("Yalova",          40.65, 29.27),
            ("Karabük",         41.20, 32.63),
            ("Kilis",           36.71, 37.12),
            ("Osmaniye",        37.07, 36.25),
            ("Düzce",           40.84, 31.16),
        };

        private const decimal IdealSicMax = 25m;
        private const decimal IdealSicMin = 10m;
        private const decimal IdealNemMin = 40m;
        private const decimal IdealNemMax = 70m;

        public SehirRiskController(
            IHttpClientFactory httpClientFactory,
            MlService mlService,
            IMemoryCache cache,
            ILogger<SehirRiskController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _mlService = mlService;
            _cache = cache;
            _logger = logger;
        }

        [HttpGet("bugday")]
        public async Task<IActionResult> BugdayRiskAnalizi([FromQuery] bool taze = false)
        {
            const string cacheKey = "sehir_risk_81il_v1";

            if (!taze && _cache.TryGetValue(cacheKey, out object? cached) && cached != null)
                return Ok(cached);

            bool mlAktif = await _mlService.SaglikKontrol();

            // Hava verilerini en fazla 15 eş zamanlı çek — Open-Meteo rate limit önlemi
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(25);
            var semOm = new SemaphoreSlim(15, 15);

            var havaGorevler = Iller.Select(async il =>
            {
                await semOm.WaitAsync();
                try { return await HavasiniCek(httpClient, il.Sehir, il.Enlem, il.Boylam); }
                finally { semOm.Release(); }
            }).ToArray();

            var havaVerileri = await Task.WhenAll(havaGorevler);

            // Hava alınamayan iller "veri yok" olarak dahil edilir — 81 il her zaman döner
            List<SehirRiskDto> sonuclar;

            if (mlAktif)
            {
                var semMl = new SemaphoreSlim(10, 10);
                var mlGorevler = Enumerable.Range(0, Iller.Count).Select(async i =>
                {
                    var hava = havaVerileri[i];
                    if (hava == null) return VeriYok(Iller[i].Sehir);
                    await semMl.WaitAsync();
                    try { return await RiskMl(Iller[i].Sehir, hava); }
                    finally { semMl.Release(); }
                }).ToArray();

                sonuclar = (await Task.WhenAll(mlGorevler))
                    .Where(s => s != null).Cast<SehirRiskDto>().ToList();
            }
            else
            {
                sonuclar = Enumerable.Range(0, Iller.Count)
                    .Select(i => havaVerileri[i] == null
                        ? VeriYok(Iller[i].Sehir)
                        : RiskKural(Iller[i].Sehir, havaVerileri[i]!))
                    .ToList();
            }

            sonuclar = sonuclar.OrderByDescending(s => s.RiskSkoru).ToList();

            var ozet = new
            {
                yuksek  = sonuclar.Count(s => s.RiskSeviyesi == "Yüksek"),
                orta    = sonuclar.Count(s => s.RiskSeviyesi == "Orta"),
                dusuk   = sonuclar.Count(s => s.RiskSeviyesi == "Düşük"),
                veriYok = sonuclar.Count(s => s.RiskSeviyesi == "Veri Yok"),
                toplam  = sonuclar.Count
            };

            var yanit = new { ozet, iller = sonuclar, mlKullanildi = mlAktif };
            _cache.Set(cacheKey, yanit, TimeSpan.FromMinutes(30));
            return Ok(yanit);
        }

        private async Task<SehirRiskDto?> RiskMl(string sehir, HavaVerisi? hava)
        {
            if (hava == null) return null;
            try
            {
                var tahmin = await _mlService.RiskTahminEt(hava);
                if (tahmin == null) return RiskKural(sehir, hava);

                decimal skor = Math.Round(Math.Min((decimal)tahmin.RiskSkoru, 1.0m), 2);
                string seviye = tahmin.Seviye switch
                {
                    "KRITIK" => "Yüksek",
                    "ORTA"   => "Orta",
                    _        => "Düşük"
                };
                var (_, etkenler) = KuralEtkenleri(hava);
                return Dto(sehir, hava, skor, seviye, etkenler);
            }
            catch
            {
                return RiskKural(sehir, hava);
            }
        }

        private static SehirRiskDto VeriYok(string sehir) => new()
        {
            Sehir         = sehir,
            RiskSkoru     = 0,
            RiskSeviyesi  = "Veri Yok",
            RiskEtkenleri = new List<string> { "Hava verisi alınamadı" }
        };

        private static SehirRiskDto RiskKural(string sehir, HavaVerisi hava)
        {
            var (risk, etkenler) = KuralEtkenleri(hava);
            risk = Math.Min(risk, 1.0m);
            string seviye = risk >= 0.50m ? "Yüksek" : risk >= 0.25m ? "Orta" : "Düşük";
            return Dto(sehir, hava, Math.Round(risk, 2), seviye, etkenler);
        }

        private static SehirRiskDto Dto(string sehir, HavaVerisi h, decimal skor, string seviye, List<string> etkenler) =>
            new()
            {
                Sehir         = sehir,
                SicaklikMax   = h.SicaklikMax,
                SicaklikMin   = h.SicaklikMin,
                Nem           = h.Nem,
                Yagis         = h.Yagis,
                RuzgarHizi    = h.RuzgarHizi,
                RiskSkoru     = skor,
                RiskSeviyesi  = seviye,
                RiskEtkenleri = etkenler
            };

        private static (decimal risk, List<string> etkenler) KuralEtkenleri(HavaVerisi h)
        {
            decimal risk = 0;
            var e = new List<string>();

            if (h.SicaklikMin.HasValue && h.SicaklikMin.Value < 0)
            { risk += 0.50m; e.Add($"Don ({h.SicaklikMin.Value:0.#}°C)"); }

            if (h.SicaklikMax.HasValue && h.SicaklikMax.Value > 35)
            { risk += 0.35m; e.Add($"Kavurucu sıcak ({h.SicaklikMax.Value:0.#}°C)"); }
            else if (h.SicaklikMax.HasValue && h.SicaklikMax.Value > IdealSicMax + 3)
            { risk += 0.22m; e.Add($"Sıcaklık yüksek ({h.SicaklikMax.Value:0.#}°C)"); }

            if (h.SicaklikMin.HasValue && h.SicaklikMin.Value >= 0 && h.SicaklikMin.Value < IdealSicMin - 2)
            { risk += 0.18m; e.Add($"Gece soğuk ({h.SicaklikMin.Value:0.#}°C)"); }

            if (h.Yagis.HasValue && h.Yagis.Value > 30)
            { risk += 0.25m; e.Add($"Yoğun yağış ({h.Yagis.Value:0.#} mm)"); }

            if (h.Nem.HasValue && h.Nem.Value < IdealNemMin - 10)
            { risk += 0.20m; e.Add($"Çok kuru (%{h.Nem.Value:0.#})"); }
            else if (h.Nem.HasValue && h.Nem.Value > IdealNemMax + 15)
            { risk += 0.20m; e.Add($"Çok nemli (%{h.Nem.Value:0.#})"); }

            if (h.RuzgarHizi.HasValue && h.RuzgarHizi.Value > 60)
            { risk += 0.15m; e.Add($"Kuvvetli rüzgar ({h.RuzgarHizi.Value:0.#} km/s)"); }

            if (e.Count == 0) e.Add("Risk yok");
            return (risk, e);
        }

        private async Task<HavaVerisi?> HavasiniCek(HttpClient client, string sehir, double enlem, double boylam)
        {
            try
            {
                var url = "https://api.open-meteo.com/v1/forecast" +
                          $"?latitude={enlem.ToString(CultureInfo.InvariantCulture)}" +
                          $"&longitude={boylam.ToString(CultureInfo.InvariantCulture)}" +
                          "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,windspeed_10m_max" +
                          "&hourly=relativehumidity_2m" +
                          "&timezone=Europe%2FIstanbul&forecast_days=1";

                var json = await client.GetStringAsync(url);
                var doc  = JsonDocument.Parse(json);
                var daily  = doc.RootElement.GetProperty("daily");
                var hourly = doc.RootElement.GetProperty("hourly");

                var nemDizi = hourly.GetProperty("relativehumidity_2m");
                var nemler  = new List<decimal>();
                for (int i = 6; i <= 18 && i < nemDizi.GetArrayLength(); i++)
                {
                    var v = Elem(nemDizi[i]);
                    if (v.HasValue) nemler.Add(v.Value);
                }

                return new HavaVerisi
                {
                    SicaklikMax = Elem(daily.GetProperty("temperature_2m_max")[0]),
                    SicaklikMin = Elem(daily.GetProperty("temperature_2m_min")[0]),
                    Yagis       = Elem(daily.GetProperty("precipitation_sum")[0]),
                    RuzgarHizi  = Elem(daily.GetProperty("windspeed_10m_max")[0]),
                    Nem         = nemler.Count > 0 ? Math.Round(nemler.Average(), 1) : null,
                    Tarih       = DateTime.Today
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Hava verisi alınamadı: {Sehir}", sehir);
                return null;
            }
        }

        private static decimal? Elem(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null) return null;
            return el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var v) ? v : null;
        }
    }

    public class SehirRiskDto
    {
        public string Sehir { get; set; } = "";
        public decimal? SicaklikMax { get; set; }
        public decimal? SicaklikMin { get; set; }
        public decimal? Nem { get; set; }
        public decimal? Yagis { get; set; }
        public decimal? RuzgarHizi { get; set; }
        public decimal RiskSkoru { get; set; }
        public string RiskSeviyesi { get; set; } = "";
        public List<string> RiskEtkenleri { get; set; } = new();
    }
}
