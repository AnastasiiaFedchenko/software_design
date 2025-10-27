#!/bin/bash
set -e

echo "🌐 Starting full stack for E2E tests..."

# Запускаем PostgreSQL
docker run -d --name e2e-postgres \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=5432 \
  -e POSTGRES_DB=postgres \
  -p 5432:5432 \
  postgres:15

# Ждем запуска PostgreSQL
echo "⏳ Waiting for PostgreSQL..."
until docker exec e2e-postgres pg_isready -U postgres; do
  sleep 2
done

sleep 5

# Создаем тестовую БД
docker exec e2e-postgres psql -U postgres -d postgres -c "CREATE DATABASE flowershoptest;"

# Запускаем приложение
docker run -d --name flowershop-app \
  --link e2e-postgres:postgres \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Port=5432;Database=flowershoptest;Username=postgres;Password=5432" \
  -p 8080:8080 \
  flowershop-app

# Ждем запуска приложения
echo "⏳ Waiting for application..."
until curl -f http://localhost:8080/health > /dev/null 2>&1; do
  sleep 5
done

echo "🌐 Running E2E tests in Docker..."

docker run --rm \
  --link e2e-postgres:postgres \
  --link flowershop-app:app \
  -e TEST_CONNECTION_STRING="Host=postgres;Port=5432;Database=flowershoptest;Username=postgres;Password=5432;Include Error Detail=true" \
  -e API_BASE_URL="http://app:8080" \
  -v $(pwd)/TestResults:/src/TestResults \
  flowershop-tests \
  dotnet test Tests/E2ETests/ --filter Category=E2E --configuration Release --verbosity normal \
  --logger "trx;LogFileName=e2e-test-results.trx" \
  --results-directory TestResults/E2E

echo "✅ E2E tests completed"

echo "🧹 Cleaning up..."
docker stop flowershop-app e2e-postgres || true
docker rm flowershop-app e2e-postgres || true