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

        public Inventory GetAllAvailableProducts()
        {
            return _inventoryRepo.create();
        }

        public Receipt MakePurchase(List<ReceiptLine> items, User customer)
        {
            if (items == null)
                throw new ArgumentNullException(nameof(items), "Items list cannot be null");

            if (items.Count == 0)
                throw new ArgumentException("Items list cannot be empty", nameof(items));

            if (customer == null)
                throw new ArgumentNullException(nameof(customer), "Customer cannot be null");

            // Additional validation for each item
            foreach (var item in items)
            {
                if (item == null)
                    throw new ArgumentException("Receipt line cannot be null", nameof(items));

                if (item.Product == null)
                    throw new ArgumentException("Product cannot be null", nameof(items));

                if (item.Amount <= 0)
                    throw new ArgumentException("Amount must be greater than zero", nameof(items));
            }

            return _receiptRepo.create(items, customer);
        }

        public bool CheckNewAmount(Guid productId, int newAmount)
        {
            if (productId == Guid.Empty)
                throw new ArgumentException("Product ID cannot be empty", nameof(productId));

            if (newAmount < 0)
                throw new ArgumentException("Amount cannot be negative", nameof(newAmount));

            return _inventoryRepo.check_new_amount(productId, newAmount);
        }
    }
}