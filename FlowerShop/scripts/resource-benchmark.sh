#!/usr/bin/env bash
set -euo pipefail

export LC_ALL=C

APP_URL="${APP_URL:-http://localhost:5000}"
CONN_STR="${CONN_STR:-Host=localhost;Port=5432;Database=flowershoptest;Username=postgres;Password=5432}"
REQUESTS="${REQUESTS:-1000}"
CSV_PATH="${CSV_PATH:-TestResults/ResourceBenchmark/resource-metrics.csv}"
MD_PATH="${MD_PATH:-TestResults/ResourceBenchmark/resource-metrics.md}"

if [[ ! -d /proc ]]; then
  echo "/proc is required for RSS sampling. Run on Linux/WSL." >&2
  exit 1
fi

kill_port() {
  if command -v fuser >/dev/null 2>&1; then
    fuser -k 5000/tcp || true
  fi
}

run_scenario() {
  local scenario="$1"
  local tracing="$2"
  local logging="$3"

  echo "=== Running scenario: $scenario (tracing=$tracing, logging=$logging) ==="
  local time_file="/tmp/time-$scenario.txt"
  local rss_file="/tmp/rss-$scenario.txt"

  /usr/bin/time -v -o "$time_file" bash -c '
    set -euo pipefail
    export LC_ALL=C
    scenario="$1"
    tracing="$2"
    logging="$3"
    app_url="$4"
    conn_str="$5"
    requests="$6"
    rss_file="$7"

    if command -v fuser >/dev/null 2>&1; then
      fuser -k 5000/tcp || true
    fi
    pkill -f "WebApp.dll" || true
    pkill -f "dotnet run" || true
    sleep 2

    log_env=()
    if [[ "$logging" == "extended" ]]; then
      log_env+=("Serilog__MinimumLevel__Default=Debug")
      log_env+=("Serilog__MinimumLevel__Override__Microsoft=Information")
      log_env+=("Serilog__MinimumLevel__Override__System=Information")
    fi

    env \
      ASPNETCORE_ENVIRONMENT=Test \
      ConnectionStrings__DefaultConnection="$conn_str" \
      Jaeger__Enabled="$tracing" \
      Jaeger__Host="localhost" \
      Jaeger__Port="6831" \
      "${log_env[@]}" \
      dotnet run --configuration Release --no-build --no-launch-profile --urls "$app_url" --project WebApp/WebApp.csproj > "/tmp/app-$scenario.log" 2>&1 &

    app_pid=$!
    target_pid="$app_pid"
    ready=0
    for i in {1..40}; do
      if curl -s -o /dev/null "$app_url/Account/Login"; then
        ready=1
        break
      fi
      if ! kill -0 "$app_pid" 2>/dev/null; then
        echo "App exited early for scenario $scenario"
        tail -200 "/tmp/app-$scenario.log" || true
        exit 1
      fi
      sleep 1
    done
    if [[ "$ready" -ne 1 ]]; then
      echo "App did not become ready for scenario $scenario"
      tail -200 "/tmp/app-$scenario.log" || true
      kill "$app_pid" || true
      exit 1
    fi

    child_pid=$(pgrep -P "$app_pid" -n dotnet || true)
    if [[ -n "$child_pid" ]]; then
      target_pid="$child_pid"
    fi

    max_rss_kb=0
    for i in $(seq 1 "$requests"); do
      curl -s -o /dev/null -f "$app_url/Account/Login"
      if ! kill -0 "$target_pid" 2>/dev/null; then
        echo "App exited during sampling for scenario $scenario"
        tail -200 "/tmp/app-$scenario.log" || true
        exit 1
      fi
      rss_kb=$(awk "/VmRSS/{print \\$2}" "/proc/$target_pid/status" 2>/dev/null || echo 0)
      if [[ "$rss_kb" -gt "$max_rss_kb" ]]; then
        max_rss_kb="$rss_kb"
      fi
    done

    echo "$max_rss_kb" > "$rss_file"

    kill "$app_pid" || true
    sleep 2
    if kill -0 "$app_pid" 2>/dev/null; then
      kill -9 "$app_pid" || true
    fi
    wait "$app_pid" || true
    if command -v fuser >/dev/null 2>&1; then
      fuser -k 5000/tcp || true
    fi
    pkill -f "WebApp.dll" || true
    pkill -f "dotnet run" || true
  ' -- "$scenario" "$tracing" "$logging" "$APP_URL" "$CONN_STR" "$REQUESTS" "$rss_file"

  local cpu_user
  local cpu_sys
  cpu_user=$(awk -F: '/User time \(seconds\)/{gsub(/^[ \t]+/,"",$2); print $2}' "$time_file")
  cpu_sys=$(awk -F: '/System time \(seconds\)/{gsub(/^[ \t]+/,"",$2); print $2}' "$time_file")
  if [[ -z "$cpu_user" ]]; then cpu_user=0; fi
  if [[ -z "$cpu_sys" ]]; then cpu_sys=0; fi

  local cpu_microseconds
  cpu_microseconds=$(awk -v u="$cpu_user" -v s="$cpu_sys" 'BEGIN { printf "%.0f", (u + s) * 1000000 }')

  local max_rss_kb
  max_rss_kb=$(cat "$rss_file" 2>/dev/null || echo 0)

  echo "$scenario,$tracing,$logging,$cpu_microseconds,$max_rss_kb" >> "$CSV_PATH"
}

mkdir -p "$(dirname "$CSV_PATH")"
echo "scenario,tracing,logging,cpu_microseconds,max_rss_kb" > "$CSV_PATH"

run_scenario "trace_off_log_default" "false" "default"
run_scenario "trace_on_log_default" "true" "default"
run_scenario "trace_off_log_extended" "false" "extended"
run_scenario "trace_on_log_extended" "true" "extended"

awk -F, '
  NR>1 { cpu[$1]=$4; rss[$1]=$5 }
  END {
    printf "| Scenario | Tracing | Logging | CPU (us) | Max RSS (MB) |\n"
    printf "|---|---|---|---|---|\n"
    printf "| Trace Off + Log Default | off | default | %.0f | %.2f |\n", cpu["trace_off_log_default"], rss["trace_off_log_default"]/1024
    printf "| Trace On + Log Default | on | default | %.0f | %.2f |\n", cpu["trace_on_log_default"], rss["trace_on_log_default"]/1024
    printf "| Trace Off + Log Extended | off | extended | %.0f | %.2f |\n", cpu["trace_off_log_extended"], rss["trace_off_log_extended"]/1024
    printf "| Trace On + Log Extended | on | extended | %.0f | %.2f |\n", cpu["trace_on_log_extended"], rss["trace_on_log_extended"]/1024
    printf "\n"
    printf "| Delta | CPU (us) | Max RSS (MB) |\n"
    printf "|---|---|---|\n"
    printf "| Tracing overhead (default log) | %.0f | %.2f |\n", cpu["trace_on_log_default"]-cpu["trace_off_log_default"], (rss["trace_on_log_default"]-rss["trace_off_log_default"])/1024
    printf "| Tracing overhead (extended log) | %.0f | %.2f |\n", cpu["trace_on_log_extended"]-cpu["trace_off_log_extended"], (rss["trace_on_log_extended"]-rss["trace_off_log_extended"])/1024
    printf "| Logging overhead (trace off) | %.0f | %.2f |\n", cpu["trace_off_log_extended"]-cpu["trace_off_log_default"], (rss["trace_off_log_extended"]-rss["trace_off_log_default"])/1024
    printf "| Logging overhead (trace on) | %.0f | %.2f |\n", cpu["trace_on_log_extended"]-cpu["trace_on_log_default"], (rss["trace_on_log_extended"]-rss["trace_on_log_default"])/1024
  }' "$CSV_PATH" > "$MD_PATH"

echo "CSV: $CSV_PATH"
echo "MD : $MD_PATH"
