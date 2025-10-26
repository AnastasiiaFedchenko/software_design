using Microsoft.AspNetCore.Mvc;
using Domain;
using Domain.InputPorts;
using WebApp2.Application.DTOs;
using AutoMapper;

namespace WebApp2.Controllers.Api.V2
{
    [ApiController]
    [Route("api/v2/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IMapper _mapper;

        public CartController(IProductService productService, IMapper mapper)
        {
            _productService = productService;
            _mapper = mapper;
        }

        [HttpPost("items")]
        public IActionResult AddToCart([FromBody] CartItemRequest request)
        {
            var product = _productService.GetInfoOnProduct(request.ProductId);
            if (product == null)
                return NotFound(new { message = "Товар не найден" });

            return Ok(new { message = "Товар добавлен в корзину", productId = request.ProductId, quantity = request.Quantity });
        }

        [HttpPost("checkout")]
        public IActionResult Checkout([FromBody] CheckoutRequest request)
        {
            try
            {
                // Маппим DTO в доменные модели
                var cartLines = new List<ReceiptLine>();
                if (request.CartItems != null)
                {
                    foreach (var itemDto in request.CartItems)
                    {
                        if (itemDto.Product != null)
                        {
                            var product = new Domain.Product(
                                itemDto.Product.IdNomenclature,
                                itemDto.Product.Price,
                                itemDto.Product.AmountInStock,
                                itemDto.Product.Type ?? string.Empty,
                                itemDto.Product.Country ?? string.Empty
                            );
                            cartLines.Add(new ReceiptLine(product, itemDto.Amount));
                        }
                    }
                }

                var receipt = _productService.MakePurchase(cartLines, request.CustomerId);

                if (receipt.Id == -1)
                    return BadRequest(new { message = "Ошибка при оформлении заказа" });

                return Ok(new { receiptId = receipt.Id, message = "Заказ успешно оформлен" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Ошибка сервера", error = ex.Message });
            }
        }
    }

    public class CartItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CheckoutRequest
    {
        public int CustomerId { get; set; }
        public List<ReceiptLineDto>? CartItems { get; set; }
    }
}