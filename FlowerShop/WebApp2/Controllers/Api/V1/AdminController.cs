using Microsoft.AspNetCore.Mvc;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authorization;

namespace WebApp2.Controllers.Api.V2
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : ControllerBase
    {
        private readonly IAnalysisService _analysisService;
        private readonly ILoadService _loadService;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IAnalysisService analysisService,
            ILoadService loadService,
            ILogger<AdminController> logger)
        {
            _analysisService = analysisService;
            _loadService = loadService;
            _logger = logger;
        }

        [HttpGet("forecast")]
        public IActionResult GetForecast()
        {
            try
            {
                var forecast = _analysisService.GetForecastOfOrders();
                return Ok(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении прогноза");
                return StatusCode(500, new { message = "Ошибка получения прогноза", error = ex.Message });
            }
        }

        [HttpGet("user-segmentation")]
        public IActionResult GetUserSegmentation()
        {
            try
            {
                var segments = _analysisService.GetUserSegmentation();
                return Ok(segments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сегментации пользователей");
                return StatusCode(500, new { message = "Ошибка получения сегментации", error = ex.Message });
            }
        }

        [HttpPost("products/loadings")]
        public async Task<IActionResult> LoadProductsBatch(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "Файл не выбран" });
            }

            try
            {
                using var stream = file.OpenReadStream();
                bool result = _loadService.LoadProductBatch(stream);

                if (!result)
                {
                    return BadRequest(new { message = "Ошибка при обработке файла" });
                }

                return Ok(new { message = "Файл успешно загружен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке файла");
                return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
            }
        }
    }
}