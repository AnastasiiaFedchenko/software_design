using Microsoft.AspNetCore.Mvc;
using Domain.InputPorts;
using WebApp2.Application.DTOs;
using AutoMapper;

namespace WebApp2.Controllers.Api.V1
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IMapper _mapper;
        private readonly IConfiguration _config;

        public ProductsController(IProductService productService, IMapper mapper, IConfiguration config)
        {
            _productService = productService;
            _mapper = mapper;
            _config = config;
        }

        [HttpGet]
        public IActionResult GetProducts([FromQuery] PaginationRequest request)
        {
            try
            {
                var defaultLimit = _config.GetValue<int>("AppSettings:DefaultPaginationLimit");
                var limit = request.Limit > 0 ? request.Limit : defaultLimit;

                var products = _productService.GetAllAvailableProducts(limit, request.Skip);

                // Преобразуем данные в упрощенную структуру
                var simplifiedProducts = products.Products.Select(p => new
                {
                    idNomenclature = p.Product.IdNomenclature,
                    type = p.Product.Type,
                    country = p.Product.Country,
                    price = p.Product.Price,
                    amountInStock = p.Product.AmountInStock
                    // Убрали дублирующиеся amount и amountInStock
                }).ToList();

                return Ok(new
                {
                    Data = simplifiedProducts,
                    TotalCount = products.TotalAmount,
                    Skip = request.Skip,
                    Limit = limit
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = ex.Message,
                    details = ex.StackTrace
                });
            }
        }

        [HttpGet("{id}")]
        public IActionResult GetProduct(int id)
        {
            try
            {
                var product = _productService.GetInfoOnProduct(id);
                if (product == null)
                    return NotFound(new { message = "Product not found" });

                // Упрощенная структура для одного продукта
                var simplifiedProduct = new
                {
                    idNomenclature = product.IdNomenclature,
                    type = product.Type,
                    country = product.Country,
                    price = product.Price,
                    amountInStock = product.AmountInStock
                };

                return Ok(simplifiedProduct);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = ex.Message
                });
            }
        }
    }
}