#!/bin/bash

if [ -z "$FLOWERSHOP_TELEMETRY_ENABLED" ]; then
    export FLOWERSHOP_TELEMETRY_ENABLED=true
fi

if [ -z "$FLOWERSHOP_TELEMETRY_SERVICE" ]; then
    export FLOWERSHOP_TELEMETRY_SERVICE="FlowerShop.Benchmarks"
fi

if [ -z "$FLOWERSHOP_TELEMETRY_TRACE_PATH" ]; then
    export FLOWERSHOP_TELEMETRY_TRACE_PATH="analysis/telemetry/benchmarks-traces.jsonl"
fi

if [ -z "$FLOWERSHOP_TELEMETRY_METRICS_PATH" ]; then
    export FLOWERSHOP_TELEMETRY_METRICS_PATH="analysis/telemetry/benchmarks-metrics.jsonl"
fi

if [ -z "$FLOWERSHOP_LOGGING_PROFILE" ]; then
    export FLOWERSHOP_LOGGING_PROFILE="Default"
fi

dotnet run -c Release --project Benchmarks
