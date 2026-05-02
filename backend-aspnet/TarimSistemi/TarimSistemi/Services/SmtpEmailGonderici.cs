using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using TarimSistemi.Configuration;

namespace TarimSistemi.Services
{
    public class SmtpEmailGonderici : IEmailGonderici
    {
        private readonly EmailAyarlari _ayarlar;
        private readonly ILogger<SmtpEmailGonderici> _logger;

        public SmtpEmailGonderici(IOptions<EmailAyarlari> ayarlar, ILogger<SmtpEmailGonderici> logger)
        {
            _ayarlar = ayarlar.Value;
            _logger = logger;
        }

        public async Task GonderAsync(string aliciEmail, string konu, string htmlGovde, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_ayarlar.SmtpHost) || string.IsNullOrWhiteSpace(_ayarlar.FromAddress))
            {
                _logger.LogWarning(
                    "E-posta gönderilmedi (SMTP veya FromAddress yapılandırılmadı). Alıcı: {Alici}, Konu: {Konu}. Doğrulama için appsettings içinde Email bölümünde SMTP ayarlayın.",
                    aliciEmail, konu);
                return;
            }

            using var msg = new MailMessage
            {
                From = new MailAddress(_ayarlar.FromAddress, _ayarlar.FromName),
                Subject = konu,
                Body = htmlGovde,
                IsBodyHtml = true
            };
            msg.To.Add(aliciEmail);

            using var client = new SmtpClient(_ayarlar.SmtpHost, _ayarlar.SmtpPort)
            {
                EnableSsl = _ayarlar.EnableSsl,
                Credentials = string.IsNullOrEmpty(_ayarlar.SmtpUser)
                    ? null
                    : new NetworkCredential(_ayarlar.SmtpUser, _ayarlar.SmtpPassword)
            };

            await client.SendMailAsync(msg, cancellationToken);
        }
    }
}
