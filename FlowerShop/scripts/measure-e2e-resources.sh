#!/bin/bash

set -euo pipefail

output_csv="analysis/e2e-resource-report.csv"

mkdir -p "analysis" "analysis/telemetry" "analysis/logs"

scenarios=(
  "e2e_tracing_off_logging_default|false|Default"
  "e2e_tracing_on_logging_default|true|Default"
  "e2e_tracing_off_logging_extended|false|Extended"
  "e2e_tracing_on_logging_extended|true|Extended"
)

echo "Scenario,Tracing,Logging,ExitCode,DurationSec,CpuMs,PeakWorkingSetMb" > "$output_csv"

run_with_metrics() {
  local command="$1"
  local log_path="$2"

  local ps_cmd=""
  if command -v powershell >/dev/null 2>&1; then
    ps_cmd="powershell"
  elif command -v powershell.exe >/dev/null 2>&1; then
    ps_cmd="powershell.exe"
  elif [ -x "/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe" ]; then
    ps_cmd="/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
  elif command -v pwsh >/dev/null 2>&1; then
    ps_cmd="pwsh"
  fi

  if [ -n "$ps_cmd" ]; then
    COMMAND="$command" LOG_PATH="$log_path" "$ps_cmd" -NoProfile -Command '
      $command = $env:COMMAND
      $logPath = $env:LOG_PATH

      function Get-ProcessTreePids([int]$rootId) {
        $pids = New-Object System.Collections.Generic.List[int]
        $pids.Add($rootId)
        for ($i = 0; $i -lt $pids.Count; $i++) {
          $currentPid = $pids[$i]
          try {
            $children = Get-CimInstance Win32_Process -Filter "ParentProcessId=$currentPid" | Select-Object -ExpandProperty ProcessId
          } catch {
            $children = @()
          }
          foreach ($child in $children) {
            if (-not $pids.Contains($child)) {
              $pids.Add($child)
            }
          }
        }
        return $pids
      }

      $sw = [System.Diagnostics.Stopwatch]::StartNew()
      $errPath = $logPath + ".err"
      try {
        if ($command.StartsWith("dotnet ")) {
          $args = $command.Substring(7)
          $process = Start-Process -FilePath "dotnet" -ArgumentList $args -NoNewWindow -PassThru `
            -RedirectStandardOutput $logPath -RedirectStandardError $errPath
        } else {
          $cmdLine = "$command > ""$logPath"" 2>&1"
          $process = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", $cmdLine -NoNewWindow -PassThru
        }
      } catch {
        $sw.Stop()
        "1,0,0,0"
        exit
      }

      if ($null -eq $process) {
        $sw.Stop()
        "1,0,0,0"
        exit
      }

      $maxCpuMs = 0
      $maxWs = 0
      while (-not $process.HasExited) {
        $pids = Get-ProcessTreePids $process.Id
        $procs = Get-Process -Id $pids -ErrorAction SilentlyContinue
        if ($procs) {
          $cpuMs = 0
          foreach ($proc in $procs) {
            $cpuMs += $proc.TotalProcessorTime.TotalMilliseconds
          }
          $cpuMs = [math]::Round($cpuMs, 2)
          if ($cpuMs -gt $maxCpuMs) { $maxCpuMs = $cpuMs }

          $wsSum = ($procs | Measure-Object -Property WorkingSet64 -Sum).Sum
          if ($wsSum -gt $maxWs) { $maxWs = $wsSum }
        }
        Start-Sleep -Milliseconds 200
        $process.Refresh()
      }

      $process.WaitForExit()

      $sw.Stop()
      if (Test-Path $errPath) {
        Add-Content -Path $logPath -Value (Get-Content $errPath)
        Remove-Item $errPath -Force
      }

      if ($null -eq $process) {
        "1,0,0,0"
        exit
      }

      $cpu = [math]::Round($maxCpuMs, 2)
      $peak = [math]::Round($maxWs / 1MB, 2)
      $duration = [math]::Round($sw.Elapsed.TotalSeconds, 2)
      $culture = [System.Globalization.CultureInfo]::InvariantCulture
      $exitCodeValue = 1
      try {
        $exitCodeValue = $process.ExitCode
      } catch {
        $exitCodeValue = 1
      }
      if ($null -eq $exitCodeValue) {
        $exitCodeValue = 1
      }
      $exitCode = [string]$exitCodeValue
      $durationStr = $duration.ToString("0.00", $culture)
      $cpuStr = $cpu.ToString("0.00", $culture)
      $peakStr = $peak.ToString("0.00", $culture)
      "{0},{1},{2},{3}" -f $exitCode, $durationStr, $cpuStr, $peakStr
    '
    return
  fi

  if command -v /usr/bin/time >/dev/null 2>&1; then
    local time_file
    time_file="$(mktemp)"
    set +e
    /usr/bin/time -f "%e,%U,%S,%M" -o "$time_file" bash -lc "$command" > "$log_path" 2>&1
    local exit_code=$?
    set -e
    local elapsed user_cpu sys_cpu max_kb
    IFS=',' read -r elapsed user_cpu sys_cpu max_kb < "$time_file"
    rm -f "$time_file"
    local cpu_ms
    cpu_ms=$(awk "BEGIN { printf \"%.2f\", ($user_cpu + $sys_cpu) * 1000 }")
    local peak_mb
    peak_mb=$(awk "BEGIN { printf \"%.2f\", $max_kb / 1024 }")
    printf "%s,%.2f,%s,%s\n" "$exit_code" "$elapsed" "$cpu_ms" "$peak_mb"
    return
  fi

  set +e
  bash -lc "$command" > "$log_path" 2>&1
  set -e
  echo "0,0,0,0"
}

start_webapp() {
  local scenario="$1"
  local telemetry="$2"
  local logging="$3"
  local log_path="analysis/logs/webapp-${scenario}.log"

  export FLOWERSHOP_TELEMETRY_ENABLED="$telemetry"
  export FLOWERSHOP_TELEMETRY_SERVICE="FlowerShop.WebApp"
  export FLOWERSHOP_TELEMETRY_TRACE_PATH="analysis/telemetry/webapp-${scenario}-traces.jsonl"
  export FLOWERSHOP_TELEMETRY_METRICS_PATH="analysis/telemetry/webapp-${scenario}-metrics.jsonl"
  export FLOWERSHOP_LOGGING_PROFILE="$logging"
  export ASPNETCORE_ENVIRONMENT="Development"
  export AuthSettings__ShowTwoFactorCode="true"
  export AuthSettings__ShowRecoveryCode="true"
  export ConnectionStrings__DefaultConnection="Host=127.0.0.1;Port=5432;Database=flowershoptest;Username=postgres;Password=5432;Include Error Detail=true"
  export TEST_CONNECTION_STRING="Host=127.0.0.1;Port=5432;Database=flowershoptest;Username=postgres;Password=5432;Include Error Detail=true"

  dotnet run --framework net7.0 --project WebApp --urls http://localhost:5031 > "$log_path" 2>&1 &
  echo $!
}

wait_for_webapp() {
  local attempts=30
  for _ in $(seq 1 "$attempts"); do
    if curl -fsS http://localhost:5031/Account/Login >/dev/null 2>&1; then
      return 0
    fi
    sleep 1
  done
  return 1
}

stop_webapp() {
  local pid="$1"
  if [ -n "$pid" ]; then
    if command -v taskkill >/dev/null 2>&1; then
      taskkill /PID "$pid" /T /F >/dev/null 2>&1 || true
    else
      kill -9 "$pid" >/dev/null 2>&1 || true
    fi
  fi
}

for scenario in "${scenarios[@]}"; do
  IFS='|' read -r name telemetry logging <<< "$scenario"
  echo "Running $name"

  webapp_pid="$(start_webapp "$name" "$telemetry" "$logging")"
  if ! wait_for_webapp; then
    stop_webapp "$webapp_pid"
    echo "$name,$telemetry,$logging,1,0,0,0" >> "$output_csv"
    continue
  fi

  export FLOWERSHOP_TELEMETRY_ENABLED="$telemetry"
  export FLOWERSHOP_TELEMETRY_SERVICE="FlowerShop.Tests.E2E"
  export FLOWERSHOP_TELEMETRY_TRACE_PATH="analysis/telemetry/tests-e2e-${name}-traces.jsonl"
  export FLOWERSHOP_TELEMETRY_METRICS_PATH="analysis/telemetry/tests-e2e-${name}-metrics.jsonl"
  export FLOWERSHOP_LOGGING_PROFILE="$logging"
  export BASE_URL="http://localhost:5031"

  log_path="analysis/logs/${name}.log"
  metrics=$(run_with_metrics "dotnet test E2E.Tests/E2E.Tests.csproj --configuration Release" "$log_path")
  exit_code=$(echo "$metrics" | cut -d',' -f1)
  duration=$(echo "$metrics" | cut -d',' -f2)
  cpu_ms=$(echo "$metrics" | cut -d',' -f3)
  peak_ws=$(echo "$metrics" | cut -d',' -f4)

  stop_webapp "$webapp_pid"

  echo "$name,$telemetry,$logging,$exit_code,$duration,$cpu_ms,$peak_ws" >> "$output_csv"
done

echo ""
echo "E2E resource report (analysis/e2e-resource-report.csv)"
printf "%-40s %-7s %-9s %-12s %-10s %-10s %-5s\n" \
  "Scenario" "Trace" "Logging" "Duration(s)" "CPU(ms)" "Peak(MB)" "Exit"
printf "%-40s %-7s %-9s %-12s %-10s %-10s %-5s\n" \
  "--------" "-----" "-------" "-----------" "-------" "--------" "----"
tail -n +2 "$output_csv" | while IFS=',' read -r name telemetry logging exit_code duration cpu_ms peak_ws; do
  printf "%-40s %-7s %-9s %-12s %-10s %-10s %-5s\n" \
    "$name" "$telemetry" "$logging" "$duration" "$cpu_ms" "$peak_ws" "$exit_code"
done

if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  {
    echo "## E2E telemetry resource report"
    echo ""
    echo "| Scenario | Trace | Logging | Duration (s) | CPU (ms) | Peak (MB) | Exit |"
    echo "| --- | --- | --- | --- | --- | --- | --- |"
    tail -n +2 "$output_csv" | while IFS=',' read -r name telemetry logging exit_code duration cpu_ms peak_ws; do
      echo "| $name | $telemetry | $logging | $duration | $cpu_ms | $peak_ws | $exit_code |"
    done
  } >> "$GITHUB_STEP_SUMMARY"
fi
