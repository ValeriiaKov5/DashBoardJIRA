@echo off
cd /d "%~dp0"
dotnet publish JiraSprintDashboard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
if errorlevel 1 (
  echo BUILD FAILED
  pause
  exit /b 1
)
echo.
echo OK: bin\Release\net8.0-windows\win-x64\publish\JiraSprintDashboard.exe
pause
