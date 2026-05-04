using Microsoft.AspNetCore.Mvc;
using TarimSistemi.Services;

namespace TarimSistemi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SistemController : ControllerBase
    {
        private readonly MlService _mlService;

        public SistemController(MlService mlService)
        {
            _mlService = mlService;
        }

        // GET /api/Sistem/durum  — auth gerekmez
        [HttpGet("durum")]
        public async Task<IActionResult> Durum()
        {
            var mlAktif = await _mlService.SaglikKontrol();
            return Ok(new { mlAktif });
        }
    }
}
