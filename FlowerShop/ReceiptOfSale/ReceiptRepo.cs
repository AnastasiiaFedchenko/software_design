using System;
using System.Collections.Generic;
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
            _connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShopPPO;Username=postgres;Password=5432";
        }

        public bool LoadReceiptItemsSale_UpdateAmount(ref Receipt receipt)
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

                        // 2. Добавляем товары в заказ
                        AddProductsToOrder(connection, transaction, receipt.Id, receipt.Products);

                        // 3. Создаем запись о продаже
                        CreateSaleRecord(connection, transaction, receipt.Id, receipt);

                        // 4. Обновляем количество товаров на складе (теперь последним шагом)
                        if (!UpdateAmount(connection, transaction, receipt))
                        {
                            throw new Exception("Failed to update product amounts");
                        }

                        transaction.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        Console.WriteLine($"Error processing receipt: {ex.Message}");
                        return false;
                    }
                }
            }
        }

        private bool UpdateAmount(NpgsqlConnection connection, NpgsqlTransaction transaction, Receipt receipt)
        {
            foreach (var productLine in receipt.Products)
            {
                int remainingAmount = productLine.Amount;
                int nomenclatureId = productLine.Product.IdNomenclature;

                while (remainingAmount > 0)
                {
                    using (var findBatchCommand = new NpgsqlCommand(
                        @"SELECT pis.id, pis.amount
                          FROM product_in_stock pis
                          JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                                    AND pis.id_nomenclature = bop.id_nomenclature
                          WHERE pis.id_nomenclature = @nomenclatureId
                            AND pis.amount > 0
                            AND bop.expiration_date > CURRENT_DATE
                          ORDER BY bop.expiration_date ASC, pis.amount DESC
                          LIMIT 1
                          FOR UPDATE",
                        connection,
                        transaction))
                    {
                        findBatchCommand.Parameters.AddWithValue("@nomenclatureId", nomenclatureId);

                        using (var reader = findBatchCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int productInStockId = reader.GetInt32(0);
                                int availableAmount = reader.GetInt32(1);
                                reader.Close();

                                int amountToReduce = Math.Min(availableAmount, remainingAmount);

                                using (var updateCommand = new NpgsqlCommand(
                                    @"UPDATE product_in_stock
                                      SET amount = amount - @amountToReduce
                                      WHERE id = @productInStockId",
                                    connection,
                                    transaction))
                                {
                                    updateCommand.Parameters.AddWithValue("@amountToReduce", amountToReduce);
                                    updateCommand.Parameters.AddWithValue("@productInStockId", productInStockId);
                                    updateCommand.ExecuteNonQuery();
                                }

                                remainingAmount -= amountToReduce;
                            }
                            else
                            {
                                Console.WriteLine($"Недостаточно товара на складе. ID: {nomenclatureId}, запрошено: {productLine.Amount}, осталось: {productLine.Amount - remainingAmount}");
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }

        private int CreateNewOrder(NpgsqlConnection connection, NpgsqlTransaction transaction, Receipt receipt)
        {
            using (var command = new NpgsqlCommand(
                @"INSERT INTO ""order"" (reg_date, counterpart, responsible) 
                  VALUES (@regDate, @counterpart, 
                         (SELECT id FROM ""user"" WHERE type = 'кладовщик' ORDER BY random() LIMIT 1))
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
                using (var findCommand = new NpgsqlCommand(
                    @"SELECT pis.id, p.id as price_id
                      FROM product_in_stock pis
                      JOIN price p ON pis.id_nomenclature = p.id_nomenclature 
                                  AND pis.id_product_batch = p.id_product_batch
                      JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                                AND pis.id_nomenclature = bop.id_nomenclature
                      WHERE pis.id_nomenclature = @nomenclatureId
                      AND pis.amount >= @amount 
                      AND bop.expiration_date > CURRENT_DATE
                      ORDER BY bop.expiration_date ASC, pis.amount DESC
                      LIMIT 1;",
                    connection,
                    transaction))
                {
                    findCommand.Parameters.AddWithValue("@nomenclatureId", productLine.Product.IdNomenclature);
                    findCommand.Parameters.AddWithValue("@amount", productLine.Amount);

                    using (var reader = findCommand.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            productLine.IdProductInStock = reader.GetInt32(0);
                            productLine.PriceId = reader.GetInt32(1);
                        }
                        else
                        {
                            throw new Exception($"Not enough stock for product {productLine.Product.IdNomenclature}");
                        }
                    }
                }

                using (var insertCommand = new NpgsqlCommand(
                    @"INSERT INTO order_product_in_stock 
                      (id_order, id_product, amount, price)
                      VALUES (@orderId, @productId, @amount, @priceId)",
                    connection,
                    transaction))
                {
                    insertCommand.Parameters.AddWithValue("@orderId", orderId);
                    insertCommand.Parameters.AddWithValue("@productId", productLine.IdProductInStock);
                    insertCommand.Parameters.AddWithValue("@amount", productLine.Amount);
                    insertCommand.Parameters.AddWithValue("@priceId", productLine.PriceId);

                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private void CreateSaleRecord(NpgsqlConnection connection, NpgsqlTransaction transaction,
                                    int orderId, Receipt receipt)
        {
            using (var getNumberCommand = new NpgsqlCommand(
                "SELECT COALESCE(MAX(receipt_number), 0) + 1 FROM sales",
                connection,
                transaction))
            {
                int receiptNumber = (int)getNumberCommand.ExecuteScalar();

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