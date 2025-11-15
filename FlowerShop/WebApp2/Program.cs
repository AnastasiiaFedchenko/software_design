using System.Security.Claims;
using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using ForecastAnalysis;
using InventoryOfProducts;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using ProductBatchLoading;
using ProductBatchReading;
using ReceiptOfSale;
using SegmentAnalysis;
using Serilog;
using UserValidation;
using ConnectionToDB;
using Microsoft.OpenApi.Models;
using WebApp2.Application.Mappings;
using Microsoft.AspNetCore.Diagnostics;
using WebApp2.Services;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// КОНФИГУРАЦИЯ СЕРВИСОВ
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "FlowerShop API",
        Version = "v1",
        Description = "REST API для системы управления цветочным магазином"
    });

    c.IgnoreObsoleteProperties();
    c.IgnoreObsoleteActions();
    c.CustomSchemaIds(x => x.FullName);
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// Сессии
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddHttpContextAccessor();

// Конфигурация
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var defaultLimit = builder.Configuration.GetValue<int>("AppSettings:DefaultPaginationLimit");
var pythonPath = builder.Configuration["PythonSettings:PythonPath"];
var scriptPath = builder.Configuration["PythonSettings:ScriptPath"];

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
    .AddTransient<IProductService, ProductService>()
    .AddSingleton<ICartStorageService, CartStorageService>(); ;

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800;
});

builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromMinutes(5);
});

// JSON опции
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new ProductJsonConverter());
    });

// Настройка аутентификации
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/api/v1/auth/login";
        options.AccessDeniedPath = "/api/v1/auth/access-denied";
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

// Настройка авторизации
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Administrator"));
    options.AddPolicy("SellerOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Seller"));
    options.AddPolicy("StorekeeperOnly", policy => policy.RequireClaim(ClaimTypes.Role, "Storekeeper"));
});

var app = builder.Build();

// CORS ДОЛЖЕН БЫТЬ ЗДЕСЬ
app.UseCors("AllowAll");

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FlowerShop API v1");
    c.RoutePrefix = "swagger";
});

// Глобальная обработка ошибок
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Глобальная ошибка");

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
        {
            error = "Внутренняя ошибка сервера",
            message = app.Environment.IsDevelopment() ? exception?.Message : "Обратитесь к администратору"
        }));
    });
});

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Тестовые endpoints для проверки
//app.MapGet("/", () => "FlowerShop API is running! " + DateTime.Now);
//app.MapGet("/health", () => new { status = "Healthy", timestamp = DateTime.Now });
//app.MapGet("/test", () => Results.Ok(new { message = "Test endpoint works!" }));

app.MapControllers();

// Логирование запуска
Console.WriteLine("=== APPLICATION STARTED ===");
Console.WriteLine($"Swagger UI: https://localhost:7036/swagger");
Console.WriteLine($"Health check: https://localhost:7036/health");
Console.WriteLine($"Test endpoint: https://localhost:7036/test");

app.Run();