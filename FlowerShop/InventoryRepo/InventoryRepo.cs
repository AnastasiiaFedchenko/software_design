using System;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace InventoryOfProducts
{
    public class InventoryRepo : IInventoryRepo
    {
        private readonly string _connectionString;
        private readonly ILogger<InventoryRepo> _logger;

        public InventoryRepo(string connectionString, ILogger<InventoryRepo> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public Inventory GetAvailableProduct(int limit, int skip)
        {
            _logger.LogInformation("Запрос доступных товаров. Limit: {Limit}, Skip: {Skip}", limit, skip);

            using var connection = new NpgsqlConnection(_connectionString);
            try
            {
                connection.Open();
                _logger.LogDebug("Установлено соединение с БД");

                var query = @"
                    SELECT 
                        n.id AS nomenclature_id,
                        n.name AS nomenclature_name,
                        p.selling_price AS price,
                        SUM(pis.amount) AS available_amount,
                        c.name AS country_name
                    FROM 
                        nomenclature n
                    JOIN 
                        price p ON n.id = p.id_nomenclature
                    JOIN 
                        product_in_stock pis ON p.id_nomenclature = pis.id_nomenclature 
                                            AND p.id_product_batch = pis.id_product_batch
                    JOIN
                        batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                            AND pis.id_nomenclature = bop.id_nomenclature
                    JOIN
                        country c ON n.country_id = c.id
                    WHERE
                        bop.expiration_date > CURRENT_DATE
                    GROUP BY 
                        n.id, n.name, p.selling_price, c.name
                    HAVING 
                        SUM(pis.amount) > 0
                    ORDER BY 
                        n.id
                    LIMIT @limit OFFSET @skip";

                var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@limit", limit);
                command.Parameters.AddWithValue("@skip", skip);

                _logger.LogDebug("Выполнение SQL запроса: {Query}", query);

                var productLines = new List<ProductLine>();
                int totalAmount = 0;

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var product = new Product(
                            id_nomenclature: reader.GetInt32(0),
                            price: reader.GetDouble(2),
                            amount_in_stock: reader.GetInt32(3),
                            type: reader.GetString(1),
                            country: reader.GetString(4)
                        );

                        productLines.Add(new ProductLine(product, reader.GetInt32(3)));
                        totalAmount += 1;
                    }
                }

                _logger.LogInformation("Получено {Count} товаров из БД", totalAmount);
                return new Inventory(
                    id: Guid.NewGuid(),
                    date: DateTime.Now,
                    total_amount: totalAmount,
                    products: productLines
                );
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка при выполнении запроса к БД");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при получении товаров");
                throw;
            }
        }

        public Product GetInfoOnProduct(int productID)
        {
            _logger.LogInformation("Запрос информации о товаре. ProductID: {ProductID}", productID);

            if (productID <= 0)
            {
                _logger.LogWarning("Некорректный ID товара: {ProductID}", productID);
                throw new ArgumentException("Id товара не может быть отрицательным или равным нулю.");
            }

            using var connection = new NpgsqlConnection(_connectionString);
            try
            {
                connection.Open();
                _logger.LogInformation("Установлено соединение с БД");

                var query = @"
                    SELECT 
                        n.id AS nomenclature_id,
                        n.name AS nomenclature_name,
                        p.selling_price AS price,
                        SUM(pis.amount) AS available_amount,
                        c.name AS country_name
                    FROM 
                        nomenclature n
                    JOIN 
                        price p ON n.id = p.id_nomenclature
                    JOIN 
                        product_in_stock pis ON p.id_nomenclature = pis.id_nomenclature 
                                            AND p.id_product_batch = pis.id_product_batch
                    JOIN
                        batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                            AND pis.id_nomenclature = bop.id_nomenclature
                    JOIN
                        country c ON n.country_id = c.id
                    WHERE
                        bop.expiration_date > CURRENT_DATE and n.id = @product_id
                    GROUP BY 
                        n.id, n.name, p.selling_price, c.name
                    HAVING 
                        SUM(pis.amount) > 0
                    ORDER BY 
                        n.id
                    LIMIT 1";

                var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@product_id", productID);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var product = new Product(
                        id_nomenclature: reader.GetInt32(0),
                        price: reader.GetDouble(2),
                        amount_in_stock: reader.GetInt32(3),
                        type: reader.GetString(1),
                        country: reader.GetString(4)
                    );

                    _logger.LogInformation("Товар найден");
                    return product;
                }

                _logger.LogWarning("Товар с ID {ProductID} не найден", productID);
                return null;
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка БД при запросе информации о товаре {ProductID}", productID);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при запросе информации о товаре {ProductID}", productID);
                throw;
            }
        }

        public bool CheckNewAmount(int product_id, int new_n)
        {
            _logger.LogInformation("Проверка доступного количества. ProductID: {ProductID}, NewAmount: {NewAmount}",
                product_id, new_n);

            if (new_n <= 0)
            {
                _logger.LogWarning("Некорректное количество: {NewAmount}", new_n);
                throw new ArgumentException("Новое количество не может быть отрицательным или равным нулю.");
            }
            if (product_id <= 0)
            {
                _logger.LogWarning("Некорректный ID товара: {ProductID}", product_id);
                throw new ArgumentException("Id товара не может быть отрицательным или равным нулю.");
            }

            using var connection = new NpgsqlConnection(_connectionString);
            try
            {
                connection.Open();
                _logger.LogInformation("Установлено соединение с БД");

                var query = @"
                    SELECT COALESCE(SUM(pis.amount), 0)
                    FROM product_in_stock pis
                    JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch 
                                             AND pis.id_nomenclature = bop.id_nomenclature
                    JOIN nomenclature n ON pis.id_nomenclature = n.id
                    WHERE n.id = @product_id::integer
                    AND bop.expiration_date > CURRENT_DATE";

                var command = new NpgsqlCommand(query, connection);
                command.Parameters.AddWithValue("@product_id", product_id.ToString());

                long availableAmount = (long)(command.ExecuteScalar() ?? 0L);
                _logger.LogInformation("Доступное количество: {AvailableAmount}, Запрашиваемое: {NewAmount}",
                    availableAmount, new_n);

                return new_n <= availableAmount;
            }
            catch (NpgsqlException ex)
            {
                _logger.LogError(ex, "Ошибка БД при проверке количества товара {ProductID}", product_id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при проверке количества товара {ProductID}", product_id);
                throw;
            }
        }
    }
}