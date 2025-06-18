using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Mail;

namespace Domain.InputPorts
{
    public interface ILoadService
    {
        public bool LoadProductBatch(Stream stream); // FileStream / может быть массив строк
    }
}
