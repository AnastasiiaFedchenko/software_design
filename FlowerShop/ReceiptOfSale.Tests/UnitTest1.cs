using Moq;
using Xunit;
using Domain;
using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using ReceiptOfSale;

namespace ReceiptOfSale.Tests
{
    public class ReceiptRepoTests
    {
        private readonly ReceiptRepo _receiptRepo;

        public ReceiptRepoTests()
        {
            _receiptRepo = new ReceiptRepo();
        }

        [Fact]
        public void Create_WithValidItemsAndCustomer_ReturnsReceipt()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 2, 10.99, 100, "Electronics", "China"),
                    1),
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 1, 5.99, 50, "Groceries", "Local"),
                    2)
            };

            var customer = new User(Guid.NewGuid(), "Customer");

            // Act
            var result = new Receipt(customer, items);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(customer, result.Customer);
            Assert.Equal(10.99 + (5.99 * 2), result.FinalPrice, 2); // Используем точность 2 знака после запятой
            Assert.Equal(3, result.TotalAmount);
            Assert.Equal(items, result.Products);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.True((DateTime.Now - result.Date).TotalSeconds < 1);
        }

        [Fact]
        public void Create_WithSingleItem_CorrectlyCalculatesTotal()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 5, 2.50, 100, "Food", "Local"),
                    3)
            };

            var customer = new User(Guid.NewGuid(), "Customer");

            // Act
            var result = new Receipt(customer, items);

            // Assert
            Assert.Equal(7.50, result.FinalPrice);
            Assert.Equal(3, result.TotalAmount);
        }

        [Fact]
        public void Create_WithZeroPriceItem_IncludesInTotal()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 5, 0.00, 100, "Free Item", "Local"),
                    1)
            };

            var customer = new User(Guid.NewGuid(), "Customer");

            // Act
            var result = new Receipt(customer, items);

            // Assert
            Assert.Equal(0.00, result.FinalPrice);
            Assert.Equal(1, result.TotalAmount);
        }

        [Fact]
        public void Create_WithNullItems_ThrowsArgumentNullException()
        {
            // Arrange
            var customer = new User(Guid.NewGuid(), "Customer");

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new Receipt(customer, null));
            Assert.Contains("items", ex.Message);
        }

        [Fact]
        public void Create_WithEmptyItems_ThrowsArgumentException()
        {
            // Arrange
            var items = new List<ReceiptLine>();
            var customer = new User(Guid.NewGuid(), "Customer");

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new Receipt(customer, items));
            Assert.Contains("items", ex.Message);
        }

        [Fact]
        public void Create_WithNullCustomer_ThrowsArgumentNullException()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 2, 10.99, 100, "Electronics", "China"),
                    1)
            };

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new Receipt(null, items));
            Assert.Contains("customer", ex.Message);
        }

        [Fact]
        public void Create_WithMultipleItems_CorrectlyCalculatesTotals()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 10, 1.99, 100, "Snacks", "Local"),
                    5),
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 5, 4.50, 50, "Drinks", "Local"),
                    2),
                new ReceiptLine(
                    new Product(Guid.NewGuid(), 20, 0.99, 200, "Candy", "Local"),
                    10)
            };

            var customer = new User(Guid.NewGuid(), "Customer");

            // Act
            var result = new Receipt(customer, items);

            // Assert
            Assert.Equal((1.99 * 5) + (4.50 * 2) + (0.99 * 10), result.FinalPrice, 2);
            Assert.Equal(17, result.TotalAmount);
        }
    }
}