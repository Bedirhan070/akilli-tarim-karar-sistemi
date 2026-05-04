using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TarimSistemi.Services;

namespace TarimSistemi.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramWebhookController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly TelegramService _telegramService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TelegramWebhookController> _logger;

        public TelegramWebhookController(
            AuthService authService,
            TelegramService telegramService,
            IConfiguration configuration,
            ILogger<TelegramWebhookController> logger)
        {
            _authService = authService;
            _telegramService = telegramService;
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            // Secret token doğrulama
            var beklenenSecret = _configuration["Telegram:WebhookSecretToken"];
            if (!string.IsNullOrWhiteSpace(beklenenSecret))
            {
                var gelenSecret = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].FirstOrDefault();
                if (gelenSecret != beklenenSecret)
                {
                    _logger.LogWarning("Telegram webhook: geçersiz secret token.");
                    return Forbid();
                }
            }

            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(Request.Body);
            }
            catch
            {
                return BadRequest();
            }

            using (doc)
            {
                if (!doc.RootElement.TryGetProperty("message", out var msg))
                    return Ok(); // Callback query vb. — görmezden gel

                if (!msg.TryGetProperty("text", out var textEl))
                    return Ok();

                var text = textEl.GetString()?.Trim() ?? "";
                if (!text.StartsWith("/start ") && text != "/start")
                    return Ok();

                var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64().ToString();
                var from = msg.TryGetProperty("from", out var fromEl)
                    ? (fromEl.TryGetProperty("first_name", out var fn) ? fn.GetString() : null)
                    : null;

                // /start TOKEN formatı — boşluktan sonrası token
                var parts = text.Split(' ', 2);
                if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                {
                    // Token yok: sıradan /start komutu
                    await _telegramService.MesajGonderAsync(chatId,
                        "Merhaba! Bu bot Akıllı Tarım Sistemi bildirimleri için kullanılır.\n\n" +
                        "Hesabınızı bağlamak için uygulamaya gidin ve <b>\"Telegram'ı Bağla\"</b> butonuna tıklayın.");
                    return Ok();
                }

                var token = parts[1].Trim();
                var (ok, adSoyad) = await _authService.TelegramBaglamaTokenuDogrula(token, chatId);

                if (!ok)
                {
                    await _telegramService.MesajGonderAsync(chatId,
                        $"⚠️ Bağlama bağlantısı geçersiz veya süresi dolmuş.\n\n" +
                        "Uygulamadan yeni bir bağlama linki alın ve tekrar deneyin.");
                    return Ok();
                }

                await _telegramService.MesajGonderAsync(chatId,
                    $"✅ <b>Hesabınız başarıyla bağlandı!</b>\n\n" +
                    $"Merhaba {System.Net.WebUtility.HtmlEncode(adSoyad)}! " +
                    "Artık tarlalarınızda kritik risk oluştuğunda Telegram üzerinden bildirim alacaksınız.");

                _logger.LogInformation("Telegram bağlandı. ChatId={ChatId} Kullanici={Ad}", chatId, adSoyad);
            }

            return Ok();
        }
    }
}