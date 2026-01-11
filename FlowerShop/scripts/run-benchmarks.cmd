@echo off
setlocal

if "%FLOWERSHOP_TELEMETRY_ENABLED%"=="" set FLOWERSHOP_TELEMETRY_ENABLED=true
if "%FLOWERSHOP_TELEMETRY_SERVICE%"=="" set FLOWERSHOP_TELEMETRY_SERVICE=FlowerShop.Benchmarks
if "%FLOWERSHOP_TELEMETRY_TRACE_PATH%"=="" set FLOWERSHOP_TELEMETRY_TRACE_PATH=analysis\telemetry\benchmarks-traces.jsonl
if "%FLOWERSHOP_TELEMETRY_METRICS_PATH%"=="" set FLOWERSHOP_TELEMETRY_METRICS_PATH=analysis\telemetry\benchmarks-metrics.jsonl
if "%FLOWERSHOP_LOGGING_PROFILE%"=="" set FLOWERSHOP_LOGGING_PROFILE=Default

dotnet run -c Release --project Benchmarks

endlocal
