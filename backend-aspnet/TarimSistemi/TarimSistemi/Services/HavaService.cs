using System.Text.Json;
using TarimSistemi.Data;
using TarimSistemi.Models;
using Microsoft.EntityFrameworkCore;

namespace TarimSistemi.Services
{
    public class HavaService
    {
        private readonly TarimDbContext _context;
        private readonly HttpClient _httpClient;

        public HavaService(TarimDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // Bugünün hava verisini döner — önce DB cache'e bakar, yoksa Open-Meteo'dan çeker
        public async Task<HavaVerisi?> GetBugunHavasi(int lokasyonId)
        {
            var bugun = DateTime.Today;

            var cached = await _context.HavaVerileri
                .FirstOrDefaultAsync(h => h.LokasyonId == lokasyonId && h.Tarih == bugun && !h.AnomaliDurumu);

            if (cached != null)
                return cached;

            var lokasyon = await _context.Lokasyonlar.FindAsync(lokasyonId);
            if (lokasyon == null)
                return null;

            return await CekVeKaydet(lokasyon, bugun);
        }

        // LSTM için 7 günlük tahmin verisini döner
        public async Task<List<HavaVerisi>> GetYediGunlukTahmin(int lokasyonId)
        {
            var lokasyon = await _context.Lokasyonlar.FindAsync(lokasyonId);
            if (lokasyon == null)
                return new List<HavaVerisi>();

            return await CekCokGunluk(lokasyon, 7);
        }

        private async Task<HavaVerisi?> CekVeKaydet(Lokasyon lokasyon, DateTime tarih)
        {
            try
            {
                var url = $"https://api.open-meteo.com/v1/forecast" +
                          $"?latitude={lokasyon.Enlem.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                          $"&longitude={lokasyon.Boylam.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                          $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,windspeed_10m_max" +
                          $"&hourly=relativehumidity_2m" +
                          $"&timezone=Europe%2FIstanbul" +
                          $"&forecast_days=1";

                var json = await _httpClient.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);

                var daily = doc.RootElement.GetProperty("daily");
                var hourly = doc.RootElement.GetProperty("hourly");

                var sicaklikMax = ParseDecimal(daily.GetProperty("temperature_2m_max")[0]);
                var sicaklikMin = ParseDecimal(daily.GetProperty("temperature_2m_min")[0]);
                var yagis = ParseDecimal(daily.GetProperty("precipitation_sum")[0]);
                var ruzgar = ParseDecimal(daily.GetProperty("windspeed_10m_max")[0]);
                var nem = HesaplaNemOrt(hourly.GetProperty("relativehumidity_2m"), 24);

                var hava = new HavaVerisi
                {
                    LokasyonId = lokasyon.LokasyonId,
                    Tarih = tarih,
                    SicaklikMax = sicaklikMax,
                    SicaklikMin = sicaklikMin,
                    Nem = nem,
                    Yagis = yagis,
                    RuzgarHizi = ruzgar,
                    ApiKaynagi = "Open-Meteo",
                    KayitZamani = DateTime.Now
                };

                _context.HavaVerileri.Add(hava);
                await _context.SaveChangesAsync();

                return hava;
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<HavaVerisi>> CekCokGunluk(Lokasyon lokasyon, int gun)
        {
            try
            {
                var url = $"https://api.open-meteo.com/v1/forecast" +
                          $"?latitude={lokasyon.Enlem.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                          $"&longitude={lokasyon.Boylam.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
                          $"&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,windspeed_10m_max" +
                          $"&hourly=relativehumidity_2m" +
                          $"&timezone=Europe%2FIstanbul" +
                          $"&forecast_days={gun}";

                var json = await _httpClient.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);

                var daily = doc.RootElement.GetProperty("daily");
                var hourly = doc.RootElement.GetProperty("hourly");
                var tarihler = daily.GetProperty("time");

                var liste = new List<HavaVerisi>();

                for (int i = 0; i < gun; i++)
                {
                    var tarih = DateTime.Parse(tarihler[i].GetString()!);

                    liste.Add(new HavaVerisi
                    {
                        LokasyonId = lokasyon.LokasyonId,
                        Tarih = tarih,
                        SicaklikMax = ParseDecimal(daily.GetProperty("temperature_2m_max")[i]),
                        SicaklikMin = ParseDecimal(daily.GetProperty("temperature_2m_min")[i]),
                        Yagis = ParseDecimal(daily.GetProperty("precipitation_sum")[i]),
                        RuzgarHizi = ParseDecimal(daily.GetProperty("windspeed_10m_max")[i]),
                        Nem = HesaplaNemOrt(hourly.GetProperty("relativehumidity_2m"), 24, i * 24),
                        ApiKaynagi = "Open-Meteo",
                        KayitZamani = DateTime.Now
                    });
                }

                return liste;
            }
            catch
            {
                return new List<HavaVerisi>();
            }
        }

        private static decimal? ParseDecimal(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Null) return null;
            return Math.Round((decimal)el.GetDouble(), 2);
        }

        private static decimal? HesaplaNemOrt(JsonElement nemDizisi, int saatSayisi, int baslangic = 0)
        {
            decimal toplam = 0;
            int sayac = 0;
            int bitis = Math.Min(baslangic + saatSayisi, nemDizisi.GetArrayLength());

            for (int i = baslangic; i < bitis; i++)
            {
                var el = nemDizisi[i];
                if (el.ValueKind != JsonValueKind.Null)
                {
                    toplam += (decimal)el.GetDouble();
                    sayac++;
                }
            }

            return sayac > 0 ? Math.Round(toplam / sayac, 1) : null;
        }
    }
}