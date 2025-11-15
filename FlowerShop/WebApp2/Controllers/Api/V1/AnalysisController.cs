using Microsoft.AspNetCore.Mvc;
using Domain.InputPorts;

namespace WebApp2.Controllers.Api.V2
{
    [ApiController]
    [Route("/api/v1/analysis")]
    public class AnalysisController : ControllerBase
    {
        private readonly IAnalysisService _analysisService;

        public AnalysisController(IAnalysisService analysisService)
        {
            _analysisService = analysisService;
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
                return StatusCode(500, new { message = "Ошибка получения сегментации", error = ex.Message });
            }
        }
    }
}