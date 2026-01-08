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
            /*
             Формат файла:
                id_поставки
                количество позиций в поставке
                поставщик
                ответственный
                id_номенклатуры|production_date|expiration_date|cost_price|amount
             */
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using (var reader = _streamReaderFactory(stream))
            {
                int batchId = ReadInt(reader, "Batch ID");
                int productsCount = ReadInt(reader, "Products count");
                int supplier = ReadInt(reader, "Supplier");
                int responsible = ReadInt(reader, "Responsible");

                var products = ReadProducts(reader, productsCount);
                return new ProductBatch(batchId, supplier, responsible, products);
            }
        }

        internal ProductInfo ParseProductLine(string line)
        {
            // id_номенклатуры|production_date|expiration_date|cost_price|amount
            if (string.IsNullOrEmpty(line))
                throw new InvalidDataException("Product line is empty");

            var parts = line.Split(';');
            if (parts.Length != 5)
                throw new InvalidDataException("Product line must contain exactly 7 parts separated by ';'");

            try
            {
                var product = new ProductInfo(
                    int.Parse(parts[0]),
                    DateTime.Parse(parts[1]),
                    DateTime.Parse(parts[2]),
                    int.Parse(parts[4]),
                    double.Parse(parts[3], System.Globalization.NumberStyles.Any,
                                           System.Globalization.CultureInfo.InvariantCulture)
                );

                return product;
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

        private static int ReadInt(StreamReader reader, string label)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrEmpty(line))
            {
                throw new InvalidDataException($"{label} line is missing");
            }

            if (!int.TryParse(line, out var value))
            {
                throw new InvalidDataException($"Invalid {label} format");
            }

            return value;
        }

        private static List<ProductInfo> ReadProducts(StreamReader reader, int productsCount)
        {
            var products = new List<ProductInfo>();
            for (int i = 0; i < productsCount; i++)
            {
                var productLine = reader.ReadLine();
                if (string.IsNullOrEmpty(productLine))
                {
                    throw new InvalidDataException($"Missing product line {i + 1}");
                }

                try
                {
                    products.Add(ParseProductLineStatic(productLine));
                }
                catch (Exception ex) when (
                    ex is FormatException ||
                    ex is ArgumentNullException ||
                    ex is InvalidDataException)
                {
                    throw new InvalidDataException($"Invalid product data at line {i + 4}: {ex.Message}", ex);
                }
            }

            return products;
        }

        private static ProductInfo ParseProductLineStatic(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                throw new InvalidDataException("Product line is empty");
            }

            var parts = line.Split(';');
            if (parts.Length != 5)
            {
                throw new InvalidDataException("Product line must contain exactly 7 parts separated by ';'");
            }

            try
            {
                return new ProductInfo(
                    int.Parse(parts[0]),
                    DateTime.Parse(parts[1]),
                    DateTime.Parse(parts[2]),
                    int.Parse(parts[4]),
                    double.Parse(parts[3], System.Globalization.NumberStyles.Any,
                                           System.Globalization.CultureInfo.InvariantCulture)
                );
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
