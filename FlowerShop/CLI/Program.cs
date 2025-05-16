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
using System;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;

class Program
{
    static void Main(string[] args)
    {
        var menu = new Menu();

        Menu.ShowMainMenu();
    }
}

class Menu
{
    private static IServiceProvider serviceProvider;
    private static IAnalysisService analysisService;
    private static ILoadService loadService;
    private static IProductService productService;

    public Menu()
    {
        serviceProvider = new ServiceCollection()
            .AddSingleton<IInventoryRepo, InventoryRepo>()
            .AddSingleton<IReceiptRepo, ReceiptRepo>()
            .AddTransient<IForecastServiceAdapter, ForecastServiceAdapter>()
            .AddTransient<IProductBatchLoader, ProductBatchLoader>()
            .AddTransient<IProductBatchReader, ProductBatchReader>()
            .AddTransient<IUserSegmentationServiceAdapter, UserSegmentationServiceAdapter>()
            .AddTransient<IAnalysisService, AnalysisService>()
            .AddTransient<ILoadService, LoadService>()
            .AddTransient<IProductService, ProductService>()
            .BuildServiceProvider();

        analysisService = serviceProvider.GetRequiredService<IAnalysisService>();
        loadService = serviceProvider.GetRequiredService<ILoadService>();
        productService = serviceProvider.GetRequiredService<IProductService>();
    }

    public static void ShowMainMenu()
    {
        while (true)
        {
            Console.Clear();
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
                    ShowOrderMenu();
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

    private static void ShowOrderMenu()
    {
        Console.WriteLine("Введите ID пользователя (продавца) :");
        int customerID = int.Parse(Console.ReadLine());
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
                    productService.MakePurchase(items, customerID);
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
        Console.Clear();
        Console.WriteLine("=== Загрузка информации о новой партии ===");
        Console.WriteLine("Введите путь к файлу с данными о партии:");
        string filePath = Console.ReadLine()?
            .Replace('\\', '/')
            .Replace("\"", "");    // Удаляем кавычки;
        //Console.WriteLine($"Путь для Visual Studio: {filePath}");
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



// Пример 1: Анализ сегментов пользователей и прогнозирование заказов
/*var analysisService = serviceProvider.GetRequiredService<IAnalysisService>();

// Получаем прогноз заказов
var forecast = analysisService.GetForecastOfOrders();
Console.WriteLine($"Прогноз заказов: {forecast.AmountOfOrders} заказов, {forecast.AmountOfProducts} товаров");
Console.WriteLine(new string('_', 70));

// Получаем сегментацию пользователей
var segments = analysisService.GetUserSegmentation();
foreach (var segment in segments)
{
    Console.WriteLine($"Сегмент: {segment.Type}, количество пользователей: {segment.Amount}");
}
Console.WriteLine(new string('_', 70));*/

// Пример 2: Загрузка партии товаров
//var loadService = serviceProvider.GetRequiredService<ILoadService>();
//var productService = serviceProvider.GetRequiredService<IProductService>();

// Создаем тестовый файл (в реальном коде это был бы настоящий FileStream)
/*if (!File.Exists(@"D:\bmstu\PPO\software_design\FlowerShop\batch1.txt"))
{
    Console.WriteLine($"Файл не найден: {@"D:\bmstu\PPO\software_design\FlowerShop\batch1.txt"}");
    Console.WriteLine("Убедитесь, что файл products.txt находится в директории:");
    return;
}

var testFile = new FileStream(@"D:\bmstu\PPO\software_design\FlowerShop\batch1.txt", FileMode.Open);
// Загружаем партию товаров
bool loadResult = loadService.LoadProductBatch(testFile);
Console.WriteLine($"Результат загрузки партии товаров: {(loadResult ? "Успешно" : "Ошибка")}");
Console.WriteLine(new string('_', 70));*/

// Пример 3: Получение списка товаров и совершение покупки
/*var inventory = productService.GetAllAvailableProducts(20, 0);
Console.WriteLine($"Доступно товаров на складе: {inventory.TotalAmount}");
foreach (var productLine in inventory.Products)
{
    Console.WriteLine($"Товар: {productLine.Product.Nomenclature} {productLine.Product.Type}, {productLine.Product.Country}, количество: {productLine.Amount}, цена: {productLine.Product.Price}");
}
Console.WriteLine(new string('_', 70));

var inventory2 = productService.GetAllAvailableProducts(20, 20);
Console.WriteLine($"Доступно товаров на складе: {inventory.TotalAmount}");
foreach (var productLine in inventory2.Products)
{
    Console.WriteLine($"Товар: {productLine.Product.Nomenclature} {productLine.Product.Type}, {productLine.Product.Country}, количество: {productLine.Amount}, цена: {productLine.Product.Price}");
}
Console.WriteLine(new string('_', 70));

Console.Write("Введите целое число: ");
string input = Console.ReadLine();
int id_product;

if (int.TryParse(input, out id_product))
    Console.WriteLine($"проверка {productService.CheckNewAmount(id_product, 6)}");
// Создаем тестового пользователя
//var customer = new User(Guid.NewGuid(), "Customer");

// Создаем список товаров для покупки
/*var productsToBuy = new List<ReceiptLine>
{
    new ReceiptLine(inventory.Products[0].Product, 2),
    new ReceiptLine(inventory.Products[1].Product, 1)
};

// Совершаем покупку
var receipt = productService.MakePurchase(productsToBuy, customer);
Console.WriteLine($"Чек создан: ID = {receipt.Id}, Итоговая сумма = {receipt.FinalPrice}, Дата = {receipt.Date}");
Console.WriteLine(new string('_', 70));

// Проверяем доступное количество товара
var productId = inventory.Products[0].Product.Nomenclature;
bool checkResult = productService.CheckNewAmount(productId, 5);
Console.WriteLine($"Проверка доступности 5 единиц товара {productId}: {(checkResult ? "Доступно" : "Недостаточно")}");*/