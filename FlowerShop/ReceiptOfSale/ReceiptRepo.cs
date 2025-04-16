using System;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;

namespace ReceiptOfSale
{
    public class ReceiptRepo : IReceiptRepo
    {
        public Receipt create(List<ReceiptLine> items, User customer)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("Items list cannot be empty", nameof(items));
            }

            if (customer == null)
            {
                throw new ArgumentNullException(nameof(customer));
            }

            var finalPrice = 0.0;
            var totalAmount = 0;

            foreach (var item in items)
            {
                finalPrice += item.Product.Price * item.Amount;
                totalAmount += item.Amount;
            }

            return new Receipt(
                id: Guid.NewGuid(),
                customer: customer,
                final_price: finalPrice,
                date: DateTime.Now,
                total_amount: totalAmount,
                products: items
            );
        }
    }
}