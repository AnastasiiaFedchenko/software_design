using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

namespace Domain
{
    public class Product
    {
        public Guid Nomenclature { get; }
        public int Amount { get; }
        public double Price { get; }
        public int AmountInStock { get; }

        public string Type { get; }
        public string Country { get; }
        public Product(Guid nomenclature, int amount, double price, int amount_in_stock, string type, string country)
        {
            Nomenclature = nomenclature;
            Amount = amount;
            Price = price;
            AmountInStock = amount_in_stock;
            Type = type;
            Country = country;
        }
    }
    public class ReceiptLine
    {
        public Product Product { get; }
        public int Amount { get; }
        public ReceiptLine(Product product, int amount)
        {
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
        public Receipt (Guid id, User customer, double final_price, DateTime date, int total_amount, List<ReceiptLine> products)
        {
            Id = id;
            Customer = customer;
            FinalPrice = final_price;
            Date = date;
            TotalAmount = total_amount;
            Products = products;
        }
    }
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

    public class User
    {

        public Guid Id { get; }
        public string Role { get; }
        public User(Guid id, string role)
        {
            Id = id;
            Role = role;
        }
    }

    public class UserSegment
    {
        public string Type { get; }
        public int Amount { get; }
        List<User> Users { get; }
        public UserSegment(string type, int amount, List<User> users)
        {
            Type = type;
            Amount = amount;
            Users = users;
        }
    }

    public class ForecastOfOrders
    {
        public int AmountOfOrders { get; set; }
        public int AmountOfProducts { get; set; }
        public List<ProductLine> Products { get; set; }
        public ForecastOfOrders(int amount_of_orders, int amount_of_products, List<ProductLine> products)
        {
            AmountOfOrders = amount_of_orders;
            AmountOfProducts = amount_of_products;
            Products = products;
        }
    }
}
