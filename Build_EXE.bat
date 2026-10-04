@echo off
chcp 65001 >nul
setlocal EnableExtensions
set "DOTNET_CLI_UI_LANGUAGE=en-US"
cd /d "%~dp0"

set "LOG=%~dp0BUILD_LOG.txt"
set "OUT=%~dp0publish"
set "FINAL=%~dp0DesktopClockWeather_V1.1.exe"
set "USERV11=%APPDATA%\DesktopClockWeather\V1.1"
set "PROFILES=%USERV11%\profiles"
set "STATE=%USERV11%\StartupState.json"
set "REPORT=%USERV11%\STARTUP_INSTALL_REPORT.txt"
set "CLEANMARKER=%~dp0CLEAN_REINSTALL_REQUESTED.flag"
set "RESET_STATE=0"

> "%LOG%" echo Desktop Clock Weather V1.1 build log
>>"%LOG%" echo Build started: %DATE% %TIME%
>>"%LOG%" echo Folder: %CD%
>>"%LOG%" echo User V1.1 folder: %USERV11%
>>"%LOG%" echo Startup mode: Task Scheduler logon trigger with automatic Startup-folder fallback
>>"%LOG%" echo.

if exist "%REPORT%" (
    del /q "%REPORT%" >nul 2>&1
    >>"%LOG%" echo Removed stale STARTUP_INSTALL_REPORT.txt before this build.
)

rem A clean uninstall leaves a marker in the source folder.  Also treat a machine
rem with no V1.1 profile JSON files as a true first install even if a stale/empty
rem StartupState.json survived a previous failed uninstall.
if exist "%CLEANMARKER%" (
    set "RESET_STATE=1"
    >>"%LOG%" echo Clean reinstall marker detected: startup working set will be reset to Default,Default2.
)
if not exist "%PROFILES%\Default.json" if not exist "%PROFILES%\Default2.json" (
    set "RESET_STATE=1"
    >>"%LOG%" echo No V1.1 Default/Default2 profile files found: treating this as first/clean install.
)

echo ==========================================
echo Desktop Clock Weather V1.1 Builder
echo ==========================================
echo.
echo Preferred startup mode: Windows Task Scheduler Fast Startup.
echo If unavailable, installation automatically falls back to the Windows Startup folder.
echo Existing V1.1 profiles and window settings are preserved during normal update.
echo Clean/first install starts with Seoul + Ann Arbor at 0%% scale.
echo.
echo Build will start automatically.
echo.

where dotnet >>"%LOG%" 2>&1
if errorlevel 1 goto :NO_DOTNET

dotnet --version >>"%LOG%" 2>&1
if errorlevel 1 goto :DOTNET_ERROR
if not exist "DesktopClockWeather.csproj" goto :NO_PROJECT

echo [1/5] Cleaning old build...
if exist "%OUT%" rmdir /s /q "%OUT%" >>"%LOG%" 2>&1
if exist "%FINAL%" del /q "%FINAL%" >>"%LOG%" 2>&1

echo [2/5] Building single-file EXE...
dotnet publish "DesktopClockWeather.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o "%OUT%" >>"%LOG%" 2>&1
if errorlevel 1 goto :BUILD_FAIL
if not exist "%OUT%\DesktopClockWeather.exe" goto :NO_PUBLISHED_EXE
copy /y "%OUT%\DesktopClockWeather.exe" "%FINAL%" >>"%LOG%" 2>&1
if errorlevel 1 goto :COPY_FAIL
if not exist "%FINAL%" goto :COPY_FAIL

echo [3/5] Verifying startup working-set state...
if not exist "%USERV11%" mkdir "%USERV11%" >>"%LOG%" 2>&1
if not exist "%USERV11%" goto :STATE_FOLDER_FAIL

if "%RESET_STATE%"=="1" (
    if exist "%STATE%" del /f /q "%STATE%" >>"%LOG%" 2>&1
)

if not exist "%STATE%" (
    >"%STATE%" echo {
    >>"%STATE%" echo   "SchemaVersion": 1,
    >>"%STATE%" echo   "Initialized": true,
    >>"%STATE%" echo   "DesiredProfiles": [
    >>"%STATE%" echo     "Default",
    >>"%STATE%" echo     "Default2"
    >>"%STATE%" echo   ],
    >>"%STATE%" echo   "LastUpdatedUtc": "2000-01-01T00:00:00Z"
    >>"%STATE%" echo }
    >>"%LOG%" echo StartupState.json initialized with Default,Default2.
) else (
    >>"%LOG%" echo StartupState.json already exists; preserving the user's current working set.
)
if not exist "%STATE%" goto :STATE_CREATE_FAIL

echo [4/5] Installing Fast Startup...
>>"%LOG%" echo Calling V1.1 --install-fast-startup...
"%FINAL%" --install-fast-startup >>"%LOG%" 2>&1
set "INSTALL_RC=%ERRORLEVEL%"
>>"%LOG%" echo Fast startup installer return code: %INSTALL_RC%
if not "%INSTALL_RC%"=="0" goto :STARTUP_INSTALL_FAIL

echo [5/5] Verifying installation...
if not exist "%REPORT%" goto :STARTUP_VERIFY_FAIL
findstr /I /C:"Build: V1.1" "%REPORT%" >nul
if errorlevel 1 goto :STARTUP_VERIFY_FAIL
findstr /I /C:"Success: True" "%REPORT%" >nul
if errorlevel 1 goto :STARTUP_VERIFY_FAIL
findstr /I /C:"DesktopClockWeather_V1.1.exe" "%REPORT%" >nul
if errorlevel 1 goto :STARTUP_VERIFY_FAIL
findstr /I /C:"Arguments: --startup" "%REPORT%" >nul
if errorlevel 1 goto :STARTUP_VERIFY_FAIL
if not exist "%STATE%" goto :STARTUP_VERIFY_FAIL

findstr /I /C:"ActiveMode: TaskSchedulerFast" "%REPORT%" >nul
if not errorlevel 1 (
    set "ACTIVE_MODE=TaskSchedulerFast"
    >>"%LOG%" echo VERIFIED active startup mode: TaskSchedulerFast
) else (
    findstr /I /C:"ActiveMode: StartupFolderFallback" "%REPORT%" >nul
    if errorlevel 1 goto :STARTUP_VERIFY_FAIL
    set "ACTIVE_MODE=StartupFolderFallback"
    >>"%LOG%" echo VERIFIED active startup mode: StartupFolderFallback
)

if exist "%CLEANMARKER%" (
    del /q "%CLEANMARKER%" >nul 2>&1
    >>"%LOG%" echo Clean reinstall marker cleared after successful installation.
)

>>"%LOG%" echo VERIFIED startup state: %STATE%
>>"%LOG%" echo VERIFIED startup report: %REPORT%
>>"%LOG%" echo SUCCESS: %FINAL%

echo.
echo ==========================================
echo SUCCESS - V1.1 build/startup registration verified.
echo ==========================================
echo Created:
echo   %FINAL%
echo Active startup mode:
echo   %ACTIVE_MODE%
echo.
echo Reboot only after this SUCCESS message.
echo.
pause
exit /b 0

:NO_DOTNET
>>"%LOG%" echo ERROR: dotnet was not found in PATH.
echo ERROR: .NET SDK was not found.
goto :SHOW_LOG
:DOTNET_ERROR
>>"%LOG%" echo ERROR: dotnet exists but could not run.
echo ERROR: dotnet could not run.
goto :SHOW_LOG
:NO_PROJECT
>>"%LOG%" echo ERROR: DesktopClockWeather.csproj was not found.
echo ERROR: DesktopClockWeather.csproj was not found.
goto :SHOW_LOG
:BUILD_FAIL
>>"%LOG%" echo ERROR: publish failed.
echo ERROR: build failed.
goto :SHOW_LOG
:NO_PUBLISHED_EXE
>>"%LOG%" echo ERROR: publish finished but EXE was not found.
echo ERROR: publish output EXE was not found.
goto :SHOW_LOG
:COPY_FAIL
>>"%LOG%" echo ERROR: final EXE copy failed.
echo ERROR: final EXE copy failed.
goto :SHOW_LOG
:STATE_FOLDER_FAIL
>>"%LOG%" echo ERROR: could not create %USERV11%.
echo ERROR: could not create the V1.1 user settings folder.
goto :SHOW_LOG
:STATE_CREATE_FAIL
>>"%LOG%" echo ERROR: StartupState.json was not created.
echo ERROR: StartupState.json was not created.
goto :SHOW_LOG
:STARTUP_INSTALL_FAIL
>>"%LOG%" echo ERROR: startup installer failed with code %INSTALL_RC%.
echo ERROR: Startup installation failed.
echo Review BUILD_LOG.txt and STARTUP_INSTALL_REPORT.txt.
goto :SHOW_LOG
:STARTUP_VERIFY_FAIL
>>"%LOG%" echo ERROR: startup registration verification failed.
echo ERROR: Startup registration verification failed.
echo Review BUILD_LOG.txt and STARTUP_INSTALL_REPORT.txt.
goto :SHOW_LOG
:SHOW_LOG
echo.
echo Diagnostic log:
echo %LOG%
start "" notepad "%LOG%"
pause
exit /b 1
