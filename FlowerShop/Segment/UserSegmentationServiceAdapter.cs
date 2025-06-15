using System;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace SegmentAnalysis
{
    public class UserSegmentationServiceAdapter : IUserSegmentationServiceAdapter
    {
        private readonly string _connectionString;
        private readonly ILogger<UserSegmentationServiceAdapter> _logger;

        public UserSegmentationServiceAdapter(string connectionString, ILogger<UserSegmentationServiceAdapter> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public List<UserSegment> Create()
        {
            _logger.LogInformation("Начало создания сегментов пользователей");

            try
            {
                var segments = new List<UserSegment>();

                var topSellers = GetTopSellersByAverageCheck();
                segments.Add(new UserSegment(
                    "Sellers by average Check",
                    topSellers.Count,
                    topSellers
                ));
                _logger.LogInformation("Создан сегмент 'Sellers by average Check' с {Count} пользователями", topSellers.Count);

                var topSuppliers = GetTopSuppliersByDeliveryCost();
                segments.Add(new UserSegment(
                    "Suppliers by batch cost",
                    topSuppliers.Count,
                    topSuppliers
                ));
                _logger.LogInformation("Создан сегмент 'Suppliers by batch cost' с {Count} пользователями", topSuppliers.Count);

                _logger.LogInformation("Успешно создано {TotalSegments} сегментов", segments.Count);
                return segments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании сегментов пользователей");
                throw;
            }
        }

        private List<User> GetTopSellersByAverageCheck()
        {
            _logger.LogInformation("Получение топ-10 продавцов по среднему чеку");

            var users = new List<User>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    _logger.LogInformation("Установлено соединение с БД для получения продавцов");

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

                    _logger.LogInformation("Получено {Count} продавцов", users.Count);
                }
                catch (NpgsqlException ex)
                {
                    _logger.LogError(ex, "Ошибка БД при получении списка продавцов");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Неожиданная ошибка при получении списка продавцов");
                    throw;
                }
            }

            return users;
        }

        private List<User> GetTopSuppliersByDeliveryCost()
        {
            _logger.LogInformation("Получение топ-10 поставщиков по стоимости поставок");

            var users = new List<User>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                try
                {
                    connection.Open();
                    _logger.LogInformation("Установлено соединение с БД для получения поставщиков");

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
                            c.id, c.name
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

                    _logger.LogInformation("Получено {Count} поставщиков", users.Count);
                }
                catch (NpgsqlException ex)
                {
                    _logger.LogError(ex, "Ошибка БД при получении списка поставщиков");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Неожиданная ошибка при получении списка поставщиков");
                    throw;
                }
            }

            return users;
        }
    }
}