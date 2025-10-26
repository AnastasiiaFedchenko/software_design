namespace WebApp2.Application.DTOs
{
    public class ProductDto
    {
        public int IdNomenclature { get; set; }
        public double Price { get; set; }
        public int AmountInStock { get; set; }
        public string? Type { get; set; }
        public string? Country { get; set; }
    }
}