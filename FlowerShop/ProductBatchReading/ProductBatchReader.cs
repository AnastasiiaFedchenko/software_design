using System;
using System.IO;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;

namespace ProductBatchReading
{
    public class ProductBatchReader : IProductBatchReader
    {
        private readonly Func<Stream, StreamReader> _streamReaderFactory;

        public ProductBatchReader() : this(null) { }

        public ProductBatchReader(Func<Stream, StreamReader> streamReaderFactory)
        {
            _streamReaderFactory = streamReaderFactory ?? (s => new StreamReader(s));
        }

        public ProductBatch create(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (var reader = _streamReaderFactory(stream))
            {
                // Чтение и валидация заголовка
                var batchIdLine = reader.ReadLine();
                if (string.IsNullOrEmpty(batchIdLine))
                    throw new InvalidDataException("Batch ID line is missing");

                var totalAmountLine = reader.ReadLine();
                if (string.IsNullOrEmpty(totalAmountLine))
                    throw new InvalidDataException("Total amount line is missing");

                var productsCountLine = reader.ReadLine();
                if (string.IsNullOrEmpty(productsCountLine))
                    throw new InvalidDataException("Products count line is missing");

                // Парсинг с обработкой ошибок
                if (!int.TryParse(batchIdLine, out var batchId))
                    throw new InvalidDataException("Invalid Batch ID format");

                if (!int.TryParse(totalAmountLine, out var totalAmount))
                    throw new InvalidDataException("Invalid Total Amount format");

                if (!int.TryParse(productsCountLine, out var productsCount))
                    throw new InvalidDataException("Invalid Products Count format");

                // Чтение продуктов
                var products = new List<ProductLine>();
                for (int i = 0; i < productsCount; i++)
                {
                    var productLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(productLine))
                        throw new InvalidDataException($"Missing product line {i + 1}");

                    try
                    {
                        products.Add(ParseProductLine(productLine));
                    }
                    catch (Exception ex) when (
                        ex is FormatException ||
                        ex is ArgumentNullException ||
                        ex is InvalidDataException)
                    {
                        throw new InvalidDataException($"Invalid product data at line {i + 4}: {ex.Message}", ex);
                    }
                }

                return new ProductBatch(batchId, totalAmount, products);
            }
        }

        internal ProductLine ParseProductLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                throw new InvalidDataException("Product line is empty");

            var parts = line.Split('|');
            if (parts.Length != 7)
                throw new InvalidDataException("Product line must contain exactly 7 parts separated by '|'");

            try
            {
                var product = new Product(
                    int.Parse(parts[0]),
                    int.Parse(parts[1]),
                    double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(parts[3]),
                    parts[4],
                    parts[5]
                );

                return new ProductLine(product, int.Parse(parts[6]));
            }
            catch (FormatException ex)
            {
                throw new InvalidDataException($"Invalid number format in product line: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Error parsing product line: {ex.Message}", ex);
            }
        }
    }
}