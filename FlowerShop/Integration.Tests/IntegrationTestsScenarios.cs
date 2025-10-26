using Moq;
using ConnectionToDB;
using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using InventoryOfProducts;
using Microsoft.Extensions.Logging;
using Npgsql;
using ReceiptOfSale;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Generic;

namespace Integration.Tests
{
    [Collection("Database collection")]
    public class ProductServiceTest : IDisposable
    {
        private readonly IProductService _productService;
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;
        private readonly ITestOutputHelper _output;
        private readonly DatabaseFixture _fixture;
        private readonly IInventoryRepo _inventoryRepo;
        private readonly IReceiptRepo _receiptRepo;

        public ProductServiceTest(ITestOutputHelper output, DatabaseFixture fixture)
        {
            _output = output;
            _fixture = fixture;

            var connectionString = _fixture.TestConnectionString;

            _inventoryRepo = new InventoryRepo(new NpgsqlConnectionFactory(connectionString));
            _receiptRepo = new ReceiptRepo(new NpgsqlConnectionFactory(connectionString));

            var loggerMock = new Mock<ILogger<ProductService>>();
            _productService = new ProductService(_inventoryRepo, _receiptRepo, loggerMock.Object);

            _connection = new NpgsqlConnection(connectionString);
            _connection.Open();
            _transaction = _connection.BeginTransaction();
        }

        public void Dispose()
        {
            try
            {
                _transaction?.Rollback();
                _transaction?.Dispose();
            }
            finally
            {
                _connection?.Close();
                _connection?.Dispose();
            }
        }

        [Fact]
        public void Scenario1_CustomerBrowsingProducts_ShouldWorkCorrectly()
        {
            _output.WriteLine("=== Сценарий 1: Просмотр товаров покупателем ===");

            // Шаг 1: Покупатель просматривает каталог товаров
            var inventory = _productService.GetAllAvailableProducts(10, 0);
            _output.WriteLine($"Показано товаров: {inventory?.Products?.Count ?? 0}");

            // Assert
            Assert.NotNull(inventory);
            Assert.NotEmpty(inventory.Products);
            Assert.True(inventory.TotalAmount > 0);

            // Шаг 2: Покупатель выбирает конкретный товар для просмотра деталей
            const int productId = 70;
            var product = _productService.GetInfoOnProduct(productId);
            _output.WriteLine($"Просмотр товара: {product?.Type}, Цена: {product?.Price}");

            // Assert
            Assert.NotNull(product);
            Assert.Equal(productId, product.IdNomenclature);
            Assert.True(product.Price > 0);
            Assert.NotNull(product.Type);

            // Шаг 3: Покупатель проверяет доступность нужного количества
            var availableStock = GetProductStock(productId);
            var canBuyOne = _productService.CheckNewAmount(productId, 1);
            var canBuyAllStock = _productService.CheckNewAmount(productId, availableStock);
            var cannotBuyMoreThanStock = _productService.CheckNewAmount(productId, availableStock + 1);

            _output.WriteLine($"Доступно: {availableStock}, Можно купить 1: {canBuyOne}, " +
                            $"Можно купить все: {canBuyAllStock}, Можно купить больше чем есть: {cannotBuyMoreThanStock}");

            // Assert
            Assert.True(canBuyOne);
            Assert.True(canBuyAllStock);
            Assert.False(cannotBuyMoreThanStock);
        }

        [Fact]
        public void Scenario2_SuccessfulPurchase_ShouldCompleteTransaction()
        {
            _output.WriteLine("=== Сценарий 2: Успешное оформление покупки ===");

            // Шаг 1: Подготовка данных для покупки
            const int productId = 70;
            const int customerId = 1;
            var availableStock = GetProductStock(productId);

            Assert.True(availableStock >= 2, $"Недостаточно товара для теста (есть {availableStock}, нужно 2)");

            var product = _productService.GetInfoOnProduct(productId);
            Assert.NotNull(product);

            // Шаг 2: Создание заказа
            var receiptLines = new List<ReceiptLine>
            {
                new ReceiptLine(product: product, amount: 2)
            };

            // Шаг 3: Оформление покупки
            var receipt = _productService.MakePurchase(receiptLines, customerId);
            _output.WriteLine($"Оформлен чек №{receipt?.Id} на {receipt?.Products?.Count} товаров");

            // Assert
            Assert.NotNull(receipt);
            Assert.True(receipt.Id > 0);
            Assert.Equal(customerId, receipt.CustomerID);
            Assert.Single(receipt.Products);
            Assert.Equal(productId, receipt.Products[0].Product.IdNomenclature);
            Assert.Equal(2, receipt.Products[0].Amount);

            // Шаг 4: Проверка обновления остатков
            var updatedStock = GetProductStock(productId);
            _output.WriteLine($"Остаток после покупки: было {availableStock}, стало {updatedStock}");

            Assert.Equal(availableStock - 2, updatedStock);
        }

        [Fact]
        public void Scenario3_FailedPurchaseScenarios_ShouldHandleErrors()
        {
            _output.WriteLine("=== Сценарий 3: Обработка неудачных попыток покупки ===");

            const int customerId = 1;
            const int productId = 70;

            // Случай 1: Попытка купить с пустой корзиной
            var emptyCartReceipt = _productService.MakePurchase(new List<ReceiptLine>(), customerId);
            _output.WriteLine($"Попытка покупки с пустой корзиной: {(emptyCartReceipt == null ? "Отклонено" : "Успешно")}");
            Assert.Null(emptyCartReceipt);

            // Случай 2: Попытка купить несуществующий товар
            var nonExistentProduct = _productService.GetInfoOnProduct(999999);
            _output.WriteLine($"Поиск несуществующего товара: {(nonExistentProduct == null ? "Не найден" : "Найден")}");
            Assert.Null(nonExistentProduct);

            // Случай 3: Попытка купить больше чем есть в наличии
            var product = _productService.GetInfoOnProduct(productId);
            Assert.NotNull(product);

            var availableStock = GetProductStock(productId);
            var excessiveOrder = new List<ReceiptLine>
            {
                new ReceiptLine(product: product, amount: availableStock + 10)
            };

            var failedReceipt = _productService.MakePurchase(excessiveOrder, customerId);
            _output.WriteLine($"Попытка купить {availableStock + 10} при наличии {availableStock}: " +
                            $"{(failedReceipt == null ? "Отклонено" : "Успешно")}");
            Assert.Null(failedReceipt);
        }

        [Fact]
        public void Scenario4_InputValidation_ShouldPreventInvalidOperations()
        {
            _output.WriteLine("=== Сценарий 4: Валидация входных данных ===");

            // Проверка валидации параметров для просмотра товаров
            Assert.Throws<ArgumentOutOfRangeException>(() => _productService.GetAllAvailableProducts(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _productService.GetAllAvailableProducts(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => _productService.GetAllAvailableProducts(10, -1));
            _output.WriteLine("Валидация параметров GetAllAvailableProducts: OK");

            // Проверка валидации параметров для проверки количества
            Assert.Throws<ArgumentException>(() => _productService.CheckNewAmount(-1, 10));
            Assert.Throws<ArgumentException>(() => _productService.CheckNewAmount(0, 10));
            Assert.Throws<ArgumentException>(() => _productService.CheckNewAmount(70, -1));
            _output.WriteLine("Валидация параметров CheckNewAmount: OK");

            // Проверка валидации параметров для получения информации о товаре
            Assert.Throws<ArgumentException>(() => _productService.GetInfoOnProduct(-1));
            Assert.Throws<ArgumentException>(() => _productService.GetInfoOnProduct(0));
            _output.WriteLine("Валидация параметров GetInfoOnProduct: OK");
        }

        [Fact]
        public void Scenario5_ProductSearchAndAvailability_ShouldProvideAccurateInfo()
        {
            _output.WriteLine("=== Сценарий 5: Поиск товаров и проверка доступности ===");

            // Шаг 1: Поиск существующего товара
            const int existingProductId = 70;
            var existingProduct = _productService.GetInfoOnProduct(existingProductId);
            Assert.NotNull(existingProduct);
            _output.WriteLine($"Найден товар: ID={existingProduct.IdNomenclature}, {existingProduct.Type}");

            // Шаг 2: Поиск несуществующего товара
            const int nonExistentProductId = 999999;
            var nonExistentProduct = _productService.GetInfoOnProduct(nonExistentProductId);
            Assert.Null(nonExistentProduct);
            _output.WriteLine($"Товар ID={nonExistentProductId}: не найден");

            // Шаг 3: Точная проверка доступности
            var availableStock = GetProductStock(existingProductId);
            var preciseAvailability = _productService.CheckNewAmount(existingProductId, availableStock);
            var overAvailability = _productService.CheckNewAmount(existingProductId, availableStock + 1);

            _output.WriteLine($"Точная проверка доступности: {availableStock} единиц - " +
                            $"{(preciseAvailability ? "Доступно" : "Недоступно")}, " +
                            $"{availableStock + 1} единиц - {(overAvailability ? "Доступно" : "Недоступно")}");

            Assert.True(preciseAvailability);
            Assert.False(overAvailability);
        }

        private int GetProductStock(int nomenclatureId)
        {
            using var cmd = new NpgsqlCommand(
                @"SELECT 
                    SUM(pis.amount) AS available_amount
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
                    bop.expiration_date > CURRENT_DATE and n.id = @nomenclatureId
                  GROUP BY 
                    n.id, n.name, p.selling_price, c.name
                  HAVING 
                    SUM(pis.amount) > 0
                  ORDER BY 
                    n.id
                  LIMIT 1",
                _connection, _transaction);

            cmd.Parameters.AddWithValue("@nomenclatureId", nomenclatureId);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }
    }
}