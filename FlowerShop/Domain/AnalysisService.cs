using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain.InputPorts;
using Domain.OutputPorts;

namespace Domain
{
    public class AnalysisService: IAnalysisService
    {
        private readonly IForecast _forecast;
        private readonly ISegment _segment;

        public AnalysisService(IForecast forecast, ISegment segment)
        {
            _forecast = forecast;
            _segment = segment;
        }

        public ForecastOfOrders GetForecastOfOrders() // опять таки в каком формате это возвращать
        {
            return _forecast.create();
        }
        public List<UserSegment> GetUserSegmentation()
        {
            return _segment.create();
        }
    }
}
