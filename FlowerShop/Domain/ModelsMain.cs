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
        public int Nomenclature { get; }
        public int Amount { get; }
        public double Price { get; }
        public int AmountInStock { get; }

        public string Type { get; }
        public string Country { get; }
        public Product(int nomenclature, int amount, double price, int amount_in_stock, string type, string country)
        {
            Nomenclature = nomenclature;
            Amount = amount;
            Price = price;
            AmountInStock = amount_in_stock;
            Type = type;
            Country = country;
        }
    }
    public class ProductInfo
    {
        /*
        id_поставки
        количество позиций в поставке
        поставщик
        ответственный
        id_номенклатуры|production_date|expiration_date|cost_price|amount
         */
        public int Nomenclature { get; }
        public DateTime ProductionDate { get; }
        public DateTime ExpirationDate { get; }
        public double CostPrice { get; }
        public int Amount { get; }
        public ProductInfo(int nomenclature, DateTime production_date, DateTime expiration_date, int amount, double price)
        {
            Nomenclature = nomenclature;
            ProductionDate = production_date;
            ExpirationDate = expiration_date;
            Amount = amount;
            CostPrice = price;
        }
    }
    public class User
    {

        public int Id { get; }
        public string Role { get; }
        public User(int id, string role)
        {
            Id = id;
            Role = role;
        }
    }
}
