using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.OutputPorts
{
    public interface IInventoryRepo
    {
        Inventory create();
        bool check_new_amount(Guid product_id, int new_n);
    }
}
