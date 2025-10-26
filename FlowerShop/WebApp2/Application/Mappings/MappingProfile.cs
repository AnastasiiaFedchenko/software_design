using AutoMapper;
using Domain;
using WebApp2.Application.DTOs;

namespace WebApp2.Application.Mappings
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Domain.Product, ProductDto>();
            CreateMap<Domain.ReceiptLine, ReceiptLineDto>();
            CreateMap<ReceiptLineDto, Domain.ReceiptLine>();
        }
    }
}