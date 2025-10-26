#!/bin/bash

# FlowerShop/scripts/setup-test-db.sh
echo "Setting up test database..."

# Check if PostgreSQL is running
if ! pg_isready -h localhost -p 5432 -U postgres > /dev/null 2>&1; then
    echo "PostgreSQL is not running on localhost:5432"
    exit 1
fi

# Create test database
echo "Creating test database..."
psql -h localhost -U postgres -d postgres -c "CREATE DATABASE flowershoptest;" 2>/dev/null || echo "Database might already exist"

# Initialize schema
echo "Initializing schema..."
psql -h localhost -U postgres -d flowershoptest -f "Integration.Tests/CreationOfTestDB.sql"

if [ $? -eq 0 ]; then
    echo "Test database setup completed!"
else
    echo "Failed to setup test database"
    exit 1
fi