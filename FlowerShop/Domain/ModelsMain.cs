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
}
