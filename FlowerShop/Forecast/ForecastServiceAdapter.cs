using Domain;
using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Text;
using System.IO;

namespace ForecastAnalysis
{
    public class ForecastServiceAdapter : IForecastServiceAdapter
    {
        public ForecastOfOrders Create()
        {
            const string pythonPath = "D:/bmstu/PPO/software_design/pythonProject/.venv/Scripts/python.exe";
            const string scriptPath = "D:/bmstu/PPO/software_design/pythonProject/ForecastOfOrders.py";

            Console.OutputEncoding = Encoding.UTF8;

            var processInfo = new ProcessStartInfo
            {
                FileName = pythonPath,
                Arguments = $"\"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = System.IO.Path.GetDirectoryName(scriptPath),
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

            var settings = new JsonSerializerSettings
            {
                StringEscapeHandling = StringEscapeHandling.EscapeNonAscii,
                Culture = System.Globalization.CultureInfo.InvariantCulture
            };

            try
            {
                dynamic pythonData = JsonConvert.DeserializeObject(jsonResult, settings);
                if (pythonData == null)
                {
                    throw new ApplicationException("Не удалось десериализовать выходные данные скрипта");
                }

                // Преобразование продуктов
                var productLines = new List<ProductLine>();
                foreach (var product in pythonData.products)
                {
                    var domainProduct = new Product(
                        id_nomenclature: (int)product.product_id,
                        price: 0,
                        amount_in_stock: 0,
                        type: (string)product.product_name,
                        country: "не указано"
                    );
                    productLines.Add(new ProductLine(domainProduct, (int)product.amount));
                }

                // Преобразование ежедневного прогноза
                var dailyForecasts = new List<DailyForecast>();
                foreach (var forecast in pythonData.daily_forecast)
                {
                    dailyForecasts.Add(new DailyForecast
                    {
                        date = (string)forecast.date,
                        day_of_week = (int)forecast.day_of_week,
                        orders = (int)forecast.orders
                    });
                }

                return new ForecastOfOrders(
                    amount_of_orders: (int)pythonData.total_orders,
                    amount_of_products: (int)pythonData.total_products,
                    products: productLines
                )
                {
                    DailyForecast = dailyForecasts
                };
            }
            catch (JsonException ex)
            {
                throw new ApplicationException($"Ошибка при обработке JSON: {ex.Message}");
            }
        }
    }
}