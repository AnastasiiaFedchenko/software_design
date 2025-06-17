using Domain.InputPorts;
using Domain.OutputPorts;
using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using Domain;
using ForecastAnalysis;
using InventoryOfProducts;
using Microsoft.Extensions.DependencyInjection;
using ProductBatchReading;
using ProductBatchLoading;
using ReceiptOfSale;
using SegmentAnalysis;
using UserValidation;
using System;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using System.Text;
using Microsoft.Extensions.Logging;
using Serilog;
using Microsoft.Extensions.Configuration;
using System.IO;
using Serilog.Core;

class Program
{
    static void Main(string[] args)
    {
        // Настройка конфигурации
        var projectDir = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName;
        var configPath = Path.Combine(projectDir, "appsettings.json");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configPath, optional: false, reloadOnChange: true)
            .Build();

        // Настройка Serilog
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            // Получаем строку подключения из конфига
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var defaultLimit = configuration.GetValue<int>("AppSettings:DefaultPaginationLimit");
            var pythonPath = configuration["PythonSettings:PythonPath"];
            var scriptPath = configuration["PythonSettings:ScriptPath"];

            if (string.IsNullOrEmpty(pythonPath))
                throw new ArgumentNullException(nameof(pythonPath));
            if (string.IsNullOrEmpty(scriptPath))
                throw new ArgumentNullException(nameof(scriptPath));

            Log.Information("Limit={DefaultLimit}", defaultLimit);
            Log.Information("Используется строка подключения: {ConnectionString}",
                connectionString.Substring(0, connectionString.LastIndexOf("Password="))); // Маскируем пароль в логах

            // Создание фабрики логгеров
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(dispose: true);
            });

            // Передаем connectionString в конструктор Menu
            var menu = new Menu(loggerFactory, connectionString, pythonPath, scriptPath);

            int Id;
            var type = Menu.WhatTypeOfUser(out Id);

            switch (type)
            {
                case UserType.Administrator:
                    Log.Information("Пользователь {UserId} вошёл как администратор", Id);
                    Menu.ShowAdminMenu(Id, defaultLimit);
                    break;
                case UserType.Seller:
                    Log.Information("Пользователь {UserId} вошёл как продавец", Id);
                    Menu.ShowSellerMenu(Id, defaultLimit);
                    break;
                case UserType.Storekeeper:
                    Log.Information("Пользователь {UserId} вошёл как кладовщик", Id);
                    Menu.ShowShopKeeperMenu(Id, defaultLimit);
                    break;
                case null:
                    Log.Warning("Неудачная попытка входа (неверный ID или пароль)");
                    Console.WriteLine("Ошибка: неверный ID или пароль.");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Приложение завершилось с ошибкой");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}

class Menu
{
    private static IServiceProvider serviceProvider;
    private static IUserService userService;
    private static IAnalysisService analysisService;
    private static ILoadService loadService;
    private static IProductService productService;
    private static ILogger<Menu> _logger;
    private readonly string _connectionString;

    public Menu(ILoggerFactory loggerFactory, string connectionString, string pythonPath, string scriptPath)
    {
        _logger = loggerFactory.CreateLogger<Menu>();
        _connectionString = connectionString;

        serviceProvider = new ServiceCollection()
            .AddLogging(logging => logging.AddSerilog())
            .AddSingleton<IUserRepo>(provider => new UserRepo(_connectionString))
            .AddSingleton<IInventoryRepo>(provider => new InventoryRepo(_connectionString))
            .AddSingleton<IReceiptRepo>(provider => new ReceiptRepo(_connectionString))
            .AddSingleton<IProductBatchLoader>(provider => new ProductBatchLoader(_connectionString))
            .AddTransient<IForecastServiceAdapter>(provider => new ForecastServiceAdapter(pythonPath, scriptPath))
            .AddTransient<IProductBatchReader>(provider => new ProductBatchReader())
            .AddTransient<IUserSegmentationServiceAdapter>(provider => new UserSegmentationServiceAdapter(_connectionString))
            .AddTransient<IUserService>(provider => new UserService(provider.GetRequiredService<IUserRepo>(), 
                                                                    provider.GetRequiredService<ILogger<UserService>>()))
            .AddTransient<IAnalysisService>(provider => new AnalysisService(provider.GetRequiredService<IForecastServiceAdapter>(),
                                                                            provider.GetRequiredService<IUserSegmentationServiceAdapter>(),
                                                                            provider.GetRequiredService<ILogger<AnalysisService>>()))
            .AddTransient<ILoadService>(provider => new LoadService(provider.GetRequiredService<IProductBatchReader>(),
                                                                    provider.GetRequiredService<IProductBatchLoader>(),
                                                                    provider.GetRequiredService<ILogger<LoadService>>()))
            .AddTransient<IProductService>(provider => new ProductService(provider.GetRequiredService<IInventoryRepo>(),
                                                                          provider.GetRequiredService<IReceiptRepo>(),
                                                                          provider.GetRequiredService<ILogger<ProductService>>()))
            .BuildServiceProvider();

        userService = serviceProvider.GetRequiredService<IUserService>();
        analysisService = serviceProvider.GetRequiredService<IAnalysisService>();
        loadService = serviceProvider.GetRequiredService<ILoadService>();       
        productService = serviceProvider.GetRequiredService<IProductService>();
    }

    public static UserType? WhatTypeOfUser(out int Id)
    {
        try
        {
            Console.Write("Введите логин (Ваш ID): ");
            if (!int.TryParse(Console.ReadLine(), out int id))
            {
                _logger.LogWarning("Некорректный ID (не число)");
                Id = 0;
                return null;
            }
            Id = id;
            Console.Write("Введите пароль: ");
            string password = Console.ReadLine() ?? string.Empty;

            var userType = userService.CheckPasswordAndGetUserType(id, password);

            if (userType == null)
                _logger.LogWarning("Неудачная попытка входа для пользователя {UserId}", id);

            return userType;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при проверке пользователя");

            Id = 0;
            return null;
        }
    }

    public static void ShowAdminMenu(int Id, int limit)
    {
        while (true)
        {
            //Console.Clear();
            Console.WriteLine("=== ГЛАВНОЕ МЕНЮ ===");
            Console.WriteLine("1. Сделать заказ");
            Console.WriteLine("2. Загрузка информации о новой партии");
            Console.WriteLine("3. Прогнозирование количества заказов");
            Console.WriteLine("4. Сегментация клиентов");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите пункт меню: ");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "0":
                    _logger.LogInformation("0. Выход");

                    return;
                case "1":
                    _logger.LogInformation("1. Сделать заказ");

                    ShowOrderMenu(Id, limit);
                    break;
                case "2":
                    _logger.LogInformation("2. Загрузка информации о новой партии");

                    ShowLoadBatchMenu();
                    break;
                case "3":
                    _logger.LogInformation("3. Прогнозирование количества заказов");

                    ShowAmountOfOrdersForecast();
                    break;
                case "4":
                    _logger.LogInformation("4. Сегментация клиентов");

                    ShowUserSegmentation();
                    break;
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    _logger.LogWarning($"Был введён неверный номер пункта меню: {choice}"); 

                    Console.ReadKey();
                    break;
            }
        }
    }
    public static void ShowSellerMenu(int Id, int limit)
    {
        while (true)
        {
            //Console.Clear();
            Console.WriteLine("=== ГЛАВНОЕ МЕНЮ ===");
            Console.WriteLine("1. Сделать заказ");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите пункт меню: ");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "0":
                    _logger.LogInformation("0. Выход");

                    return;
                case "1":
                    _logger.LogInformation("1. Сделать заказ");

                    ShowOrderMenu(Id, limit);
                    break;
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    _logger.LogWarning($"Был введён неверный номер пункта меню: {choice}");

                    Console.ReadKey();
                    break;
            }
        }
    }

    public static void ShowShopKeeperMenu(int Id, int limit)
    {
        while (true)
        {
            //Console.Clear();
            Console.WriteLine("=== ГЛАВНОЕ МЕНЮ ===");
            Console.WriteLine("1. Показать доступные товары");
            Console.WriteLine("2. Загрузка информации о новой партии");
            Console.WriteLine("0. Выход");
            Console.Write("Выберите пункт меню: ");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "0":
                    _logger.LogInformation("0. Выход");

                    return;
                case "1":
                    _logger.LogInformation("1.  Показать доступные товары");

                    // Показать доступные товары
                    string choice2;
                    int skip = 0;
                    do
                    {
                        var inventory_temp = productService.GetAllAvailableProducts(limit, skip);
                        foreach (var pL in inventory_temp.Products)
                        {
                            Console.WriteLine($"Товар: {pL.Product.IdNomenclature} {pL.Product.Type}, " +
                                            $"{pL.Product.Country}, количество: {pL.Amount}, " +
                                            $"цена: {pL.Product.Price}");
                        }
                        Console.WriteLine(new string('_', 70));
                        skip += limit;
                        if (inventory_temp.TotalAmount < limit)
                        {
                            Console.WriteLine("Выведены все доступные товары.");
                            choice2 = "0";
                        }
                        else
                        {
                            Console.Write("Продолжить вывод доступных товаров? (0 - остановиться, 1 - продолжить): ");
                            choice2 = Console.ReadLine();
                        }
                    } while (choice2 == "1");
                    break;
                case "2":
                    _logger.LogInformation("2. Загрузка информации о новой партии");

                    ShowLoadBatchMenu();
                    break;
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    _logger.LogWarning($"Был введён неверный номер пункта меню: {choice}");

                    Console.ReadKey();
                    break;
            }
        }
    }

    private static void ShowOrderMenu(int Id, int limit)
    {
        int customerID = Id;
        List<ReceiptLine> items = new List<ReceiptLine>();
        while (true)
        {
            //Console.Clear();
            Console.WriteLine("=== Сделать заказ ===");
            Console.WriteLine("1. Показать доступные товары");
            Console.WriteLine("2. Добавить товар в корзину");
            Console.WriteLine("3. Изменить количество товара в корзине");
            Console.WriteLine("4. Удалить товар из корзины");
            Console.WriteLine("5. Показать содержание корзины");
            Console.WriteLine("6. Заказать");
            Console.WriteLine("0. Назад (если вернуться, содержание корзины обнулится)");
            Console.Write("Выберите пункт меню: ");

            var choice = Console.ReadLine();
            switch (choice)
            {
                case "0":
                    _logger.LogInformation("0. Выход из меню заказа");

                    return;
                case "1": // 1. Показать доступные товары
                    _logger.LogInformation("1. Показать доступные товары");

                    string choice2;
                    int skip = 0;
                    do
                    {
                        Inventory inventory_temp = productService.GetAllAvailableProducts(limit, skip);
                        foreach (var pL in inventory_temp.Products)
                        {
                            Console.WriteLine($"Товар: {pL.Product.IdNomenclature} {pL.Product.Type}, " +
                                            $"{pL.Product.Country}, количество: {pL.Amount}, " +
                                            $"цена: {pL.Product.Price}");
                        }
                        Console.WriteLine(new string('_', 70));
                        skip += limit;
                        if (inventory_temp.TotalAmount < limit)
                        {
                            Console.WriteLine("Выведены все доступные товары.");
                            choice2 = "0";
                        }
                        else
                        {
                            Console.Write("Продолжить вывод доступных товаров? (0 - остановиться, 1 - продолжить): ");
                            choice2 = Console.ReadLine();
                        }                        
                    } while (choice2 == "1");
                    break;
                case "2": // 2. Добавить товар в корзину
                    _logger.LogInformation("2. Добавить товар в корзину");

                    Console.Write("Введите id товара для добавления в корзину: ");
                    int productID = int.Parse(Console.ReadLine());
                    Product temp = productService.GetInfoOnProduct(productID);
                    if (temp == null)
                        Console.WriteLine("Продукта с таким Id нет в наличие.");
                    else
                    {
                        Console.Write("Введите количество товара для добавления в корзину: ");
                        int amount = int.Parse(Console.ReadLine());
                        if (amount > temp.AmountInStock)
                            Console.WriteLine("Доступно только " + temp.AmountInStock);
                        else
                        {
                            items.Add(new ReceiptLine(temp, amount));
                            Console.WriteLine("Товар добавлен в корзину.");
                        }
                    }
                    break;
                case "3": // 3. Изменить количество товара в корзине
                    _logger.LogInformation("3. Изменить количество товара в корзине");

                    Console.Write("Введите id товара для изменения количества в корзине: ");
                    int productID2 = int.Parse(Console.ReadLine());
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].Product.IdNomenclature == productID2)
                        {
                            Console.Write("Введите новое количество товара: ");
                            int amount2 = int.Parse(Console.ReadLine());
                            if (amount2 > items[i].Product.AmountInStock)
                                Console.WriteLine("Доступно только " + items[i].Product.AmountInStock);
                            else
                            {
                                items[i].Amount = amount2;
                                Console.WriteLine("Количество товара изменено.");
                            }
                        }
                    }
                    break;
                case "4": // 4. Удалить товар из корзины
                    _logger.LogInformation("4. Удалить товар из корзины");

                    Console.Write("Введите id товара для удаления из корзины: ");
                    int productID3 = int.Parse(Console.ReadLine());
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i].Product.IdNomenclature == productID3)
                        {
                            items.RemoveAt(i);
                            Console.WriteLine("Товар удалён из корзины.");
                            i--;
                        }
                    }
                    break; 
                case "5": // 5. Показать содержание корзины
                    _logger.LogInformation("5. Показать содержание корзины");

                    Console.WriteLine("Корзина:");
                    foreach (var item in items)
                        Console.WriteLine($"{item.Product.IdNomenclature} {item.Product.Type}, " +
                                            $"{item.Product.Country}, количество: {item.Amount}, " +
                                            $"цена за шт.: {item.Product.Price}");
                    if (items.Count == 0)
                        Console.WriteLine("Корзина пуста.");
                    break;
                case "6": // 6. Заказать
                    _logger.LogInformation("6. Заказать");

                    Receipt receipt = productService.MakePurchase(items, customerID);
                    items = new List<ReceiptLine>();
                    if (receipt.Id == -1)
                        Console.WriteLine($"Произошли проблемы при оформлении товара.");
                    else
                        Console.WriteLine($"Заказ оформлен. Номер чека {receipt.Id}");
                    break;
                default:
                    Console.WriteLine("Неверный выбор. Попробуйте еще раз.");
                    _logger.LogWarning($"Был введён неверный номер пункта меню: {choice}");

                    Console.ReadKey();
                    break;
            }
        }
    }

    private static void ShowLoadBatchMenu()
    {
        //Console.Clear();
        Console.WriteLine("=== Загрузка информации о новой партии ===");
        Console.WriteLine("Введите путь к файлу с данными о партии:");
        string filePath = Console.ReadLine()?
                                    .Replace('\\', '/')
                                    .Replace("\"", "");    
        try
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                bool result = loadService.LoadProductBatch(fileStream);
                Console.WriteLine(result ? "Загрузка прошла успешно!" : "Ошибка при загрузке данных");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message.ToString());
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static void ShowAmountOfOrdersForecast()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Clear();
        Console.WriteLine("=== ПРОГНОЗ ЗАКАЗОВ НА НЕДЕЛЮ ===");
        Console.WriteLine(new string('=', 30));

        try
        {
            var forecast = analysisService.GetForecastOfOrders();

            Console.WriteLine($"\nОБЩАЯ СТАТИСТИКА:");
            Console.WriteLine($"• Прогноз заказов: {forecast.AmountOfOrders}");

            Console.WriteLine("\nПРОГНОЗ ПО ДНЯМ:");
            Console.WriteLine("{0,-15} {1,-10} {2,-10}", "Дата", "День недели", "Заказов");
            Console.WriteLine(new string('-', 35));

            foreach (var day in forecast.DailyForecast)
            {
                string dayOfWeek = GetRussianDayOfWeek(day.day_of_week);
                Console.WriteLine("{0,-15} {1,-10} {2,-10}",
                    day.date,
                    dayOfWeek,
                    day.orders);
            }

            Console.WriteLine("\nТОП 10 ТОВАРОВ ДЛЯ ЗАКАЗА:");
            Console.WriteLine("{0,-5} {1,-40} {2,-15} {3,-15}", "ID", "Название", "Кол-во в наличие", "Надо заказать");
            Console.WriteLine(new string('-', 60));

            var topProducts = forecast.Products
                .OrderByDescending(p => p.Amount)
                .Take(10);

            foreach (var product in topProducts)
            {
                Console.WriteLine("{0,-5} {1,-40} {2,-15} {3,-15}",
                    product.Product.IdNomenclature,
                    product.Product.Type,
                    product.AmountInStock, 
                    product.Amount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message.ToString());

            Console.WriteLine("\nОшибка при получении прогноза:");
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine("\nНажмите любую клавишу для возврата в меню...");
        Console.ReadKey();
    }

    private static string GetRussianDayOfWeek(int dayOfWeek)
    {
        return dayOfWeek switch
        {
            0 => "Понедельник",
            1 => "Вторник",
            2 => "Среда",
            3 => "Четверг",
            4 => "Пятница",
            5 => "Суббота",
            6 => "Воскресенье",
            _ => "Неизвестно"
        };
    }

    private static void ShowUserSegmentation()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;
        Console.Clear();
        Console.WriteLine("=== СЕГМЕНТАЦИЯ ПОЛЬЗОВАТЕЛЕЙ ===");
        Console.WriteLine(new string('=', 30));

        try
        {
            var segments = analysisService.GetUserSegmentation();

            foreach (var segment in segments)
            {
                Console.WriteLine($"\n{segment.Type.ToUpper()}:");
                Console.WriteLine($"Всего пользователей: {segment.Amount}");
                Console.WriteLine(new string('-', 80));

                Console.WriteLine("{0,-5} {1,-20} {2,-40}", "ID", "Роль", "Имя");
                Console.WriteLine(new string('-', 80));

                int counter = 1;
                foreach (var user in segment.Users)
                {
                    string roleName = user.Role switch
                    {
                        UserType.Administrator => "Администратор",
                        UserType.Seller => "Продавец",
                        UserType.Storekeeper => "Кладовщик",
                        _ => user.Role.ToString()
                    };

                    if (user.Password == "supplier_no_password")
                    {
                        roleName = "Поставщик";
                    }

                    Console.WriteLine("{0,-5} {1,-20} {2,-40}",
                        user.Id,
                        roleName,
                        user.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message.ToString());

            Console.WriteLine("\nОшибка при получении сегментации:");
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine("\nНажмите любую клавишу для возврата в меню...");
        Console.ReadKey();
    }
}
