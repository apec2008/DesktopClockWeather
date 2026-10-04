# Desktop Clock Weather V1.1

Desktop Clock Weather is a lightweight Windows desktop clock and weather widget built with **C# / WPF / .NET 8**.

## Highlights

- Up to **3 independent clock windows**
- City-based time zone and weather using Open-Meteo
- Celsius / Fahrenheit selection
- Scale: `-30%, -20%, -10%, 0%, +10%, +20%, +30%`
- Independent city, color, scale and position per profile
- Weather and full city-name tooltips
- Sleep/Wake recovery and profile persistence
- Fast logon startup using **Windows Task Scheduler**, with Startup-folder fallback
- Clean uninstall and clean reinstall support

## Default first-run profiles

1. Seoul
2. Ann Arbor

Both start at `0%` scale. A third window can be created by launching the EXE again.

## Install (release package)

Keep `Install.bat` and `DesktopClockWeather_V1.1.exe` in the same folder, then run `Install.bat`. The release EXE is self-contained; end users do not need the .NET SDK.

## Build (developers)

Requirements: Windows x64 and .NET 8 SDK.

Run:

```text
Build_EXE.bat
```

Successful output:

```text
DesktopClockWeather_V1.1.exe
```

The build also installs/updates the startup registration and verifies it before reporting success.

## Uninstall

Run:

```text
Uninstall_DesktopClockWeather.bat
```

This is a clean uninstall and removes V1.1 startup registration, installed copy, profiles, state, logs and reports. The extracted source/build folder is not removed.

## Startup

Preferred mode: **TaskSchedulerFast**. If Task Scheduler registration is unavailable, V1.1 automatically falls back to a standard Windows Startup-folder shortcut.

## Known issue

On some dual-monitor laptop configurations, the first clean-install boot or lid-close/open cycle can occasionally select the external display or introduce a small placement drift. Saved placement is normally restored on subsequent boots.

## Documentation

- `REQUIREMENTS.md`
- `ARCHITECTURE.md`
- `TEST_PLAN.md`
- `RELEASE_NOTES.md`
- `WINDOWS_STARTUP_INTEGRATION_KNOWHOW_KO.md`
- `PROJECT_STATUS.md`
