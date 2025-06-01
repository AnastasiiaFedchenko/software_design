﻿using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using Npgsql;
using ReceiptOfSale;
using System.Transactions;
using Xunit;
using Xunit.Abstractions;

namespace Integration.Tests
{
    public class ReceiptRepoTest
    {
        private readonly IReceiptRepo _receiptRepo;
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;

        public ReceiptRepoTest()
        {
            _receiptRepo = new ReceiptRepo("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            _connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            _connection.Open();
            _transaction = _connection.BeginTransaction();
        }

        [Fact]
        public void ProcessOrder_ShouldCreateOrderAndUpdateStock()
        {
            // Arrange
            // Предполагаем, что в БД уже есть товар с ID=23 (Пион)
            int IdInStock;
            var initialStock = GetProductStock(23, out IdInStock);
            Assert.True(initialStock >= 2, "Недостаточно товара на складе для теста");

            var receipt = new Receipt(
                customerID: 1,
                items: new List<ReceiptLine>
                {
                new ReceiptLine(
                    product: new Product(23, 4522.5, initialStock, "Пион", "Turkmenistan"),
                    amount: 2
                )
                }
            );
            // Act
            var result = _receiptRepo.load(ref receipt);

            // Assert
            Assert.True(result);

            // Проверяем создание заказа
            var orderExists = CheckOrderExists(receipt.Id);
            Assert.True(orderExists);

            // Проверяем изменение количества товара
            int temp;
            var updatedStock = GetProductStock(23, out temp);
            Assert.Equal(initialStock - 2, updatedStock);

            // Проверяем создание записи о продаже
            var saleExists = CheckSaleExists(receipt.Id);
            Assert.True(saleExists);
            MakeClean(receipt, initialStock, IdInStock);
            _transaction.Commit();
        }

        private void MakeClean(Receipt receipt, int initialStock, int IdInStock)
        {
            using var cmd = new NpgsqlCommand(
                            @"
                        delete from sales where order_id = @order_id;
                        delete from order_product_in_stock where id_order = @order_id;
                        delete from ""order"" where id = @order_id;",
                _connection, _transaction);
            cmd.Parameters.AddWithValue("@order_id", receipt.Id);
            cmd.ExecuteNonQuery();
            for (int i = 0; i < receipt.Products.Count; i++)
            {
                using var cmd2 = new NpgsqlCommand(
                            @"
                        update product_in_stock set amount = @initialStock where id = @product_in_stock_id; ",
                _connection, _transaction);
                cmd2.Parameters.AddWithValue("@product_in_stock_id", IdInStock);
                cmd2.Parameters.AddWithValue("@initialStock", initialStock);
                cmd2.ExecuteNonQuery();
            }

        }
        private int GetProductStock(int nomenclatureId, out int idInStock)
        {
            idInStock = 0; // Initialize output parameter

            using var cmd = new NpgsqlCommand(
                @"
                SELECT 
                    SUM(pis.amount) AS available_amount, 
                    MIN(pis.id) AS id_in_stock
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
                    AND n.id = @nomenclatureId
                GROUP BY 
                    n.id, n.name, p.selling_price, c.name
                HAVING 
                    SUM(pis.amount) > 0
                ORDER BY 
                    n.id
                LIMIT 1",
                _connection, _transaction);

            cmd.Parameters.AddWithValue("@nomenclatureId", nomenclatureId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                var availableAmount = reader.GetInt32(0);
                idInStock = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                return availableAmount;
            }

            return 0; // Return 0 if no stock found
        }

        private bool CheckOrderExists(int orderId)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM \"order\" WHERE id = @orderId",
                _connection, _transaction);

            cmd.Parameters.AddWithValue("@orderId", orderId);
            int temp = Convert.ToInt32(cmd.ExecuteScalar());
            return temp == 1;
        }

        private bool CheckSaleExists(int orderId)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM sales WHERE order_id = @orderId",
                _connection, _transaction);

            cmd.Parameters.AddWithValue("@orderId", orderId);
            return Convert.ToInt64(cmd.ExecuteScalar()) == 1;
        }
    }
}
