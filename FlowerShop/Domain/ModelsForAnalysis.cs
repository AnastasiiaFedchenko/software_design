﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
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
