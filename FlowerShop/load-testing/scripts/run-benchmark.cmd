@echo off
setlocal
set SCRIPT_DIR=%~dp0

set PS_EXE=
where powershell >nul 2>&1 && set PS_EXE=powershell
if "%PS_EXE%"=="" (
  where pwsh >nul 2>&1 && set PS_EXE=pwsh
)

if "%PS_EXE%"=="" (
  echo PowerShell executable not found. Please install PowerShell or ensure it is in PATH.
  exit /b 1
)

%PS_EXE% -ExecutionPolicy Bypass -File "%SCRIPT_DIR%run-benchmark.ps1" %*
endlocal
