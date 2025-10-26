namespace FlowerShop.Spa.Models
{
    public class Product
    {
        public int IdNomenclature { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int AmountInStock { get; set; }
    }

    public class Inventory
    {
        public List<Product> Products { get; set; } = new();
        public int TotalAmount { get; set; }
    }

    public class ReceiptLine
    {
        public Product Product { get; set; } = new();
        public int Amount { get; set; }
    }

    public class CartItem
    {
        public Product Product { get; set; } = new();
        public int Quantity { get; set; }
    }
}
