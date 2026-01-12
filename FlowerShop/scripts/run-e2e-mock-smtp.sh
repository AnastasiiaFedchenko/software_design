#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Running E2E SMTP mock test..."
dotnet test "${SCRIPT_DIR}/../E2E.Tests/E2E.Tests.csproj" --filter "FullyQualifiedName~EmailIntegrationMockTests" --configuration Release --logger "trx" --results-directory "TestResults/E2E"
echo "E2E SMTP mock test passed!"
