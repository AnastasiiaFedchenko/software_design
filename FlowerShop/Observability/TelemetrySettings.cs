using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace Observability;

public sealed class TelemetrySettings
{
    public bool Enabled { get; init; }
    public string ServiceName { get; init; } = "FlowerShop";
    public string TraceFilePath { get; init; } = string.Empty;
    public string MetricsFilePath { get; init; } = string.Empty;
    public string Exporter { get; init; } = "file";
    public string JaegerHost { get; init; } = "127.0.0.1";
    public int JaegerPort { get; init; } = 6831;
    public string OtlpEndpoint { get; init; } = "http://127.0.0.1:4318/v1/traces";

    public static TelemetrySettings FromConfiguration(IConfiguration configuration, string serviceName)
    {
        var enabledValue = configuration["Telemetry:Enabled"];
        var enabled = GetBool("FLOWERSHOP_TELEMETRY_ENABLED", enabledValue);

        var tracePath = GetSetting(configuration, "FLOWERSHOP_TELEMETRY_TRACE_PATH", "Telemetry:TraceFilePath");
        var metricsPath = GetSetting(configuration, "FLOWERSHOP_TELEMETRY_METRICS_PATH", "Telemetry:MetricsFilePath");

        var resolvedService = GetSettingOrDefault(
            configuration,
            "FLOWERSHOP_TELEMETRY_SERVICE",
            "Telemetry:ServiceName",
            serviceName);
        var exporter = GetSettingOrDefault(
            configuration,
            "FLOWERSHOP_TELEMETRY_EXPORTER",
            "Telemetry:Exporter",
            "file");
        var jaegerHost = GetSettingOrDefault(
            configuration,
            "FLOWERSHOP_JAEGER_HOST",
            "Telemetry:Jaeger:Host",
            "127.0.0.1");
        var jaegerPortValue = GetSetting(configuration, "FLOWERSHOP_JAEGER_PORT", "Telemetry:Jaeger:Port");
        var otlpEndpoint = GetSettingOrDefault(
            configuration,
            "FLOWERSHOP_OTLP_ENDPOINT",
            "Telemetry:Otlp:Endpoint",
            "http://127.0.0.1:4318/v1/traces");

        return new TelemetrySettings
        {
            Enabled = enabled,
            ServiceName = resolvedService,
            TraceFilePath = ResolvePath(tracePath, resolvedService, "traces"),
            MetricsFilePath = ResolvePath(metricsPath, resolvedService, "metrics"),
            Exporter = exporter,
            JaegerHost = jaegerHost,
            JaegerPort = ParsePort(jaegerPortValue, 6831),
            OtlpEndpoint = otlpEndpoint,
        };
    }

    public static TelemetrySettings FromEnvironment(string serviceName)
    {
        var enabled = GetBool("FLOWERSHOP_TELEMETRY_ENABLED", null);
        var tracePath = Environment.GetEnvironmentVariable("FLOWERSHOP_TELEMETRY_TRACE_PATH");
        var metricsPath = Environment.GetEnvironmentVariable("FLOWERSHOP_TELEMETRY_METRICS_PATH");
        var resolvedService = Environment.GetEnvironmentVariable("FLOWERSHOP_TELEMETRY_SERVICE") ?? serviceName;
        var exporter = Environment.GetEnvironmentVariable("FLOWERSHOP_TELEMETRY_EXPORTER") ?? "file";
        var jaegerHost = Environment.GetEnvironmentVariable("FLOWERSHOP_JAEGER_HOST") ?? "127.0.0.1";
        var jaegerPortValue = Environment.GetEnvironmentVariable("FLOWERSHOP_JAEGER_PORT");
        var otlpEndpoint = Environment.GetEnvironmentVariable("FLOWERSHOP_OTLP_ENDPOINT")
                           ?? "http://127.0.0.1:4318/v1/traces";

        return new TelemetrySettings
        {
            Enabled = enabled,
            ServiceName = resolvedService,
            TraceFilePath = ResolvePath(tracePath, resolvedService, "traces"),
            MetricsFilePath = ResolvePath(metricsPath, resolvedService, "metrics"),
            Exporter = exporter,
            JaegerHost = jaegerHost,
            JaegerPort = ParsePort(jaegerPortValue, 6831),
            OtlpEndpoint = otlpEndpoint,
        };
    }

    private static string? GetSetting(IConfiguration configuration, string envVar, string configKey)
    {
        return Environment.GetEnvironmentVariable(envVar) ?? configuration[configKey];
    }

    private static string GetSettingOrDefault(
        IConfiguration configuration,
        string envVar,
        string configKey,
        string defaultValue)
    {
        return GetSetting(configuration, envVar, configKey) ?? defaultValue;
    }

    private static bool GetBool(string envVar, string? configValue)
    {
        var value = Environment.GetEnvironmentVariable(envVar) ?? configValue;
        return bool.TryParse(value, out var parsed) && parsed;
    }

    private static string ResolvePath(string? explicitPath, string serviceName, string suffix)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var safeName = SanitizeFileName(serviceName);
        var basePath = Path.Combine("analysis", "telemetry");
        return Path.Combine(basePath, $"{safeName}-{suffix}.jsonl");
    }

    private static int ParsePort(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = value;
        foreach (var ch in invalid)
        {
            sanitized = sanitized.Replace(ch, '_');
        }

        return sanitized.Replace(' ', '_');
    }
}
