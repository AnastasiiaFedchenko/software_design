using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Observability;

public sealed class TelemetryHandle : IDisposable
{
    private readonly TracerProvider? _tracerProvider;
    private readonly MeterProvider? _meterProvider;
    private readonly ActivityFileListener? _activityListener;

    public TelemetryHandle(
        TracerProvider? tracerProvider,
        MeterProvider? meterProvider,
        ActivityFileListener? activityListener)
    {
        _tracerProvider = tracerProvider;
        _meterProvider = meterProvider;
        _activityListener = activityListener;
    }

    public void Dispose()
    {
        _activityListener?.Dispose();
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
    }
}

public static class TelemetryBootstrap
{
    public static void ConfigureOpenTelemetry(
        IServiceCollection services,
        TelemetrySettings settings,
        IEnumerable<string> activitySources,
        IEnumerable<string> meters,
        bool includeAspNetCore,
        bool includeHttpClient,
        bool includeRuntimeMetrics)
    {
        _ = includeAspNetCore;
        _ = includeHttpClient;
        _ = includeRuntimeMetrics;

        if (!settings.Enabled)
        {
            return;
        }

        var otelBuilder = services.AddOpenTelemetry();
        ConfigureTracing(
            otelBuilder,
            settings,
            activitySources,
            includeAspNetCore,
            includeHttpClient);
        ConfigureMetrics(otelBuilder, settings, meters);
    }

    public static TelemetryHandle StartSdk(
        TelemetrySettings settings,
        IEnumerable<string> activitySources,
        IEnumerable<string> meters,
        bool includeRuntimeMetrics)
    {
        _ = includeRuntimeMetrics;

        if (!settings.Enabled)
        {
            return new TelemetryHandle(null, null, null);
        }

        var resourceBuilder = ResourceBuilder.CreateDefault().AddService(settings.ServiceName);
        var supportsTracing = TraceStateSupported();
        var useJaeger = IsJaeger(settings) && supportsTracing;
        var useOtlp = IsOtlp(settings) && supportsTracing;

        TracerProvider? tracerProvider = null;
        ActivityFileListener? activityListener = null;
        if (supportsTracing)
        {
            var tracerBuilder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder);

            foreach (var source in activitySources)
            {
                tracerBuilder.AddSource(source);
            }

            if (useOtlp)
            {
                tracerBuilder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(settings.OtlpEndpoint);
                    options.Protocol = OtlpExportProtocol.HttpProtobuf;
                });
            }
            else if (useJaeger)
            {
                tracerBuilder.AddJaegerExporter(options =>
                {
                    options.AgentHost = settings.JaegerHost;
                    options.AgentPort = settings.JaegerPort;
                });
            }
            else
            {
                tracerBuilder.AddProcessor(new SimpleActivityExportProcessor(
                    new FileActivityExporter(settings.TraceFilePath)));
            }

            tracerProvider = tracerBuilder.Build();
        }
        else
        {
            activityListener = new ActivityFileListener(settings.TraceFilePath, activitySources);
        }

        var meterBuilder = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder);

        foreach (var meter in meters)
        {
            meterBuilder.AddMeter(meter);
        }

        meterBuilder.AddReader(new PeriodicExportingMetricReader(
            new FileMetricExporter(settings.MetricsFilePath)));

        return new TelemetryHandle(tracerProvider, meterBuilder.Build(), activityListener);
    }

    private static bool IsJaeger(TelemetrySettings settings)
    {
        return string.Equals(settings.Exporter, "jaeger", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOtlp(TelemetrySettings settings)
    {
        return string.Equals(settings.Exporter, "otlp", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConfigureTracing(
        IOpenTelemetryBuilder otelBuilder,
        TelemetrySettings settings,
        IEnumerable<string> activitySources,
        bool includeAspNetCore,
        bool includeHttpClient)
    {
        if (!TraceStateSupported())
        {
            _ = new ActivityFileListener(settings.TraceFilePath, activitySources);
            return;
        }

        otelBuilder.WithTracing(builder =>
        {
            builder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(settings.ServiceName));
            foreach (var source in activitySources)
            {
                builder.AddSource(source);
            }

            if (includeAspNetCore)
            {
                builder.AddAspNetCoreInstrumentation();
            }

            if (includeHttpClient)
            {
                builder.AddHttpClientInstrumentation();
            }

            ConfigureTracingExporter(builder, settings);
        });
    }

    private static void ConfigureTracingExporter(
        TracerProviderBuilder builder,
        TelemetrySettings settings)
    {
        if (IsOtlp(settings))
        {
            builder.AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri(settings.OtlpEndpoint);
                options.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
            return;
        }

        if (IsJaeger(settings))
        {
            builder.AddJaegerExporter(options =>
            {
                options.AgentHost = settings.JaegerHost;
                options.AgentPort = settings.JaegerPort;
            });
            return;
        }

        builder.AddProcessor(new SimpleActivityExportProcessor(
            new FileActivityExporter(settings.TraceFilePath)));
    }

    private static void ConfigureMetrics(
        IOpenTelemetryBuilder otelBuilder,
        TelemetrySettings settings,
        IEnumerable<string> meters)
    {
        otelBuilder.WithMetrics(builder =>
        {
            builder.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(settings.ServiceName));
            foreach (var meter in meters)
            {
                builder.AddMeter(meter);
            }

            builder.AddReader(new PeriodicExportingMetricReader(
                new FileMetricExporter(settings.MetricsFilePath)));
        });
    }

    private static bool TraceStateSupported()
    {
        if (Environment.Version.Major < 8)
        {
            return false;
        }

        var optionsType = typeof(ActivityCreationOptions<ActivityContext>);
        var traceStateProperty = optionsType.GetProperty("TraceState");
        return traceStateProperty?.CanWrite == true;
    }
}
