using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TarimSistemi.Data;
using TarimSistemi.Models;

namespace TarimSistemi.Services
{
    public class AuthService
    {
        private readonly TarimDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(TarimDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<(bool Success, string Message)> KayitOl(string adSoyad, string email, string sifre, string? telefon)
        {
            if (await _context.Kullanicilar.AnyAsync(k => k.Email == email))
                return (false, "Bu email zaten kayıtlı");

            var kullanici = new Kullanici
            {
                AdSoyad = adSoyad,
                Email = email,
                SifreHash = BCrypt.Net.BCrypt.HashPassword(sifre),
                Telefon = telefon,
                KayitTarihi = DateTime.Now
            };

            _context.Kullanicilar.Add(kullanici);
            await _context.SaveChangesAsync();

            return (true, "Kayıt başarılı");
        }

        public async Task<(bool Success, string? Token, string Message)> GirisYap(string email, string sifre)
        {
            var kullanici = await _context.Kullanicilar.FirstOrDefaultAsync(k => k.Email == email);

            if (kullanici == null || !BCrypt.Net.BCrypt.Verify(sifre, kullanici.SifreHash))
                return (false, null, "Email veya şifre hatalı");

            kullanici.SonGirisTarihi = DateTime.Now;
            await _context.SaveChangesAsync();

            return (true, GenerateJwtToken(kullanici), "Giriş başarılı");
        }

        private string GenerateJwtToken(Kullanici kullanici)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, kullanici.KullaniciId.ToString()),
                new Claim(ClaimTypes.Email,          kullanici.Email),
                new Claim(ClaimTypes.Name,           kullanici.AdSoyad)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}