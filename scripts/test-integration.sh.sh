#!/bin/bash
set -e

echo "🧩 Starting PostgreSQL for integration tests..."
docker run -d --name test-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=5432 \
  -e POSTGRES_DB=postgres \
  -p 5432:5432 \
  postgres:15

# Ждем запуска PostgreSQL
echo "⏳ Waiting for PostgreSQL..."
until docker exec test-postgres pg_isready -U postgres; do
  sleep 2
done

sleep 5

# Создаем тестовую БД
docker exec test-postgres psql -U postgres -d postgres -c "CREATE DATABASE flowershoptest;"

echo "🧩 Running integration tests in Docker..."

docker run --rm \
  --link test-postgres:postgres \
  -e TEST_CONNECTION_STRING="Host=postgres;Port=5432;Database=flowershoptest;Username=postgres;Password=5432;Include Error Detail=true" \
  -v $(pwd)/TestResults:/src/TestResults \
  flowershop-tests \
  dotnet test Tests/IntegrationTests/ --filter Category=Integration --configuration Release --verbosity normal \
  --logger "trx;LogFileName=integration-test-results.trx" \
  --results-directory TestResults/Integration

echo "✅ Integration tests completed"

echo "🧹 Cleaning up..."
docker stop test-postgres || true
docker rm test-postgres || true