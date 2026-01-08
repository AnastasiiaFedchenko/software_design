@echo off
setlocal
set PROJECT_DIR=%~dp0\..
pushd "%PROJECT_DIR%"
dotnet tool restore || exit /b 1
dotnet format --verify-no-changes || exit /b 1
dotnet run --project tools/StaticAnalysis -- --max-cyclomatic 10 --out-dir analysis || exit /b 1
popd
endlocal
