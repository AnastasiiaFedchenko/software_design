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
                var batchIdLine = reader.ReadLine();
                if (string.IsNullOrEmpty(batchIdLine))
                    throw new InvalidDataException("Batch ID line is missing");

                var productsCountLine = reader.ReadLine();
                if (string.IsNullOrEmpty(productsCountLine))
                    throw new InvalidDataException("Products count line is missing");
                
                var supplierLine = reader.ReadLine();
                if (string.IsNullOrEmpty(supplierLine))
                    throw new InvalidDataException("Supplier line is missing");

                var responsibleLine = reader.ReadLine();
                if (string.IsNullOrEmpty(responsibleLine))
                    throw new InvalidDataException("Responsible line is missing");


                if (!int.TryParse(batchIdLine, out var batchId))
                    throw new InvalidDataException("Invalid Batch ID format");

                if (!int.TryParse(productsCountLine, out var productsCount))
                    throw new InvalidDataException("Invalid Products Count format");
                
                if (!int.TryParse(supplierLine, out var supplier))
                    throw new InvalidDataException("Invalid Batch ID format");

                if (!int.TryParse(responsibleLine, out var responsible))
                    throw new InvalidDataException("Invalid Batch ID format");

                // Чтение продуктов
                var products = new List<ProductInfo>();
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
    }
}