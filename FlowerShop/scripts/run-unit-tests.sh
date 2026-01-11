#!/bin/bash

# FlowerShop/scripts/run-unit-tests.sh
echo "Running Unit Tests only..."

if [ -z "$FLOWERSHOP_TELEMETRY_ENABLED" ]; then
    export FLOWERSHOP_TELEMETRY_ENABLED=true
fi
export FLOWERSHOP_TELEMETRY_SERVICE="FlowerShop.Tests.Unit"
export FLOWERSHOP_TELEMETRY_TRACE_PATH="analysis/telemetry/tests-Unit-traces.jsonl"
export FLOWERSHOP_TELEMETRY_METRICS_PATH="analysis/telemetry/tests-Unit-metrics.jsonl"

dotnet test --filter "Category=Unit" --configuration Release --logger "trx" --results-directory "TestResults/Unit"

if [ $? -eq 0 ]; then
    echo " Unit tests passed!"
else
    echo " Unit tests failed!"
    exit 1
fi
