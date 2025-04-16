using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.InputPorts
{
    public interface IProductService
    {
        Inventory GetAllAvailableProducts(); // в каком формате вот это вернуть List<ProductLine>
        Receipt MakePurchase(List<ReceiptLine> items, User customer); // сюда по идее передаётся корзина, но в каком формате List<ReceiptLine>  
        bool CheckNewAmount(Guid product_id, int new_n); // есть ли у меня реально id продукта
    }
}
