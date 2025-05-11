using Domain;
using Domain.InputPorts;
using Domain.OutputPorts;
using Microsoft.Extensions.DependencyInjection;
using ForecastAnalysis;
using InventoryOfProducts;
using ProductBatchLoading;
using ProductBatchReading;
using ReceiptOfSale;
using SegmentAnalysis;

var serviceProvider = new ServiceCollection()
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

// Пример 1: Анализ сегментов пользователей и прогнозирование заказов
var analysisService = serviceProvider.GetRequiredService<IAnalysisService>();

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
Console.WriteLine(new string('_', 70));

// Пример 2: Загрузка партии товаров
var loadService = serviceProvider.GetRequiredService<ILoadService>();
var productService = serviceProvider.GetRequiredService<IProductService>();

// Создаем тестовый файл (в реальном коде это был бы настоящий FileStream)
if (!File.Exists(@"D:\bmstu\PPO\software_design\FlowerShop\batch1.txt"))
{
    Console.WriteLine($"Файл не найден: {@"D:\bmstu\PPO\software_design\FlowerShop\batch1.txt"}");
    Console.WriteLine("Убедитесь, что файл products.txt находится в директории:");
    return;
}

var testFile = new FileStream(@"D:\bmstu\PPO\software_design\FlowerShop\batch1.txt", FileMode.Open);
// Загружаем партию товаров
bool loadResult = loadService.LoadProductBatch(testFile);
Console.WriteLine($"Результат загрузки партии товаров: {(loadResult ? "Успешно" : "Ошибка")}");
Console.WriteLine(new string('_', 70));

// Пример 3: Получение списка товаров и совершение покупки
var inventory = productService.GetAllAvailableProducts(20, 0);
Console.WriteLine($"Доступно товаров на складе: {inventory.TotalAmount}");
foreach (var productLine in inventory.Products)
{
    Console.WriteLine($"Товар: {productLine.Product.Type}, количество: {productLine.Amount}, цена: {productLine.Product.Price}");
}
Console.WriteLine(new string('_', 70));

// Создаем тестового пользователя
var customer = new User(Guid.NewGuid(), "Customer");

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