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
    public class Inventory
    {
        public Guid Id { get; }
        public DateTime Date { get; }

        public int TotalAmount { get; set; }
        public List<ProductLine> Products { get; }
        public Inventory(Guid id, DateTime date, int total_amount, List<ProductLine> products)
        {
            Id = id;
            Date = date;
            if (total_amount <= 0)
                throw new ArgumentException("Amount must be greater than zero");
            TotalAmount = total_amount;
            if (products == null)
                throw new ArgumentException("Products can not be null");
            Products = products;
        }
    }

    public class ProductBatch
    {
        public int Id { get; }
        public int Supplier { get; }
        public int Responsible { get; }

        public List<ProductInfo> ProductsInfo { get; }
        public ProductBatch(int id, int supplier, int responsible, List<ProductInfo> products)
        {
            Id = id;
            Supplier = supplier;
            Responsible = responsible;
            if (products == null)
                throw new ArgumentException("Products can not be null");
            ProductsInfo = products;
        }
    }

}
