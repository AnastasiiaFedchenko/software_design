using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using FlowerShop.Spa;
using FlowerShop.Spa.Services;
using FlowerShop.Spa.Components;
using Domain.InputPorts;
using UserValidation;
using InventoryOfProducts;
using ProductBatchLoading;
using ForecastAnalysis;
using ConnectionToDB;
using Domain;
using Domain.OutputPorts;
using ProductBatchReading;
using ReceiptOfSale;
using SegmentAnalysis;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// УБИРАЕМ HttpClient - он больше не нужен
// builder.Services.AddScoped(sp => new HttpClient { ... });

// Получаем конфигурацию как в WebApp
var connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShopPPO;Username=postgres;Password=5432";
var pythonPath = "D:/bmstu/PPO/software_design/pythonProject/.venv/Scripts/python.exe";
var scriptPath = "D:/bmstu/PPO/software_design/pythonProject/ForecastOfOrders.py";

// Регистрация сервисов КАК В WEBAPP
// Регистрация сервисов
builder.Services
    .AddSingleton<IUserRepo>(_ => new UserRepo(new NpgsqlConnectionFactory(connectionString)))
    .AddSingleton<IInventoryRepo>(_ => new InventoryRepo(new NpgsqlConnectionFactory(connectionString)))
    .AddSingleton<IReceiptRepo>(_ => new ReceiptRepo(new NpgsqlConnectionFactory(connectionString)))
    .AddScoped<IProductBatchLoader>(_ => new ProductBatchLoader(connectionString))
    .AddTransient<IForecastServiceAdapter>(_ => new ForecastServiceAdapter(pythonPath, scriptPath))
    .AddTransient<IProductBatchReader>(_ => new ProductBatchReader())
    .AddTransient<IUserSegmentationServiceAdapter>(_ => new UserSegmentationServiceAdapter(connectionString))
    .AddTransient<IUserService, UserService>()
    .AddTransient<IAnalysisService, AnalysisService>()
    .AddScoped<ILoadService, LoadService>()
    .AddTransient<IProductService, ProductService>();

// Регистрируем наши сервисы
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<CartService>();

await builder.Build().RunAsync();