@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

set "EXE=%~dp0DesktopClockWeather_V1.1.exe"
set "REPORT=%APPDATA%\DesktopClockWeather\V1.1\STARTUP_INSTALL_REPORT.txt"

echo ============================================================
echo Desktop Clock Weather V1.1 - INSTALL
echo ============================================================
echo.
echo This installs Desktop Clock Weather for the current Windows user
echo and registers automatic startup.
echo.

if not exist "%EXE%" (
    echo ERROR: DesktopClockWeather_V1.1.exe was not found in this folder.
    echo Keep Install.bat and DesktopClockWeather_V1.1.exe in the same folder.
    echo.
    pause
    exit /b 1
)

rem Do not overwrite/register while the release executable is still running.
tasklist /FI "IMAGENAME eq DesktopClockWeather_V1.1.exe" 2^>nul | find /I "DesktopClockWeather_V1.1.exe" >nul
if not errorlevel 1 (
    echo.
    echo Desktop Clock Weather is currently running.
    echo Please close all clock windows and run Install.bat again.
    echo.
    pause
    exit /b 3
)

if exist "%REPORT%" del /q "%REPORT%" >nul 2>&1

echo Installing V1.1 and registering Fast Startup...
"%EXE%" --install-fast-startup
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" goto :failed

if not exist "%REPORT%" goto :failed
findstr /I /C:"Build: V1.1" "%REPORT%" >nul
if errorlevel 1 goto :failed
findstr /I /C:"Success: True" "%REPORT%" >nul
if errorlevel 1 goto :failed
findstr /I /C:"DesktopClockWeather_V1.1.exe" "%REPORT%" >nul
if errorlevel 1 goto :failed

echo.
echo SUCCESS - Desktop Clock Weather V1.1 installed.
echo Automatic startup is registered for the current Windows user.
echo You may start the clock now by running DesktopClockWeather_V1.1.exe,
echo or simply reboot/sign in later.
echo.
pause
exit /b 0

:failed
echo.
echo ERROR - Installation or startup registration failed.
echo Check:
echo   %REPORT%
echo.
pause
exit /b 2
