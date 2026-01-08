#!/usr/bin/env bash
set -euo pipefail

OUT_FILE="${1:-}"
STOP_FILE="${2:-}"

if [[ -z "$OUT_FILE" ]]; then
  echo "OutFile is required" >&2
  exit 1
fi

mkdir -p "$(dirname "$OUT_FILE")"
echo "timestamp,container,cpu_percent,mem_usage,mem_percent,net_io,block_io" > "$OUT_FILE"

while [[ ! -f "$STOP_FILE" ]]; do
  timestamp=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
  while IFS= read -r line; do
    echo "$timestamp,$line" >> "$OUT_FILE"
  done < <(docker stats --no-stream --format "{{.Name}},{{.CPUPerc}},{{.MemUsage}},{{.MemPerc}},{{.NetIO}},{{.BlockIO}}")
  sleep 1
done
