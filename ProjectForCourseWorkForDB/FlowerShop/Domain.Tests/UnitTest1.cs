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
        public void CheckNewAmount_WithEmptyProductId_ShouldThrowArgumentException()
        {
            // Arrange
            int productId = 0;
            var amount = 5;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                _productService.CheckNewAmount(productId, amount));
        }
    }
}