using System;
using System.Collections.Generic;
using System.Data;
using Domain;
using Domain.OutputPorts;
using Npgsql;

namespace ReceiptOfSale
{
    public class ReceiptRepo : IReceiptRepo
    {
        private readonly string _connectionString;

        public ReceiptRepo(string connectionString)
        {
            _connectionString = connectionString;
        }

        public bool load(ref Receipt receipt)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Создаем новый заказ
                        receipt.Id = CreateNewOrder(connection, transaction, receipt);
                        //Console.WriteLine("Создали новый заказ в таблице order");

                        // 2. Добавляем товары в заказ
                        AddProductsToOrder(connection, transaction, receipt.Id, receipt.Products);
                        //Console.WriteLine("Добавили товары в таблицу order_product_in_stock");

                        // 3. Создаем запись о продаже
                        CreateSaleRecord(connection, transaction, receipt.Id, receipt);
                        //Console.WriteLine("Создали запись о продаже в таблице sales");

                        transaction.Commit();
                        return true;
                    }
                    catch (PostgresException ex) when (ex.SqlState == "42501")
                    {
                        transaction.Rollback();

                        // Получаем имя текущего пользователя и проверяем права
                        using (var cmd = new NpgsqlCommand(
                            "SELECT current_user, has_table_privilege(current_user, 'batch_of_products', 'SELECT')",
                            connection))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    Console.WriteLine($"User: {reader.GetString(0)}, Has SELECT on batch_of_products: {reader.GetBoolean(1)}");
                                }
                            }
                        }

                        Console.WriteLine($"Detailed permission error: {ex.Message}");
                        receipt.Id = -1;
                        return false;
                    }
                }
            }
        }

        private int CreateNewOrder(NpgsqlConnection connection, NpgsqlTransaction transaction, Receipt receipt)
        {
            using (var command = new NpgsqlCommand(
                @"INSERT INTO ""order"" (reg_date, counterpart, responsible) 
                  VALUES (@regDate, @counterpart, 
                         (SELECT id FROM ""user"" WHERE type = 'продавец' ORDER BY random() LIMIT 1))
                  RETURNING id",
                connection,
                transaction))
            {
                command.Parameters.AddWithValue("@regDate", receipt.Date);
                command.Parameters.AddWithValue("@counterpart", receipt.CustomerID);

                return (int)command.ExecuteScalar();
            }
        }

        private void AddProductsToOrder(NpgsqlConnection connection, NpgsqlTransaction transaction,
                                      int orderId, List<ReceiptLine> products)
        {
            foreach (var productLine in products)
            {
                // Находим подходящий товар на складе
                int productInStockId, priceId;

                using (var findCommand = new NpgsqlCommand(
                    @"SELECT pis.id, p.id as price_id
                        FROM product_in_stock pis
                        JOIN price p ON pis.id_nomenclature = p.id_nomenclature 
                                    AND pis.id_product_batch = p.id_product_batch
                        JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                                  AND pis.id_nomenclature = bop.id_nomenclature
                        WHERE pis.id_nomenclature = @nomenclatureId
                        AND pis.amount >= @amount 
                        AND bop.expiration_date > CURRENT_DATE  -- Проверка срока годности
                        ORDER BY bop.expiration_date ASC,  -- Сначала партии с ближайшим сроком годности
                                 pis.amount DESC
                        LIMIT 1;",
                    connection,
                    transaction))
                {
                    findCommand.Parameters.AddWithValue("@nomenclatureId", productLine.Product.Nomenclature);
                    findCommand.Parameters.AddWithValue("@amount", productLine.Amount);

                    using (var reader = findCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            productInStockId = reader.GetInt32(0);
                            priceId = reader.GetInt32(1);
                        }
                        else
                        {
                            throw new Exception($"Not enough stock for product {productLine.Product.Nomenclature}");
                        }
                    }
                }

                // Добавляем товар в заказ
                using (var insertCommand = new NpgsqlCommand(
                    @"INSERT INTO order_product_in_stock 
                      (id_order, id_product, amount, price)
                      VALUES (@orderId, @productId, @amount, @priceId)",
                    connection,
                    transaction))
                {
                    insertCommand.Parameters.AddWithValue("@orderId", orderId);
                    insertCommand.Parameters.AddWithValue("@productId", productInStockId);
                    insertCommand.Parameters.AddWithValue("@amount", productLine.Amount);
                    insertCommand.Parameters.AddWithValue("@priceId", priceId);

                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private void CreateSaleRecord(NpgsqlConnection connection, NpgsqlTransaction transaction,
                                    int orderId, Receipt receipt)
        {
            // Получаем следующий номер чека
            using (var getNumberCommand = new NpgsqlCommand(
                "SELECT COALESCE(MAX(receipt_number), 0) + 1 FROM sales",
                connection,
                transaction))
            {
                int receiptNumber = (int)getNumberCommand.ExecuteScalar();

                // Создаем запись о продаже
                using (var insertCommand = new NpgsqlCommand(
                    @"INSERT INTO sales 
                      (receipt_number, counterpart, order_id, order_status, final_price)
                      VALUES (@receiptNumber, @counterpart, @orderId, 'Получен', @finalPrice)",
                    connection,
                    transaction))
                {
                    insertCommand.Parameters.AddWithValue("@receiptNumber", receiptNumber);
                    insertCommand.Parameters.AddWithValue("@counterpart", receipt.CustomerID);
                    insertCommand.Parameters.AddWithValue("@orderId", orderId);
                    insertCommand.Parameters.AddWithValue("@finalPrice", receipt.FinalPrice);

                    insertCommand.ExecuteNonQuery();
                }
            }
        }
    }
}