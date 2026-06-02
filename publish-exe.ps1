$ErrorActionPreference = "Stop"

Write-Host "Сборка self-contained .exe для Windows x64..."
dotnet publish ".\JiraSprintDashboard.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true

Write-Host ""
Write-Host "Готово."
Write-Host "EXE: .\bin\Release\net8.0-windows\win-x64\publish\JiraSprintDashboard.exe"
