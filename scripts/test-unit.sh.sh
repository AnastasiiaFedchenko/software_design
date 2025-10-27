#!/bin/bash
set -e

echo "🔬 Running unit tests in Docker..."

docker run --rm \
  -v $(pwd)/TestResults:/src/TestResults \
  flowershop-tests \
  dotnet test Tests/UnitTests/ --filter Category=Unit --configuration Release --verbosity normal \
  --logger "trx;LogFileName=unit-test-results.trx" \
  --results-directory TestResults/Unit

echo "✅ Unit tests completed"