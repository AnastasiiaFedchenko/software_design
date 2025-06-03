using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.OutputPorts
{
    public interface IReceiptRepo
    {
        bool LoadReceiptItemsSale_UpdateAmount(ref Receipt receipt); // один вызов тк одна транзакция
    }
}
