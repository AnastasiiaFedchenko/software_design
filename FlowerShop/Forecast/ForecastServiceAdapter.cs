using Domain.OutputPorts;
using Domain;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace ForecastAnalysis
{
    public class ForecastServiceAdapter : IForecastServiceAdapter
    {
        private readonly string _pythonPath;
        private readonly string _scriptPath;
        private readonly IProcessRunner _processRunner;
        private readonly IJsonParser _jsonParser;

        public ForecastServiceAdapter(string pythonPath, string scriptPath)
            : this(pythonPath, scriptPath, new ProcessRunner(), new JsonParser())
        {
        }

        // Конструктор для тестирования
        public ForecastServiceAdapter(string pythonPath, string scriptPath,
            IProcessRunner processRunner, IJsonParser jsonParser)
        {
            _pythonPath = pythonPath;
            _scriptPath = scriptPath;
            _processRunner = processRunner;
            _jsonParser = jsonParser;
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
                WorkingDirectory = Path.GetDirectoryName(_scriptPath),
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                Environment = { ["PYTHONIOENCODING"] = "utf-8" }
            };

            var (jsonResult, errors, exitCode) = _processRunner.RunProcess(processInfo);

            if (exitCode != 0)
            {
                throw new ApplicationException($"Ошибка выполнения Python-скрипта: {errors}");
            }

            return ParseForecastData(jsonResult);
        }

        internal ForecastOfOrders ParseForecastData(string jsonResult)
        {
            try
            {
                dynamic pythonData = _jsonParser.ParseJson(jsonResult);
                if (pythonData == null)
                {
                    throw new ApplicationException("Не удалось десериализовать выходные данные скрипта");
                }

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

    public interface IProcessRunner
    {
        (string output, string errors, int exitCode) RunProcess(ProcessStartInfo startInfo);
    }

    public interface IJsonParser
    {
        dynamic ParseJson(string json);
    }

    public class ProcessRunner : IProcessRunner
    {
        public (string output, string errors, int exitCode) RunProcess(ProcessStartInfo startInfo)
        {
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                string output;
                using (var reader = new StreamReader(process.StandardOutput.BaseStream, Encoding.UTF8))
                {
                    output = reader.ReadToEnd();
                }

                string errors;
                using (var errorReader = new StreamReader(process.StandardError.BaseStream, Encoding.UTF8))
                {
                    errors = errorReader.ReadToEnd();
                }

                process.WaitForExit();

                return (output, errors, process.ExitCode);
            }
        }
    }

    public class JsonParser : IJsonParser
    {
        public dynamic ParseJson(string json)
        {
            return JsonConvert.DeserializeObject(json);
        }
    }
}