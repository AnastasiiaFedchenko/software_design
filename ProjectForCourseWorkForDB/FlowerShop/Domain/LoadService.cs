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
            for (int i = 0; i < batch.ProductsInfo.Count; i++)
            {
                ProductInfo p = batch.ProductsInfo[i];
                var sb = new StringBuilder();
                sb.AppendLine("╔════════════════╦═════════════════════════╦════════════════╦══════════════╦════════════════╗");
                sb.AppendLine("║ Номенклатура   ║ Даты (произв./годен)    ║ Количество     ║ Цена         ║ Срок годности  ║");
                sb.AppendLine("╠════════════════╬═════════════════════════╬════════════════╬══════════════╬════════════════╣");
                sb.AppendLine($"║ {p.IdNomenclature.ToString().PadRight(14)} ║ {p.ProductionDate:dd.MM.yyyy} / {p.ExpirationDate:dd.MM.yyyy} ║ {p.Amount.ToString().PadRight(14)} ║ {p.CostPrice:F2} руб. ║ {(p.ExpirationDate - DateTime.Today).Days} дней       ║");
                sb.AppendLine("╚════════════════╩═════════════════════════╩════════════════╩══════════════╩════════════════╝");
                Console.WriteLine(sb.ToString());
            }


            return _product_batch_loader.load(batch);
        }
    }
}
