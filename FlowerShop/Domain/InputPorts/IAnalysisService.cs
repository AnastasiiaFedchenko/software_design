using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.InputPorts
{
    public interface IAnalysisService
    {
        public ForecastOfOrders GetForecastOfOrders(); // опять таки в каком формате это возвращать
        public List<UserSegment> GetUserSegmentation();
    }
}
