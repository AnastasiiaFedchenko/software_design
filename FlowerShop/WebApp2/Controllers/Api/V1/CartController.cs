using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Domain;
using Domain.InputPorts;
using WebApp2.Application.DTOs;
using AutoMapper;
using WebApp2.Services;

namespace WebApp2.Controllers.Api.V1
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IMapper _mapper;
        private readonly ICartStorageService _cartStorage;

        public CartController(IProductService productService, IMapper mapper, ICartStorageService cartStorage)
        {
            _productService = productService;
            _mapper = mapper;
            _cartStorage = cartStorage;
        }

        [HttpGet]
        public IActionResult GetCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = _cartStorage.GetCart(userId);

                var receiptLines = cartItems.Select(item => new ReceiptLineDto
                {
                    Product = _mapper.Map<ProductDto>(item.Product),
                    Amount = item.Quantity
                }).ToList();

                return Ok(receiptLines);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Ошибка при получении корзины",
                    error = ex.Message
                });
            }
        }

        [HttpPost("items")]
        public IActionResult AddToCart([FromBody] CartItemRequest request)
        {
            try
            {
                var product = _productService.GetInfoOnProduct(request.ProductId);
                if (product == null)
                    return NotFound(new { message = "Товар не найден" });

                if (request.Quantity <= 0)
                    return BadRequest(new { message = "Количество должно быть больше 0" });

                var userId = GetCurrentUserId();

                var cartItem = new CartItem
                {
                    ProductId = request.ProductId,
                    Product = product,
                    Quantity = request.Quantity
                };

                _cartStorage.AddToCart(userId, cartItem);

                return Ok(new
                {
                    message = "Товар добавлен в корзину",
                    productId = request.ProductId,
                    quantity = request.Quantity
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Ошибка при добавлении товара в корзину",
                    error = ex.Message
                });
            }
        }

        [HttpPatch("products/{productId}")]
        public IActionResult UpdateCartItem(int productId, [FromBody] UpdateCartRequest request)
        {
            try
            {
                if (request.NewQuantity <= 0)
                    return BadRequest(new { message = "Количество должно быть больше 0" });

                var userId = GetCurrentUserId();

                if (!_cartStorage.CartExists(userId))
                    return NotFound(new { message = "Корзина не найдена" });

                _cartStorage.UpdateCartItem(userId, productId, request.NewQuantity);

                return Ok(new
                {
                    message = "Количество товара обновлено",
                    productId = productId,
                    newQuantity = request.NewQuantity
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Ошибка при обновлении количества товара",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("products/{productId}")]
        public IActionResult RemoveFromCart(int productId)
        {
            try
            {
                var userId = GetCurrentUserId();

                if (!_cartStorage.CartExists(userId))
                    return NotFound(new { message = "Корзина не найдена" });

                _cartStorage.RemoveFromCart(userId, productId);

                return Ok(new
                {
                    message = "Товар удален из корзины",
                    productId = productId
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Ошибка при удалении товара из корзины",
                    error = ex.Message
                });
            }
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

    public class UpdateCartRequest
    {
        public int NewQuantity { get; set; }
    }
}