using System;
using System.IO;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;

namespace ProductBatchReading
{
    public class ProductBatchReader : IProductBatchReader
    {
        private readonly Func<Stream, StreamReader> _streamReaderFactory;
        private readonly ILogger<ProductBatchReader> _logger;

        public ProductBatchReader(ILogger<ProductBatchReader> logger) : this(null, logger) { }

        public ProductBatchReader(Func<Stream, StreamReader> streamReaderFactory, ILogger<ProductBatchReader> logger)
        {
            _streamReaderFactory = streamReaderFactory ?? (s => new StreamReader(s));
            _logger = logger;
        }

        public ProductBatch create(Stream stream)
        {
            _logger.LogInformation("Начало обработки файла с партией товаров");

            if (stream == null)
            {
                _logger.LogError("Передан null Stream");
                throw new ArgumentNullException(nameof(stream));
            }

            try
            {
                using (var reader = _streamReaderFactory(stream))
                {
                    var batchIdLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(batchIdLine))
                    {
                        _logger.LogError("Отсутствует строка с ID партии");
                        throw new InvalidDataException("Batch ID line is missing");
                    }

                    var productsCountLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(productsCountLine))
                    {
                        _logger.LogError("Отсутствует строка с количеством товаров");
                        throw new InvalidDataException("Products count line is missing");
                    }

                    var supplierLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(supplierLine))
                    {
                        _logger.LogError("Отсутствует строка с поставщиком");
                        throw new InvalidDataException("Supplier line is missing");
                    }

                    var responsibleLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(responsibleLine))
                    {
                        _logger.LogError("Отсутствует строка с ответственным");
                        throw new InvalidDataException("Responsible line is missing");
                    }

                    if (!int.TryParse(batchIdLine, out var batchId))
                    {
                        _logger.LogError("Неверный формат ID партии: {BatchIdLine}", batchIdLine);
                        throw new InvalidDataException("Invalid Batch ID format");
                    }

                    if (!int.TryParse(productsCountLine, out var productsCount))
                    {
                        _logger.LogError("Неверный формат количества товаров: {ProductsCountLine}", productsCountLine);
                        throw new InvalidDataException("Invalid Products Count format");
                    }

                    if (!int.TryParse(supplierLine, out var supplier))
                    {
                        _logger.LogError("Неверный формат поставщика: {SupplierLine}", supplierLine);
                        throw new InvalidDataException("Invalid Supplier format");
                    }

                    if (!int.TryParse(responsibleLine, out var responsible))
                    {
                        _logger.LogError("Неверный формат ответственного: {ResponsibleLine}", responsibleLine);
                        throw new InvalidDataException("Invalid Responsible format");
                    }

                    _logger.LogInformation("Обработка партии ID: {BatchId}, поставщик: {Supplier}, товаров: {ProductsCount}",
                        batchId, supplier, productsCount);

                    // Чтение продуктов
                    var products = new List<ProductInfo>();
                    for (int i = 0; i < productsCount; i++)
                    {
                        var productLine = reader.ReadLine();
                        if (string.IsNullOrEmpty(productLine))
                        {
                            _logger.LogError("Отсутствует строка с товаром {LineNumber}", i + 1);
                            throw new InvalidDataException($"Missing product line {i + 1}");
                        }

                        try
                        {
                            var product = ParseProductLine(productLine);
                            products.Add(product);
                            _logger.LogInformation("Добавлен товар: {ProductInfo}", product);
                        }
                        catch (Exception ex) when (
                            ex is FormatException ||
                            ex is ArgumentNullException ||
                            ex is InvalidDataException)
                        {
                            _logger.LogError(ex, "Ошибка обработки строки товара {LineNumber}: {ProductLine}",
                                i + 4, productLine);
                            throw new InvalidDataException($"Invalid product data at line {i + 4}: {ex.Message}", ex);
                        }
                    }

                    _logger.LogInformation("Успешно обработано {ProductsCount} товаров", products.Count);
                    return new ProductBatch(batchId, supplier, responsible, products);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки файла с партией товаров");
                throw;
            }
        }

        internal ProductInfo ParseProductLine(string line)
        {
            _logger.LogTrace("Парсинг строки товара: {ProductLine}", line);

            if (string.IsNullOrEmpty(line))
            {
                _logger.LogError("Пустая строка товара");
                throw new InvalidDataException("Product line is empty");
            }

            var parts = line.Split(';');
            if (parts.Length != 5)
            {
                _logger.LogError("Неверное количество частей в строке товара: {PartsCount} (ожидалось 5)", parts.Length);
                throw new InvalidDataException("Product line must contain exactly 5 parts separated by ';'");
            }

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

                _logger.LogInformation("Успешно распарсен товар: ID {ProductId}, срок годности до {ExpirationDate}",
                    product.IdNomenclature, product.ExpirationDate);

                return product;
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Ошибка формата данных в строке товара");
                throw new InvalidDataException($"Invalid number format in product line: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка парсинга строки товара");
                throw new InvalidDataException($"Error parsing product line: {ex.Message}", ex);
            }
        }
    }
}