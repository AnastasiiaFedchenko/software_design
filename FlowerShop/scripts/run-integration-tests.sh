#!/bin/bash

# FlowerShop/scripts/run-integration-tests.sh
echo "Running Integration Tests..."
echo "Note: Make sure PostgreSQL is running on localhost:5432"

if [ -z "$FLOWERSHOP_TELEMETRY_ENABLED" ]; then
    export FLOWERSHOP_TELEMETRY_ENABLED=true
fi
export FLOWERSHOP_TELEMETRY_SERVICE="FlowerShop.Tests.Integration"
export FLOWERSHOP_TELEMETRY_TRACE_PATH="analysis/telemetry/tests-Integration-traces.jsonl"
export FLOWERSHOP_TELEMETRY_METRICS_PATH="analysis/telemetry/tests-Integration-metrics.jsonl"

# Check if PostgreSQL is running
if ! pg_isready -h localhost -p 5432 -U postgres > /dev/null 2>&1; then
    echo "PostgreSQL is not running on localhost:5432"
    echo "Start PostgreSQL first: pg_ctl start -D /path/to/your/postgres/data"
    exit 1
fi

dotnet test --filter "Category=Integration" --configuration Release --logger "trx" --results-directory "TestResults/Integration"

if [ $? -eq 0 ]; then
    echo "Integration tests passed!"
else
    echo "Integration tests failed!"
    exit 1
fi
