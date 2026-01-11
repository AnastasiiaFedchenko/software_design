using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Observability;

public sealed class FileMetricExporter : BaseExporter<Metric>
{
    private readonly string _path;
    private readonly object _sync = new();

    public FileMetricExporter(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        EnsureDirectory(_path);
    }

    public override ExportResult Export(in Batch<Metric> batch)
    {
        if (batch.Count == 0)
        {
            return ExportResult.Success;
        }

        lock (_sync)
        {
            using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);

            foreach (var metric in batch)
            {
                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    var record = new MetricExportRecord
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Name = metric.Name,
                        Unit = metric.Unit,
                        MetricType = metric.MetricType.ToString(),
                        Tags = FormatTags(metricPoint.Tags),
                        Value = FormatMetricValue(metric.MetricType, metricPoint),
                    };

                    var json = JsonSerializer.Serialize(record);
                    writer.WriteLine(json);
                }
            }
        }

        return ExportResult.Success;
    }

    private static Dictionary<string, string?> FormatTags(ReadOnlyTagCollection tags)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            result[tag.Key] = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
        }

        return result;
    }

    private static string FormatMetricValue(MetricType metricType, in MetricPoint metricPoint)
    {
        return metricType switch
        {
            MetricType.LongSum => metricPoint.GetSumLong().ToString(CultureInfo.InvariantCulture),
            MetricType.DoubleSum => metricPoint.GetSumDouble().ToString(CultureInfo.InvariantCulture),
            MetricType.LongGauge => metricPoint.GetGaugeLastValueLong().ToString(CultureInfo.InvariantCulture),
            MetricType.DoubleGauge => metricPoint.GetGaugeLastValueDouble().ToString(CultureInfo.InvariantCulture),
            MetricType.Histogram => FormatHistogram(metricPoint),
            _ => "unsupported"
        };
    }

    private static string FormatHistogram(in MetricPoint metricPoint)
    {
        var sum = metricPoint.GetHistogramSum();
        var count = metricPoint.GetHistogramCount();
        return $"count={count.ToString(CultureInfo.InvariantCulture)},sum={sum.ToString(CultureInfo.InvariantCulture)}";
    }

    private static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed class MetricExportRecord
    {
        public DateTimeOffset TimestampUtc { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Unit { get; init; }
        public string MetricType { get; init; } = string.Empty;
        public string Value { get; init; } = string.Empty;
        public Dictionary<string, string?> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
