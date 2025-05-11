using Moq;
using Xunit;
using System;
using System.IO;
using ProductBatchReading;
using Domain;
using Domain.OutputPorts;

namespace ProductBatchReader_Tests
{
    public class ProductBatchReaderTests
    {
        [Fact]
        public void Create_WithValidData_ReturnsProductBatch()
        {
            // Arrange
            var testData = string.Join(Environment.NewLine,
                "3fa85f64-5717-4562-b3fc-2c963f66afa6",
                "150",
                "2",
                "3fa85f64-5717-4562-b3fc-2c963f66afa6|50|12,99|100|Electronics|China|30",
                "3fa85f64-5717-4562-b3fc-2c963f66afa7|30|8,50|75|Appliances|Germany|40");

            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream);
            writer.Write(testData);
            writer.Flush();
            memoryStream.Position = 0;

            var reader = new ProductBatchReader();

            // Act
            var result = reader.create(memoryStream);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Products.Count);
            Assert.Equal(150, result.TotalAmount);
        }

        [Fact]
        public void Create_WithEmptyFile_ThrowsException()
        {
            // Arrange
            using var emptyStream = new MemoryStream();
            var reader = new ProductBatchReader();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => reader.create(emptyStream));
        }

        [Fact]
        public void Create_WithInvalidData_ThrowsException()
        {
            // Arrange
            var invalidData = string.Join(Environment.NewLine,
                "invalid-guid",
                "150",
                "1",
                "product-data");

            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream);
            writer.Write(invalidData);
            writer.Flush();
            memoryStream.Position = 0;

            var reader = new ProductBatchReader();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => reader.create(memoryStream));
        }
    }
}