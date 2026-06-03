@echo off
cd /d "%~dp0"
dotnet publish JiraSprintDashboard.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
exit /b %ERRORLEVEL%
