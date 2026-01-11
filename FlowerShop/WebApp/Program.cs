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
using WebApp.Services;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

var jaegerHost = builder.Configuration.GetValue<string>("Jaeger:Host") ?? "localhost";
var jaegerPort = builder.Configuration.GetValue<int?>("Jaeger:Port") ?? 6831;
var jaegerServiceName = builder.Configuration.GetValue<string>("Jaeger:ServiceName") ?? builder.Environment.ApplicationName;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(jaegerServiceName))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddJaegerExporter(options =>
            {
                options.AgentHost = jaegerHost;
                options.AgentPort = jaegerPort;
            });
    });

// Конфигурация сервисов
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["TEST_CONNECTION_STRING"];
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

builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("AuthSettings"));
builder.Services.AddSingleton<AuthStateService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

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

public partial class Program { }
