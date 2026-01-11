using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Observability;

public sealed class FileActivityExporter : BaseExporter<Activity>
{
    private readonly string _path;
    private readonly object _sync = new();

    public FileActivityExporter(string path)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        EnsureDirectory(_path);
    }

    public override ExportResult Export(in Batch<Activity> batch)
    {
        if (batch.Count == 0)
        {
            return ExportResult.Success;
        }

        lock (_sync)
        {
            using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);

            foreach (var activity in batch)
            {
                var record = new ActivityExportRecord
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    TraceId = activity.TraceId.ToString(),
                    SpanId = activity.SpanId.ToString(),
                    ParentSpanId = activity.ParentSpanId.ToString(),
                    Name = activity.DisplayName,
                    Kind = activity.Kind.ToString(),
                    DurationMs = activity.Duration.TotalMilliseconds,
                    Status = activity.Status.ToString(),
                    Tags = FormatTags(activity.TagObjects),
                };

                var json = JsonSerializer.Serialize(record);
                writer.WriteLine(json);
            }
        }

        return ExportResult.Success;
    }

    private static Dictionary<string, string?> FormatTags(IEnumerable<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            result[tag.Key] = FormatTagValue(tag.Value);
        }

        return result;
    }

    private static string? FormatTagValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static void EnsureDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed class ActivityExportRecord
    {
        public DateTimeOffset TimestampUtc { get; init; }
        public string TraceId { get; init; } = string.Empty;
        public string SpanId { get; init; } = string.Empty;
        public string ParentSpanId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public double DurationMs { get; init; }
        public string Status { get; init; } = string.Empty;
        public Dictionary<string, string?> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
