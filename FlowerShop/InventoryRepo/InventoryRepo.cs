using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Domain;
using Domain.OutputPorts;

namespace InventoryOfProducts
{
    public class InventoryRepo: IInventoryRepo
    {
        public Inventory create()
        {
            // Создаем заглушки для всех необходимых объектов
            var emptyUser = new User(Guid.Empty, "Unknown");
            var emptyProduct = new Product(
                nomenclature: Guid.NewGuid(),
                amount: 0,
                price: 0.0,
                amount_in_stock: 0,
                type: "Undefined",
                country: "Unknown"
            );

            var emptyProductLine = new ProductLine(emptyProduct, 0);
            var emptyProductLines = new List<ProductLine> { emptyProductLine };

            return new Inventory(
                id: Guid.NewGuid(),
                date: DateTime.Now,
                supplier: emptyUser,
                responsible: emptyUser,
                total_amount: 0,
                products: emptyProductLines
            );
        }
        public bool check_new_amount(Guid product_id, int new_n)
        {
            return true;
        }
    }
}
