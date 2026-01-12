#!/bin/bash
set -euo pipefail

if [[ -z "${SMTP_HOST:-}" ]]; then
  echo "Set SMTP_HOST, SMTP_USER, SMTP_PASSWORD, SMTP_FROM before starting."
  exit 1
fi

echo "Starting WebApp with real SMTP settings from environment variables"
dotnet run --project "${SCRIPT_DIR}/../WebApp/WebApp.csproj" --urls=http://localhost:5031
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
