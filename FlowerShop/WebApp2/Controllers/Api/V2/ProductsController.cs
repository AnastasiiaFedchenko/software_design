using Microsoft.AspNetCore.Mvc;
using Domain.InputPorts;
using WebApp2.Application.DTOs;
using AutoMapper;

namespace WebApp2.Controllers.Api.V2
{
    [ApiController]
    [Route("api/v2/[controller]")]
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
        public ActionResult<PagedResponse<ProductDto>> GetProducts([FromQuery] PaginationRequest request)
        {
            var defaultLimit = _config.GetValue<int>("AppSettings:DefaultPaginationLimit");
            var limit = request.Limit > 0 ? request.Limit : defaultLimit;

            var products = _productService.GetAllAvailableProducts(limit, request.Skip);
            var productDtos = _mapper.Map<List<ProductDto>>(products.Products);

            return Ok(new PagedResponse<ProductDto>
            {
                Data = productDtos,
                TotalCount = products.TotalAmount,
                Skip = request.Skip,
                Limit = limit
            });
        }

        [HttpGet("{id}")]
        public ActionResult<ProductDto> GetProduct(int id)
        {
            var product = _productService.GetInfoOnProduct(id);
            if (product == null)
                return NotFound();

            return Ok(_mapper.Map<ProductDto>(product));
        }
    }
}