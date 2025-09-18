using System;
using System.Collections.Generic;
using System.Data;
using Domain;
using Domain.OutputPorts;
using ConnectionToDB;

namespace ReceiptOfSale
{
    public class ReceiptRepo : IReceiptRepo
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ReceiptRepo(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool LoadReceiptItemsSale_UpdateAmount(ref Receipt receipt)
        {
            using (var connection = _connectionFactory.CreateOpenConnection())
            {
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

                        // 4. Обновляем количество товаров на складе
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

        private bool UpdateAmount(IDbConnection connection, IDbTransaction transaction, Receipt receipt)
        {
            foreach (var productLine in receipt.Products)
            {
                int remainingAmount = productLine.Amount;
                int nomenclatureId = productLine.Product.IdNomenclature;

                while (remainingAmount > 0)
                {
                    using (var findBatchCommand = connection.CreateCommand())
                    {
                        findBatchCommand.Transaction = transaction;
                        findBatchCommand.CommandText = @"
                            SELECT pis.id, pis.amount
                            FROM product_in_stock pis
                            JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                                      AND pis.id_nomenclature = bop.id_nomenclature
                            WHERE pis.id_nomenclature = @nomenclatureId
                              AND pis.amount > 0
                              AND bop.expiration_date > CURRENT_DATE
                            ORDER BY bop.expiration_date ASC, pis.amount DESC
                            LIMIT 1
                            FOR UPDATE";

                        AddParameter(findBatchCommand, "@nomenclatureId", nomenclatureId);

                        using (var reader = findBatchCommand.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int productInStockId = reader.GetInt32(0);
                                int availableAmount = reader.GetInt32(1);
                                reader.Close();

                                int amountToReduce = Math.Min(availableAmount, remainingAmount);

                                using (var updateCommand = connection.CreateCommand())
                                {
                                    updateCommand.Transaction = transaction;
                                    updateCommand.CommandText = @"
                                        UPDATE product_in_stock
                                        SET amount = amount - @amountToReduce
                                        WHERE id = @productInStockId";

                                    AddParameter(updateCommand, "@amountToReduce", amountToReduce);
                                    AddParameter(updateCommand, "@productInStockId", productInStockId);
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

        private int CreateNewOrder(IDbConnection connection, IDbTransaction transaction, Receipt receipt)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = @"
                    INSERT INTO ""order"" (reg_date, counterpart, responsible) 
                    VALUES (@regDate, @counterpart, 
                           (SELECT id FROM ""user"" WHERE type = 'кладовщик' ORDER BY random() LIMIT 1))
                    RETURNING id";

                AddParameter(command, "@regDate", receipt.Date);
                AddParameter(command, "@counterpart", receipt.CustomerID);

                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void AddProductsToOrder(IDbConnection connection, IDbTransaction transaction,
                                      int orderId, List<ReceiptLine> products)
        {
            foreach (var productLine in products)
            {
                using (var findCommand = connection.CreateCommand())
                {
                    findCommand.Transaction = transaction;
                    findCommand.CommandText = @"
                        SELECT pis.id, p.id as price_id
                        FROM product_in_stock pis
                        JOIN price p ON pis.id_nomenclature = p.id_nomenclature 
                                    AND pis.id_product_batch = p.id_product_batch
                        JOIN batch_of_products bop ON pis.id_product_batch = bop.id_product_batch
                                                  AND pis.id_nomenclature = bop.id_nomenclature
                        WHERE pis.id_nomenclature = @nomenclatureId
                        AND pis.amount >= @amount 
                        AND bop.expiration_date > CURRENT_DATE
                        ORDER BY bop.expiration_date ASC, pis.amount DESC
                        LIMIT 1";

                    AddParameter(findCommand, "@nomenclatureId", productLine.Product.IdNomenclature);
                    AddParameter(findCommand, "@amount", productLine.Amount);

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

                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
                        INSERT INTO order_product_in_stock 
                        (id_order, id_product, amount, price)
                        VALUES (@orderId, @productId, @amount, @priceId)";

                    AddParameter(insertCommand, "@orderId", orderId);
                    AddParameter(insertCommand, "@productId", productLine.IdProductInStock);
                    AddParameter(insertCommand, "@amount", productLine.Amount);
                    AddParameter(insertCommand, "@priceId", productLine.PriceId);

                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        private void CreateSaleRecord(IDbConnection connection, IDbTransaction transaction,
                                    int orderId, Receipt receipt)
        {
            using (var getNumberCommand = connection.CreateCommand())
            {
                getNumberCommand.Transaction = transaction;
                getNumberCommand.CommandText = "SELECT COALESCE(MAX(receipt_number), 0) + 1 FROM sales";

                int receiptNumber = Convert.ToInt32(getNumberCommand.ExecuteScalar());

                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = @"
                        INSERT INTO sales 
                        (receipt_number, counterpart, order_id, order_status, final_price)
                        VALUES (@receiptNumber, @counterpart, @orderId, 'Получен', @finalPrice)";

                    AddParameter(insertCommand, "@receiptNumber", receiptNumber);
                    AddParameter(insertCommand, "@counterpart", receipt.CustomerID);
                    AddParameter(insertCommand, "@orderId", orderId);
                    AddParameter(insertCommand, "@finalPrice", receipt.FinalPrice);

                    insertCommand.ExecuteNonQuery();
                }
            }
        }

        // Вспомогательный метод для добавления параметров
        private void AddParameter(IDbCommand command, string parameterName, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}