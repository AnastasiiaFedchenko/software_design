param(
  [string]$OutFile,
  [string]$StopFile
)

$ErrorActionPreference = "Stop"

if (-not $OutFile) {
  throw "OutFile is required"
}

$header = "timestamp,container,cpu_percent,mem_usage,mem_percent,net_io,block_io"
Set-Content -Path $OutFile -Value $header

while (-not (Test-Path $StopFile)) {
  $timestamp = (Get-Date).ToString("o")
  $lines = docker stats --no-stream --format "{{.Name}},{{.CPUPerc}},{{.MemUsage}},{{.MemPerc}},{{.NetIO}},{{.BlockIO}}"
  foreach ($line in $lines) {
    Add-Content -Path $OutFile -Value "$timestamp,$line"
  }
  Start-Sleep -Seconds 1
}
