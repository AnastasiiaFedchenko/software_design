using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using ReceiptOfSale;
using Npgsql;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;
using ConnectionToDB;

namespace Integration.Tests
{
    [Collection("Database collection")]
    public class ReceiptTest : IDisposable
    {
        private readonly IReceiptRepo _receiptRepo;
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;
        private readonly ITestOutputHelper _output;
        private readonly DatabaseFixture _fixture;

        public ReceiptTest(ITestOutputHelper output, DatabaseFixture fixture)
        {
            _output = output;
            _fixture = fixture;
            _receiptRepo = new ReceiptRepo(new NpgsqlConnectionFactory(_fixture.TestConnectionString));
            _connection = new NpgsqlConnection(_fixture.TestConnectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction();
        }

        public void Dispose()
        {
            try
            {
                _transaction?.Rollback();
            }
            finally
            {
                _connection?.Close();
                _connection?.Dispose();
            }
        }

        [Fact]
        public void ProcessOrder_ShouldCreateOrderAndUpdateStock()
        {
            // Arrange
            int idInStock;
            var initialStock = GetProductStock(70, out idInStock);
            Assert.True(initialStock >= 2, $"Недостаточно товара на складе для теста (есть {initialStock}, нужно 2)");

            var receipt = new Receipt(
                customerID: 1,
                items: new List<ReceiptLine>
                {
                    new ReceiptLine(
                        product: new Product(70, 4522.5, initialStock, "Астильба", "Democratic Republic of Congo"),
                        amount: 2
                    )
                }
            );

            // Act
            try
            {
                var result = _receiptRepo.LoadReceiptItemsSale_UpdateAmount(ref receipt);
                _output.WriteLine($"Результат операции: {result}, ID заказа: {receipt.Id}");

                // Assert
                Assert.True(result, "Метод LoadReceiptItemsSale_UpdateAmount вернул false");

                var orderExists = CheckOrderExists(receipt.Id);
                Assert.True(orderExists, $"Заказ с ID {receipt.Id} не найден в БД");

                int temp;
                var updatedStock = GetProductStock(70, out temp);
                Assert.Equal(initialStock - 2, updatedStock);

                var saleExists = CheckSaleExists(receipt.Id);
                Assert.True(saleExists, $"Продажа для заказа {receipt.Id} не найдена");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Ошибка при выполнении теста: {ex}");
                throw;
            }
        }

        private int GetProductStock(int nomenclatureId, out int idInStock)
        {
            idInStock = 0;

            using var cmd = new NpgsqlCommand(
                @"SELECT 
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

            return 0;
        }

        private bool CheckOrderExists(int orderId)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM \"order\" WHERE id = @orderId",
                _connection, _transaction);

            cmd.Parameters.AddWithValue("@orderId", orderId);
            return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
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