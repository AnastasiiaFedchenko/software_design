using Domain;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminController : Controller
    {
        private readonly IProductService _productService;
        private readonly IAnalysisService _analysisService;
        private readonly ILoadService _loadService;
        private readonly ILogger<AdminController> _logger;
        private readonly int _defaultLimit;

        public AdminController(
            IProductService productService,
            IAnalysisService analysisService,
            ILoadService loadService,
            ILogger<AdminController> logger,
            IConfiguration config)
        {
            _productService = productService;
            _analysisService = analysisService;
            _loadService = loadService;
            _logger = logger;
            _defaultLimit = config.GetValue<int>("AppSettings:DefaultPaginationLimit");
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Order()
        {
            var products = _productService.GetAllAvailableProducts(_defaultLimit, 0);
            return View(new Inventory(products.Id, products.Date, products.TotalAmount, products.Products));
        }

        public IActionResult LoadBatch()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoadBatch(IFormFile batchFile)
        {
            if (batchFile == null || batchFile.Length == 0)
            {
                TempData["Error"] = "Please select a file";
                return View();
            }

            try
            {
                using var stream = batchFile.OpenReadStream();
                bool result = _loadService.LoadProductBatch(stream);

                if (result)
                {
                    TempData["Message"] = "Batch loaded successfully!";
                    return RedirectToAction("Index");
                }

                TempData["Error"] = "Error loading batch data";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading product batch");
                TempData["Error"] = ex.Message;
                return View();
            }
        }

        public IActionResult Forecast()
        {
            try
            {
                var forecast = _analysisService.GetForecastOfOrders();
                return View(forecast);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении прогноза");
                TempData["Error"] = ex.Message;
                return View();
            }
        }

        public IActionResult UserSegmentation()
        {
            try
            {
                var segments = _analysisService.GetUserSegmentation();
                return View(segments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении сегментации пользователей");
                TempData["Error"] = ex.Message;
                return View();
            }
        }
    }
}
