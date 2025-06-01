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
            _receiptRepo = new ReceiptRepo("Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=postgres;Password=5432");
        }

        [Fact]
        public void Create_WithValidItemsAndCustomer_ReturnsReceipt()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 10.99, 100, "����", "������"),
                    1),
                new ReceiptLine(
                    new Product(2, 5.99, 50, "�����", "����������"),
                    2)
            };

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10.99 + (5.99 * 2), result.FinalPrice, 2); // ���������� �������� 2 ����� ����� �������
            Assert.Equal(3, result.TotalAmount);
            Assert.Equal(items, result.Products);
            Assert.True((DateTime.Now - result.Date).TotalSeconds < 1);
        }

        [Fact]
        public void Create_WithSingleItem_CorrectlyCalculatesTotal()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 2.50, 100, "����", "������"),
                    3)
            };

            // Act
            var result = new Receipt(1, items);

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
                    new Product(1, 0.00, 100, "����", "������"),
                    1)
            };

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.Equal(0.00, result.FinalPrice);
            Assert.Equal(1, result.TotalAmount);
        }

        [Fact]
        public void Create_WithNullItems_ThrowsArgumentNullException()
        {
            // Arrange

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new Receipt(1, null));
            Assert.Contains("items", ex.Message);
        }

        [Fact]
        public void Create_WithEmptyItems_ThrowsArgumentException()
        {
            // Arrange
            var items = new List<ReceiptLine>();

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => new Receipt(1, items));
            Assert.Contains("items", ex.Message);
        }

        [Fact]
        public void Create_WithMultipleItems_CorrectlyCalculatesTotals()
        {
            // Arrange
            var items = new List<ReceiptLine>
            {
                new ReceiptLine(
                    new Product(1, 1.99, 100, "����", "������"),
                    5),
                new ReceiptLine(
                    new Product(2, 4.50, 50, "�����", "����������"),
                    2),
                new ReceiptLine(
                    new Product(3, 0.99, 200, "�������", "�������"),
                    10)
            };

            // Act
            var result = new Receipt(1, items);

            // Assert
            Assert.Equal((1.99 * 5) + (4.50 * 2) + (0.99 * 10), result.FinalPrice, 2);
            Assert.Equal(17, result.TotalAmount);
        }
    }
}