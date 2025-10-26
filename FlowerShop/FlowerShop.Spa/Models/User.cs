namespace FlowerShop.Spa.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public int UserId { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public int UserId { get; set; }
    }

    public class Product
    {
        public int IdNomenclature { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public double Price { get; set; }
        public int AmountInStock { get; set; }
    }

    public class InventoryItem
    {
        public Product Product { get; set; } = new Product();
        public int Amount { get; set; }
    }

    public class Inventory
    {
        public List<InventoryItem> Products { get; set; } = new List<InventoryItem>();
        public int TotalAmount { get; set; }
    }

    public class ReceiptLine
    {
        public ReceiptLine() { }

        public ReceiptLine(Product product, int amount)
        {
            Product = product;
            Amount = amount;
        }

        public Product Product { get; set; } = new Product();
        public int Amount { get; set; }
    }
}