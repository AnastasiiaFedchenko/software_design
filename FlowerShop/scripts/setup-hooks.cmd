@echo off
setlocal
set PROJECT_DIR=%~dp0\..
for /f "delims=" %%A in ('git -C "%PROJECT_DIR%" rev-parse --show-toplevel 2^>nul') do set REPO_ROOT=%%A
if "%REPO_ROOT%"=="" (
  echo Git repository root not found.
  exit /b 1
)
git -C "%REPO_ROOT%" config core.hooksPath FlowerShop/.githooks
echo Git hooks installed (FlowerShop/.githooks). If using Git Bash, run: chmod +x FlowerShop/.githooks/pre-commit
endlocal
