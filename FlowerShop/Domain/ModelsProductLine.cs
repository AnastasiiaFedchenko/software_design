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
            Product = product;
            Amount = amount;
        }
    }
    public class Inventory
    {
        public Guid Id { get; }
        public DateTime Date { get; }

        public User Supplier { get; }
        public User Responsible { get; }
        public int TotalAmount { get; }
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
        public Guid Id { get; }
        public int TotalAmount { get; }
        public List<ProductLine> Products { get; }
        public ProductBatch(Guid id, int total_amount, List<ProductLine> products)
        {
            Id = id;
            TotalAmount = total_amount;
            Products = products;
        }
    }

}
