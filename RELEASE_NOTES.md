# Desktop Clock Weather V1.1 — Official Release

## Release summary

- Promotes the stabilized V1.1 codebase to the official release.
- Final executable name: `DesktopClockWeather_V1.1.exe`.
- Settings note `Up to 3 clock windows can be displayed.` is left-aligned.
- Footer credit: `Developed by D.J. Lee`.
- Fast Startup uses a Windows Task Scheduler logon trigger with automatic Startup-folder fallback.
- Clean uninstall/reinstall and dynamic 1/2/3-window startup-state persistence are validated.
- City-name tooltip and localized temperature tooltip are included.
- Known non-blocking issue: on some dual-monitor laptop configurations, first clean-install boot or lid-close/open can occasionally select the external display or introduce small placement drift.

## Development history

# V1.1

## Settings footer / stabilization update

- Adds the English settings note: `Up to 3 clock windows can be displayed.`
- Adds the footer credit: `Developed by D.J. Lee`.
- Corrects the Settings window version title to V1.1.
- No runtime changes to startup, profile persistence, weather, scaling, or display-placement logic.
- Beta 1.28 behavior remains the functional baseline; the intermittent small reboot/display-placement drift remains under soak-test observation.

---

# V1.1 Beta 1.28

## Beta 1.28 clean-reinstall state fix

- Fixed a regression where manually closing all clocks before a clean uninstall could leave `StartupState.json` with an empty `DesiredProfiles` list. A subsequent build preserved that empty list, so Task Scheduler started correctly but the launcher intentionally opened no clocks.
- `Build_EXE.bat` now treats absence of both `Default.json` and `Default2.json` as first/clean install and resets the startup working set to `Default, Default2`.
- Clean uninstall now verifies that the installed folder and the entire V1.1 user-data folder were actually removed. It no longer reports success on a partial delete.
- Clean uninstall writes `CLEAN_REINSTALL_REQUESTED.flag`; the next successful build consumes the marker and initializes Seoul + Ann Arbor.
- Startup mode remains TaskSchedulerFast with Startup-folder fallback. Runtime profile persistence behavior is unchanged.

# V1.1 Beta 1.28

## Stabilization baseline / installer-uninstaller UX audit
- Runtime behavior is frozen from Beta 1.26 for soak testing; no new feature was added.
- Reviewed first-install and clean-uninstall console messages for a new user.
- `Build_EXE.bat` keeps the no-confirmation flow: double-click is the explicit install/build action.
- Clean uninstall intentionally keeps Y/N because it deletes V1.1 user data.
- Fixed current-version references in Fast/Standard startup switch scripts and uninstaller.
- Uninstall now also requests cleanup of legacy HKCU Run names left by older experimental builds.
- Rewrote Korean setup guidance to match the actual Task Scheduler Fast Startup + Startup-folder fallback architecture; stale VBS/V3-specific instructions were removed from current-user documentation.
- Added `WINDOWS_STARTUP_INTEGRATION_KNOWHOW_KO.md` documenting root causes, fixes, verified results, logging discipline, and reusable Windows desktop startup integration know-how.

# V1.1 Beta 1.28

## Automatic Korean/English weather ToolTip
- Weather ToolTip now follows the Windows UI culture automatically.
- Korean UI (`ko-*`): `현재 / 최고 / 최저`.
- Other UI languages: `Current / High / Low`.
- No settings toggle is required.
- Fast Startup behavior from Beta 1.25 is unchanged.

# V1.1 Beta 1.25

## Clean-install correctness and build UX

- Removed automatic import of legacy V1.0/old-beta profile JSON when a V1.1 profile is missing. A clean install now creates fresh V1.1 defaults instead of resurrecting stale scale, colour, city, or placement values. Existing V1.1 profiles are still preserved on normal upgrades.
- Fresh secondary/tertiary profiles are initially stacked below the preceding clock with a 12-DIP gap and right alignment instead of using the old 18-DIP near-overlap cascade.
- First-run defaults are 0% scale (100%), Celsius, Seoul for Default and Ann Arbor for Default2.
- Removed the Build_EXE.bat Y/N confirmation. Double-clicking the build script is now the explicit consent to build/install; destructive CLEAN UNINSTALL retains its Y/N confirmation.
- No functional changes to StartupState, weather, city tooltip, scale anchoring, or Sleep/Wake recovery.

## Evidence-driven Startup installer correction

- Beta 1.19 BUILD_LOG proved the installer failed at `Install_Startup_Shortcut.vbs(1,1)` with VBScript "invalid character" and return code 1.
- Binary inspection confirmed the VBS began with UTF-8 BOM bytes `EF BB BF`. Both VBS files are now packaged as plain ASCII with CRLF and **no BOM**.
- `Build_EXE.bat` now verifies that the installer has no UTF-8 BOM before build/install.
- A `/preflight` VBS mode proves `cscript.exe` can parse and start the script **without changing Startup** before the real install is attempted.
- Startup installation fails closed: success requires cscript return code 0, a Beta 1.25 report target, the `--startup` argument, the state file, and the Startup shortcut itself.
- Antivirus wording is product-neutral; V3 is no longer hard-coded into the builder UI.
- `StartupState.json` is preserved if it already exists; it remains the only source of truth for which profile windows reopen.

## Startup installation policy

- Restores the simple automatic Startup-shortcut installation used by earlier betas.
- `Build_EXE.bat` creates/verifies `StartupState.json` before calling the Startup installer.
- `StartupState.json` is the only source of truth for which profiles reopen after reboot.
- First initialization is exactly `Default` (Seoul) + `Default2` (Ann Arbor).
- Later launches/closes update the desired working set; reboot restores that saved set.
- Runtime self-registration and HKCU Run registration are not used.
- AhnLab V3 may classify the explicit VBS/cscript Startup-registration step as persistence. The builder therefore asks the tester to pause/allow V3 before continuing and reminds them to re-enable it immediately after the build.

# Beta 1.25

## Beta 1.25 startup-registration redesign

- Removed the `Install_Startup_Shortcut.vbs` / `Remove_Startup_Shortcut.vbs` dependency and no longer launches `cscript.exe`.
- Windows auto-start is owned by C# through the per-user `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` entry `DesktopClockWeatherV11`.
- The current build is copied to a versioned installed target under `%LOCALAPPDATA%\DesktopClockWeather\V1.1`.
- The installer removes named legacy Startup-folder entries before enabling the Run entry and fails closed if a legacy entry remains.
- `StartupState.json` is initialized inside the app to exactly `Default` + `Default2` only when absent, and is verified before startup registration succeeds.
- `StartupProfiles.txt` is normalized to timing settings only; historical `StartupCount=` / `Profile=` lines are removed.
- `STARTUP_INSTALL_REPORT.txt` now records build ID, registration method, target and state verification.


- StartupState initialization is now performed directly by `Build_EXE.bat` before the VBS installer runs.
- Build no longer reports SUCCESS when Windows Startup registration fails.
- Startup registration is verified after installation by checking the generated report for the Beta 1.25 target.
- Existing `StartupState.json` is preserved so normal upgrades do not overwrite the user's working set.
- First migration with no state file initializes exactly `Default` and `Default2` (Seoul + Ann Arbor).

# V1.1 Beta 1.25

## Startup installer + working-set persistence hardening

- Root cause found after Beta 1.13: the new `StartupState.json` logic was correct in the new executable, but the Startup repair path could leave an older installed executable active when the previous startup EXE was still running/locked. In that case reboot still ran the older 3-window logic, so no `StartupState.json` was created.
- Startup installation now copies the current build to a **versioned installed filename** (`%LOCALAPPDATA%\DesktopClockWeather\V1.1\DesktopClockWeather_V1.1_Beta1_25.exe`) and repoints the Startup shortcut to it. This avoids in-place overwrite of a potentially locked old EXE.
- If `%APPDATA%\DesktopClockWeather\V1.1\StartupState.json` does not exist, the installer creates it immediately with exactly `Default` + `Default2` (Seoul + Ann Arbor). This allows the first reboot test to be deterministic even before the Beta EXE has been manually launched.
- Existing `StartupState.json` is preserved on later upgrades, so user working-set choices are not reset by a build/update.
- `StartupProfiles.txt` remains timing-only; historical `StartupCount=` / `Profile=` entries are ignored by the app.
- The install report now records the exact Startup target, StartupState path, and whether the state file was created during this install.

## Startup working-set behavior

- First initialization: Seoul (`Default`) + Ann Arbor (`Default2`).
- User opens/closes a clock: `StartupState.json` is updated immediately.
- Windows reboot/logoff: automatic closes do not remove profiles from the saved working set.
- Reboot: only profiles listed in `StartupState.json` are launched.

## Scale visual-anchor correction (carried from Beta 1.12)

- Scale changes now preserve the **visible clock content top-right point**, not the transparent WPF Window edge.
- Fixes the first 0% -> +30% / -30% jump seen near the right side of the screen.
- Prevents the -30% case from moving the visible Settings/hover area off the right edge.
- Uses `TimeRow` as the visual reference boundary; Date/Weather rows are already constrained to that same width.
- Added `ScaleVisualAnchorCaptured` and `ScaleVisualTopRightAnchorApplied` diagnostics.

- Fixed cumulative horizontal drift when repeatedly changing clock size.
- Root cause: the scale-change path clamped the captured WPF window coordinate to a monitor WorkArea coordinate; on mixed-DPI/multi-monitor layouts those coordinate domains can differ, so the anchor itself moved.
- The exact pre-change top-right screen point is now immutable during scaling; monitor geometry is used only to persist the final anchor metadata.
- Scale anchor capture uses explicit `Window.Width` rather than potentially stale `ActualWidth`.
- Added `ScaleAnchorCaptured` diagnostics for direct verification.
- Final build artifact includes the full Beta revision: `DesktopClockWeather_V1.1_Beta1_25.exe`.

# V1.1 Beta 1.5

- Fixed ineffective Beta 1.4 Default3 placement migration.
- Bumped settings schema to 16 so existing Beta 1.3/1.4 profiles actually run the one-time cleanup.
- Default2 and Default3 placement only are reset once; city, colours, unit, scale and weather cache are preserved.
- Default2 now cascades from Default and Default3 cascades from Default2 on first Beta 1.5 launch, then user-adjusted positions persist normally.
- Maximum remains three clock windows; fourth launch shows the concise maximum-window guidance.

# V1.1 Beta 1.4

- Fixed the apparent missing third window caused by a legacy/stale Default3 placement overlapping another clock.
- Default3 placement is reset once when migrating from older test builds, then cascaded from Default2.
- Fourth-launch message now says only that up to three clocks are supported and cities can be changed in Settings.


## V1.1 Beta 1.4

- Fixed normal-instance allocation to explicitly support exactly 3 clock windows.
- Slot mapping is deterministic: Default, Default2, Default3.
- Third normal launch now uses Default3; Default3 defaults to Saratoga when first created.
- Fourth normal launch is intentionally blocked with a clear message.
- Added NormalSlotAcquired / InstanceLimitReached diagnostic events.
- Added a visible startup-error dialog if a profile fails during hidden initialization.
# V1.1 Beta 1 — Refactor build

This build replaces the patch-on-patch lifecycle and placement logic with separated managers/coordinators.

Notable changes:
- structured lifecycle state handling
- hidden startup initialization
- persistent profile slot isolation
- edge-anchored monitor placement
- display topology stabilization before placement
- fixed-timing hidden startup launcher
- structured daily diagnostics
- atomic profile persistence
- scale control (-30% through +30%, default -10%)

This build must pass the V1.1 test plan before it is promoted to a stable release.


## Beta 1.1 build fix
- Fixed CS0051 inconsistent accessibility in MainWindow constructor.
- Build script now uses UTF-8 code page and forces English .NET CLI diagnostics to prevent Korean mojibake in BUILD_LOG.txt.


## V1.1 Beta 1.6
- Fixed secondary/tertiary launch failure caused by JSON serialization of NaN placement sentinel values.
- Enabled named floating-point literal handling for profile JSON load/save.
- Removed forced Default2/Default3 position reset introduced in Beta 1.5; user-saved positions are preserved.
- Settings schema bumped to 17.


## Beta 1.7
- Reduced initial cascade offset for newly created secondary/tertiary clocks from 28 DIP to 18 DIP.
- Existing user-saved positions are never reset or migrated.
- No change to profile, weather, sleep/wake, reboot, or scaling logic.


## Beta 1.8
- Fixed reboot startup using an older Beta executable from a previous folder by auto-updating an existing Startup shortcut after a successful build.
- Startup supports 1/2/3 windows through `StartupCount` and Beta validation defaults to 3.
- Startup launches profiles sequentially and waits for each window to signal that it is visible before launching the next.
- Scale default changed from -10% to 0% (100%).
- Scale menu reordered: +30, +20, +10, 0 (Default), -10, -20, -30.
- Existing explicit -30/+30/etc. profile scale values are preserved; prior -10 beta-default values migrate once to 0%.
- Added executable path/build ID to startup diagnostics to identify stale-binary launches.

- V1.1 profile storage is now isolated under `%APPDATA%\DesktopClockWeather\V1.1\profiles`; legacy `Default*.json` files are imported once. This prevents an older executable launched at reboot from deleting newer V1.1 settings fields.


## Beta 1.9 — startup ownership repair
- Root cause of reboot-only legacy/default windows: old Startup-folder launchers could remain registered while the new Beta was launched manually.
- The installer now scans the entire user Startup folder and removes every Desktop Clock Weather legacy EXE/BAT shortcut before creating one canonical V1.1 shortcut.
- Windows startup now targets a stable installed copy under `%LOCALAPPDATA%\DesktopClockWeather\V1.1\DesktopClockWeather_V1.1.exe`, not a transient Beta source folder.
- Added a startup-launcher mutex to prevent duplicate V1.1 launchers.
- Startup configuration is authoritative under `%APPDATA%\DesktopClockWeather\V1.1\StartupProfiles.txt`.
- Beta validation startup count is 3.
- Scale values are now preserved exactly; valid `-10%` is no longer auto-migrated to `0%`. Fresh/default profiles still use `0%`.
- Added `STARTUP_INSTALL_REPORT.txt` with removed-entry count and exact startup target.

## Beta 1.25 startup-state correction
- Build_EXE.bat now always installs/updates the V1.1 Startup shortcut during beta validation instead of using the previous repair-if-present gate.
- This removes the early-exit path that prevented `StartupState.json` from being created when the installer did not detect an existing Startup-folder entry.
- First initialization creates `StartupState.json` with exactly `Default` and `Default2`.
- Startup window selection remains sourced only from `StartupState.json`; legacy `StartupCount=` and `Profile=` directives are ignored by the Beta 1.25 launcher.

## Beta 1.25 - antivirus-safe startup separation
- Removed all automatic Windows persistence registration from the build and normal app launch.
- Build_EXE.bat now builds only; it never writes HKCU Run, never creates Startup-folder items, and never invokes VBScript/cscript.exe.
- Removed automatic startup self-repair from normal launch.
- StartupRegistrationManager is diagnostics-only.
- Added STARTUP_SETUP_GUIDE_KO.txt for one-time manual Startup-folder shortcut creation.
- StartupState.json remains the only source of truth for which profiles reopen; first initialization is Default + Default2.
- This change specifically avoids V3's Persistence/MDP.AutoRun behavioral detection path observed in Beta 1.17.

## Beta 1.25
- Replaced VBScript/cscript Startup registration with native Windows ShellLink COM from C#.
- Removed HKCU Run writes from the installer path.
- Build invokes the app only in explicit `--install-startup` mode, which copies the build to LocalAppData and creates/verifies a Startup-folder `.lnk` with `--startup`.
- StartupState.json remains the sole source of truth for which profiles are restored.
- Installation fails closed and writes STARTUP_INSTALL_REPORT.txt with the exact exception on failure.

## Beta 1.25
- Fixes Beta 1.21 compile failure in `StartupRegistrationManager.cs` by importing `System.IO` explicitly.
- No functional change to profile, weather, scale, Sleep/Wake, or StartupState behavior.
- Build diagnostics now remove a stale prior `STARTUP_INSTALL_REPORT.txt` before building, so a failed compile cannot be confused with an older installer result.
- Installer invocation output is appended to `BUILD_LOG.txt` for a single traceable failure record.

## V1.1 Beta 1.25
- Added full-city-name ToolTip on hover for shortened city labels (6+ characters).
- City hover now suppresses the hover toolbar, matching temperature ToolTip priority behavior.
- Added `Uninstall_DesktopClockWeather.bat` for a true clean-uninstall / first-install regression test.
- Clean uninstall removes only V1.1 Startup registration, LocalAppData installed copy, and V1.1 AppData state; source/build folders are not removed.

## Beta 1.25 - Fast Startup A/B experiment
- Keeps Beta 1.24 behavior as the rollback baseline.
- Prefers a per-user Windows Task Scheduler logon trigger with a 3-second delay to avoid the variable Startup-folder launch delay observed in testing.
- Automatically falls back to the normal Startup-folder ShellLink if Task Scheduler registration is rejected.
- Adds `Switch_To_Standard_Startup.bat` for one-click rollback without changing profiles/settings.
- Adds `Switch_To_Fast_Startup.bat` to re-enable the experiment.
- No changes to profile schema, weather, scale, Sleep/Wake, or window placement logic in this experiment.
