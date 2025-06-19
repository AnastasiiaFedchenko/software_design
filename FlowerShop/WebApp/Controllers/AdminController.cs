using System.Security.Claims;
using System.Text.Json;
using Domain;
using Domain.InputPorts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

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

        [HttpGet]
        public IActionResult Order(int skip = 0)
        {
            var products = _productService.GetAllAvailableProducts(_defaultLimit, skip);
            var cart = GetCartFromSession();

            ViewBag.Products = products.Products;
            ViewBag.Skip = skip;
            ViewBag.Limit = _defaultLimit;
            ViewBag.IsLastPage = products.TotalAmount <= skip + _defaultLimit;
            ViewBag.CartItems = cart;

            return View(products);
        }

        private List<ReceiptLine> GetCartFromSession()
        {
            var cartData = HttpContext.Session.Get("Cart");
            if (cartData == null || cartData.Length == 0)
                return new List<ReceiptLine>();

            try
            {
                var jsonReader = new Utf8JsonReader(cartData.AsSpan());
                var cart = new List<ReceiptLine>();

                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType == JsonTokenType.StartObject)
                    {
                        using var doc = JsonDocument.ParseValue(ref jsonReader);
                        var root = doc.RootElement;
                        var productElement = root.GetProperty("Product");
                        var product = new Product(
                            productElement.GetProperty("IdNomenclature").GetInt32(),
                            productElement.GetProperty("Price").GetDouble(),
                            productElement.GetProperty("AmountInStock").GetInt32(),
                            productElement.GetProperty("Type").GetString(),
                            productElement.GetProperty("Country").GetString()
                        );
                        cart.Add(new ReceiptLine(
                            product,
                            root.GetProperty("Amount").GetInt32()
                        ));
                    }
                }
                return cart;
            }
            catch
            {
                return new List<ReceiptLine>();
            }
        }

        private void SaveCartToSession(List<ReceiptLine> cart)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartArray();
                foreach (var item in cart)
                {
                    writer.WriteStartObject();
                    writer.WriteStartObject("Product");
                    writer.WriteNumber("IdNomenclature", item.Product.IdNomenclature);
                    writer.WriteNumber("Price", item.Product.Price);
                    writer.WriteNumber("AmountInStock", item.Product.AmountInStock);
                    writer.WriteString("Type", item.Product.Type);
                    writer.WriteString("Country", item.Product.Country);
                    writer.WriteEndObject();
                    writer.WriteNumber("Amount", item.Amount);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
            }

            HttpContext.Session.Set("Cart", stream.ToArray());
        }

        [HttpPost]
        public IActionResult AddToCart(int productId, int quantity)
        {
            var product = _productService.GetInfoOnProduct(productId);
            if (product == null) return NotFound();

            var cart = GetCartFromSession();

            var existingItem = cart.FirstOrDefault(x => x.Product.IdNomenclature == productId);
            if (existingItem != null)
            {
                // Для иммутабельного Product создаем новый ReceiptLine
                var newItem = new ReceiptLine(
                    existingItem.Product,
                    existingItem.Amount + quantity
                );
                cart.Remove(existingItem);
                cart.Add(newItem);
            }
            else
            {
                cart.Add(new ReceiptLine(product, quantity));
            }

            SaveCartToSession(cart);
            return RedirectToAction("Order", new { skip = ViewBag.Skip });
        }

        [HttpPost]
        public IActionResult RemoveFromCart(int productId)
        {
            try
            {
                var cart = GetCartFromSession();
                var itemToRemove = cart.FirstOrDefault(x => x.Product.IdNomenclature == productId);

                if (itemToRemove != null)
                {
                    cart.Remove(itemToRemove);
                    SaveCartToSession(cart);
                    TempData["Message"] = "Товар удалён из корзины";
                }
                return RedirectToAction("Order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении товара");
                TempData["Error"] = "Ошибка при удалении товара";
                return RedirectToAction("Order");
            }
        }


        [HttpPost]
        public IActionResult UpdateCartItem(int productId, int newQuantity)
        {
            try
            {
                if (newQuantity <= 0)
                {
                    return RemoveFromCart(productId);
                }

                var cart = GetCartFromSession();
                var existingItem = cart.FirstOrDefault(x => x.Product.IdNomenclature == productId);

                if (existingItem != null)
                {
                    cart.Remove(existingItem);
                    cart.Add(new ReceiptLine(
                        existingItem.Product,
                        newQuantity
                    ));

                    SaveCartToSession(cart);
                    TempData["Message"] = "Количество товара обновлено";
                }
                return RedirectToAction("Order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении количества");
                TempData["Error"] = "Ошибка при изменении количества";
                return RedirectToAction("Order");
            }
        }

        [HttpPost]
        public IActionResult SubmitOrder()
        {
            try
            {
                var cartJson = HttpContext.Session.GetString("Cart");
                if (string.IsNullOrEmpty(cartJson))
                {
                    TempData["Error"] = "Корзина пуста";
                    return RedirectToAction("Order");
                }

                // Десериализация с использованием JsonDocument
                using (JsonDocument doc = JsonDocument.Parse(cartJson))
                {
                    var cart = new List<ReceiptLine>();
                    foreach (var item in doc.RootElement.EnumerateArray())
                    {
                        var productElement = item.GetProperty("Product");
                        var product = new Product(
                            productElement.GetProperty("IdNomenclature").GetInt32(),
                            productElement.GetProperty("Price").GetDouble(),
                            productElement.GetProperty("AmountInStock").GetInt32(),
                            productElement.GetProperty("Type").GetString(),
                            productElement.GetProperty("Country").GetString()
                        );
                        cart.Add(new ReceiptLine(
                            product,
                            item.GetProperty("Amount").GetInt32()
                        ));
                    }

                    var customerId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                    var receipt = _productService.MakePurchase(cart, customerId);

                    HttpContext.Session.Remove("Cart");

                    if (receipt.Id == -1)
                    {
                        TempData["Error"] = "Ошибка при оформлении заказа";
                        return RedirectToAction("Order");
                    }

                    TempData["Message"] = $"Заказ оформлен. Номер чека: {receipt.Id}";
                    return RedirectToAction("Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оформлении заказа");
                TempData["Error"] = "Произошла ошибка при оформлении заказа";
                return RedirectToAction("Order");
            }
        }
        public IActionResult LoadBatch()
        {
            return View();
        }

        [HttpPost]
        public IActionResult LoadBatch(IFormFile batchFile)
        {
            // Проверка наличия файла
            if (batchFile == null || batchFile.Length == 0)
            {
                TempData["Error"] = "Пожалуйста, выберите файл для загрузки";
                return RedirectToAction("Index");
            }

            // Проверка размера файла (макс. 10MB)
            if (batchFile.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "Размер файла не должен превышать 10MB";
                return RedirectToAction("Index");
            }

            // Проверка расширения файла
            var allowedExtensions = new[] { ".csv", ".xlsx", ".txt" };
            var fileExtension = Path.GetExtension(batchFile.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["Error"] = "Неподдерживаемый формат файла. Разрешены: CSV, XLSX, TXT";
                return RedirectToAction("Index");
            }

            try
            {
                // Создаем временный файл для обработки
                var tempFilePath = Path.GetTempFileName();
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    batchFile.CopyTo(stream);
                }

                // Обработка файла
                bool result;
                using (var fileStream = new FileStream(tempFilePath, FileMode.Open))
                {
                    result = _loadService.LoadProductBatch(fileStream);
                }

                // Удаляем временный файл
                System.IO.File.Delete(tempFilePath);

                if (result)
                {
                    TempData["Message"] = "Партия товаров успешно загружена!";
                    _logger.LogInformation($"Успешно загружена партия из файла: {batchFile.FileName}");
                }
                else
                {
                    TempData["Error"] = "Ошибка при обработке данных файла";
                    _logger.LogWarning($"Ошибка при загрузке партии из файла: {batchFile.FileName}");
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Ошибка доступа к файлу");
                TempData["Error"] = "Ошибка доступа к файлу. Проверьте права.";
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Ошибка ввода-вывода");
                TempData["Error"] = "Ошибка чтения файла. Файл может быть поврежден.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке партии");
                TempData["Error"] = $"Произошла ошибка: {ex.Message}";
            }

            return RedirectToAction("Index");
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