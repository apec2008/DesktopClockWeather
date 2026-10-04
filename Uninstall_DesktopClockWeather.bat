@echo off
setlocal EnableExtensions
chcp 65001 >nul
cd /d "%~dp0"

echo ============================================================
echo Desktop Clock Weather V1.1 - CLEAN UNINSTALL
echo ============================================================
echo.
echo This is a CLEAN uninstall. It removes:
echo   - Windows startup registration
echo   - Installed V1.1 application copy
echo   - V1.1 profiles, settings, startup state, reports, and logs
echo.
echo Your extracted source/build folder will NOT be deleted.
echo.
choice /C YN /N /M "Continue with CLEAN uninstall? [Y/N]: "
if errorlevel 2 goto :cancel

set "STARTUP=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup"
set "USERROOT=%APPDATA%\DesktopClockWeather\V1.1"
set "INSTALLROOT=%LOCALAPPDATA%\DesktopClockWeather\V1.1"
set "SOURCEEXE=%~dp0DesktopClockWeather_V1.1.exe"
set "INSTALLEDEXE=%INSTALLROOT%\DesktopClockWeather_V1.1.exe"
set "TASKNAME=DesktopClockWeather V1.1 Fast Startup"
set "CLEANMARKER=%~dp0CLEAN_REINSTALL_REQUESTED.flag"

echo.
echo [1/5] Removing Startup registration...
if exist "%SOURCEEXE%" (
    "%SOURCEEXE%" --remove-startup >nul 2>&1
) else if exist "%INSTALLEDEXE%" (
    "%INSTALLEDEXE%" --remove-startup >nul 2>&1
)
schtasks /Delete /TN "%TASKNAME%" /F >nul 2>&1
del /F /Q "%STARTUP%\Desktop Clock Weather V1.1.lnk" 2>nul
for %%F in ("%STARTUP%\DesktopClockWeather*.lnk" "%STARTUP%\Start_DesktopClockWeather*.lnk" "%STARTUP%\Start_DesktopClockWeather*.bat") do (
    if exist "%%~fF" del /F /Q "%%~fF" 2>nul
)

echo [2/5] Removing installed V1.1 application copy...
if exist "%INSTALLROOT%" rmdir /S /Q "%INSTALLROOT%"

echo [3/5] Removing V1.1 profiles/settings/state/logs...
if exist "%USERROOT%" rmdir /S /Q "%USERROOT%"
if exist "%USERROOT%" (
    timeout /t 1 /nobreak >nul
    rmdir /S /Q "%USERROOT%"
)

echo [4/5] Verifying CLEAN removal...
set "REMOVE_FAIL=0"
schtasks /Query /TN "%TASKNAME%" >nul 2>&1
if not errorlevel 1 (
    echo ERROR: Fast Startup task still exists.
    set "REMOVE_FAIL=1"
)
if exist "%INSTALLROOT%" (
    echo ERROR: Installed application folder still exists:
    echo   %INSTALLROOT%
    set "REMOVE_FAIL=1"
)
if exist "%USERROOT%" (
    echo ERROR: V1.1 user-data folder still exists:
    echo   %USERROOT%
    set "REMOVE_FAIL=1"
)

if "%REMOVE_FAIL%"=="1" goto :remove_failed

echo [5/5] Marking next build as a true first/clean install...
>"%CLEANMARKER%" echo Clean uninstall completed on %DATE% %TIME%

echo.
echo CLEAN UNINSTALL COMPLETE.
echo All V1.1 startup registration and user state were removed.
echo.
echo IMPORTANT:
echo   The program is now uninstalled and will NOT start after reboot.
echo   To reinstall a release package, run Install.bat before rebooting.
   Developers rebuilding from source may run Build_EXE.bat instead.
echo.
pause
exit /b 0

:remove_failed
echo.
echo CLEAN UNINSTALL FAILED VERIFICATION.
echo Do NOT reboot for a clean-install test yet.
echo Close any remaining Desktop Clock Weather process and run this uninstall again.
echo.
pause
exit /b 2

:cancel
echo.
echo Uninstall cancelled. Nothing was changed.
pause
exit /b 1
