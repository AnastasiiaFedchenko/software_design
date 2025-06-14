using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.OutputPorts
{
    public interface IProductBatchReader
    {
        // как вообще происходит загрузка файла на сервер
        ProductBatch create(Stream stream); // тут по идее должен передаваться файл
    }
}
