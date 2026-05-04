using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
using TarimSistemi.Data;
using TarimSistemi.Models;

namespace TarimSistemi.Services
{
    public class GunlukBildirimServisi : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<GunlukBildirimServisi> _logger;
        private readonly IConfiguration _configuration;

        public GunlukBildirimServisi(
            IServiceScopeFactory scopeFactory,
            ILogger<GunlukBildirimServisi> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _configuration = configuration;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTime.Now;
                var bildirimSaati = int.TryParse(_configuration["Telegram:GunlukBildirimSaati"], out var s) ? s : 8;
                var sonraki = new DateTime(now.Year, now.Month, now.Day, bildirimSaati, 0, 0);
                if (now >= sonraki) sonraki = sonraki.AddDays(1);

                _logger.LogInformation("Günlük tarım bildirimi {Zaman} tarihinde çalışacak.", sonraki);
                await Task.Delay(sonraki - now, stoppingToken);

                if (stoppingToken.IsCancellationRequested) break;

                try { await BildirimleriGonder(stoppingToken); }
                catch (Exception ex) { _logger.LogError(ex, "Günlük bildirim döngüsünde hata."); }
            }
        }

        /// <summary>Belirli bir kullanıcıya anlık günlük rapor gönderir (test/manuel tetikleme).</summary>
        public async Task<(bool Ok, string Message)> TekKullaniciGonder(int kullaniciId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TarimDbContext>();
            var hava = scope.ServiceProvider.GetRequiredService<HavaService>();
            var ml = scope.ServiceProvider.GetRequiredService<MlService>();
            var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();

            if (!telegram.Aktif)
                return (false, "Telegram botu yapılandırılmamış.");

            var kullanici = await db.Kullanicilar
                .Include(k => k.Lokasyonlar).ThenInclude(l => l.UrunBilgisi)
                .FirstOrDefaultAsync(k => k.KullaniciId == kullaniciId);

            if (kullanici == null || string.IsNullOrWhiteSpace(kullanici.TelegramChatId))
                return (false, "Kullanıcının Telegram hesabı bağlı değil.");

            var aktifLokasyonlar = kullanici.Lokasyonlar
                .Where(l => l.UrunId.HasValue && l.UrunBilgisi != null)
                .ToList();

            if (!aktifLokasyonlar.Any())
                return (false, "Ürün atanmış tarla bulunamadı. Tarlalarım sayfasından tarlanıza bir ürün atayın.");

            await KullaniciyaGonder(kullanici, hava, ml, telegram);
            return (true, "Günlük tarım raporu Telegram'a gönderildi!");
        }

        private async Task BildirimleriGonder(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TarimDbContext>();
            var hava = scope.ServiceProvider.GetRequiredService<HavaService>();
            var ml = scope.ServiceProvider.GetRequiredService<MlService>();
            var telegram = scope.ServiceProvider.GetRequiredService<TelegramService>();

            if (!telegram.Aktif) return;

            var kullanicilar = await db.Kullanicilar
                .AsNoTracking()
                .Where(k => k.TelegramChatId != null && k.EmailOnayli)
                .Include(k => k.Lokasyonlar).ThenInclude(l => l.UrunBilgisi)
                .ToListAsync(ct);

            _logger.LogInformation("Günlük bildirim: {Adet} kullanıcı işlenecek.", kullanicilar.Count);

            foreach (var kullanici in kullanicilar)
            {
                if (ct.IsCancellationRequested) break;
                try { await KullaniciyaGonder(kullanici, hava, ml, telegram); }
                catch (Exception ex) { _logger.LogWarning(ex, "Kullanici {Id} bildirimi başarısız.", kullanici.KullaniciId); }
            }
        }

        private async Task KullaniciyaGonder(
            Kullanici kullanici, HavaService havaService, MlService mlService, TelegramService telegram)
        {
            var lokasyonlar = kullanici.Lokasyonlar
                .Where(l => l.UrunId.HasValue && l.UrunBilgisi != null)
                .ToList();

            if (!lokasyonlar.Any()) return;

            var tr = CultureInfo.GetCultureInfo("tr-TR");
            var sb = new StringBuilder();
            sb.AppendLine($"🌅 <b>Günlük Tarım Raporu — {DateTime.Now.ToString("dd MMMM yyyy", tr)}</b>");
            sb.AppendLine($"Merhaba <b>{System.Net.WebUtility.HtmlEncode(kullanici.AdSoyad)}</b>!\n");

            bool veri = false;

            foreach (var lokasyon in lokasyonlar)
            {
                var havaVerisi = await havaService.GetBugunHavasi(lokasyon.LokasyonId, false);
                if (havaVerisi == null) continue;

                veri = true;
                var urun = lokasyon.UrunBilgisi!;
                string konum = string.IsNullOrWhiteSpace(lokasyon.Ilce)
                    ? lokasyon.Sehir
                    : $"{lokasyon.Ilce}, {lokasyon.Sehir}";

                var (riskSkoru, riskTipi, anomali, mlKullanildi) = await RiskHesapla(havaVerisi, urun, mlService);

                string emoji = riskTipi switch { "Kritik" => "⛔", "Uyarı" => "⚠️", _ => "✅" };

                sb.AppendLine($"{emoji} <b>{urun.UrunAdi}</b> · {konum}");
                sb.AppendLine(HavaSatiri(havaVerisi));
                sb.AppendLine($"📊 Risk: <b>{riskTipi}</b> (%{(int)Math.Round(riskSkoru * 100)}) " +
                              $"<i>({(mlKullanildi ? "ML" : "kural tabanlı")})</i>");

                if (anomali)
                    sb.AppendLine("🔬 <i>Bu konum için olağandışı hava koşulu tespit edildi.</i>");

                sb.AppendLine($"<i>{KisaTavsiye(riskTipi, havaVerisi, urun, anomali)}</i>\n");
            }

            if (!veri) return;

            sb.AppendLine("─────────────────");
            sb.Append("<i>Detaylı analiz ve 7 günlük tahmin için Akıllı Tarım uygulamasını açın.</i>");

            await telegram.MesajGonderAsync(kullanici.TelegramChatId!, sb.ToString());
            _logger.LogInformation("Günlük bildirim gönderildi. KullaniciId={Id}", kullanici.KullaniciId);
        }

        private static async Task<(decimal Skor, string Tip, bool Anomali, bool MlKullanildi)> RiskHesapla(
            HavaVerisi hava, UrunBilgisi urun, MlService mlService)
        {
            try
            {
                var tahmin = await mlService.RiskTahminEt(hava);
                var anomali = await mlService.AnomaliTespit(hava);

                if (tahmin != null)
                {
                    bool anomaliBulundu = anomali?.Durum == "anomali";
                    var tip = tahmin.Seviye switch
                    {
                        "KRITIK" => "Kritik",
                        "ORTA" => "Uyarı",
                        _ => "Güvenli"
                    };
                    // Anomali varsa "Güvenli" diyemeyiz
                    if (anomaliBulundu && tip == "Güvenli") tip = "Uyarı";
                    return (Math.Round((decimal)tahmin.RiskSkoru, 2), tip, anomaliBulundu, true);
                }
            }
            catch { /* ML kapalıysa kural tabanlı hesapla */ }

            var (skor, tipKural) = KuralTabanliRisk(hava, urun);
            return (skor, tipKural, false, false);
        }

        private static (decimal Skor, string Tip) KuralTabanliRisk(HavaVerisi hava, UrunBilgisi urun)
        {
            decimal risk = 0;

            if (hava.SicaklikMin.HasValue && hava.SicaklikMin < 2) risk += 0.50m;
            if (hava.SicaklikMax.HasValue && hava.SicaklikMax > 38) risk += 0.30m;
            if ((hava.Yagis ?? 0) > 30) risk += 0.25m;
            if (hava.Nem.HasValue && hava.Nem < 30) risk += 0.20m;
            if (hava.RuzgarHizi.HasValue && hava.RuzgarHizi > 60) risk += 0.20m;

            if (urun.IdealSicaklikMax.HasValue && hava.SicaklikMax.HasValue
                && hava.SicaklikMax > urun.IdealSicaklikMax + 3) risk += 0.22m;
            if (urun.IdealSicaklikMin.HasValue && hava.SicaklikMin.HasValue
                && hava.SicaklikMin < urun.IdealSicaklikMin - 2) risk += 0.20m;
            if (urun.IdealNemMin.HasValue && hava.Nem.HasValue
                && hava.Nem < urun.IdealNemMin - 10) risk += 0.15m;

            risk = Math.Min(risk, 1.0m);
            return (Math.Round(risk, 2), risk > 0.70m ? "Kritik" : risk > 0.40m ? "Uyarı" : "Güvenli");
        }

        private static string HavaSatiri(HavaVerisi h)
        {
            var parcalar = new List<string>();

            if (h.SicaklikMin.HasValue && h.SicaklikMax.HasValue)
                parcalar.Add($"🌡 {Fmt(h.SicaklikMin.Value)}–{Fmt(h.SicaklikMax.Value)}°C");
            else if (h.SicaklikMax.HasValue)
                parcalar.Add($"🌡 {Fmt(h.SicaklikMax.Value)}°C");

            if (h.Nem.HasValue)
                parcalar.Add($"💧 %{Fmt(h.Nem.Value)}");

            if ((h.Yagis ?? 0) > 0)
                parcalar.Add($"🌧 {Fmt(h.Yagis!.Value)} mm");

            if (h.RuzgarHizi.HasValue && h.RuzgarHizi.Value > 20)
                parcalar.Add($"💨 {Fmt(h.RuzgarHizi.Value)} km/s");

            return string.Join("  ", parcalar);
        }

        private static string KisaTavsiye(string riskTipi, HavaVerisi hava, UrunBilgisi urun, bool anomali)
        {
            string ad = urun.UrunAdi;
            decimal nem = hava.Nem ?? 50;
            decimal sicMax = hava.SicaklikMax ?? 20;
            decimal sicMin = hava.SicaklikMin ?? 10;
            decimal yagis = hava.Yagis ?? 0;
            decimal ruzgar = hava.RuzgarHizi ?? 0;

            if (riskTipi == "Kritik")
            {
                if (sicMin < 0)
                    return $"{ad} için don tehlikesi! Hassas bitkilerinizi örtün, sabah erken kontrol edin.";
                if (sicMax > 38)
                    return $"Kavurucu sıcak ({Fmt(sicMax)}°C). {ad} için öğle saatlerinde tarlada çalışmayın, sulamayı artırın.";
                if (yagis > 30)
                    return $"Yoğun yağış ({Fmt(yagis)} mm). {ad} tarlasına makineyle girmeyin, su birikmesini kontrol edin.";
                if (ruzgar > 60)
                    return $"Kuvvetli rüzgar ({Fmt(ruzgar)} km/s). {ad} için ilaçlama ve gübre uygulaması yapmayın.";
                return $"{ad} için bugün kritik koşullar var. Ekim, ilaçlama ve makine işlerini erteleyin.";
            }

            if (riskTipi == "Uyarı")
            {
                // Anomali varsa hava durumu referanslı genel uyarı
                if (anomali && yagis >= 5)
                    return $"Olağandışı hava: {Fmt(yagis)} mm yağış var. {ad} için sulama yapmayın; toprağı ve bitkiyi gözlemleyin.";
                if (anomali)
                    return $"Bu konum için olağandışı hava koşulları var. {ad} tarlasını yakından gözlemleyin.";

                // Anomali yoksa koşul bazlı
                if (yagis >= 10)
                    return $"Yoğun yağış ({Fmt(yagis)} mm). {ad} için sulama yapmayın; su birikmesine dikkat edin.";
                if (urun.IdealNemMin.HasValue && nem < urun.IdealNemMin.Value - 10)
                    return $"{ad} nem ihtiyacı yüksek (şu an %{Fmt(nem)}, ideal %{Fmt(urun.IdealNemMin.Value)}+). Toprağı yoklayın, sulayın.";
                if (urun.IdealNemMax.HasValue && nem > urun.IdealNemMax.Value + 15)
                    return $"Nem çok yüksek (%{Fmt(nem)}). {ad} yapraklarını küf ve hastalık belirtileri için kontrol edin.";
                if (urun.IdealSicaklikMax.HasValue && sicMax > urun.IdealSicaklikMax.Value + 3)
                    return $"Hava {ad} için fazla sıcak ({Fmt(sicMax)}°C, ideal üst sınır {Fmt(urun.IdealSicaklikMax.Value)}°C). Sulamayı artırın.";
                if (urun.IdealSicaklikMin.HasValue && sicMin < urun.IdealSicaklikMin.Value - 2)
                    return $"Gece soğuyor ({Fmt(sicMin)}°C). {ad} büyümesi yavaşlayabilir; don riski varsa bitkilerinizi koruyun.";
                return $"{ad} için dikkat gerektiren koşullar var. Tarlayı gözlemleyin.";
            }

            // Güvenli
            if (yagis >= 10)
                return $"Yağmur var ({Fmt(yagis)} mm). {ad} için bugün sulama yapmayın; toprağın nemini takip edin.";
            if (yagis >= 3)
                return $"Hafif yağış var ({Fmt(yagis)} mm). Toprağı yoklayın, ıslaksa sulamayı erteleyin.";
            if (sicMax > 28 && (urun.IdealSicaklikMax ?? 99) < 32)
                return $"Hava sıcak ama {ad} için risk yok. Sulamayı sabah erken veya akşam serinliğinde yapın.";
            if (nem < 40 && (urun.IdealNemMin ?? 0) > 40)
                return $"Nem biraz düşük (%{Fmt(nem)}). {ad} toprağını yoklayın; kuruyorsa sulayın.";

            return $"{ad} için bugün koşullar normal. Rutin bakım ve sulamaya devam edebilirsiniz.";
        }

        private static string Fmt(decimal d)
        {
            var tr = CultureInfo.GetCultureInfo("tr-TR");
            return d == Math.Truncate(d) ? ((int)d).ToString() : d.ToString("0.#", tr);
        }
    }
}