using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TarimSistemi.Models;

namespace TarimSistemi.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
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
    }
}
