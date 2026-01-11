using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Domain;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using Observability;
using Serilog;
using Serilog.Extensions.Logging;

var telemetrySettings = TelemetrySettings.FromEnvironment("FlowerShop.Benchmarks");
var telemetryHandle = TelemetryBootstrap.StartSdk(
    telemetrySettings,
    new[] { Domain.Diagnostics.ActivitySourceName },
    new[] { Domain.Diagnostics.MeterName },
    includeRuntimeMetrics: true);

AppDomain.CurrentDomain.ProcessExit += (_, _) => telemetryHandle.Dispose();

BenchmarkRunner.Run<ProductServiceBenchmarks>();

[MemoryDiagnoser]
public class ProductServiceBenchmarks
{
    private ProductService _service = null!;
    private List<ReceiptLine> _items = null!;
    private SerilogLoggerFactory _loggerFactory = null!;

    [GlobalSetup]
    public void Setup()
    {
        _loggerFactory = new SerilogLoggerFactory(CreateLogger(), dispose: true);
        _service = new ProductService(
            new FakeInventoryRepo(),
            new FakeReceiptRepo(),
            _loggerFactory.CreateLogger<ProductService>());

        _items = new List<ReceiptLine>
        {
            new ReceiptLine(new Product(1, 9.99, 100, "Standard", "Local"), 2),
            new ReceiptLine(new Product(2, 4.50, 50, "Snack", "Local"), 1),
        };
    }

    [Benchmark]
    public Inventory GetAllAvailableProducts()
    {
        return _service.GetAllAvailableProducts(10, 0);
    }

    [Benchmark]
    public Product GetInfoOnProduct()
    {
        return _service.GetInfoOnProduct(1);
    }

    [Benchmark]
    public bool CheckNewAmount()
    {
        return _service.CheckNewAmount(1, 2);
    }

    [Benchmark]
    public Receipt? MakePurchase()
    {
        return _service.MakePurchase(_items, 1);
    }

    private static Serilog.ILogger CreateLogger()
    {
        var loggingProfile = Environment.GetEnvironmentVariable("FLOWERSHOP_LOGGING_PROFILE") ?? "Default";
        Directory.CreateDirectory(Path.Combine("analysis", "logs"));
        var loggerConfiguration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                path: Path.Combine("analysis", "logs", $"benchmarks-{loggingProfile.ToLowerInvariant()}.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7);

        if (string.Equals(loggingProfile, "Extended", StringComparison.OrdinalIgnoreCase))
        {
            loggerConfiguration
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("LoggingProfile", "Extended");
        }

        return loggerConfiguration.CreateLogger();
    }

    private sealed class FakeInventoryRepo : IInventoryRepo
    {
        private readonly List<ProductLine> _products = new()
        {
            new ProductLine(new Product(1, 9.99, 100, "Standard", "Local"), 10, 100),
            new ProductLine(new Product(2, 4.50, 50, "Snack", "Local"), 5, 50),
        };

        public Inventory GetAvailableProduct(int limit, int skip)
        {
            return new Inventory(Guid.NewGuid(), DateTime.UtcNow, _products.Count, _products);
        }

        public bool CheckNewAmount(int product_id, int new_n)
        {
            return new_n > 0 && new_n <= 100;
        }

        public Product GetInfoOnProduct(int productID)
        {
            return new Product(productID, 9.99, 100, "Standard", "Local");
        }
    }

    private sealed class FakeReceiptRepo : IReceiptRepo
    {
        private int _nextId = 1;

        public bool LoadReceiptItemsSale_UpdateAmount(ref Receipt receipt)
        {
            receipt.Id = _nextId++;
            return true;
        }
    }
}
