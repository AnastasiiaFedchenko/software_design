#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
REPO_ROOT="$(git -C "$PROJECT_DIR" rev-parse --show-toplevel 2>/dev/null || true)"

if [[ -z "$REPO_ROOT" ]]; then
  echo "Git repository root not found." >&2
  exit 1
fi

git -C "$REPO_ROOT" config core.hooksPath "FlowerShop/.githooks"
chmod +x "$PROJECT_DIR/.githooks/pre-commit"
echo "Git hooks installed (FlowerShop/.githooks)."
