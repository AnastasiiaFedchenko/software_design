#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$PROJECT_DIR"

dotnet tool restore
dotnet format --verify-no-changes
dotnet run --project tools/StaticAnalysis -- --max-cyclomatic 10 --out-dir analysis
