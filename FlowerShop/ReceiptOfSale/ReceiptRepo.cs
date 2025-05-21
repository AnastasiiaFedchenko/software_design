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

        public ReceiptRepo()
        {
            _connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432";
        }

        public bool load(Receipt receipt)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Создаем новый заказ
                        int newOrderId = CreateNewOrder(connection, transaction, receipt);
                        //Console.WriteLine("Создали новый заказ в таблице order");

                        // 2. Добавляем товары в заказ
                        AddProductsToOrder(connection, transaction, newOrderId, receipt.Products);
                        //Console.WriteLine("Добавили товары в таблицу order_product_in_stock");

                        // 3. Создаем запись о продаже
                        CreateSaleRecord(connection, transaction, newOrderId, receipt);
                        //Console.WriteLine("Создали запись о продаже в таблице sales");

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Error loading receipt: {ex.Message}");
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
                      WHERE pis.id_nomenclature = @nomenclatureId
                      AND pis.amount >= @amount
                      ORDER BY pis.id_product_batch, pis.amount DESC
                      LIMIT 1",
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