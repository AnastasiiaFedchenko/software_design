using Domain.InputPorts;
using Domain.OutputPorts;
using Domain;
using InventoryOfProducts;
using Microsoft.Extensions.DependencyInjection;
using ProductBatchReading;
using ProductBatchLoading;
using ReceiptOfSale;
using UserValidation;
using System;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;

class Program
{
    static void Main(string[] args)
    {
        int Id;
        var check_user = new CheckUsers();
        var type = CheckUsers.WhatTypeOfUser(out Id);
        string connectionString = null;

        switch (type)
        {
            case UserType.Administrator:
                Console.WriteLine("Вы вошли как администратор.");
                connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=flower_admin;Password=admin_password";
                break;
            case UserType.Seller:
                Console.WriteLine("Вы вошли как продавец.");
                connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=flower_seller;Password=seller_password";
                break;
            case UserType.Storekeeper:
                Console.WriteLine("Вы вошли как кладовщик.");
                connectionString = "Host=127.0.0.1;Port=5432;Database=FlowerShop;Username=flower_storekeeper;Password=storekeeper_password";
                break;
            case null:
                Console.WriteLine("Ошибка: неверный ID или пароль.");
                return; // Выходим из программы
        }

        // Создаем меню с передачей строки подключения
        var menu = new Menu(connectionString);

        // Показываем соответствующее меню
        switch (type)
        {
            case UserType.Administrator:
                menu.ShowAdminMenu(Id);
                break;
            case UserType.Seller:
                menu.ShowSellerMenu(Id);
                break;
            case UserType.Storekeeper:
                menu.ShowShopKeeperMenu(Id);
                break;
        }
    }
}

class CheckUsers
{
    private readonly IServiceProvider serviceProvider;
    private static IUserService userService;

    public CheckUsers()
    {
        serviceProvider = new ServiceCollection()
            .AddSingleton<IUserRepo, UserRepo>()
            .AddTransient<IUserService, UserService>()
            .BuildServiceProvider();

        userService = serviceProvider.GetRequiredService<IUserService>();
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
}

class Menu
{
    private readonly IServiceProvider serviceProvider;
    private readonly string _connectionString;
    private static ILoadService loadService;
    private static IProductService productService;

    public Menu(string connectionString)
    {
        _connectionString = connectionString;

        serviceProvider = new ServiceCollection()
            .AddSingleton<IInventoryRepo>(_ => new InventoryRepo(_connectionString))
            .AddSingleton<IReceiptRepo>(_ => new ReceiptRepo(_connectionString))
            .AddSingleton<IProductBatchLoader>(_ => new ProductBatchLoader(_connectionString))
            .AddTransient<IProductBatchReader, ProductBatchReader>()
            .AddTransient<ILoadService, LoadService>()
            .AddTransient<IProductService, ProductService>()
            .BuildServiceProvider();

        loadService = serviceProvider.GetRequiredService<ILoadService>();
        productService = serviceProvider.GetRequiredService<IProductService>();
    }

    public void ShowAdminMenu(int Id)
    {
        while (true)
        {
            //Console.Clear();
            Console.WriteLine("=== ГЛАВНОЕ МЕНЮ ===");
            Console.WriteLine("1. Сделать заказ");
            Console.WriteLine("2. Загрузка информации о новой партии");
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
                default:
                    Console.WriteLine("Неверный номер пункта меню. Попробуйте еще раз.");
                    Console.ReadKey();
                    break;
            }
        }
    }
    public void ShowSellerMenu(int Id)
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

    public void ShowShopKeeperMenu(int Id)
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
                            Console.WriteLine($"Товар: {pL.Product.Nomenclature} {pL.Product.Type}, " +
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
                        var inventory_temp = productService.GetAllAvailableProducts(limit, skip);
                        foreach (var pL in inventory_temp.Products)
                        {
                            Console.WriteLine($"Товар: {pL.Product.Nomenclature} {pL.Product.Type}, " +
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
                        if (items[i].Product.Nomenclature == productID2)
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
                        if (items[i].Product.Nomenclature == productID3)
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
                        Console.WriteLine($"{item.Product.Nomenclature} {item.Product.Type}, " +
                                            $"{item.Product.Country}, количество: {item.Amount}, " +
                                            $"цена за шт.: {item.Product.Price}");
                    if (items.Count == 0)
                        Console.WriteLine("Корзина пуста.");
                    break;
                case "6": // 6. Заказать
                    var receipt = productService.MakePurchase(items, customerID);
                    items = new List<ReceiptLine>();
                    if (receipt.Id == -1)
                        Console.WriteLine($"Произошли проблемы при оформлении товара.");
                    else
                        Console.WriteLine($"Заказ оформлен. Номер чека {receipt.Id}");
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
}
