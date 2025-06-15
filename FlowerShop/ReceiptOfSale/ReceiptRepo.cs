using System;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ReceiptOfSale
{
    public class ReceiptRepo : IReceiptRepo
    {
        private readonly string _connectionString;
        private readonly ILogger<ReceiptRepo> _logger;

        public ReceiptRepo(string connectionString, ILogger<ReceiptRepo> logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        public bool LoadReceiptItemsSale_UpdateAmount(ref Receipt receipt)
        {
            _logger.LogInformation("Начало обработки чека для клиента {CustomerId}", receipt.CustomerID);
            _logger.LogInformation("Чек содержит {ProductCount} товаров на сумму {TotalPrice}",
                receipt.Products.Count, receipt.FinalPrice);

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();
                _logger.LogInformation("Установлено соединение с БД");

                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // 1. Создаем новый заказ
                        var orderId = CreateNewOrder(connection, transaction, receipt);
                        _logger.LogInformation("Создан новый заказ ID: {OrderId}", orderId);

                        // 2. Добавляем товары в заказ
                        AddProductsToOrder(connection, transaction, orderId, receipt.Products);
                        _logger.LogInformation("Добавлены товары в заказ");

                        // 3. Создаем запись о продаже
                        receipt.Id = CreateSaleRecord(connection, transaction, orderId, receipt);
                        _logger.LogInformation("Создана запись о продаже");

                        // 4. Обновляем количество товаров на складе
                        if (!UpdateAmount(connection, transaction, receipt))
                        {
                            _logger.LogError("Не удалось обновить количество товаров на складе");
                            throw new Exception("Failed to update product amounts");
                        }
                        _logger.LogInformation("Обновлено количество товаров на складе");

                        transaction.Commit();
                        _logger.LogInformation("Транзакция успешно завершена");
                        return true;
                    }
                    catch (NpgsqlException ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Ошибка БД при обработке чека. Транзакция откачена");
                        return false;
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        _logger.LogError(ex, "Неожиданная ошибка при обработке чека. Транзакция откачена");
                        return false;
                    }
                }
            }
        }

        private bool UpdateAmount(NpgsqlConnection connection, NpgsqlTransaction transaction, Receipt receipt)
        {
            _logger.LogDebug("Начало обновления количества товаров на складе");

            foreach (var productLine in receipt.Products)
            {
                int remainingAmount = productLine.Amount;
                int nomenclatureId = productLine.Product.IdNomenclature;

                _logger.LogInformation("Обработка товара ID: {NomenclatureId}, запрошено: {Amount}",
                    nomenclatureId, productLine.Amount);

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
                                _logger.LogError("Недостаточно товара на складе. ID: {NomenclatureId}, запрошено: {Amount}, осталось: {RemainingAmount}",
                                    nomenclatureId, productLine.Amount, productLine.Amount - remainingAmount);
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
            _logger.LogInformation("Создание нового заказа для клиента {CustomerId}", receipt.CustomerID);

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

                var orderId = (int)command.ExecuteScalar();
                _logger.LogInformation("Создан заказ ID: {OrderId}", orderId);
                return orderId;
            }
        }

        private void AddProductsToOrder(NpgsqlConnection connection, NpgsqlTransaction transaction,
                                      int orderId, List<ReceiptLine> products)
        {
            _logger.LogInformation("Добавление {ProductCount} товаров в заказ {OrderId}", products.Count, orderId);

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
                            _logger.LogError("Недостаточно товара на складе для номенклатуры {NomenclatureId}",
                                productLine.Product.IdNomenclature);
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
                    _logger.LogInformation("Товар {ProductId} добавлен в заказ {OrderId}",
                        productLine.IdProductInStock, orderId);
                }
            }
        }

        private int CreateSaleRecord(NpgsqlConnection connection, NpgsqlTransaction transaction,
                                    int orderId, Receipt receipt)
        {
            _logger.LogInformation("Создание записи о продаже для заказа {OrderId}", orderId);

            using (var getNumberCommand = new NpgsqlCommand(
                "SELECT COALESCE(MAX(receipt_number), 0) + 1 FROM sales",
                connection,
                transaction))
            {
                int receiptNumber = (int)getNumberCommand.ExecuteScalar();
                _logger.LogInformation("Получен номер чека: {ReceiptNumber}", receiptNumber);

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
                    _logger.LogInformation("Создана продажа с номером чека {ReceiptNumber} на сумму {FinalPrice}",
                        receiptNumber, receipt.FinalPrice);
                }
                return receiptNumber;
            }
            return -1;
        }
    }
}