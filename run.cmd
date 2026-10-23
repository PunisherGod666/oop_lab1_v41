@echo off
rem Builds and starts the app (needs .NET 10 SDK with the maui-windows workload)
dotnet run --project "%~dp0SpreadsheetApp\SpreadsheetApp.csproj" -c Release
if errorlevel 1 pause
