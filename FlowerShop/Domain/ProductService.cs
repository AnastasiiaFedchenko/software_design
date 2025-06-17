using System;
using System.Collections.Generic;
using Domain.InputPorts;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;

namespace Domain
{
    public class ProductService : IProductService
    {
        private readonly IInventoryRepo _inventoryRepo;
        private readonly IReceiptRepo _receiptRepo;
        private readonly ILogger<ProductService> _logger;

        public ProductService(
            IInventoryRepo inventoryRepo,
            IReceiptRepo receiptRepo,
            ILogger<ProductService> logger)
        {
            _inventoryRepo = inventoryRepo ?? throw new ArgumentNullException(nameof(inventoryRepo));
            _receiptRepo = receiptRepo ?? throw new ArgumentNullException(nameof(receiptRepo));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _logger.LogInformation("Сервис товаров инициализирован");
        }

        public Inventory GetAllAvailableProducts(int limit, int skip)
        {
            _logger.LogInformation("Запрос доступных товаров. Лимит: {limit}, Отступ: {skip}", limit, skip);

            if (limit <= 0)
            {
                _logger.LogError("Некорректный лимит: {limit}", limit);
                throw new ArgumentOutOfRangeException(nameof(limit));
            }
            if (skip < 0)
            {
                _logger.LogError("Некорректное значение пропуска: {skip}", skip);
                throw new ArgumentOutOfRangeException(nameof(skip));
            }

            try
            {
                var inventory = _inventoryRepo.GetAvailableProduct(limit, skip);
                if (inventory != null)
                    _logger.LogInformation("Успешно получена информация о {count} товарах", inventory.TotalAmount);

                return inventory;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка товаров");
                throw;
            }
        }

        public Receipt? MakePurchase(List<ReceiptLine> items, int customerID)
        {
            _logger.LogInformation("Оформление покупки для клиента {IDКлиента}. Товаров: {КоличествоТоваров}",
                customerID, items.Count);

            if (items == null || items.Count == 0)
            {
                _logger.LogWarning("Попытка оформить покупку без товаров");
                return null;
            }

            try
            {
                Receipt receipt = new Receipt(customerID, items);

                if (_receiptRepo.LoadReceiptItemsSale_UpdateAmount(ref receipt))
                {
                    _logger.LogInformation("Покупка успешно оформлена. Номер чека: {НомерЧека}", receipt.Id);
                    return receipt;
                }

                _logger.LogError("Не удалось оформить покупку для клиента {IDКлиента}", customerID);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при оформлении покупки для клиента {IDКлиента}", customerID);
                throw;
            }
        }

        public Product GetInfoOnProduct(int productId)
        {
            _logger.LogInformation("Запрос информации о товаре {IDТовара}", productId);

            if (productId <= 0)
            {
                _logger.LogError("Некорректный ID товара: {IDТовара}", productId);
                throw new ArgumentException("ID товара не может быть <= 0", nameof(productId));
            }

            try
            {
                var product = _inventoryRepo.GetInfoOnProduct(productId);

                if (product == null)
                {
                    _logger.LogWarning("Товар {IDТовара} не найден", productId);
                }
                else
                {
                    _logger.LogInformation("Найден товар: {IDТовара} - {НазваниеТовара}",
                        product.IdNomenclature, product.Type);
                }

                return product;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о товаре {IDТовара}", productId);
                throw;
            }
        }

        public bool CheckNewAmount(int productId, int newAmount)
        {
            _logger.LogInformation("Проверка доступного количества для товара {IDТовара}. Запрашиваемое количество: {Количество}",
                productId, newAmount);

            if (productId <= 0)
            {
                _logger.LogError("Некорректный ID товара: {IDТовара}", productId);
                throw new ArgumentException("ID товара не может быть <= 0", nameof(productId));
            }

            if (newAmount <= 0)
            {
                _logger.LogError("Некорректное количество: {Количество}", newAmount);
                throw new ArgumentException("Количество не может быть отрицательным", nameof(newAmount));
            }

            try
            {
                bool isAvailable = _inventoryRepo.CheckNewAmount(productId, newAmount);
                _logger.LogInformation("Результат проверки количества для товара {IDТовара}: {Результат}",
                    productId, isAvailable ? "Доступно" : "Недоступно");

                return isAvailable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке количества товара {IDТовара}", productId);
                throw;
            }
        }
    }
}