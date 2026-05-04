using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;
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
        private readonly MlService _mlService;
        private readonly TelegramService _telegramService;
        private readonly ILogger<AnalizController> _logger;

        public AnalizController(TarimDbContext context, HavaService havaService, MlService mlService, TelegramService telegramService, ILogger<AnalizController> logger)
        {
            _context = context;
            _havaService = havaService;
            _mlService = mlService;
            _telegramService = telegramService;
            _logger = logger;
        }

        // POST /api/Analiz/anlik/{lokasyonId}?taze=true — bugünkü önbelleği atıp Open-Meteo'dan yeniden çeker
        [HttpPost("anlik/{lokasyonId}")]
        public async Task<IActionResult> AnlikAnaliz(int lokasyonId, [FromQuery] bool taze = false)
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

            var hava = await _havaService.GetBugunHavasi(lokasyonId, taze);

            if (hava == null)
                return StatusCode(503, new { message = "Hava durumu verisi alınamadı. Lütfen tekrar deneyin." });

            var urun = lokasyon.UrunBilgisi;
            var mlSonuc = await MlServisiniCagir(hava, lokasyon, urun);

            var yediGunTahmin = await _havaService.GetYediGunlukTahmin(lokasyonId);
            var sulamaYagisOnerileri = SulamaVeYagisOnerileri(urun, hava, yediGunTahmin);

            var kayitTavsiye = mlSonuc.TavsiyeMetni;
            if (sulamaYagisOnerileri.Count > 0)
            {
                kayitTavsiye += Environment.NewLine + Environment.NewLine
                    + "── Sulama ve yağış (5 günlük tahmin) ──" + Environment.NewLine
                    + string.Join(Environment.NewLine + Environment.NewLine, sulamaYagisOnerileri);
            }

            var oneri = new Oneri
            {
                KullaniciId = kullaniciId,
                LokasyonId = lokasyonId,
                UrunId = lokasyon.UrunId,
                RiskSkoru = mlSonuc.RiskSkoru,
                RiskTipi = mlSonuc.RiskTipi,
                TavsiyeMetni = kayitTavsiye,
                OlusturulmaZamani = DateTime.Now
            };

            _context.Oneriler.Add(oneri);
            await _context.SaveChangesAsync();

            // Kritik veya anomali → Telegram bildirimi gönder
            if (_telegramService.Aktif && (mlSonuc.RiskTipi == "Kritik" || mlSonuc.AnomaliBulundu))
            {
                var kullanici = await _context.Kullanicilar.FindAsync(kullaniciId);
                if (!string.IsNullOrWhiteSpace(kullanici?.TelegramChatId))
                {
                    string emoji = mlSonuc.RiskTipi == "Kritik" ? "⛔" : "⚠️";
                    string baslik = mlSonuc.AnomaliBulundu ? "Anormal hava koşulu tespit edildi!" : "Kritik risk uyarısı!";
                    string tarla = KonumEtiketi(lokasyon);
                    string bildirim =
                        $"{emoji} <b>Akıllı Tarım — {baslik}</b>\n\n" +
                        $"🌾 Ürün: {urun.UrunAdi}\n" +
                        $"📍 Tarla: {tarla}\n" +
                        $"📊 Risk skoru: %{(int)Math.Round(mlSonuc.RiskSkoru * 100)}\n\n" +
                        $"<b>Ne yapmalısınız?</b>\n{mlSonuc.TavsiyeMetni.Split('\n')[0]}";

                    _ = _telegramService.MesajGonderAsync(kullanici.TelegramChatId, bildirim);
                }
            }

            return Ok(new AnalizSonucDto
            {
                RiskSkoru = mlSonuc.RiskSkoru,
                RiskTipi = mlSonuc.RiskTipi,
                TavsiyeMetni = mlSonuc.TavsiyeMetni,
                AnomaliBulundu = mlSonuc.AnomaliBulundu,
                MlKullanildi = mlSonuc.MlKullanildi,
                RiskEtkenleri = mlSonuc.RiskEtkenleri,
                UrunId = urun.UrunId,
                UrunAdi = urun.UrunAdi,
                UrunOzeti = UrunOzetiMetni(urun),
                KonumEtiket = KonumEtiketi(lokasyon),
                KonumEnlem = lokasyon.Enlem,
                KonumBoylam = lokasyon.Boylam,
                SulamaYagisOnerileri = sulamaYagisOnerileri.Count > 0 ? sulamaYagisOnerileri : null,
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

        // POST /api/Analiz/tahmin/{lokasyonId}  (7 günlük — ML + fallback)
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
            var sirali = tahminVerisi.OrderBy(h => h.Tarih).ToList();

            // Her gün için ML'den paralel risk skoru al
            var mlSkorlar = new decimal?[sirali.Count];
            bool mlKullanildi = false;
            try
            {
                var mlGorevler = sirali.Select(h => _mlService.RiskTahminEt(h)).ToArray();
                var mlSonuclari = await Task.WhenAll(mlGorevler);
                for (int i = 0; i < mlSonuclari.Length; i++)
                {
                    if (mlSonuclari[i] != null)
                    {
                        mlSkorlar[i] = Math.Round((decimal)mlSonuclari[i]!.RiskSkoru, 2);
                        mlKullanildi = true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML servisi 7 günlük tahmin için yanıt vermedi, kural tabanlı hesaba geçiliyor.");
            }

            var sonuc = sirali.Select((h, i) =>
            {
                decimal skor;
                if (mlSkorlar[i].HasValue)
                {
                    skor = mlSkorlar[i]!.Value;
                }
                else
                {
                    var (kRisk, _) = HesaplaRiskDetay(h, urun, h.Tarih);
                    skor = Math.Round(Math.Min(kRisk, 1.0m), 2);
                }
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
            }).ToList();

            string aciklama = mlKullanildi
                ? $"7 günlük risk skoru Random Forest (ML) ile hesaplandı — {urun?.UrunAdi ?? "ürün"} + hava tahmini."
                : urun == null
                    ? "Ürün atanmadı; günlük risk skoru yalnızca hava etkenlerine göre hesaplanır."
                    : "ML servisi aktif değil; risk skoru kural tabanlı hesaplandı.";

            return Ok(new { tahminler = sonuc, urunId = lokasyon.UrunId, urunAdi = urun?.UrunAdi, mlKullanildi, aciklama });
        }

        private async Task<MlSonuc> MlServisiniCagir(HavaVerisi hava, Lokasyon lokasyon, UrunBilgisi urun)
        {
            try
            {
                var tahmin = await _mlService.RiskTahminEt(hava);
                var anomali = await _mlService.AnomaliTespit(hava);

                var (_, riskler) = HesaplaRiskDetay(hava, urun, DateTime.Today);
                var riskSkoru = (decimal)(tahmin?.RiskSkoru ?? 0);
                bool anomaliBulundu = anomali?.Durum == "anomali";

                var riskTipi = tahmin?.Seviye switch
                {
                    "KRITIK" => "Kritik",
                    "ORTA" => "Uyarı",
                    _ => "Güvenli"
                };

                // Anomali varsa riski en az Uyarı'ya çek; "her şey yolunda" diyemeyiz
                if (anomaliBulundu && riskTipi == "Güvenli")
                    riskTipi = "Uyarı";

                // Anomaliyi etken listesine ekle — tavsiyede görünecek
                if (anomaliBulundu)
                    riskler.Insert(0, "🔬 Bu konum ve mevsim için olağandışı hava koşulları tespit edildi.");

                string yer = KonumEtiketi(lokasyon);

                // Uyarı seviyesinde riskler listesi boşsa (anomali dışında kural tetiklenmedi),
                // mevcut hava durumunu özet olarak yaz
                string etkenler = riskler.Any()
                    ? string.Join(" ", riskler) + " "
                    : HavaOzetCumlesi(hava) + " ";

                var tavsiye = riskTipi switch
                {
                    "Kritik" =>
                        $"⛔ {yer} — {urun.UrunAdi} için bugün risk yüksek. "
                        + etkenler
                        + "Ekim, ilaçlama ve makine işlerini bugün erteleyin. Yarın tekrar bakın.",
                    "Uyarı" =>
                        $"⚠️ {yer} — {urun.UrunAdi} için dikkat gerektiren koşullar var. "
                        + etkenler
                        + "Tarlayı gözlemleyin; hava durumu netleşince karar verin.",
                    _ => GuvenliTavsiyeMetni(lokasyon, urun, hava, riskSkoru, mlKullanildi: true)
                };

                return new MlSonuc
                {
                    RiskSkoru = Math.Round(riskSkoru, 2),
                    RiskTipi = riskTipi,
                    TavsiyeMetni = tavsiye,
                    AnomaliBulundu = anomaliBulundu,
                    MlKullanildi = true,
                    RiskEtkenleri = GenisletilmisRiskListesi(riskler, lokasyon, hava)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ML servisi anlık analiz için yanıt vermedi, kural tabanlı hesaba geçiliyor. LokasyonId={LokasyonId}", lokasyon.LokasyonId);
                return KuralTabanliHesapla(hava, lokasyon, urun);
            }
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

        private static string HavaOzetCumlesi(HavaVerisi h)
        {
            string max = h.SicaklikMax.HasValue ? Formatta(h.SicaklikMax.Value) + "°C" : "—";
            string min = h.SicaklikMin.HasValue ? Formatta(h.SicaklikMin.Value) + "°C" : "—";
            string nem = h.Nem.HasValue ? "%" + Formatta(h.Nem.Value) : "—";
            string yag = (h.Yagis ?? 0) > 0 ? Formatta(h.Yagis!.Value) + " mm yağmur" : "yağmur yok";
            string ruz = h.RuzgarHizi.HasValue ? Formatta(h.RuzgarHizi.Value) + " km/s rüzgar" : "rüzgar yok";
            return $"Bugün: en yüksek {max}, gece en düşük {min}, nem {nem}, {yag}, {ruz}.";
        }

        private static string Formatta(decimal d) =>
            d == Math.Truncate(d) ? ((int)d).ToString(CultureInfo.GetCultureInfo("tr-TR"))
                : d.ToString("0.##", CultureInfo.GetCultureInfo("tr-TR"));

        /// <summary>Güvenli senaryoda hava değerlerine göre bağlamlı, kısa tavsiye üretir.</summary>
        private static string GuvenliTavsiyeMetni(
            Lokasyon lokasyon, UrunBilgisi urun, HavaVerisi hava, decimal riskSkoru, bool mlKullanildi)
        {
            string yer = KonumEtiketi(lokasyon);
            var sb = new StringBuilder();
            decimal yagis = hava.Yagis ?? 0;
            string sicaklik = hava.SicaklikMax.HasValue ? $"{Formatta(hava.SicaklikMax.Value)}°C" : "—";

            // Açılış — yağış durumunu yansıt
            string havaDurumu;
            if (yagis >= 10)
                havaDurumu = $"yağışlı ({Formatta(yagis)} mm yağış)";
            else if (yagis >= 3)
                havaDurumu = $"hafif yağışlı ({Formatta(yagis)} mm, {sicaklik})";
            else if (hava.SicaklikMax.HasValue && hava.SicaklikMax.Value > 30)
                havaDurumu = $"sıcak ({sicaklik})";
            else if (hava.SicaklikMax.HasValue && hava.SicaklikMax.Value < 10)
                havaDurumu = $"serin ({sicaklik})";
            else
                havaDurumu = $"ılıman ({sicaklik})";

            sb.AppendLine($"✅ {yer} — {urun.UrunAdi} için bugün belirgin bir risk yok. Hava {havaDurumu}.");
            sb.AppendLine();

            // Yağış varken sulama önerme; yokken sıcaklık/nem bazlı tavsiye ver
            if (yagis >= 10)
            {
                sb.AppendLine($"Yağış var ({Formatta(yagis)} mm); bugün sulama yapmayın. Toprağın nem durumunu takip edin.");
                if (hava.Nem.HasValue && hava.Nem.Value > 75)
                    sb.AppendLine($"Nem de yüksek (%{Formatta(hava.Nem.Value)}); yaprakları küf ve hastalık belirtileri için kontrol edin.");
            }
            else if (yagis >= 3)
            {
                sb.AppendLine($"Hafif yağış var ({Formatta(yagis)} mm); toprağı yoklayın, ıslaksa sulamayı erteleyin.");
            }
            else
            {
                // Sıcaklığa göre tavsiye
                if (hava.SicaklikMax.HasValue && hava.SicaklikMax.Value > 30)
                    sb.AppendLine("Sıcak havalarda sulamayı sabah erken ya da akşam güneş baterken yapın. Öğle arası verin.");
                else if (hava.SicaklikMax.HasValue && hava.SicaklikMax.Value < 10)
                    sb.AppendLine("Serin havada ilaçlama ve gübre daha yavaş etki eder. Geceleri sıcaklığın sıfıra yaklaşıp yaklaşmadığını takip edin.");
                else
                    sb.AppendLine("Hava ılıman; rutin bakım planınızı sürdürebilirsiniz.");

                // Nem durumu
                if (hava.Nem.HasValue)
                {
                    if (hava.Nem.Value < 40)
                        sb.AppendLine($"Nem düşük (%{Formatta(hava.Nem.Value)}); toprağı elle yoklayın, kuru ise sulayın.");
                    else if (hava.Nem.Value > 75)
                        sb.AppendLine($"Nem yüksek (%{Formatta(hava.Nem.Value)}); yaprakları küf veya leke için gözlemleyin.");
                }
            }

            // Rüzgar
            if (hava.RuzgarHizi.HasValue && hava.RuzgarHizi.Value > 25)
                sb.AppendLine($"Rüzgar var ({Formatta(hava.RuzgarHizi.Value)} km/s); ilaçlama için daha sakin bir gün bekleyin.");

            return sb.ToString().TrimEnd();
        }

        private static string KonumEtiketi(Lokasyon l) =>
            string.IsNullOrWhiteSpace(l.Ilce)
                ? l.Sehir
                : $"{l.Ilce}, {l.Sehir}";

        /// <summary>
        /// 5 günlük yağış tahmini ve ürün tipine göre sulama yönlendirmesi (eğitim projesi — sahada toprak nemi ile doğrulanmalı).
        /// </summary>
        private static List<string> SulamaVeYagisOnerileri(
            UrunBilgisi urun, HavaVerisi bugun, List<HavaVerisi>? tahminGunler)
        {
            var sonuc = new List<string>();
            var tr = CultureInfo.GetCultureInfo("tr-TR");

            if (tahminGunler == null || tahminGunler.Count == 0)
            {
                sonuc.Add(
                    "5 günlük yağış tahmini alınamadı. Sulama planı için sayfayı yenileyip tekrar analiz alın veya alttaki 7 günlük tahmin tablosunu kullanın.");
                return sonuc;
            }

            var gunler = tahminGunler.OrderBy(h => h.Tarih).Take(5).ToList();
            decimal toplamYagis = gunler.Sum(h => h.Yagis ?? 0);
            var maksler = gunler.Where(h => h.SicaklikMax.HasValue).Select(h => h.SicaklikMax!.Value).ToList();
            decimal? ortMaks = maksler.Count > 0
                ? Math.Round(maksler.Average(), 1, MidpointRounding.AwayFromZero)
                : null;

            string gunSatirlari = string.Join(" · ", gunler.Select(h =>
            {
                string gunAdi = h.Tarih.ToString("ddd", tr);
                decimal y = h.Yagis ?? 0;
                return $"{gunAdi} {h.Tarih:dd.MM}: {Formatta(y)} mm";
            }));

            sonuc.Add(
                $"💧 Önümüzdeki 5 günlük yağış tahmini: {gunSatirlari}. Toplam yaklaşık {Formatta(toplamYagis)} mm bekleniyor.");

            string ad = urun.UrunAdi.Trim();
            bool bugday = ad.Contains("Buğday", StringComparison.OrdinalIgnoreCase);
            bool arpa = ad.Contains("Arpa", StringComparison.OrdinalIgnoreCase);
            bool misir = ad.Contains("Mısır", StringComparison.OrdinalIgnoreCase);
            decimal nemMinRef = urun.IdealNemMin ?? 40;
            bool nemDusuk = bugun.Nem.HasValue && bugun.Nem.Value < nemMinRef - 8;
            bool sicak = bugun.SicaklikMax.HasValue && bugun.SicaklikMax.Value >= 22;
            decimal bugunYagis = bugun.Yagis ?? 0;

            // Bugün önemli yağış varsa sulama tavsiyesini tamamen değiştir
            if (bugunYagis >= 10)
            {
                string tahminYorum = toplamYagis >= 20
                    ? $"Bu hafta da {Formatta(toplamYagis)} mm yağış bekleniyor; sulama gerekmiyor. Su birikintisi ve drenajı kontrol edin."
                    : $"Önümüzdeki günler daha az yağış bekleniyor (~{Formatta(toplamYagis)} mm); yarın toprağı yoklayın ve kuru ise sulamayı değerlendirin.";
                sonuc.Add($"🌧 Bugün {Formatta(bugunYagis)} mm yağış var; sulama yapmayın. {tahminYorum}");
                return sonuc;
            }

            if (bugday || arpa)
            {
                if (toplamYagis < 5m)
                {
                    string ekStr = (sicak || nemDusuk)
                        ? "Bugün hava da sıcak veya kuru; sulamayı geciktirmeyin."
                        : "Bir sonraki yağışa kadar toprağı gözlemleyin.";
                    sonuc.Add(
                        $"{urun.UrunAdi} için bu hafta yağmur az (~{Formatta(toplamYagis)} mm). "
                        + $"Toprağı elle yoklayın; kuru ise sulayın. {ekStr}");
                }
                else if (toplamYagis < 20m)
                {
                    sonuc.Add(
                        $"Yağmur biraz var (~{Formatta(toplamYagis)} mm / 5 gün), ortalama sıcaklık ~{ortMaks?.ToString("0.#", tr) ?? "—"}°C. "
                        + "Toprak hâlâ kuruyorsa ek sulama yapın; nemliyse bekleyebilirsiniz.");
                }
                else
                {
                    sonuc.Add(
                        $"Bu hafta yeterli yağmur bekleniyor (~{Formatta(toplamYagis)} mm). "
                        + "Sulama yapmayın; su birikintisine dikkat edin.");
                }
            }
            else if (misir)
            {
                if (toplamYagis < 8m && sicak)
                {
                    sonuc.Add(
                        "Mısır sıcakta çok su ister; bu hafta yağmur az. Toprağı kontrol edip sulamayı aksatmayın.");
                }
                else if (toplamYagis >= 30m)
                    sonuc.Add("Bu hafta çok yağmur var. Sulamayı bırakın; su birikintisi ve kök çürümesine dikkat edin.");
                else
                    sonuc.Add(
                        $"Yağış toplamı ~{Formatta(toplamYagis)} mm. Toprağa bakarak sulama kararı verin.");
            }
            else
            {
                string nemBugün = bugun.Nem.HasValue ? $"%{bugun.Nem.Value.ToString("0.#", tr)}" : "—";
                sonuc.Add(
                    $"Bugün nem {nemBugün}; bu hafta toplam ~{Formatta(toplamYagis)} mm yağmur bekleniyor. "
                    + "Toprak ve bitkiye bakarak sulama kararı verin.");
            }

            return sonuc;
        }

        private static List<string> GenisletilmisRiskListesi(List<string> riskler, Lokasyon lokasyon, HavaVerisi hava)
        {
            if (riskler == null || riskler.Count == 0)
            {
                string yer = KonumEtiketi(lokasyon);
                string sicaklik = hava.SicaklikMax.HasValue ? $"{Formatta(hava.SicaklikMax.Value)}°C" : "—";
                string nem = hava.Nem.HasValue ? $"%{Formatta(hava.Nem.Value)}" : "—";
                string yagis = (hava.Yagis ?? 0) > 0 ? $"{Formatta(hava.Yagis!.Value)} mm yağış" : "yağış yok";
                return new List<string>
                {
                    $"✅ Bugün {yer} için dikkat çeken bir risk yok. Sıcaklık {sicaklik}, nem {nem}, {yagis}."
                };
            }

            return riskler;
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
                string donSiddet = hava.SicaklikMin.Value < -3 ? "Dondurucu soğuk" : "Don tehlikesi";
                riskler.Add(
                    $"❄️ {donSiddet} — gece sıcaklığı {Formatta(hava.SicaklikMin.Value)}°C. "
                    + "Çiçekli veya meyveli bitkilerinizi örtün; sabah erken tarlayı kontrol edin.");
            }

            if (hava.SicaklikMax.HasValue && hava.SicaklikMax > 38)
            {
                risk += 0.3m;
                riskler.Add(
                    $"🌡️ Kavurucu sıcak — bugün {Formatta(hava.SicaklikMax.Value)}°C. "
                    + "Öğle saatlerinde tarlada çalışmayın; sabah erken veya akşam üstü sulayın.");
            }

            if (hava.Yagis.HasValue && hava.Yagis > 30)
            {
                risk += 0.25m;
                riskler.Add(
                    $"🌧️ Yoğun yağış — {Formatta(hava.Yagis.Value)} mm yağdı. "
                    + "Tarlaya traktörle girmeyin; su birikmesine dikkat edin.");
            }

            if (hava.Nem.HasValue && hava.Nem < 30 && (hava.Yagis == null || hava.Yagis < 1))
            {
                risk += 0.2m;
                riskler.Add(
                    $"🏜️ Hava çok kuru — nem %{Formatta(hava.Nem.Value)}, yağış yok. "
                    + "Toprağınızı kontrol edin; kuruyorsa sulamayı bekletmeyin.");
            }

            if (hava.RuzgarHizi.HasValue && hava.RuzgarHizi > 60)
            {
                risk += 0.2m;
                riskler.Add(
                    $"💨 Kuvvetli rüzgar — {Formatta(hava.RuzgarHizi.Value)} km/s. "
                    + "Bugün ilaçlama ve gübre sıkma yapmayın; rüzgar dinene kadar bekleyin.");
            }

            if (urun != null)
            {
                if (urun.IdealSicaklikMax.HasValue && hava.SicaklikMax.HasValue
                    && hava.SicaklikMax > urun.IdealSicaklikMax + 3)
                {
                    risk += 0.22m;
                    riskler.Add(
                        $"🌿 {urun.UrunAdi} için hava fazla sıcak — {Formatta(hava.SicaklikMax.Value)}°C "
                        + $"(bu ürün için üst sınır ~{Formatta(urun.IdealSicaklikMax.Value)}°C). "
                        + "Öğle sıcağında sulamayı artırın; yapraklarda yanma varsa gölge verin.");
                }

                if (urun.IdealSicaklikMin.HasValue && hava.SicaklikMin.HasValue
                    && hava.SicaklikMin < urun.IdealSicaklikMin - 2)
                {
                    risk += 0.2m;
                    riskler.Add(
                        $"🌿 {urun.UrunAdi} için hava fazla soğuk — {Formatta(hava.SicaklikMin.Value)}°C "
                        + $"(bu ürün için alt sınır ~{Formatta(urun.IdealSicaklikMin.Value)}°C). "
                        + "Büyüme yavaşlayabilir; don riski varsa bitkilerinizi koruyun.");
                }

                if (urun.IdealNemMin.HasValue && hava.Nem.HasValue && hava.Nem < urun.IdealNemMin - 10)
                {
                    risk += 0.15m;
                    riskler.Add(
                        $"💧 {urun.UrunAdi} susuz kalıyor olabilir — nem %{Formatta(hava.Nem.Value)} "
                        + $"(bu ürün için ideal %{Formatta(urun.IdealNemMin.Value)} üzeri). "
                        + "Toprağı elle yoklayın; kuru ise sulama yapın.");
                }

                if (urun.IdealNemMax.HasValue && hava.Nem.HasValue && hava.Nem > urun.IdealNemMax + 15)
                {
                    risk += 0.12m;
                    riskler.Add(
                        $"🍄 Hastalık riski — nem çok yüksek (%{Formatta(hava.Nem.Value)}). "
                        + $"{urun.UrunAdi} için ideal nem %{Formatta(urun.IdealNemMax.Value)} altında olmalı. "
                        + "Yaprakları kontrol edin; küf veya leke varsa ilaçlamayı düşünün.");
                }

                var ekimAylari = EkimAylariniOku(urun.EkimAylari);
                if (ekimAylari.Count > 0 && !ekimAylari.Contains(referansGun.Month))
                {
                    risk += 0.08m;
                    var trCal = CultureInfo.GetCultureInfo("tr-TR");
                    string buAy = trCal.DateTimeFormat.GetMonthName(referansGun.Month);
                    var liste = ekimAylari.Select(a => trCal.DateTimeFormat.GetMonthName(a)).ToList();
                    riskler.Add(
                        $"📅 Mevsim hatırlatması — {buAy} ayındasınız; {urun.UrunAdi} için tipik ekim zamanı: {string.Join(", ", liste)}. "
                        + "Bakım ve hasat takviminizi gözden geçirin.");
                }
            }

            return (risk, riskler);
        }

        private static MlSonuc KuralTabanliHesapla(HavaVerisi hava, Lokasyon lokasyon, UrunBilgisi urun)
        {
            var (risk, riskler) = HesaplaRiskDetay(hava, urun, DateTime.Today);
            risk = Math.Min(risk, 1.0m);

            string tavsiye;
            string riskTipi;
            string yerKural = KonumEtiketi(lokasyon);

            if (risk > 0.70m)
            {
                riskTipi = "Kritik";
                bool don = riskler.Any(r => r.Contains("don", StringComparison.OrdinalIgnoreCase));
                tavsiye = don
                    ? $"⛔ {yerKural} — {urun.UrunAdi} için don tehlikesi! Gece sıcaklığı {hava.SicaklikMin}°C. "
                      + "Hassas bitkilerinizi örtün; sabah erken tarlayı kontrol edin."
                    : $"⛔ {yerKural} — {urun.UrunAdi} için bugün risk yüksek. "
                      + string.Join(" ", riskler)
                      + " Tarlada makine işi, ilaçlama ve ekim yapmayın; hava düzelince devam edin.";
            }
            else if (risk > 0.40m)
            {
                riskTipi = "Uyarı";
                bool kuru = riskler.Any(r => r.Contains("kuru", StringComparison.OrdinalIgnoreCase)
                    || r.Contains("nem", StringComparison.OrdinalIgnoreCase));
                tavsiye = kuru
                    ? $"⚠️ {yerKural} — {urun.UrunAdi} tarlası kuruma belirtisi gösteriyor (nem %{hava.Nem}). "
                      + "Toprağı kontrol edin; gerekirse sulayın. Yarın tekrar bakın."
                    : $"⚠️ {yerKural} — {urun.UrunAdi} için dikkat. "
                      + string.Join(" ", riskler)
                      + " Tarlayı gözlemleyin; hava düzelirse işlere devam edebilirsiniz.";
            }
            else
            {
                riskTipi = "Güvenli";
                tavsiye = GuvenliTavsiyeMetni(lokasyon, urun, hava, risk, mlKullanildi: false);
            }

            return new MlSonuc
            {
                RiskSkoru = Math.Round(risk, 2),
                RiskTipi = riskTipi,
                TavsiyeMetni = tavsiye,
                AnomaliBulundu = false,
                RiskEtkenleri = GenisletilmisRiskListesi(riskler, lokasyon, hava)
            };
        }
    }

    public class AnalizSonucDto
    {
        public decimal RiskSkoru { get; set; }
        public string? RiskTipi { get; set; }
        public string? TavsiyeMetni { get; set; }
        public bool AnomaliBulundu { get; set; }
        public bool MlKullanildi { get; set; }
        public List<string>? RiskEtkenleri { get; set; }
        public int? UrunId { get; set; }
        public string? UrunAdi { get; set; }
        public string? UrunOzeti { get; set; }
        /// <summary>Analizde kullanılan tarla konumu (Open-Meteo isteği bu koordinatlarla yapılır).</summary>
        public string? KonumEtiket { get; set; }
        public decimal? KonumEnlem { get; set; }
        public decimal? KonumBoylam { get; set; }
        /// <summary>Open-Meteo 5 günlük yağış tahmini + ürüne göre sulama ipuçları.</summary>
        public List<string>? SulamaYagisOnerileri { get; set; }
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
        public bool MlKullanildi { get; set; }
        public List<string> RiskEtkenleri { get; set; } = new();
    }
}