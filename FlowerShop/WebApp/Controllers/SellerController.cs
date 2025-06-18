using Domain;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApp.Controllers
{
    [Authorize(Policy = "SellerOnly")]
    public class SellerController : Controller
    {
        private readonly IProductService _productService;
        private readonly ILogger<SellerController> _logger;
        private readonly int _defaultLimit;

        public SellerController(
            IProductService productService,
            ILogger<SellerController> logger,
            IConfiguration config)
        {
            _productService = productService;
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

        [HttpPost]
        public IActionResult CreateOrder(List<ReceiptLine> items)
        {
            try
            {
                var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var receipt = _productService.MakePurchase(items, customerId);

                if (receipt.Id == -1)
                {
                    TempData["Error"] = "Ошибка при оформлении заказа";
                    return RedirectToAction("Order");
                }

                TempData["Message"] = $"Заказ оформлен. Номер чека: {receipt.Id}";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании заказа");
                TempData["Error"] = ex.Message;
                return RedirectToAction("Order");
            }
        }
    }
}