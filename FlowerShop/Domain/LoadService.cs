using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;
using Domain.InputPorts;
using Domain.OutputPorts;

namespace Domain
{
    public class LoadService: ILoadService
    {
        private readonly IProductBatchReader _product_batch_reader;
        private readonly IProductBatchLoader _product_batch_loader;
        
        public LoadService(IProductBatchReader product_batch_reader, IProductBatchLoader product_batch_loader)
        {
            _product_batch_reader = product_batch_reader;
            _product_batch_loader = product_batch_loader;
        }
        public bool LoadProductBatch(FileStream stream) // сюда тоже по идее передаётся файл
        {
            ProductBatch batch = _product_batch_reader.create(stream);
            Console.WriteLine("Needs to be loaded");
            for (int i = 0; i < batch.Products.Count; i++)
                Console.WriteLine($"{0} {1}", i, batch.Products[i].Product.Nomenclature, batch.Products[i].Product.Amount);

            return _product_batch_loader.load(batch);
        }
    }
}
