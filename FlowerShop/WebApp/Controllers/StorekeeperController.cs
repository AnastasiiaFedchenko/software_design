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

            ViewBag.Products = products.Products;
            ViewBag.Skip = skip;
            ViewBag.Limit = _defaultLimit;
            ViewBag.IsLastPage = products.TotalAmount <= skip + _defaultLimit;
            return View(products);
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
                TempData["Error"] = "Файл не выбран.";
                return RedirectToAction("Index");
            }

            string tempFilePath = Path.GetTempFileName();
            try
            {
                using (var stream = new FileStream(tempFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose))
                {
                    batchFile.CopyTo(stream);
                    stream.Position = 0;

                    bool result = _loadService.LoadProductBatch(stream);
                    if (!result)
                    {
                        TempData["Error"] = "Ошибка при обработке файла.";
                        return RedirectToAction("Index");
                    }
                }
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO ошибка при загрузке файла");
                TempData["Error"] = "Ошибка чтения/записи файла.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неизвестная ошибка");
                TempData["Error"] = "Произошла ошибка: " + ex.Message;
                return RedirectToAction("Index");
            }
            finally
            {
                if (System.IO.File.Exists(tempFilePath))
                    System.IO.File.Delete(tempFilePath);
            }

            TempData["Message"] = "Файл успешно загружен!";
            return RedirectToAction("Index");
        }
    }
}