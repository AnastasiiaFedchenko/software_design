using System;
using System.Collections.Generic;
using Domain.InputPorts;
using Domain.OutputPorts;

namespace Domain
{
    public class ProductService : IProductService
    {
        private readonly IInventoryRepo _inventoryRepo;
        private readonly IReceiptRepo _receiptRepo;

        public ProductService(IInventoryRepo inventoryRepo, IReceiptRepo receiptRepo)
        {
            _inventoryRepo = inventoryRepo ?? throw new ArgumentNullException(nameof(inventoryRepo));
            _receiptRepo = receiptRepo ?? throw new ArgumentNullException(nameof(receiptRepo));
        }

        public Inventory GetAllAvailableProducts(int limit, int skip)
        {
            return _inventoryRepo.GetAvailableProduct(limit, skip);
        }

        public Receipt MakePurchase(List<ReceiptLine> items, User customer)
        {
            Receipt receipt = new Receipt(customer, items);

            return receipt;
        }

        public bool CheckNewAmount(Guid productId, int newAmount)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException("Product ID cannot be empty", nameof(productId));

            if (newAmount <= 0)
                throw new ArgumentException("Amount cannot be negative", nameof(newAmount));

            return _inventoryRepo.CheckNewAmount(productId, newAmount);
        }
    }
}