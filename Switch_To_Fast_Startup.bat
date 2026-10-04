@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0DesktopClockWeather_V1.1.exe"

if not exist "%EXE%" (
    echo ERROR: %EXE% was not found.
    echo Run Build_EXE.bat first.
    pause
    exit /b 1
)

"%EXE%" --install-fast-startup
if errorlevel 1 (
    echo ERROR: Fast Startup registration failed.
    echo Check STARTUP_INSTALL_REPORT.txt.
    pause
    exit /b 1
)

echo.
echo Startup registration completed.
echo Check STARTUP_INSTALL_REPORT.txt for ActiveMode.
echo.
pause
exit /b 0
