using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.OutputPorts
{
    public interface IProductBatchLoader
    {
        // как вообще происходит загрузка файла на сервер
        bool Load(ProductBatch batch);
    }
}
