using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain;
using Domain.OutputPorts;

namespace ProductBatchLoading
{
    public class ProductBatchLoader: IProductBatchLoader
    {
        public bool load(ProductBatch batch)
        {
            return true;
        }
    }
}
