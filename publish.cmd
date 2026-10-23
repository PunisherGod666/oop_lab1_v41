@echo off
rem Builds a standalone folder with SpreadsheetApp.exe that runs on any Windows 10/11 x64 PC
dotnet publish "%~dp0SpreadsheetApp\SpreadsheetApp.csproj" -c Release -r win-x64 --self-contained -o "%~dp0publish"
if errorlevel 1 (
    pause
    exit /b 1
)
echo.
echo Done: "%~dp0publish\SpreadsheetApp.exe"
pause
