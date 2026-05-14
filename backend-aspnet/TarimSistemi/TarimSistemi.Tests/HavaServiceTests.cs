using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TarimSistemi.Data;
using TarimSistemi.Models;
using TarimSistemi.Services;
using Xunit;

namespace TarimSistemi.Tests;

/// <summary>HttpClient'ı sabit bir yanıtla mock'layan yardımcı handler.</summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseBody;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseBody = responseBody;
        _statusCode   = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage
        {
            StatusCode = _statusCode,
            Content    = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        });
    }
}

public class HavaServiceTests
{
    private static TarimDbContext DbOlustur(string isim)
    {
        var options = new DbContextOptionsBuilder<TarimDbContext>()
            .UseInMemoryDatabase(isim)
            .Options;
        return new TarimDbContext(options);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 1 — Var olmayan lokasyonId → null döner (null şehir/konum hata yönetimi)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task Test_GetHavaDurumu_NullCityName_HataYonetimi()
    {
        // Arrange — boş DB, hiçbir lokasyon yok
        using var db     = DbOlustur("TestDb_T1_NullLokasyon");
        var handler      = new MockHttpMessageHandler("{}");
        var httpClient   = new HttpClient(handler);
        var logger       = Mock.Of<ILogger<HavaService>>();
        var service      = new HavaService(db, httpClient, logger);

        // Act — DB'de bulunmayan lokasyonId verildi
        var result = await service.GetBugunHavasi(lokasyonId: 9999);

        // Assert — lokasyon bulunamayınca null dönmeli
        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 2 — Geçerli lokasyon + mock Open-Meteo yanıtı → veri döner, sıcaklık geçerli aralıkta
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetHavaDurumu_GecerliSehir_VeriDonmelidir()
    {
        // Arrange
        using var db = DbOlustur("TestDb_T2_BitlisHava");

        var lokasyon = new Lokasyon
        {
            LokasyonId  = 1,
            KullaniciId = 1,
            Isim        = "Bitlis Test Tarlası",
            Sehir       = "Bitlis",
            Enlem       = 38.3919m,
            Boylam      = 42.1233m,
            KayitTarihi = DateTime.Now
        };
        db.Lokasyonlar.Add(lokasyon);
        await db.SaveChangesAsync();

        // Open-Meteo formatında sahte yanıt
        var openMeteoJson = """
            {
                "daily": {
                    "time": ["2024-06-15"],
                    "temperature_2m_max": [25.4],
                    "temperature_2m_min": [12.1],
                    "precipitation_sum": [0.0],
                    "windspeed_10m_max": [18.5]
                },
                "hourly": {
                    "relativehumidity_2m": [55,54,53,52,51,50,49,48,50,55,60,65,67,68,69,70,69,68,67,65,63,61,59,57]
                }
            }
            """;

        var handler    = new MockHttpMessageHandler(openMeteoJson);
        var httpClient = new HttpClient(handler);
        var logger     = Mock.Of<ILogger<HavaService>>();
        var service    = new HavaService(db, httpClient, logger);

        // Act
        var result = await service.GetBugunHavasi(lokasyonId: 1);

        // Assert
        Assert.NotNull(result);
        Assert.InRange(result.SicaklikMax!.Value, -50m, 60m);
        Assert.InRange(result.SicaklikMin!.Value, -50m, 60m);
        Assert.Equal("Open-Meteo", result.ApiKaynagi);
    }
}
