#!/bin/bash

# FlowerShop/scripts/run-tests.sh
echo "Starting test execution from: $(pwd)"
echo ""

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Function to run tests with error handling
run_test() {
    local category=$1
    local description=$2

    if [ -z "$FLOWERSHOP_TELEMETRY_ENABLED" ]; then
        export FLOWERSHOP_TELEMETRY_ENABLED=true
    fi
    export FLOWERSHOP_TELEMETRY_SERVICE="FlowerShop.Tests.$category"
    export FLOWERSHOP_TELEMETRY_TRACE_PATH="analysis/telemetry/tests-${category}-traces.jsonl"
    export FLOWERSHOP_TELEMETRY_METRICS_PATH="analysis/telemetry/tests-${category}-metrics.jsonl"
    
    echo -e "${YELLOW}=== $description ===${NC}"
    echo "Running: dotnet test --filter Category=$category --configuration Release --logger \"trx\" --results-directory TestResults/$category"
    
    dotnet test --filter "Category=$category" --configuration Release --logger "trx" --results-directory "TestResults/$category"
    
    local exit_code=$?
    if [ $exit_code -eq 0 ]; then
        echo -e "${GREEN}✓ $description passed!${NC}"
        return 0
    else
        echo -e "${RED}✗ $description failed!${NC}"
        return $exit_code
    fi
}

# Create test results directory
mkdir -p TestResults

# Step 1: Unit Tests
run_test "Unit" "Unit Tests"
if [ $? -eq 0 ]; then
    # Step 2: Integration Tests
    echo ""
    echo -e "${CYAN}Note: Make sure PostgreSQL is running on localhost:5432${NC}"
    echo -e "${CYAN}Use: pg_ctl start -D /path/to/your/postgres/data${NC}"
    
    run_test "Integration" "Integration Tests"
    if [ $? -eq 0 ]; then
        # Step 3: E2E Tests
        echo ""
        echo -e "${CYAN}Note: Make sure the application is running on http://localhost:5031${NC}"
        echo -e "${CYAN}Use: dotnet run --project WebApp --urls=http://localhost:5031${NC}"
        
        run_test "E2E" "E2E Tests"
        if [ $? -eq 0 ]; then
            echo ""
            echo -e "${GREEN} All tests passed!${NC}"
        else
            echo -e "${RED} E2E tests failed!${NC}"
            exit 1
        fi
    else
        echo -e "${RED} Integration tests failed! Skipping E2E tests.${NC}"
        exit 1
    fi
else
    echo -e "${RED} Unit tests failed! Skipping integration and E2E tests.${NC}"
    exit 1
fi

echo ""
echo -e "${CYAN}Test results saved to: TestResults/${NC}"
