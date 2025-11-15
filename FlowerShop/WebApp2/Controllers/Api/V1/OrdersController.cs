using Microsoft.AspNetCore.Mvc;
using Domain;
using Domain.InputPorts;
using System.Security.Claims;
using WebApp2.Services;

namespace WebApp2.Controllers.Api.V2
{
    [ApiController]
    [Route("api/orders")]
    public class OrdersController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<OrdersController> _logger;
        private readonly ICartStorageService _cartStorage;

        public OrdersController(IProductService productService,
                              ILogger<OrdersController> logger,
                              ICartStorageService cartStorage)
        {
            _productService = productService;
            _logger = logger;
            _cartStorage = cartStorage;
        }

        [HttpPost]
        public IActionResult CreateOrder()
        {
            try
            {
                var cart = GetCartFromSession();

                if (cart == null || !cart.Any())
                {
                    return BadRequest(new { message = "Корзина пуста" });
                }

                var customerId = GetCurrentUserId();
                var receipt = _productService.MakePurchase(cart, customerId);

                if (receipt.Id == -1)
                {
                    return BadRequest(new { message = "Ошибка при оформлении заказа" });
                }

                ClearCart();

                return Ok(new { receiptId = receipt.Id, message = "Заказ успешно оформлен" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оформлении заказа");
                return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
            }
        }

        private List<ReceiptLine> GetCartFromSession()
        {
            var userId = GetCurrentUserId();
            var cartItems = _cartStorage.GetCart(userId);

            var receiptLines = cartItems.Select(item => new ReceiptLine(item.Product, item.Quantity)).ToList();

            return receiptLines;
        }

        private void ClearCart()
        {
            var userId = GetCurrentUserId();
            _cartStorage.ClearCart(userId);
        }

        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userIdClaim))
            {
                throw new UnauthorizedAccessException("User ID not found in claims");
            }

            if (!int.TryParse(userIdClaim, out int userId))
            {
                throw new InvalidOperationException($"Invalid user ID format: {userIdClaim}");
            }

            return userId;
        }
    }
}