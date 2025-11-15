using Domain;

namespace WebApp2.Application.DTOs
{
    public class ProductDto
    {
        public int IdNomenclature { get; set; }
        public string Type { get; set; }
        public string Country { get; set; }
        public decimal Price { get; set; }
        public int AmountInStock { get; set; }
    }

    public class PaginationRequest
    {
        public int Skip { get; set; } = 0;
        public int Limit { get; set; } = 10;
    }

    public class UpdateCartRequest
    {
        public int NewQuantity { get; set; }
    }

    public class CartItemRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class CartItem
    {
        public int ProductId { get; set; }
        public Product Product { get; set; }
        public int Quantity { get; set; }
    }

    public class ReceiptLineDto
    {
        public ProductDto Product { get; set; }
        public int Amount { get; set; }
    }

    public class LoginRequest
    {
        public int Id { get; set; }
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        public int UserId { get; set; }
        public string Role { get; set; }
        public string Token { get; set; }
    }
}