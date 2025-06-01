using Domain;
using Domain.OutputPorts;
using ProductBatchLoading;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Integration.Tests
{
    public class ProductBatchLoaderTest
    {
        private readonly IProductBatchLoader _batchLoader;
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;
        private readonly ITestOutputHelper _output;

        public ProductBatchLoaderTest(ITestOutputHelper output)
        {
            _output = output;
            _batchLoader = new ProductBatchLoader("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            _connection = new NpgsqlConnection("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
            _connection.Open();
            _transaction = _connection.BeginTransaction();
        }

        [Fact]
        public void Load_ShouldInsertBatchAndUpdateStock()
        {
            // Arrange
            var batch = new ProductBatch(
                id: 600,
                responsible: 1,
                supplier: 3,
                products: new List<ProductInfo>
                {
                new ProductInfo(
                    id_nomenclature: 1,
                    amount: 20,
                    price: 200,
                    production_date: DateTime.Now.AddDays(-1),
                    expiration_date: DateTime.Now.AddDays(30)
                )
                }
            );

            // Act
            var result = _batchLoader.load(batch);
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
                "SELECT SUM(amount) FROM product_in_stock WHERE id_nomenclature = 1 and id_product_batch = 600",
                _connection, _transaction))
            {
                var amount = Convert.ToInt32(cmd.ExecuteScalar());
                _output.WriteLine($"Количество товара на складе: {amount}");
                Assert.Equal(20, amount);
            }
            // Проверяем, что добавлена цена о товаре
            using (var cmd = new NpgsqlCommand(
                "SELECT 1 FROM price WHERE id_nomenclature = 1 and id_product_batch = 600",
                _connection, _transaction))
            {
                var there = Convert.ToBoolean(cmd.ExecuteScalar());
                _output.WriteLine($"Информация о цене записана: {there}");
                Assert.Equal(true, there);
            }

            MakeClean();
            _transaction.Commit();
        }

        private void MakeClean()
        {
            using var cmd = new NpgsqlCommand(
                            @"
                        delete from price where id_nomenclature = 1 and id_product_batch = 600;
                        delete from product_in_stock where id_nomenclature = 1 and id_product_batch = 600;
                        delete from batch_of_products where id_product_batch = 600",
                _connection, _transaction);
            cmd.ExecuteNonQuery();
        }
    }
}