using Moq;
using System;
using System.Data;
using Xunit;
using Allure.Xunit.Attributes;
using Allure.Xunit;
using Domain;
using ReceiptOfSale;
using ConnectionToDB;
using System.Collections.Generic;
using Allure.Net.Commons;

namespace ReceiptOfSale.Tests
{
    [AllureSuite("ReceiptRepo Layer")]
    [AllureFeature("Product Management")]
    [AllureSubSuite("Product Entity")]
    public class ReceiptRepoTests
    {
        // ¬спомогательный класс дл€ хранени€ состо€ни€ in-memory "базы данных"
        private class MockDatabaseState
        {
            public int NextOrderId { get; set; } = 1;
            public int NextReceiptNumber { get; set; } = 100;
            public Dictionary<int, int> ProductStock { get; } = new Dictionary<int, int>();
            public List<Order> Orders { get; } = new List<Order>();
            public List<Sale> Sales { get; } = new List<Sale>();
            public List<OrderProduct> OrderProducts { get; } = new List<OrderProduct>();
        }

        [Fact]
        [AllureStory("ReceiptRepo")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("ReceiptRepo Team")]
        public void LoadReceiptItemsSale_UpdateAmount_SuccessfulTransaction()
        {
            // Arrange
            var mockDatabaseState = new MockDatabaseState();

            // настройка начального состо€ни€ бд
            mockDatabaseState.ProductStock[1] = 50; // “овар с ID 1, количество 50
            mockDatabaseState.ProductStock[2] = 30; // “овар с ID 2, количество 30

            var mockConnectionFactory = new Mock<IDbConnectionFactory>();
            var mockConnection = new Mock<IDbConnection>();
            var mockTransaction = new Mock<IDbTransaction>();
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var mockParameters = new Mock<IDataParameterCollection>();
            
            // настройка моков
            mockConnectionFactory
                .Setup(f => f.CreateOpenConnection())
                .Returns(mockConnection.Object);

            mockConnection.Setup(c => c.BeginTransaction()).Returns(mockTransaction.Object);
            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);

            mockCommand.Setup(c => c.CreateParameter()).Returns(() =>
            {
                var paramMock = new Mock<IDbDataParameter>();
                return paramMock.Object;
            });

            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupGet(c => c.Parameters).Returns(mockParameters.Object);

            // настройка ответов на различные sql запросы
            mockCommand.Setup(c => c.ExecuteScalar())
                .Returns(() =>
                {
                    var cmdText = mockCommand.Object.CommandText;

                    if (cmdText.Contains("INSERT INTO \"order\""))
                    {
                        var newId = mockDatabaseState.NextOrderId++;
                        mockDatabaseState.Orders.Add(new Order { Id = newId });
                        return newId;
                    }

                    if (cmdText.Contains("SELECT COALESCE(MAX(receipt_number), 0) + 1 FROM sales"))
                    {
                        return mockDatabaseState.NextReceiptNumber++;
                    }

                    return null;
                });

            mockCommand.Setup(c => c.ExecuteReader())
                .Returns(() =>
                {
                    var cmdText = mockCommand.Object.CommandText;

                    if (cmdText.Contains("SELECT pis.id, p.id as price_id"))
                    {
                        // поиск товара на складе
                        mockReader.SetupSequence(r => r.Read())
                            .Returns(true)
                            .Returns(false);

                        mockReader.Setup(r => r.GetInt32(0)).Returns(101); // id продукта на складе
                        mockReader.Setup(r => r.GetInt32(1)).Returns(201); // id цены
                    }
                    else if (cmdText.Contains("SELECT pis.id, pis.amount"))
                    {
                        // поиск партии товара
                        mockReader.SetupSequence(r => r.Read())
                            .Returns(true) 
                            .Returns(false);

                        mockReader.Setup(r => r.GetInt32(0)).Returns(101); // id продукта на складе
                        mockReader.Setup(r => r.GetInt32(1)).Returns(10);  // доступное количество
                    }

                    return mockReader.Object;
                });

            mockCommand.Setup(c => c.ExecuteNonQuery())
                .Returns(() =>
                {
                    var cmdText = mockCommand.Object.CommandText;

                    if (cmdText.Contains("UPDATE product_in_stock"))
                    {
                        return 1;
                    }
                    else if (cmdText.Contains("INSERT INTO order_product_in_stock"))
                    {
                        return 1;
                    }
                    else if (cmdText.Contains("INSERT INTO sales"))
                    {
                        return 1;
                    }

                    return 0;
                });

            var repo = new ReceiptRepo(mockConnectionFactory.Object);

            var list_of_products = new List<ReceiptLine>();
            list_of_products.Add(new ReceiptLine(new Product(1, 100, 100, "first", "country1"), 5));
            list_of_products.Add(new ReceiptLine(new Product(2, 200, 200, "second", "country2"), 3));
            var receipt = new Receipt(10, list_of_products);

            // Act
            var result = repo.LoadReceiptItemsSale_UpdateAmount(ref receipt);

            // Assert
            Assert.True(result);
            Assert.True(receipt.Id > 0);

            mockConnection.Verify(c => c.BeginTransaction(), Times.Once);
            mockTransaction.Verify(t => t.Commit(), Times.Once);
            mockTransaction.Verify(t => t.Rollback(), Times.Never);
        }

        [Fact]
        [AllureStory("ReceiptRepo")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("ReceiptRepo Team")]
        public void LoadReceiptItemsSale_UpdateAmount_NotEnoughStock_ReturnsFalse()
        {
            // Arrange
            var mockConnectionFactory = new Mock<IDbConnectionFactory>();
            var mockConnection = new Mock<IDbConnection>();
            var mockTransaction = new Mock<IDbTransaction>();
            var mockCommand = new Mock<IDbCommand>();
            var mockReader = new Mock<IDataReader>();
            var mockParameters = new Mock<IDataParameterCollection>();

            mockConnectionFactory
                .Setup(f => f.CreateOpenConnection())
                .Returns(mockConnection.Object);

            mockConnection.Setup(c => c.BeginTransaction()).Returns(mockTransaction.Object);
            mockConnection.Setup(c => c.CreateCommand()).Returns(mockCommand.Object);
            mockCommand.Setup(c => c.CreateParameter()).Returns(new Mock<IDbDataParameter>().Object);
            mockCommand.SetupProperty(c => c.CommandText);
            mockCommand.SetupGet(c => c.Parameters).Returns(mockParameters.Object);

            mockCommand.Setup(c => c.ExecuteScalar()).Returns(1);

            mockReader.SetupSequence(r => r.Read())
                .Returns(true)  
                .Returns(false);
            mockReader.Setup(r => r.GetInt32(0)).Returns(101); // id продукта на складе
            mockReader.Setup(r => r.GetInt32(1)).Returns(201); // id цены

            var emptyReaderMock = new Mock<IDataReader>();
            emptyReaderMock.Setup(r => r.Read()).Returns(false);

            mockCommand.SetupSequence(c => c.ExecuteReader())
                .Returns(mockReader.Object)  
                .Returns(emptyReaderMock.Object); 

            var repo = new ReceiptRepo(mockConnectionFactory.Object);

            var list_of_products = new List<ReceiptLine>();
            list_of_products.Add(new ReceiptLine(new Product(1, 10, 10, "first", "country1"), 100));
            var receipt = new Receipt(1, list_of_products);

            // Act
            var result = repo.LoadReceiptItemsSale_UpdateAmount(ref receipt);

            // Assert
            Assert.False(result);

            mockTransaction.Verify(t => t.Rollback(), Times.Once);
            mockTransaction.Verify(t => t.Commit(), Times.Never);
        }
    }
    internal class Order
    {
        public int Id { get; set; }
    }

    internal class Sale
    {
        public int ReceiptNumber { get; set; }
        public int OrderId { get; set; }
    }

    internal class OrderProduct
    {
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Amount { get; set; }
        public int PriceId { get; set; }
    }
}