#!/bin/bash

# FlowerShop/scripts/run-e2e-tests.sh
echo "Running E2E Tests..."
echo "Note: Make sure the application is running on http://localhost:5031"

# Simple check if application might be running
if curl -f http://localhost:5031/health > /dev/null 2>&1 || \
   curl -f http://localhost:5031/Account/Login > /dev/null 2>&1; then
    echo "Application seems to be running"
else
    echo "Application might not be running on http://localhost:5031"
    echo "Start it with: dotnet run --project WebApp --urls=http://localhost:5031"
fi

dotnet test --filter "Category=E2E" --configuration Release --logger "trx" --results-directory "TestResults/E2E"

if [ $? -eq 0 ]; then
    echo "E2E tests passed!"
else
    echo "E2E tests failed!"
    exit 1
fi