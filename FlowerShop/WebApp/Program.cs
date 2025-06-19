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

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

// Конфигурация сервисов
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
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

// Регистрация сервисов
builder.Services
    .AddSingleton<IUserRepo>(_ => new UserRepo(connectionString))
    .AddSingleton<IInventoryRepo>(_ => new InventoryRepo(connectionString))
    .AddSingleton<IReceiptRepo>(_ => new ReceiptRepo(connectionString))
    .AddSingleton<IProductBatchLoader>(_ => new ProductBatchLoader(connectionString))
    .AddTransient<IForecastServiceAdapter>(_ => new ForecastServiceAdapter(pythonPath, scriptPath))
    .AddTransient<IProductBatchReader>(_ => new ProductBatchReader())
    .AddTransient<IUserSegmentationServiceAdapter>(_ => new UserSegmentationServiceAdapter(connectionString))
    .AddTransient<IUserService, UserService>()
    .AddTransient<IAnalysisService, AnalysisService>()
    .AddTransient<ILoadService, LoadService>()
    .AddTransient<IProductService, ProductService>();

/*builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
    options.AllowSynchronousIO = true; // Если нужно
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.ConfigureHttpsDefaults(httpsOptions =>
    {
        // Используем встроенный сертификат разработки
        httpsOptions.ServerCertificate = new X509Certificate2(
            Path.Combine("..", "..", "..", "..", "localhost.pfx"), 
            "123456" // Пароль для сертификата разработки
        );
    });
});*/

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52428800; // 50MB
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

var app = builder.Build();

// Конфигурация HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();