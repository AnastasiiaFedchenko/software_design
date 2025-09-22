using Moq;
using Xunit;
using Allure.Xunit.Attributes;
using Allure.Xunit;
using System;
using System.IO;
using ProductBatchReading;
using Domain;
using Domain.OutputPorts;
using Allure.Net.Commons;

namespace ProductBatchReader_Tests
{
    // пример лондоского теста 
    // не поняла почему это лондонский тут же нет моков
    // это же классический
    [AllureSuite("ProductBatchReader Layer")]
    [AllureFeature("Product Management")]
    [AllureSubSuite("Product Entity")]
    public class ProductBatchReaderTests
    {
        [Fact]
        [AllureStory("ProductBatchReader")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("ProductBatchReader Team")]
        public void Create_WithValidData_ReturnsProductBatch()
        {
            // Arrange
            var testData = string.Join(Environment.NewLine,
                "501",
                "11",
                "138",
                "32",
                "1000; 2025 - 05 - 18; 2025 - 05 - 25; 936.39; 20",
                "658; 2025 - 05 - 18; 2025 - 06 - 01; 997.0; 53",
                "66; 2025 - 05 - 18; 2025 - 11 - 14; 631.74; 84",
                "884; 2025 - 05 - 18; 2025 - 11 - 14; 2808.1; 49",
                "204; 2025 - 05 - 18; 2025 - 11 - 14; 4310.21; 88",
                "368; 2025 - 05 - 18; 2025 - 06 - 01; 4886.39; 19",
                "638; 2025 - 05 - 18; 2025 - 11 - 14; 4177.49; 15",
                "173; 2025 - 05 - 18; 2026 - 05 - 18; 4881.49; 78",
                "833; 2025 - 05 - 18; 2025 - 05 - 25; 146.15; 24",
                "665; 2025 - 05 - 18; 2025 - 08 - 16; 1151.64; 93",
                "451; 2025 - 05 - 18; 2025 - 11 - 14; 131.41; 81");

            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream);
            writer.Write(testData);
            writer.Flush();
            memoryStream.Position = 0;

            var reader = new ProductBatchReader();

            // Act
            var result = reader.create(memoryStream);

            // Assert
            Assert.NotNull(result.ProductsInfo);
            Assert.Equal(11, result.ProductsInfo.Count);
            Assert.Equal(138, result.Supplier);
            Assert.Equal(32, result.Responsible);
        }

        [Fact]
        [AllureStory("ProductBatchReader")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("ProductBatchReader Team")]
        public void Create_WithEmptyFile_ThrowsException()
        {
            // Arrange
            using var emptyStream = new MemoryStream();
            var reader = new ProductBatchReader();

            // Act & Assert
            Assert.Throws<InvalidDataException>(() => reader.create(emptyStream));
        }

        [Fact]
        [AllureStory("ProductBatchReader")]
        [AllureTag("Unit")]
        [AllureSeverity(SeverityLevel.critical)]
        [AllureOwner("ProductBatchReader Team")]
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