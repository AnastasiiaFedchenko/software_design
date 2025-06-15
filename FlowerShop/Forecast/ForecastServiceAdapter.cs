using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ForecastAnalysis
{
    public class ForecastServiceAdapter : IForecastServiceAdapter
    {
        private readonly ILogger<ForecastServiceAdapter> _logger;
        private const string PythonPath = "D:/bmstu/PPO/software_design/pythonProject/.venv/Scripts/python.exe";
        private const string ScriptPath = "D:/bmstu/PPO/software_design/pythonProject/ForecastOfOrders.py";

        public ForecastServiceAdapter(ILogger<ForecastServiceAdapter> logger)
        {
            _logger = logger;
            _logger.LogDebug("Инициализация ForecastServiceAdapter");
        }

        public ForecastOfOrders Create()
        {
            _logger.LogInformation("Запуск прогнозирования заказов через Python-скрипт");

            try
            {
                Console.OutputEncoding = Encoding.UTF8;

                var processInfo = new ProcessStartInfo
                {
                    FileName = PythonPath,
                    Arguments = $"\"{ScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(ScriptPath),
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    Environment = { ["PYTHONIOENCODING"] = "utf-8" }
                };

                _logger.LogDebug("Настройки запуска Python: {PythonPath}, скрипт: {ScriptPath}",
                    PythonPath, ScriptPath);

                string jsonResult;
                using (var process = new Process { StartInfo = processInfo })
                {
                    _logger.LogDebug("Запуск Python-процесса");
                    process.Start();

                    using (var reader = new StreamReader(process.StandardOutput.BaseStream, Encoding.UTF8))
                    {
                        jsonResult = reader.ReadToEnd();
                        _logger.LogTrace("Получены выходные данные Python: {JsonResult}", jsonResult);
                    }

                    string errors;
                    using (var errorReader = new StreamReader(process.StandardError.BaseStream, Encoding.UTF8))
                    {
                        errors = errorReader.ReadToEnd();
                        if (!string.IsNullOrEmpty(errors))
                        {
                            _logger.LogError("Ошибки Python-скрипта: {PythonErrors}", errors);
                        }
                    }

                    process.WaitForExit();
                    _logger.LogDebug("Python-процесс завершился с кодом: {ExitCode}", process.ExitCode);

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Ошибка выполнения Python-скрипта. Код: {ExitCode}, Ошибки: {Errors}",
                            process.ExitCode, errors);
                        throw new ApplicationException($"Ошибка выполнения Python-скрипта: {errors}");
                    }
                }

                try
                {
                    _logger.LogDebug("Десериализация JSON-результата");
                    dynamic pythonData = JsonConvert.DeserializeObject(jsonResult);
                    if (pythonData == null)
                    {
                        _logger.LogError("Не удалось десериализовать выходные данные скрипта");
                        throw new ApplicationException("Не удалось десериализовать выходные данные скрипта");
                    }

                    // Преобразование продуктов
                    var productLines = ProcessProducts(pythonData);

                    // Преобразование ежедневного прогноза
                    var dailyForecasts = ProcessDailyForecast(pythonData);

                    // Создание итогового объекта ForecastOfOrders
                    var resultForecast = new ForecastOfOrders(
                        amount_of_orders: (int)pythonData.total_orders,
                        products: productLines
                    )
                    {
                        DailyForecast = dailyForecasts
                    };

                    _logger.LogInformation("Прогноз успешно создан. Всего заказов: {TotalOrders}",
                        resultForecast.AmountOfOrders);

                    return resultForecast;
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Ошибка десериализации JSON. Данные: {JsonData}", jsonResult);
                    throw new ApplicationException($"Ошибка при обработке данных: {ex.Message}");
                }
                catch (ArgumentException ex)
                {
                    _logger.LogError(ex, "Ошибка формата данных. Данные: {JsonData}", jsonResult);
                    throw new ApplicationException($"Ошибка формата данных: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Неожиданная ошибка при выполнении прогнозирования");
                throw;
            }
        }

        private List<ProductLine> ProcessProducts(dynamic pythonData)
        {
            var productLines = new List<ProductLine>();
            int productCount = 0;

            try
            {
                foreach (var productItem in pythonData.products)
                {
                    if ((int)productItem.amount > 0)
                    {
                        productCount++;
                        var domainProduct = new Product(
                            id_nomenclature: (int)productItem.product_id,
                            price: 0,
                            amount_in_stock: (int)productItem.current_stock,
                            type: (string)productItem.product_name,
                            country: "не указано"
                        );

                        productLines.Add(new ProductLine(
                            product: domainProduct,
                            amount: (int)productItem.amount,
                            amount_in_stock: (int)productItem.current_stock
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки списка продуктов. Обработано: {ProcessedCount}", productCount);
                throw;
            }

            return productLines;
        }

        private List<DailyForecast> ProcessDailyForecast(dynamic pythonData)
        {
            var dailyForecasts = new List<DailyForecast>();
            int dayCount = 0;

            try
            {
                foreach (var dailyItem in pythonData.daily_forecast)
                {
                    dayCount++;
                    dailyForecasts.Add(new DailyForecast
                    {
                        date = (string)dailyItem.date,
                        day_of_week = (int)dailyItem.day_of_week,
                        orders = (int)dailyItem.orders
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка обработки дневного прогноза. Обработано: {ProcessedCount}", dayCount);
                throw;
            }

            return dailyForecasts;
        }
    }
}