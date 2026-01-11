using System;
using System.Diagnostics;
using System.Collections.Generic;
using Domain.InputPorts;
using Domain.OutputPorts;
using Microsoft.Extensions.Logging;
using SegmentAnalysis;

namespace Domain
{
    public class AnalysisService : IAnalysisService
    {
        private readonly IForecastServiceAdapter _forecast;
        private readonly IUserSegmentationServiceAdapter _segment;
        private readonly ILogger<AnalysisService> _logger;

        public AnalysisService(
            IForecastServiceAdapter forecast,
            IUserSegmentationServiceAdapter segment,
            ILogger<AnalysisService> logger)
        {
            _forecast = forecast ?? throw new ArgumentNullException(nameof(forecast));
            _segment = segment ?? throw new ArgumentNullException(nameof(segment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ForecastOfOrders GetForecastOfOrders()
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("AnalysisService.GetForecastOfOrders");
            Diagnostics.RecordOperation("AnalysisService.GetForecastOfOrders");

            _logger.LogInformation("Запрос прогноза заказов");

            try
            {
                var forecast = _forecast.Create();
                _logger.LogInformation(
                    "Успешно получен прогноз заказов. Всего заказов: {TotalOrders}, Продуктов: {ProductsCount}, Дней прогноза: {DaysCount}",
                    forecast.AmountOfOrders,
                    forecast.Products?.Count ?? 0,
                    forecast.DailyForecast?.Count ?? 0);

                return forecast;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Ошибка при получении прогноза заказов");
                throw;
            }
        }

        public List<UserSegment> GetUserSegmentation()
        {
            using var activity = Diagnostics.ActivitySource.StartActivity("AnalysisService.GetUserSegmentation");
            Diagnostics.RecordOperation("AnalysisService.GetUserSegmentation");

            _logger.LogInformation("Запрос сегментации пользователей");

            try
            {
                var segments = _segment.Create();
                _logger.LogInformation(
                    "Успешно получена сегментация пользователей. Всего сегментов: {SegmentsCount}, Всего пользователей: {TotalUsers}",
                    segments.Count,
                    segments.Sum(s => s.Users?.Count ?? 0));

                return segments;
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                _logger.LogError(ex, "Ошибка при получении сегментации пользователей");
                throw;
            }
        }
    }
}
