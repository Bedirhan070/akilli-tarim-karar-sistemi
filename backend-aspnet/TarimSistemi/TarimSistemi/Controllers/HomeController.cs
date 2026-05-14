using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TarimSistemi.Models;
using TarimSistemi.Services;

namespace TarimSistemi.Controllers
{
    public class HomeController : Controller
    {
        private readonly AuthService _authService;

        public HomeController(AuthService authService)
        {
            _authService = authService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        // GET: /Home/Login
        public IActionResult Login()
        {
            return View();
        }

        // GET: /Home/SifreSifirla?token=... (token yoksa e-posta isteme formu)
        public IActionResult SifreSifirla()
        {
            var q = Request.Query["token"];
            ViewBag.ResetToken = q.Count > 0 ? q[0] : null;
            return View();
        }

        // GET: /Home/Kayit
        public IActionResult Kayit()
        {
            return View();
        }
        // GET: /Home/Dashboard
        [Authorize]
        public IActionResult Dashboard()
        {
            return View();
        }

        // GET: /Home/Lokasyonlar
        [Authorize]
        public IActionResult Lokasyonlar()
        {
            return View();
        }

        // GET: /Home/OneriGecmisi
        [Authorize]
        public IActionResult OneriGecmisi()
        {
            return View();
        }

        // GET: /Home/Hesabim
        [Authorize]
        public IActionResult Hesabim()
        {
            return View();
        }

        // GET: /Home/AnlikAnaliz
        [Authorize]
        public IActionResult AnlikAnaliz()
        {
            return View();
        }

        // GET: /Home/HaftalikTahmin
        [Authorize]
        public IActionResult HaftalikTahmin()
        {
            return View();
        }

        // GET: /Home/Bildirimler
        [Authorize]
        public IActionResult Bildirimler()
        {
            return View();
        }

        // GET: /Home/SehirRiskAnalizi
        [Authorize]
        public IActionResult SehirRiskAnalizi()
        {
            return View();
        }

        // GET: /Home/SifreEmailOnay?token=... (şifre değiştirme onayı — hâlâ link tabanlı)
        public async Task<IActionResult> SifreEmailOnay(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Basari = false;
                ViewBag.Baslik = "Geçersiz bağlantı";
                ViewBag.Mesaj = "Onay adresi eksik veya hatalı.";
                return View("OnaySonuc");
            }

            var (ok, mesaj) = await _authService.OnaylaSifreDegisikligiAsync(token);
            ViewBag.Basari = ok;
            ViewBag.Baslik = ok ? "Şifre güncellendi" : "İşlem başarısız";
            ViewBag.Mesaj = mesaj;
            return View("OnaySonuc");
        }
    }
}
