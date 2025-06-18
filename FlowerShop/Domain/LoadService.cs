using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Domain.InputPorts;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;

namespace Domain
{
    public class LoadService : ILoadService
    {
        private readonly IProductBatchReader _productBatchReader;
        private readonly IProductBatchLoader _productBatchLoader;
        private readonly ILogger<LoadService> _logger;

        public LoadService(
            IProductBatchReader productBatchReader,
            IProductBatchLoader productBatchLoader,
            ILogger<LoadService> logger)
        {
            _productBatchReader = productBatchReader ?? throw new ArgumentNullException(nameof(productBatchReader));
            _productBatchLoader = productBatchLoader ?? throw new ArgumentNullException(nameof(productBatchLoader));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool LoadProductBatch(FileStream stream)
        {
            if (stream == null)
            {
                _logger.LogError("Попытка загрузить пустой файл");
                throw new ArgumentNullException(nameof(stream));
            }

            _logger.LogInformation("Начало загрузки партии товаров");

            try
            {
                ProductBatch batch = _productBatchReader.create(stream);
                _logger.LogInformation("Партия успешно распознана. Количество товаров: {КоличествоТоваров}",
                    batch.ProductsInfo.Count);

                if (batch.ProductsInfo.Count > 0)
                {
                    LogBatchDetails(batch);
                }
                else
                {
                    _logger.LogWarning("Получена пустая партия товаров");
                }

                bool loadResult = _productBatchLoader.Load(batch);

                if (loadResult)
                {
                    _logger.LogInformation("Партия товаров успешно загружена в систему");
                }
                else
                {
                    _logger.LogError("Ошибка загрузки партии товаров");
                }

                return loadResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке партии товаров");
                throw;
            }
        }

        private void LogBatchDetails(ProductBatch batch)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════╦═════════════════════════╦════════════════╦════════════╦══════════════╦════════════════╗");
            sb.AppendLine("║ Номенклатура   ║ Даты (произв./годен)    ║ Количество     ║ Цена       ║ Место хран.  ║ Срок годности  ║");
            sb.AppendLine("╠════════════════╬═════════════════════════╬════════════════╬════════════╬══════════════╬════════════════╣");

            foreach (var p in batch.ProductsInfo)
            {
                sb.AppendLine("╠════════════════╬═════════════════════════╬════════════════╬════════════╬══════════════╬════════════════╣");
                sb.AppendLine($"║ {p.IdNomenclature.ToString().PadRight(14)} ║ {p.ProductionDate:dd.MM.yyyy} / {p.ExpirationDate:dd.MM.yyyy} ║ {p.Amount.ToString().PadRight(14)} ║ {p.CostPrice:F2} руб ║ {p.StoragePlace.ToString().PadRight(12)} ║ {(p.ExpirationDate - DateTime.Today).Days} дней    ║");

                // Логируем каждый продукт отдельно
                _logger.LogInformation("Товар: ID={id}, Количество={amount}, Цена={price}, Место={storage}, СрокГодности={expiration_date} дней",
                    p.IdNomenclature, p.Amount, p.CostPrice, p.StoragePlace, (p.ExpirationDate - DateTime.Today).Days);
            }

            sb.AppendLine("╚════════════════╩═════════════════════════╩════════════════╩════════════╩══════════════╩════════════════╝");

            // Выводим в консоль
            Console.WriteLine(sb.ToString());
        }
    }
}