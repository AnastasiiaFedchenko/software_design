using Moq;
using Xunit;
using Allure.Xunit.Attributes;
using Allure.Xunit;
using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Allure.Net.Commons;


namespace Domain.Tests
{
    [AllureSuite("Domain Layer")]
    [AllureFeature("Product Management")]
    [AllureSubSuite("Product Entity")]
    public class ProductServiceTests
    {
        private readonly Mock<IInventoryRepo> _mockInventoryRepo;
        private readonly Mock<IReceiptRepo> _mockReceiptRepo;
        private readonly Mock<ILogger<ProductService>> _mocklogger;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            _mockInventoryRepo = new Mock<IInventoryRepo>();
            _mockReceiptRepo = new Mock<IReceiptRepo>();
            _mocklogger = new Mock<ILogger<ProductService>>();
            _productService = new ProductService(
                _mockInventoryRepo.Object,
                _mockReceiptRepo.Object,
                _mocklogger.Object);
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void GetAllAvailableProducts_ShouldReturnInventory()
        {
            // Arrange
            var expectedInventory = new Inventory(
                id: Guid.NewGuid(),
                date: DateTime.Now,
                total_amount: 10,
                products: new List<ProductLine>()
            );

            _mockInventoryRepo.Setup(x => x.GetAvailableProduct(20, 0))
                .Returns(expectedInventory);

            // Act
            var result = _productService.GetAllAvailableProducts(20, 0);

            // Assert
            Assert.Equal(expectedInventory, result);
            _mockInventoryRepo.Verify(x => x.GetAvailableProduct(20, 0), Times.Once);
        }

        //тест был удалён так как повторялся
        // тесты отсюда были убраны так как они повторяли тесты в create receipt и полностью зависело от внутреннего функционала

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CheckNewAmount_WithValidProductAndAmount_ShouldReturnTrue()
        {
            // Arrange
            var productId = 148;
            var amount = 10;
            _mockInventoryRepo.Setup(x => x.CheckNewAmount(productId, amount))
                .Returns(true);

            // Act
            var result = _productService.CheckNewAmount(productId, amount);

            // Assert
            Assert.True(result);
            _mockInventoryRepo.Verify(x => x.CheckNewAmount(productId, amount), Times.Once);
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CheckNewAmount_WithInvalidProduct_ShouldReturnFalse()
        {
            // Arrange
            var productId = 148;
            var amount = 5;
            _mockInventoryRepo.Setup(x => x.CheckNewAmount(productId, amount))
                .Returns(false);

            // Act
            var result = _productService.CheckNewAmount(productId, amount);

            // Assert
            Assert.False(result);
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CheckNewAmount_WithNegativeAmount_ShouldThrowArgumentException()
        {
            // Arrange
            var productId = 148;
            var amount = -1;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _productService.CheckNewAmount(productId, amount));
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CheckNewAmount_WithEmptyProductId_ShouldThrowArgumentException()
        {
            // Arrange
            int productId = 0;
            var amount = 5;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _productService.CheckNewAmount(productId, amount));
        }
        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CreateReceipt_WithValidItemsAndCustomer_ReturnsReceipt()
        {
            // Arrange
            var items = ReceiptData_Mother.GetBuilder()
                .WithReceiptLine(
                    Product_Builder.ForProduct()
                        .WithId(1)
                        .WithPrice(10.99)
                        .WithAmountInStock(100)
                        .WithType("Electronics")
                        .WithCountry("China")
                        .Build(),
                    1)
                .WithReceiptLine(
                    Product_Builder.ForProduct()
                        .WithId(2)
                        .WithPrice(5.99)
                        .WithAmountInStock(50)
                        .WithType("Groceries")
                        .WithCountry("Local")
                        .Build(),
                    2)
                .Build();

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10.99 + (5.99 * 2), result.FinalPrice, 2);
            Assert.Equal(3, result.TotalAmount);
            Assert.Equal(items, result.Products);
            Assert.True((DateTime.Now - result.Date).TotalSeconds < 1);
        }

        [Fact]
        public void CreateReceipt_WithSingleItem_CorrectlyCalculatesTotal()
        {
            // Arrange
            var items = ReceiptData_Mother.CreateReceipt_WithSingleItem();

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.Equal(7.50, result.FinalPrice);
            Assert.Equal(3, result.TotalAmount);
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CreateReceipt_WithZeroPriceItem_IncludesInTotal()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 0.00, 100, "Free Item", "Local"),
                    1)
            };

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.Equal(0.00, result.FinalPrice);
            Assert.Equal(1, result.TotalAmount);
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CreateReceipt_WithNullItems_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new Receipt(1, null));
            Assert.Contains("items", ex.Message);
        }

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CreateReceipt_WithEmptyItems_ThrowsArgumentException()
        {
            // Arrange
            var items = new List<ReceiptLine>();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new Receipt(1, items));
            Assert.Contains("items", ex.Message);
        }
        // тут был тест, но он утратил смысл

        [Fact]
        [AllureStory("Product creation")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("Domain Team")]
        public void CreateReceipt_WithMultipleItems_CorrectlyCalculatesTotals()
        {
            // Arrange
            var items = ReceiptData_Mother.CreateReceipt_WithMultipleItems();

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.Equal((1.99 * 5) + (4.50 * 2) + (0.99 * 10), result.FinalPrice, 2);
            Assert.Equal(17, result.TotalAmount);
        }
    }

    // Builder ReceiptLine
    public class ReceiptListBuilder
    {
        private readonly List<ReceiptLine> _items = new List<ReceiptLine>();

        public ReceiptListBuilder WithReceiptLine(Product product, int quantity)
        {
            _items.Add(new ReceiptLine(product, quantity));
            return this;
        }

        public ReceiptListBuilder WithReceiptLine(ReceiptLine line)
        {
            _items.Add(line);
            return this;
        }

        public List<ReceiptLine> Build()
        {
            return _items;
        }
    }

    public class Product_Builder
    {
        private int IdNomenclature = 1;
        private double Price = 1.00;
        private int AmountInStock = 100;
        private string Type = "General";
        private string Country = "Local";

        private Product_Builder() { }

        public static Product_Builder ForProduct()
        {
            return new Product_Builder();
        }

        public Product_Builder WithId(int id)
        {
            IdNomenclature = id;
            return this;
        }

        public Product_Builder WithPrice(double price)
        {
            Price = price;
            return this;
        }

        public Product_Builder WithAmountInStock(int amount_in_stock)
        {
            AmountInStock = amount_in_stock;
            return this;
        }

        public Product_Builder WithType(string type)
        {
            Type = type;
            return this;
        }

        public Product_Builder WithCountry(string country)
        {
            Country = country;
            return this;
        }

        public Product Build()
        {
            return new Product(IdNomenclature, Price, AmountInStock, Type, Country);
        }
    }

    // Object Mother
    public class ReceiptData_Mother
    {
        public static List<ReceiptLine> CreateReceipt_WithSingleItem()
        {
            return new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 2.50, 100, "Food", "Local"),
                    3)
            };
        }

        public static List<ReceiptLine> CreateReceipt_WithMultipleItems()
        {
            return new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 1.99, 100, "Snacks", "Local"),
                    5),
                new ReceiptLine(
                    new Product(2, 4.50, 50, "Drinks", "Local"),
                    2),
                new ReceiptLine(
                    new Product(3, 0.99, 200, "Candy", "Local"),
                    10)
            };
        }

        public static List<ReceiptLine> CreateReceipt_WithZeroPriceItem()
        {
            return new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 0.00, 100, "Free Item", "Local"),
                    1)
            };
        }

        // возвращает билдер
        public static ReceiptListBuilder GetBuilder()
        {
            return new ReceiptListBuilder();
        }

        public static List<ReceiptLine> CreateStandardReceipt()
        {
            return GetBuilder()
                .WithReceiptLine(
                    Product_Builder.ForProduct()
                        .WithId(1)
                        .WithPrice(9.99)
                        .WithType("Standard Product")
                        .Build(),
                    2)
                .WithReceiptLine(
                    Product_Builder.ForProduct()
                        .WithId(2)
                        .WithPrice(4.99)
                        .Build(),
                    1)
                .Build();
        }
    }
}