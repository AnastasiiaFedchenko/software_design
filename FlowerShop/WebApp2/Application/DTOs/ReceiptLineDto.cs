namespace WebApp2.Application.DTOs
{
    public class ReceiptLineDto
    {
        public ProductDto? Product { get; set; }
        public int Amount { get; set; }
    }
}