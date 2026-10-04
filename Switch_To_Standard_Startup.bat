@echo off
chcp 65001 >nul
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0DesktopClockWeather_V1.1.exe"

echo ================================================
echo Desktop Clock Weather V1.1 - STANDARD STARTUP
ECHO ================================================
echo.
echo This disables the V1.1 Fast Startup scheduled task
echo and restores the proven Windows Startup-folder method.
echo Your profiles, positions, cities, colors and StartupState are preserved.
echo.
if not exist "%EXE%" (
    echo ERROR: %EXE% was not found.
    echo Run Build_EXE.bat first.
    pause
    exit /b 1
)

"%EXE%" --install-standard-startup
if errorlevel 1 (
    echo ERROR: Could not restore the standard Startup method.
    echo Check STARTUP_INSTALL_REPORT.txt.
    pause
    exit /b 1
)

echo.
echo SUCCESS: Standard Startup-folder mode restored.
echo Reboot normally when convenient.
echo.
pause
exit /b 0
