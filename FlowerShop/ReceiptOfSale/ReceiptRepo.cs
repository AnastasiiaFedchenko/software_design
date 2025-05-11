using System;
using System.Collections.Generic;
using Domain;
using Domain.OutputPorts;

namespace ReceiptOfSale
{
    public class ReceiptRepo : IReceiptRepo
    {
        public bool load(Receipt receipt)
        {
            // тут загрузка в бд
            return true;
        }
    }
}