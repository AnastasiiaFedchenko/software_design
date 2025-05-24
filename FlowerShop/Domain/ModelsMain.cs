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
        public int IdNomenclature { get; }
        public double Price { get; }
        public int AmountInStock { get; }
        public string Type { get; }
        public string Country { get; }
        public Product(int id_nomenclature, double price, int amount_in_stock, string type, string country)
        {
            IdNomenclature = id_nomenclature;
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
        public int IdNomenclature { get; }
        public DateTime ProductionDate { get; }
        public DateTime ExpirationDate { get; }
        public double CostPrice { get; }
        public int Amount { get; }
        public int StoragePlace { get; }
        public ProductInfo(int id_nomenclature, DateTime production_date, DateTime expiration_date, int amount, double price)
        {
            IdNomenclature = id_nomenclature;
            ProductionDate = production_date;
            ExpirationDate = expiration_date;
            Amount = amount;
            CostPrice = price;
            Random random = new Random();
            StoragePlace = random.Next(1, 21);
        }
    }
    public enum UserType
    {
        Administrator = 1,
        Seller = 2,
        Storekeeper = 3
    }

    public class User
    {

        public int Id { get; }
        public string Name { get; }
        public UserType Role { get; }
        public string Password { get; }
        public User(int id, UserType role, string password)
        {
            Id = id;
            Name = "unknown";
            Role = role;
            Password = password;
        }
        public User(int id, string name, UserType role, string password)
        {
            Id = id;
            Name = name;
            Role = role;
            Password = password;
        }
    }
}
