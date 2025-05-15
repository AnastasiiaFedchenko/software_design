using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class ProductLine
    {
        public Product Product { get; }
        public int Amount { get; }
        public ProductLine(Product product, int amount)
        {
            if (product == null)
                throw new ArgumentException("Product cannot be null");

            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero");

            Product = product;
            Amount = amount;
        }
    }
    public class ProductIDLine
    {
        public int ProductID { get; }
        public int Amount { get; }
        public ProductIDLine(int productID, int amount)
        {
            if (productID <= 0)
                throw new ArgumentException("ProductID cannot be <= 0");

            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero");

            ProductID = productID;
            Amount = amount;
        }
    }
    public class Inventory
    {
        public Guid Id { get; }
        public DateTime Date { get; }

        public User Supplier { get; }
        public User Responsible { get; }
        public int TotalAmount { get; set; }
        public List<ProductLine> Products { get; }
        public Inventory(Guid id, DateTime date, User supplier, User responsible, int total_amount, List<ProductLine> products)
        {
            Id = id;
            Date = date;
            Supplier = supplier;
            Responsible = responsible;
            TotalAmount = total_amount;
            Products = products;
        }
    }

    public class ProductBatch
    {
        public int Id { get; }
        public int TotalAmount { get; }
        public List<ProductLine> Products { get; }
        public ProductBatch(int id, int total_amount, List<ProductLine> products)
        {
            Id = id;
            TotalAmount = total_amount;
            Products = products;
        }
    }

}
