using AutoMapper;
using Domain;
using WebApp2.Application.DTOs;

namespace WebApp2.Application.Mappings
{
    public class ProductProfile : Profile
    {
        public ProductProfile()
        {
            // Базовый маппинг Product -> ProductDto
            CreateMap<Product, ProductDto>();

            // Если есть ProductLine, добавьте его маппинг
            // CreateMap<ProductLine, ProductDto>()
            //     .ForMember(dest => dest.IdNomenclature, opt => opt.MapFrom(src => src.Product.IdNomenclature))
            //     .ForMember(dest => dest.Type, opt => opt.MapFrom(src => src.Product.Type))
            //     .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Product.Country))
            //     .ForMember(dest => dest.Price, opt => opt.MapFrom(src => src.Product.Price))
            //     .ForMember(dest => dest.AmountInStock, opt => opt.MapFrom(src => src.Product.AmountInStock));
        }
    }
}