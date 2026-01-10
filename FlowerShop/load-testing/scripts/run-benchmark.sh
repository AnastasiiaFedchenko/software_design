#!/usr/bin/env bash
set -euo pipefail

RUNS=100
SCENARIOS=("read" "mixed" "stress")
OUT_DIR="load-testing/results"

while [[ $# -gt 0 ]]; do
  case "$1" in
    -Runs|--runs)
      RUNS="$2"
      shift 2
      ;;
    -Scenarios|--scenarios)
      IFS=',' read -r -a SCENARIOS <<< "$2"
      shift 2
      ;;
    -OutDir|--out-dir)
      OUT_DIR="$2"
      shift 2
      ;;
    *)
      echo "Unknown аргумент: $1"
      exit 1
      ;;
  esac
done

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$ROOT_DIR/load-testing/docker-compose.yml"
DOCKERFILE="$ROOT_DIR/load-testing/Dockerfile.webapp2"
K6_DIR="$ROOT_DIR/load-testing/k6"
OUT_PATH="$ROOT_DIR/$OUT_DIR"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker not found in PATH" >&2
  exit 1
fi

if ! command -v k6 >/dev/null 2>&1; then
  echo "k6 not found in PATH" >&2
  exit 1
fi

mkdir -p "$OUT_PATH"

START_INDEX=1
last_run_dir=$(ls -1d "$OUT_PATH"/run_* 2>/dev/null | sort | tail -n1 || true)
if [[ -n "$last_run_dir" ]]; then
  last_base=$(basename "$last_run_dir")
  last_num=${last_base#run_}
  if [[ "$last_num" =~ ^[0-9]+$ ]]; then
    START_INDEX=$((10#$last_num + 1))
  fi
fi

PROGRESS_LOG="$OUT_PATH/progress.log"

wait_for_api() {
  local url="$1"
  local retries=60
  echo "Checking API readiness: $url"
  for ((i=0; i<retries; i++)); do
    if curl -4 -fsS --connect-timeout 1 --max-time 2 "$url" >/dev/null 2>&1; then
      return 0
    fi
    if (( i % 10 == 0 )); then
      echo "Waiting for API... ($i/$retries)"
    fi
    sleep 1
  done
  echo "API did not become ready in time: $url" >&2
  return 1
}

runs_done=0
current_index=$START_INDEX

while (( runs_done < RUNS )); do
  run_id=$(printf "%04d" "$current_index")
  run_dir="$OUT_PATH/run_$run_id"
  if [[ -d "$run_dir" && -n "$(ls -A "$run_dir")" ]]; then
    echo "Skipping existing run_$run_id"
    current_index=$((current_index + 1))
    continue
  fi

  mkdir -p "$run_dir"
  echo "$(date +"%Y-%m-%d %H:%M:%S") START run_$run_id" | tee -a "$PROGRESS_LOG"

  image_tag="flowershop-api:run-$run_id"
  docker build -f "$DOCKERFILE" -t "$image_tag" "$ROOT_DIR"

  export APP_IMAGE="$image_tag"
  docker compose -f "$COMPOSE_FILE" up -d --force-recreate

  cleanup() {
    docker compose -f "$COMPOSE_FILE" down -v || true
  }
  trap cleanup EXIT

  wait_for_api "http://localhost:8080/api/products?skip=0&limit=1"
  echo "API is ready"

  for scenario in "${SCENARIOS[@]}"; do
    scenario_dir="$run_dir/$scenario"
    mkdir -p "$scenario_dir"

    stop_file="$scenario_dir/stop.signal"
    rm -f "$stop_file"
    stats_file="$scenario_dir/docker-stats.csv"
    bash "$ROOT_DIR/load-testing/tools/collect-docker-stats.sh" "$stats_file" "$stop_file" &
    stats_pid=$!

    k6_script="$K6_DIR/scenario_${scenario}.js"
    k6_json="$scenario_dir/k6.json"
    summary_json="$scenario_dir/summary.json"

    echo "Running k6 scenario: $scenario"
    echo "$(date +"%Y-%m-%d %H:%M:%S") run_$run_id scenario=$scenario start" | tee -a "$PROGRESS_LOG"
    k6 run --out "json=$k6_json" --summary-export "$summary_json" -e BASE_URL="http://localhost:8080" "$k6_script"
    echo "$(date +"%Y-%m-%d %H:%M:%S") run_$run_id scenario=$scenario end" | tee -a "$PROGRESS_LOG"

    touch "$stop_file"
    wait "$stats_pid" || true
  done

  docker compose -f "$COMPOSE_FILE" down -v
  echo "$(date +"%Y-%m-%d %H:%M:%S") END run_$run_id" | tee -a "$PROGRESS_LOG"
  trap - EXIT
  runs_done=$((runs_done + 1))
  current_index=$((current_index + 1))
done
