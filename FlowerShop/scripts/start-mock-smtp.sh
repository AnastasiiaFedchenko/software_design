#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${1:-$(pwd)/TestResults/MockSmtp}"
MOCK_SMTP_PORT="${MOCK_SMTP_PORT:-8025}"

echo "Starting Mock SMTP server on 127.0.0.1:${MOCK_SMTP_PORT}"
echo "Output dir: ${OUTPUT_DIR}"

dotnet run --project "${SCRIPT_DIR}/../MockSmtpServer/MockSmtpServer.csproj" -- --host 127.0.0.1 --port "${MOCK_SMTP_PORT}" --output "${OUTPUT_DIR}"
