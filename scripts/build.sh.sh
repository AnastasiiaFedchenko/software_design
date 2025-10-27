#!/bin/bash
set -e

echo "🔨 Building Docker images..."

# Базовый образ с SDK
docker build -t flowershop-base -f Dockerfile.base .

# Основное приложение
docker build -t flowershop-app -f Dockerfile .

# Образ для тестов
docker build -t flowershop-tests -f Dockerfile.tests .

echo "✅ Docker images built successfully"