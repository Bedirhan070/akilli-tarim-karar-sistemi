namespace TarimSistemi.Models
{
    public class HavaVerisi
    {
        public int HavaVeriId { get; set; }
        public int LokasyonId { get; set; }
        public DateTime Tarih { get; set; }
        public decimal? SicaklikMax { get; set; }
        public decimal? SicaklikMin { get; set; }
        public decimal? Nem { get; set; }
        public decimal? Yagis { get; set; }
        public decimal? RuzgarHizi { get; set; }
        public string? ApiKaynagi { get; set; }
        public bool AnomaliDurumu { get; set; } = false;
        public DateTime KayitZamani { get; set; } = DateTime.Now;

        // İlişki
        public Lokasyon Lokasyon { get; set; }
    }
}