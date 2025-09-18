using Domain;
using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using System.IO;
using System.Linq;
using Serilog;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace ForecastAnalysis
{
    public class ForecastServiceAdapter : IForecastServiceAdapter
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;
        public ForecastServiceAdapter(string pythonPath, string scriptPath)
        {
            _pythonPath = pythonPath;
            _scriptPath = scriptPath;
        }
        public ForecastOfOrders Create()
        {
            Console.OutputEncoding = Encoding.UTF8;

            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(_scriptPath),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                Environment = { ["PYTHONIOENCODING"] = "utf-8" }
            };

            string jsonResult;
            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();

                using (var reader = new StreamReader(process.StandardOutput.BaseStream, Encoding.UTF8))
                {
                    jsonResult = reader.ReadToEnd();
                }

                string errors;
                using (var errorReader = new StreamReader(process.StandardError.BaseStream, Encoding.UTF8))
                {
                    errors = errorReader.ReadToEnd();
                }

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new ApplicationException($"Ошибка выполнения Python-скрипта: {errors}");
                }
            }

            try
            {
                // Динамическая десериализация JSON
                dynamic pythonData = JsonConvert.DeserializeObject(jsonResult);
                if (pythonData == null)
                {
                    throw new ApplicationException("Не удалось десериализовать выходные данные скрипта");
                }

                // Преобразование продуктов
                var productLines = new List<ProductLine>();
                foreach (var productItem in pythonData.products)
                {
                    if ((int)productItem.amount > 0)
                    {
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

                // Преобразование ежедневного прогноза
                var dailyForecasts = new List<DailyForecast>();
                foreach (var dailyItem in pythonData.daily_forecast)
                {
                    dailyForecasts.Add(new DailyForecast
                    {
                        date = (string)dailyItem.date,
                        day_of_week = (int)dailyItem.day_of_week,
                        orders = (int)dailyItem.orders
                    });
                }

                // Создание итогового объекта ForecastOfOrders
                var resultForecast = new ForecastOfOrders(
                    amount_of_orders: (int)pythonData.total_orders,
                    products: productLines
                )
                {
                    DailyForecast = dailyForecasts
                };

                return resultForecast;
            }
            catch (Exception ex) when (ex is JsonException || ex is ArgumentException)
            {
                throw new ApplicationException($"Ошибка при обработке данных: {ex.Message}\nДанные: {jsonResult}");
            }
        }
    }
}