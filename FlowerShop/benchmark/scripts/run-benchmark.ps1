param(
  [int]$Runs = 100,
  [string[]]$Scenarios = @("read", "mixed", "stress"),
  [string]$OutDir = "benchmark/results"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path "$PSScriptRoot\..\.."
$composeFile = Join-Path $root "benchmark\docker-compose.yml"
$dockerfile = Join-Path $root "benchmark\Dockerfile.webapp2"
$k6Dir = Join-Path $root "benchmark\k6"

function Wait-ForApi {
  param([string]$Url, [int]$Retries = 60)
  for ($i = 0; $i -lt $Retries; $i++) {
    try {
      $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 2
      if ($response.StatusCode -eq 200) {
        return
      }
    } catch {
      Start-Sleep -Seconds 1
    }
  }
  throw "API did not become ready in time: $Url"
}

function Start-StatsCollector {
  param([string]$OutFile, [string]$StopFile)
  $collector = Join-Path $root "benchmark\tools\collect-docker-stats.ps1"
  return Start-Job -FilePath $collector -ArgumentList @($OutFile, $StopFile)
}

New-Item -ItemType Directory -Force -Path (Join-Path $root $OutDir) | Out-Null

for ($i = 1; $i -le $Runs; $i++) {
  $runId = "{0:D4}" -f $i
  $runDir = Join-Path $root "$OutDir\run_$runId"
  New-Item -ItemType Directory -Force -Path $runDir | Out-Null

  $imageTag = "flowershop-api:run-$runId"
  docker build -f $dockerfile -t $imageTag $root | Out-Host

  $env:APP_IMAGE = $imageTag
  docker compose -f $composeFile up -d --force-recreate | Out-Host

  try {
    Wait-ForApi -Url "http://localhost:8080/api/products?skip=0&limit=1"

    foreach ($scenario in $Scenarios) {
      $scenarioDir = Join-Path $runDir $scenario
      New-Item -ItemType Directory -Force -Path $scenarioDir | Out-Null

      $stopFile = Join-Path $scenarioDir "stop.signal"
      if (Test-Path $stopFile) { Remove-Item $stopFile -Force }
      $statsFile = Join-Path $scenarioDir "docker-stats.csv"
      $job = Start-StatsCollector -OutFile $statsFile -StopFile $stopFile

      $k6Script = Join-Path $k6Dir "scenario_$scenario.js"
      $k6Json = Join-Path $scenarioDir "k6.json"
      $summaryJson = Join-Path $scenarioDir "summary.json"

      k6 run --out "json=$k6Json" --summary-export $summaryJson -e BASE_URL="http://localhost:8080" $k6Script | Out-Host

      New-Item -ItemType File -Path $stopFile -Force | Out-Null
      Wait-Job $job | Out-Null
      Receive-Job $job | Out-Null
      Remove-Job $job | Out-Null
    }
  } finally {
    docker compose -f $composeFile down -v | Out-Host
  }
}
