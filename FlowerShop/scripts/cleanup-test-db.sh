#!/bin/bash

# FlowerShop/scripts/cleanup-test-db.sh
echo "Cleaning up test database..."

# Check if PostgreSQL is running
if ! pg_isready -h localhost -p 5432 -U postgres > /dev/null 2>&1; then
    echo "PostgreSQL is not running"
    exit 1
fi

# Drop test database
echo "Dropping test database..."
psql -h localhost -U postgres -d postgres -c "
SELECT pg_terminate_backend(pg_stat_activity.pid) 
FROM pg_stat_activity 
WHERE pg_stat_activity.datname = 'flowershoptest' 
AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS flowershoptest;"

if [ $? -eq 0 ]; then
    echo "Test database cleanup completed!"
else
    echo "Failed to cleanup test database"
    exit 1
fi