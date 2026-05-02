namespace TarimSistemi.Configuration
{
    public class EmailAyarlari
    {
        /// <summary>Uygulamanın dışarıdan erişilen kök adresi (e-postadaki bağlantılar için). Örn: https://tarim.ornek.com</summary>
        public string PublicBaseUrl { get; set; } = "";

        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = "";
        public string SmtpPassword { get; set; } = "";
        public string FromAddress { get; set; } = "";
        public string FromName { get; set; } = "Akıllı Tarım";
        public bool EnableSsl { get; set; } = true;
    }
}
