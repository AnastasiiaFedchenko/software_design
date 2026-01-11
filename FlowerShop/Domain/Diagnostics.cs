using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Domain;

public static class Diagnostics
{
    public const string ActivitySourceName = "FlowerShop.Domain";
    public const string MeterName = "FlowerShop.Domain";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> OperationCounter =
        Meter.CreateCounter<long>("domain.operations");

    public static void RecordOperation(string operation)
    {
        OperationCounter.Add(1, new KeyValuePair<string, object?>("operation", operation));
    }
}
