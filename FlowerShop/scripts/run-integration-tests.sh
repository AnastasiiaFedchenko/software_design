#!/bin/bash

# FlowerShop/scripts/run-integration-tests.sh
echo "Running Integration Tests..."
echo "Note: Make sure PostgreSQL is running on localhost:5432"

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