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

class Program
{
    static void Main(string[] args)
    {

        var menu = new Menu();
        int Id;
        var type = Menu.WhatTypeOfUser(out Id);
        switch (type)
        {
            case UserType.Administrator:
                Console.WriteLine("Вы вошли как администратор.");
                Menu.ShowAdminMenu(Id);
                break;
            case UserType.Seller:
                Console.WriteLine("Вы вошли как продавец.");
                Menu.ShowSellerMenu(Id);
                break;
            case UserType.Storekeeper:
                Console.WriteLine("Вы вошли как кладовщик.");
                Menu.ShowShopKeeperMenu(Id);
                break;
            case null:
                Console.WriteLine("Ошибка: неверный ID или пароль.");
                break;
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

    public Menu()
    {
        serviceProvider = new ServiceCollection()
            .AddSingleton<IUserRepo, UserRepo>()
            .AddSingleton<IInventoryRepo, InventoryRepo>()
            .AddSingleton<IReceiptRepo, ReceiptRepo>()
            .AddTransient<IForecastServiceAdapter, ForecastServiceAdapter>()
            .AddTransient<IProductBatchLoader, ProductBatchLoader>()
            .AddTransient<IProductBatchReader, ProductBatchReader>()
            .AddTransient<IUserSegmentationServiceAdapter, UserSegmentationServiceAdapter>()
            .AddTransient<IUserService, UserService>()
            .AddTransient<IAnalysisService, AnalysisService>()
            .AddTransient<ILoadService, LoadService>()
            .AddTransient<IProductService, ProductService>()
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
                Console.WriteLine("Ошибка: ID должен быть числом!");
                Id = 0;
                return null;
            }
            Id = id;
            Console.Write("Введите пароль: ");
            string password = Console.ReadLine() ?? string.Empty;

            var userType = userService.CheckPasswordAndGetUserType(id, password);

            return userType;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
            Id = 0;
            return null;
        }
    }

    public static void ShowAdminMenu(int Id)
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
                    return;
                case "1":
                    ShowOrderMenu(Id);
                    break;
                case "2":
                    ShowLoadBatchMenu();
                    break;
                case "3":
                    ShowAmountOfOrdersForecast();
                    break;
                case "4":
                    ShowUserSegmentation();
                    break;
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    Console.ReadKey();
                    break;
            }
        }
    }
    public static void ShowSellerMenu(int Id)
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
                    return;
                case "1":
                    ShowOrderMenu(Id);
                    break;
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    Console.ReadKey();
                    break;
            }
        }
    }

    public static void ShowShopKeeperMenu(int Id)
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
                    return;
                case "1":
                    // Показать доступные товары
                    string choice2;
                    int limit = 20;
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
                        if (inventory_temp.TotalAmount < 20)
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
                    ShowLoadBatchMenu();
                    break;
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    Console.ReadKey();
                    break;
            }
        }
    }

    private static void ShowOrderMenu(int Id)
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
                    return;
                case "1": // Показать доступные товары
                    string choice2;
                    int limit = 20;
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
                        if (inventory_temp.TotalAmount < 20)
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
                case "2": // Добавить товар в корзину
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
                case "3": // Изменить количество товара в корзине
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
                    Console.WriteLine("Корзина:");
                    foreach (var item in items)
                        Console.WriteLine($"{item.Product.IdNomenclature} {item.Product.Type}, " +
                                            $"{item.Product.Country}, количество: {item.Amount}, " +
                                            $"цена за шт.: {item.Product.Price}");
                    if (items.Count == 0)
                        Console.WriteLine("Корзина пуста.");
                    break;
                case "6": // 6. Заказать
                    productService.MakePurchase(items, customerID);
                    items = new List<ReceiptLine>();
                    break;
                default:
                    Console.WriteLine("Неверный выбор. Попробуйте еще раз.");
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
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static void ShowAmountOfOrdersForecast()
    {
        Console.Clear();
        Console.WriteLine("=== Прогнозирование количества заказов ===");

        var forecast = analysisService.GetForecastOfOrders();
        Console.WriteLine($"Прогнозируемое количество заказов: {forecast.AmountOfOrders}");
        Console.WriteLine($"Прогнозируемое количество товаров: {forecast.AmountOfProducts}");

        Console.WriteLine("Нажмите любую клавишу для продолжения...");
        Console.ReadKey();
    }

    private static void ShowUserSegmentation()
    {
        Console.Clear();
        Console.WriteLine("=== Сегментация клиентов ===");

        var segments = analysisService.GetUserSegmentation();
        foreach (var segment in segments)
        {
            Console.WriteLine($"Тип сегмента: {segment.Type}, количество клиентов: {segment.Amount}");
        }

        Console.WriteLine("Нажмите любую клавишу для продолжения...");
        Console.ReadKey();
    }
}
