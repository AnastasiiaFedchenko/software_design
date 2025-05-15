using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.InputPorts
{
    public interface IProductService
    {
        Inventory GetAllAvailableProducts(int limit, int skip); // в каком формате вот это вернуть List<ProductLine>
        Receipt MakePurchase(List<ReceiptLine> items, int customerID); // сюда по идее передаётся корзина, но в каком формате List<ReceiptLine>  
        public Product GetInfoOnProduct(int productID);
        bool CheckNewAmount(int product_id, int new_n); // есть ли у меня реально id продукта
    }
}
