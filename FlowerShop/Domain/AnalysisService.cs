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
        private readonly IForecastServiceAdapter _forecast;
        private readonly IUserSegmentationServiceAdapter _segment;

        public AnalysisService(IForecastServiceAdapter forecast, IUserSegmentationServiceAdapter segment)
        {
            _forecast = forecast;
            _segment = segment;
        }

        public ForecastOfOrders GetForecastOfOrders() // опять таки в каком формате это возвращать
        {
            return _forecast.Create();
        }
        public List<UserSegment> GetUserSegmentation()
        {
            return _segment.create();
        }
    }
}
