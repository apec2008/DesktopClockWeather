# Distribution — Desktop Clock Weather V1.1

## Release artifact

Build on Windows x64 with `Build_EXE.bat`.

Primary executable:

`DesktopClockWeather_V1.1.exe`

Recommended GitHub Release tag:

`v1.1`

Recommended Release title:

`Desktop Clock Weather V1.1`

## Suggested GitHub Release assets

- `DesktopClockWeather_V1.1.exe`
- `DesktopClockWeather_V1.1_Source.zip`
- optional: `Windows_Desktop_App_Startup_Integration_KnowHow_KO_Beta1_28.docx` or its current successor

## Release acceptance

Before publishing:

1. Build reports success.
2. `STARTUP_INSTALL_REPORT.txt` reports `Success: True`.
3. Reboot restores the requested 1/2/3-window working set.
4. Sleep/Wake is acceptable.
5. Clean uninstall → build/install → reboot starts Seoul + Ann Arbor at 0% scale.
6. City and temperature tooltips work.
7. Settings footer displays `Developed by D.J. Lee`.
8. Settings note `Up to 3 clock windows can be displayed.` is left-aligned.

## Known issue disclosure

On some dual-monitor laptop configurations, the first clean-install boot or lid-close/open transition may temporarily choose the external display or produce a small placement drift. This is non-blocking for V1.1 because saved placement generally restores correctly on subsequent boots.
