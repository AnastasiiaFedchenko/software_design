#!/bin/bash
set -euo pipefail

required_vars=(SMTP_HOST SMTP_USER SMTP_PASSWORD SMTP_FROM E2E_EMAIL_USER E2E_EMAIL_PASSWORD)
for var in "${required_vars[@]}"; do
  if [[ -z "${!var:-}" ]]; then
    echo "${var} is required for real SMTP/IMAP E2E tests."
    exit 1
  fi
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
echo "Running real SMTP/IMAP E2E tests..."
dotnet test "${SCRIPT_DIR}/../E2E.Tests/E2E.Tests.csproj" --filter "FullyQualifiedName~AuthenticationBddTests" --configuration Release --logger "trx" --results-directory "TestResults/E2E"
echo "Real SMTP/IMAP E2E tests passed!"
