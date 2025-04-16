using Moq;
using Xunit;
using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using System;
using System.Collections.Generic;

namespace Domain.Tests
{
    public class ProductServiceTests
    {
        private readonly Mock<IInventoryRepo> _mockInventoryRepo;
        private readonly Mock<IReceiptRepo> _mockReceiptRepo;
        private readonly ProductService _productService;

        public ProductServiceTests()
        {
            _mockInventoryRepo = new Mock<IInventoryRepo>();
            _mockReceiptRepo = new Mock<IReceiptRepo>();
            _productService = new ProductService(
                _mockInventoryRepo.Object,
                _mockReceiptRepo.Object);
        }

        [Fact]
        public void GetAllAvailableProducts_ShouldReturnInventory()
        {
            // Arrange
            var expectedInventory = new Inventory(
                id: Guid.NewGuid(),
                date: DateTime.Now,
                supplier: new User(Guid.NewGuid(), "Supplier1"),
                responsible: new User(Guid.NewGuid(), "Responsible1"),
                total_amount: 10,
                products: new List<ProductLine>()
            );

            _mockInventoryRepo.Setup(x => x.create())
                .Returns(expectedInventory);

            // Act
            var result = _productService.GetAllAvailableProducts();

            // Assert
            Assert.Equal(expectedInventory, result);
            _mockInventoryRepo.Verify(x => x.create(), Times.Once);
        }

        [Fact]
        public void MakePurchase_WithValidItemsAndCustomer_ShouldReturnReceipt()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 2, 10.99, 100, "Electronics", "China"),
                    1)
            };
            var customer = new User(Guid.NewGuid(), "Customer");
            var expectedReceipt = new Receipt(
                Guid.NewGuid(), customer, 10.99, DateTime.Now, 1, items);

            _mockReceiptRepo.Setup(x => x.create(items, customer))
                .Returns(expectedReceipt);

            // Act
            var result = _productService.MakePurchase(items, customer);

            // Assert
            Assert.Equal(expectedReceipt, result);
            _mockReceiptRepo.Verify(x => x.create(items, customer), Times.Once);
        }

        [Fact]
        public void MakePurchase_WithNullItems_ShouldThrowArgumentNullException()
        {
            // Arrange
            var customer = new User(Guid.NewGuid(), "Customer");

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _productService.MakePurchase(null, customer));
        }

        [Fact]
        public void MakePurchase_WithEmptyItems_ShouldThrowArgumentException()
        {
            // Arrange
            var items = new List<ReceiptLine>();
            var customer = new User(Guid.NewGuid(), "Customer");

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _productService.MakePurchase(items, customer));
        }

        [Fact]
        public void MakePurchase_WithNullCustomer_ShouldThrowArgumentNullException()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 2, 10.99, 100, "Electronics", "China"),
                    1)
            };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                _productService.MakePurchase(items, null));
        }

        [Fact]
        public void CheckNewAmount_WithValidProductAndAmount_ShouldReturnTrue()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var amount = 5;
            _mockInventoryRepo.Setup(x => x.check_new_amount(productId, amount))
                .Returns(true);

            // Act
            var result = _productService.CheckNewAmount(productId, amount);

            // Assert
            Assert.True(result);
            _mockInventoryRepo.Verify(x => x.check_new_amount(productId, amount), Times.Once);
        }

        [Fact]
        public void CheckNewAmount_WithInvalidProduct_ShouldReturnFalse()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var amount = 5;
            _mockInventoryRepo.Setup(x => x.check_new_amount(productId, amount))
                .Returns(false);

            // Act
            var result = _productService.CheckNewAmount(productId, amount);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void CheckNewAmount_WithNegativeAmount_ShouldThrowArgumentException()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var amount = -1;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _productService.CheckNewAmount(productId, amount));
        }

        [Fact]
        public void CheckNewAmount_WithEmptyProductId_ShouldThrowArgumentException()
        {
            // Arrange
            var productId = Guid.Empty;
            var amount = 5;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _productService.CheckNewAmount(productId, amount));
        }
    }
}