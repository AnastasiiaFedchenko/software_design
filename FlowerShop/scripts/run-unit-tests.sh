#!/bin/bash

# FlowerShop/scripts/run-unit-tests.sh
echo "Running Unit Tests only..."

dotnet test --filter "Category=Unit" --configuration Release --logger "trx" --results-directory "TestResults/Unit"

if [ $? -eq 0 ]; then
    echo " Unit tests passed!"
else
    echo " Unit tests failed!"
    exit 1
fi