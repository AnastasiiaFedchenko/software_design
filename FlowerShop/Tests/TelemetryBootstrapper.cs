using System;
using System.Runtime.CompilerServices;
using Domain;
using Observability;

namespace Domain.Tests;

internal static class TelemetryBootstrapper
{
    private static TelemetryHandle? _handle;

    [ModuleInitializer]
    public static void Initialize()
    {
        var settings = TelemetrySettings.FromEnvironment("FlowerShop.Tests");
        if (!settings.Enabled)
        {
            return;
        }

        _handle = TelemetryBootstrap.StartSdk(
            settings,
            new[] { Diagnostics.ActivitySourceName },
            new[] { Diagnostics.MeterName },
            includeRuntimeMetrics: true);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => _handle?.Dispose();
    }
}
