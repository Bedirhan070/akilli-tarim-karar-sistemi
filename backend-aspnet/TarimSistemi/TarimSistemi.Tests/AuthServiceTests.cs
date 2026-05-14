using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using TarimSistemi.Configuration;
using TarimSistemi.Data;
using TarimSistemi.Models;
using TarimSistemi.Services;
using Xunit;

namespace TarimSistemi.Tests;

public class AuthServiceTests
{
    private static TarimDbContext DbOlustur(string isim)
    {
        var options = new DbContextOptionsBuilder<TarimDbContext>()
            .UseInMemoryDatabase(isim)
            .Options;
        return new TarimDbContext(options);
    }

    private static IConfiguration TestConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"]      = "TarimSistemiSuperSecretTestKey2024!XYZ",
                ["Jwt:Issuer"]   = "TarimSistemiTest",
                ["Jwt:Audience"] = "TarimSistemiTestKullanicilari"
            })
            .Build();

    private static AuthService ServisOlustur(TarimDbContext db)
    {
        var config       = TestConfig();
        var emailMock    = Mock.Of<IEmailGonderici>();
        var emailOptions = Options.Create(new EmailAyarlari { PublicBaseUrl = "https://localhost" });
        return new AuthService(db, config, emailMock, emailOptions);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TEST 4 — JWT token içinde kullanıcı claim'leri doğru yazılmalı
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task JWT_Token_KullaniciBilgileriIle_Olusturma_Basarili()
    {
        // Arrange
        using var db = DbOlustur("TestDb_T4_JWT");

        const int    testId      = 42;
        const string testEmail   = "ciftci42@example.com";
        const string testSifre   = "Test1234!";
        const string testAdSoyad = "Ahmet Yılmaz";

        db.Kullanicilar.Add(new Kullanici
        {
            KullaniciId = testId,
            AdSoyad     = testAdSoyad,
            Email       = testEmail,
            SifreHash   = BCrypt.Net.BCrypt.HashPassword(testSifre),
            EmailOnayli = true,
            KayitTarihi = DateTime.Now
        });
        await db.SaveChangesAsync();

        var service = ServisOlustur(db);

        // Act
        var (success, token, _) = await service.GirisYap(testEmail, testSifre);

        // Assert — giriş başarılı ve token üretildi
        Assert.True(success);
        Assert.NotNull(token);

        // Token'ı decode et ve claim'leri doğrula
        var jwtToken  = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var idClaim   = jwtToken.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var nameClaim = jwtToken.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        var emailClaim = jwtToken.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;

        Assert.Equal(testId.ToString(), idClaim);
        Assert.Equal(testAdSoyad, nameClaim);
        Assert.Equal(testEmail, emailClaim);
    }
}
