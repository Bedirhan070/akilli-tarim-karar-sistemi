namespace TarimSistemi.Services
{
    public interface IEmailGonderici
    {
        Task GonderAsync(string aliciEmail, string konu, string htmlGovde, CancellationToken cancellationToken = default);
    }
}
