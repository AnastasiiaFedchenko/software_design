using Domain;
using Domain.OutputPorts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ForecastAnalysis
{
    public class Forecast: IForecast
    {
        public ForecastOfOrders create()
        {
            return new ForecastOfOrders(0, 0, new List<ProductLine>());
        }
    }
}
