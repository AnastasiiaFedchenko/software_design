using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class ReceiptLine
    {
        public Product Product { get; }
        public int Amount { get; }
        public ReceiptLine(Product product, int amount)
        {
            if (product == null)
                throw new ArgumentException("Product cannot be null");

            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero");

            Product = product;
            Amount = amount;
        }
    }
    public class Receipt
    {
        public Guid Id { get; }
        public User Customer { get; }
        public double FinalPrice { get; }
        public DateTime Date { get; }
        public int TotalAmount { get; }
        public List<ReceiptLine> Products { get; }
        public Receipt(User customer, List<ReceiptLine> items)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items));

            if (items.Count == 0)
                throw new ArgumentException("Items list cannot be empty", nameof(items));

            if (customer == null)
                throw new ArgumentNullException(nameof(customer));

            FinalPrice = 0.0;
            TotalAmount = 0;

            foreach (var item in items)
            {
                FinalPrice += item.Product.Price * item.Amount;
                TotalAmount += item.Amount;
            }

            Id = Guid.NewGuid();
            Customer = customer;
            Date = DateTime.Now;
            Products = items;
        }
    }
}
