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
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Diagnostics;
using ConnectionToDB;
using Microsoft.OpenApi.Models;
using WebApp2.Application.Mappings;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Конфигурация сервисов
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Добавление API и Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new OpenApiInfo
    {
        Title = "FlowerShop API v2",
        Version = "v2"
    });
    
    // Проигнорировать проблемы с типами
    c.IgnoreObsoleteProperties();
    c.IgnoreObsoleteActions();
    
    // Простая схема именования
    c.CustomSchemaIds(x => x.FullName);
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var defaultLimit = builder.Configuration.GetValue<int>("AppSettings:DefaultPaginationLimit");
var pythonPath = builder.Configuration["PythonSettings:PythonPath"];
var scriptPath = builder.Configuration["PythonSettings:ScriptPath"];

// Регистрация сервисов (оставляем как есть)
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

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
});

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
});
builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new ProductJsonConverter());
    });

// Настройка аутентификации
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Home/AccessDenied";
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

AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
{
    var exception = e.ExceptionObject as Exception;
    Log.Fatal(exception, "Необработанное исключение");
};

var app = builder.Build();

// Конфигурация HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(exception, "Глобальная ошибка");

        context.Response.StatusCode = 500;
        await context.Response.WriteAsync("Произошла ошибка. Подробности в логах.");
    });
});

// Включение Swagger в Development режиме
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v2/swagger.json", "FlowerShop API v2");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Map API контроллеры
app.MapControllers(); // Это для API контроллеров

// Map MVC контроллеры (легаси)
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();