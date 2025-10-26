using Moq;
using Xunit;
using Domain;
using Domain.OutputPorts;
using ForecastAnalysis;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Diagnostics;

[Trait("Category", "Unit")]
public class ForecastServiceAdapterTests
{
    private readonly Mock<IProcessRunner> _mockProcessRunner;
    private readonly Mock<IJsonParser> _mockJsonParser;
    private readonly ForecastServiceAdapter _adapter;
    private readonly string _pythonPath = "python";
    private readonly string _scriptPath = "script.py";

    public ForecastServiceAdapterTests()
    {
        _mockProcessRunner = new Mock<IProcessRunner>();
        _mockJsonParser = new Mock<IJsonParser>();

        _adapter = new ForecastServiceAdapter(
            _pythonPath,
            _scriptPath,
            _mockProcessRunner.Object,
            _mockJsonParser.Object
        );
    }

    [Fact]
    public void Create_ShouldReturnCorrectForecast_WhenPythonScriptReturnsValidJson()
    {
        // Arrange
        var expectedJson = GetTestJson();

        // Настраиваем моки
        _mockProcessRunner
            .Setup(x => x.RunProcess(It.IsAny<ProcessStartInfo>()))
            .Returns((expectedJson, "", 0));

        var dynamicData = JsonConvert.DeserializeObject(expectedJson);
        _mockJsonParser
            .Setup(x => x.ParseJson(expectedJson))
            .Returns(dynamicData);

        // Act
        var result = _adapter.Create();

        // Assert
        Assert.NotNull(result);
        Assert.Equal(59, result.AmountOfOrders);
        Assert.Equal(2, result.Products.Count);
        Assert.Equal(3, result.DailyForecast.Count);

        // Проверяем продукты
        var product2 = result.Products.Find(p => p.Product.IdNomenclature == 2);
        Assert.NotNull(product2);
        Assert.Equal("Упаковочная бумага серебряная 1x100м", product2.Product.Type);
        Assert.Equal(8, product2.Amount);

        // Проверяем daily forecast
        Assert.Equal("2025-05-29", result.DailyForecast[0].date);
        Assert.Equal(6, result.DailyForecast[0].orders);
    }

    [Fact]
    public void Create_ShouldThrowException_WhenProcessReturnsError()
    {
        // Arrange
        _mockProcessRunner
            .Setup(x => x.RunProcess(It.IsAny<ProcessStartInfo>()))
            .Returns(("", "Python error", 1));

        // Act & Assert
        var exception = Assert.Throws<ApplicationException>(() => _adapter.Create());
        Assert.Contains("Ошибка выполнения Python-скрипта: Python error", exception.Message);
    }

    private string GetTestJson()
    {
        return @"{
            ""total_orders"": 59,
            ""forecast_start_date"": ""2025-05-29"",
            ""forecast_end_date"": ""2025-06-04"",
            ""daily_forecast"": [
                {
                    ""date"": ""2025-05-29"",
                    ""day_of_week"": 3,
                    ""orders"": 6
                },
                {
                    ""date"": ""2025-05-30"",
                    ""day_of_week"": 4,
                    ""orders"": 6
                },
                {
                    ""date"": ""2025-05-31"",
                    ""day_of_week"": 5,
                    ""orders"": 10
                }
            ],
            ""products"": [
                {
                    ""product_id"": 2,
                    ""product_name"": ""Упаковочная бумага серебряная 1x100м"",
                    ""predicted_demand"": 51,
                    ""current_stock"": 0,
                    ""amount"": 8
                },
                {
                    ""product_id"": 4,
                    ""product_name"": ""Сетария"",
                    ""predicted_demand"": 98,
                    ""current_stock"": 2,
                    ""amount"": 14
                }
            ]
        }";
    }
}