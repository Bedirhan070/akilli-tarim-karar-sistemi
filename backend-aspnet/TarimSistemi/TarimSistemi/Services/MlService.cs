using System.Net.Http.Json;
using System.Text.Json.Serialization;
using TarimSistemi.Models;

namespace TarimSistemi.Services
{
    public class MlService
    {
        private readonly HttpClient _http;
        private readonly ILogger<MlService> _logger;

        public MlService(HttpClient http, ILogger<MlService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<bool> SaglikKontrol()
        {
            try
            {
                var yanit = await _http.GetAsync("");
                return yanit.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<MlTahminSonuc?> RiskTahminEt(HavaVerisi hava)
        {
            var istek = YapHavaIstegi(hava);
            var yanit = await _http.PostAsJsonAsync("predict", istek);
            yanit.EnsureSuccessStatusCode();
            return await yanit.Content.ReadFromJsonAsync<MlTahminSonuc>();
        }

        public async Task<MlAnomaliSonuc?> AnomaliTespit(HavaVerisi hava)
        {
            var istek = YapHavaIstegi(hava);
            var yanit = await _http.PostAsJsonAsync("anomaly", istek);
            yanit.EnsureSuccessStatusCode();
            return await yanit.Content.ReadFromJsonAsync<MlAnomaliSonuc>();
        }

        private static object YapHavaIstegi(HavaVerisi hava) => new
        {
            max_sicaklik = (double)(hava.SicaklikMax ?? 20),
            min_sicaklik = (double)(hava.SicaklikMin ?? 10),
            yagis        = (double)(hava.Yagis ?? 0),
            ruzgar_hizi  = (double)(hava.RuzgarHizi ?? 0),
            nem          = (double)(hava.Nem ?? 50)
        };
    }

    public class MlTahminSonuc
    {
        [JsonPropertyName("sinif")]
        public string? Sinif { get; set; }

        [JsonPropertyName("risk_skoru")]
        public double RiskSkoru { get; set; }

        [JsonPropertyName("seviye")]
        public string? Seviye { get; set; }

        [JsonPropertyName("renk")]
        public string? Renk { get; set; }

        [JsonPropertyName("olasiliklar")]
        public Dictionary<string, double>? Olasiliklar { get; set; }
    }

    public class MlAnomaliSonuc
    {
        [JsonPropertyName("durum")]
        public string? Durum { get; set; }

        [JsonPropertyName("skor")]
        public double Skor { get; set; }

        [JsonPropertyName("guvenilir")]
        public bool Guvenilir { get; set; }

        [JsonPropertyName("mesaj")]
        public string? Mesaj { get; set; }
    }
}
