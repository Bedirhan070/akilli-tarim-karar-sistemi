using System.Diagnostics;
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
        public IActionResult Dashboard()
        {
            return View();
        }

        // GET: /Home/Lokasyonlar
        public IActionResult Lokasyonlar()
        {
            return View();
        }

        // GET: /Home/OneriGecmisi
        public IActionResult OneriGecmisi()
        {
            return View();
        }

        // GET: /Home/Hesabim
        public IActionResult Hesabim()
        {
            return View();
        }

        // GET: /Home/EmailOnay?token=...
        public async Task<IActionResult> EmailOnay(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                ViewBag.Basari = false;
                ViewBag.Baslik = "Geçersiz bağlantı";
                ViewBag.Mesaj = "Doğrulama adresi eksik veya hatalı.";
                return View("OnaySonuc");
            }

            var (ok, mesaj) = await _authService.OnaylaKayitEmailiAsync(token);
            ViewBag.Basari = ok;
            ViewBag.Baslik = ok ? "Hesabınız doğrulandı" : "Doğrulama başarısız";
            ViewBag.Mesaj = mesaj;
            return View("OnaySonuc");
        }

        // GET: /Home/SifreEmailOnay?token=...
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
