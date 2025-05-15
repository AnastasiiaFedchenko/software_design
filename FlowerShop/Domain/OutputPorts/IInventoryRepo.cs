using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.OutputPorts
{
    public interface IInventoryRepo
    {
        Inventory GetAvailableProduct(int limit, int skip);
        bool CheckNewAmount(int product_id, int new_n);
        public Product GetInfoOnProduct(int productID);
    }
}
