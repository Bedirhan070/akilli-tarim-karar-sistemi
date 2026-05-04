using System.Net.Http.Json;
using System.Text.Json;

namespace TarimSistemi.Services
{
    public class TelegramService
    {
        private readonly HttpClient _http;
        private readonly string? _token;
        private readonly string? _botUsername;
        private readonly ILogger<TelegramService> _logger;

        public TelegramService(HttpClient http, IConfiguration config, ILogger<TelegramService> logger)
        {
            _http = http;
            _token = config["Telegram:BotToken"];
            _botUsername = config["Telegram:BotUsername"];
            _logger = logger;
        }

        public bool Aktif => !string.IsNullOrWhiteSpace(_token);
        public string? BotUsername => _botUsername;

        public async Task<bool> MesajGonderAsync(string chatId, string mesaj)
        {
            if (!Aktif || string.IsNullOrWhiteSpace(chatId))
                return false;

            try
            {
                var url = $"https://api.telegram.org/bot{_token}/sendMessage";
                var body = new { chat_id = chatId, text = mesaj, parse_mode = "HTML" };
                var res = await _http.PostAsJsonAsync(url, body);
                if (!res.IsSuccessStatusCode)
                {
                    var hata = await res.Content.ReadAsStringAsync();
                    _logger.LogWarning("Telegram API hatası. ChatId={ChatId} Status={Status} Body={Body}",
                        chatId, (int)res.StatusCode, hata);
                }
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram mesajı gönderilemedi. ChatId={ChatId}", chatId);
                return false;
            }
        }

        public async Task<bool> WebhookKaydetAsync(string webhookUrl, string? secretToken = null)
        {
            if (!Aktif) return false;
            try
            {
                var body = new Dictionary<string, object?> { ["url"] = webhookUrl };
                if (!string.IsNullOrWhiteSpace(secretToken))
                    body["secret_token"] = secretToken;
                var res = await _http.PostAsJsonAsync($"https://api.telegram.org/bot{_token}/setWebhook", body);
                if (res.IsSuccessStatusCode)
                    _logger.LogInformation("Telegram webhook kaydedildi: {Url}", webhookUrl);
                else
                {
                    var hata = await res.Content.ReadAsStringAsync();
                    _logger.LogWarning("Telegram webhook kaydedilemedi: {Hata}", hata);
                }
                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram webhook kayıt isteği başarısız.");
                return false;
            }
        }
    }
}