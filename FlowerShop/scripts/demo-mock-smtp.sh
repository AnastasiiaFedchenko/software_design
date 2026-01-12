#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$(pwd)/TestResults/MockSmtp"
LOG_FILE="${OUTPUT_DIR}/mock-smtp.log"
mkdir -p "${OUTPUT_DIR}"
MOCK_SMTP_PORT="${MOCK_SMTP_PORT:-8025}"
echo "Starting Mock SMTP server in background on port ${MOCK_SMTP_PORT}..."
dotnet run --project "${SCRIPT_DIR}/../MockSmtpServer/MockSmtpServer.csproj" -- --host 127.0.0.1 --port "${MOCK_SMTP_PORT}" --output "${OUTPUT_DIR}" > "${LOG_FILE}" 2>&1 &
SMTP_PID=$!

trap "kill ${SMTP_PID}" EXIT

echo "Waiting for Mock SMTP to start..."
started=false
for i in {1..30}; do
  if ! kill -0 "${SMTP_PID}" 2>/dev/null; then
    echo "Mock SMTP exited early. See log: ${LOG_FILE}"
    exit 1
  fi
  if (echo > /dev/tcp/127.0.0.1/"${MOCK_SMTP_PORT}") >/dev/null 2>&1; then
    started=true
    break
  fi
  sleep 0.5
done

if [[ "${started}" != "true" ]]; then
  echo "Mock SMTP did not open port ${MOCK_SMTP_PORT}. See log: ${LOG_FILE}"
  exit 1
fi

export ASPNETCORE_ENVIRONMENT=Mock
export EmailSettings__SmtpHost="127.0.0.1"
export EmailSettings__SmtpPort="${MOCK_SMTP_PORT}"
export EmailSettings__UseSsl="false"
export EmailSettings__FromEmail="noreply@local.test"
export EmailSettings__AdminEmail="admin@local.test"
echo "Starting WebApp with ASPNETCORE_ENVIRONMENT=Mock"
dotnet run --project "${SCRIPT_DIR}/../WebApp/WebApp.csproj" --urls=http://localhost:5031
