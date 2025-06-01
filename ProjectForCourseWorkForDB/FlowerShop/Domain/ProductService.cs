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

        public Receipt MakePurchase(List<ReceiptLine> items, int customerID)
        {
            Receipt receipt = new Receipt(customerID, items);
            _receiptRepo.load(ref receipt);
            return receipt;
        }
        public Product GetInfoOnProduct(int productId)
        {
            return _inventoryRepo.GetInfoOnProduct(productId);
        }

        public bool CheckNewAmount(int productId, int newAmount)
        {
            if (productId == 0)
                throw new ArgumentException("Product ID cannot be empty", nameof(productId));

            if (newAmount <= 0)
                throw new ArgumentException("Amount cannot be negative", nameof(newAmount));

            return _inventoryRepo.CheckNewAmount(productId, newAmount);
        }
    }
}