using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Domain;
using Domain.OutputPorts;

namespace SegmentAnalysis
{
    public class UserSegmentationServiceAdapter : IUserSegmentationServiceAdapter
    {
        private readonly string _connectionString;

        public UserSegmentationServiceAdapter()
        {
            _connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShopPPO;Username=postgres;Password=5432";
        }

        public List<UserSegment> Create()
        {
            var segments = new List<UserSegment>();

            var topSellers = GetTopSellersByAverageCheck();
            segments.Add(new UserSegment(
                "Sellers by average Check",
                topSellers.Count,
                topSellers
            ));

            var topSuppliers = GetTopSuppliersByDeliveryCost();
            segments.Add(new UserSegment(
                "Suppliers by batch cost",
                topSuppliers.Count,
                topSuppliers
            ));

            return segments;
        }

        private List<User> GetTopSellersByAverageCheck()
        {
            var users = new List<User>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"
                            SELECT 
                                u.id, 
                                u.name,
                                AVG(s.final_price) as avg_check
                            FROM 
                                ""user"" u
                            JOIN 
                                ""order"" o ON u.id = o.responsible
                            JOIN 
                                sales s ON o.id = s.order_id
                            WHERE 
                                u.type = 'продавец'::user_role
                            GROUP BY 
                                u.id, u.name
                            ORDER BY 
                                avg_check DESC
                            LIMIT 10";

                using (var command = new NpgsqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string name = reader.GetString(1);
                        decimal avgCheck = reader.GetDecimal(2);

                        var user = new User(
                            id: id,
                            role: UserType.Seller,
                            password: "seller_password",
                            name: $"{name} (Средний чек: {avgCheck:N2} руб.)"
                        );

                        users.Add(user);
                    }
                }
            }

            return users;
        }

        private List<User> GetTopSuppliersByDeliveryCost()
        {
            var users = new List<User>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                var query = @"
                            SELECT 
                                c.id,
                                c.name,
                                SUM(bop.cost_price * bop.amount) as total_delivery_cost
                            FROM 
                                counterpart c
                            JOIN 
                                batch_of_products bop ON c.id = bop.suppliers
                            WHERE 
                                c.type = 'поставщик'::counterpart_role
                            GROUP BY 
                                c.id, c.name  -- Добавляем name в GROUP BY
                            ORDER BY 
                                total_delivery_cost DESC
                            LIMIT 10";

                using (var command = new NpgsqlCommand(query, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        string name = reader.GetString(1);
                        decimal totalCost = reader.GetDecimal(2);

                        var user = new User(
                            id: id,
                            role: UserType.Storekeeper,
                            password: "supplier_no_password",
                            name: $"{name} (Поставки на {totalCost:N2} руб.)" 
                        );

                        users.Add(user);
                    }
                }
            }

            return users;
        }
    }
}