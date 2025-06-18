using Domain;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApp.Controllers
{
    [Authorize(Policy = "StorekeeperOnly")]
    public class StorekeeperController : Controller
    {
        private readonly IProductService _productService;
        private readonly ILoadService _loadService;
        private readonly ILogger<StorekeeperController> _logger;
        private readonly int _defaultLimit;

        public StorekeeperController(
            IProductService productService,
            ILoadService loadService,
            ILogger<StorekeeperController> logger,
            IConfiguration config)
        {
            _productService = productService;
            _loadService = loadService;
            _logger = logger;
            _defaultLimit = config.GetValue<int>("AppSettings:DefaultPaginationLimit");
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Products(int skip = 0)
        {
            var products = _productService.GetAllAvailableProducts(_defaultLimit, skip);
            return View(products);
        }

        public IActionResult LoadBatch()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoadBatch(IFormFile batchFile)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                batchFile.CopyTo(memoryStream);
                memoryStream.Position = 0;

                bool result = _loadService.LoadProductBatch(memoryStream);

                if (result)
                {
                    TempData["Message"] = "Загрузка прошла успешно!";
                    return RedirectToAction("Index");
                }

                TempData["Error"] = "Ошибка при загрузке данных";
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке партии товаров");
                TempData["Error"] = ex.Message;
                return View();
            }
        }
    }
}