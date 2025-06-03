using Domain;
using Domain.OutputPorts;
using ProductBatchLoading;
using Npgsql;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;

namespace Integration.Tests
{
    [Collection("Database collection")]
    public class ProductBatchLoaderTest : IDisposable
    {
        private readonly IProductBatchLoader _batchLoader;
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;
        private readonly ITestOutputHelper _output;
        private readonly DatabaseFixture _fixture;

        public ProductBatchLoaderTest(ITestOutputHelper output, DatabaseFixture fixture)
        {
            _output = output;
            _fixture = fixture;
            _batchLoader = new ProductBatchLoader(_fixture.TestConnectionString);
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
        public void Load_ShouldInsertBatchAndUpdateStock()
        {
            // Arrange
            var batch = new ProductBatch(
                id: 600,
                responsible: 62,
                supplier: 101,
                products: new List<ProductInfo>
                {
                    new ProductInfo(
                        id_nomenclature: 70,
                        amount: 20,
                        price: 200,
                        production_date: DateTime.Now.AddDays(-1),
                        expiration_date: DateTime.Now.AddDays(30))
                }
            );

            // Act
            var result = _batchLoader.Load(batch);
            _output.WriteLine($"Результат загрузки партии: {result}");

            // Assert
            Assert.True(result);

            // Проверяем, что партия добавлена
            using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM batch_of_products WHERE id_product_batch = 600",
                _connection, _transaction))
            {
                var count = (long)cmd.ExecuteScalar();
                _output.WriteLine($"Найдено партий: {count}");
                Assert.Equal(1, count);
            }

            // Проверяем, что товар добавлен на склад
            using (var cmd = new NpgsqlCommand(
                "SELECT SUM(amount) FROM product_in_stock WHERE id_nomenclature = 70 AND id_product_batch = 600",
                _connection, _transaction))
            {
                var amount = Convert.ToInt32(cmd.ExecuteScalar());
                _output.WriteLine($"Количество товара на складе: {amount}");
                Assert.Equal(20, amount);
            }

            // Проверяем, что добавлена цена о товаре
            using (var cmd = new NpgsqlCommand(
                "SELECT COUNT(*) FROM price WHERE id_nomenclature = 70 AND id_product_batch = 600",
                _connection, _transaction))
            {
                var count = Convert.ToInt32(cmd.ExecuteScalar());
                _output.WriteLine($"Информация о цене записана: {count > 0}");
                Assert.True(count > 0);
            }
        }
    }
}