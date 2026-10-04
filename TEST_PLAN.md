# V1.1 Beta 1.5 focused gate

1. Close every running clock process.
2. Build and launch once: Default/Seoul should appear.
3. Launch second time: Default2/Ann Arbor should appear near the first window, slightly left/down.
4. Launch third time: Default3/Saratoga should appear near Default2, slightly left/down.
5. Launch fourth time: only the three-window guidance should appear.
6. Drag Default2 and Default3 to desired positions, close all three in any order, relaunch three times.
7. Verify each city/color/unit/scale and each user position persist independently.


## Beta 1.4 focused gate - instance allocation

1. Close all Desktop Clock Weather instances.
2. Launch EXE once: Default / Seoul must open.
3. Launch a second time: Default2 / Ann Arbor must open.
4. Launch a third time: Default3 / Saratoga must open.
5. Adjust all three windows independently and close them in a different order.
6. Launch three times again: each profile must restore its own city, color, scale, unit, and position.
7. Launch a fourth time: no fourth clock is created; an informational maximum-3 message is shown.
8. Confirm logs contain NormalSlotAcquired for slots 0, 1, and 2.
# Desktop Clock Weather V1.1 Beta 1 Test Plan

Build ID expected in log: `V1.1-release`

Log folder:
`%APPDATA%\DesktopClockWeather\logs\`

## Gate A — basic persistence

### TP-01 One window
1. Start one EXE.
2. Set Seoul, Light Blue, -10%, place it at the desired location.
3. Close and reopen once.
Expected: exact profile, color, scale and placement.

### TP-02 Two windows
1. Start first and second EXE.
2. Set Default=Seoul/Light Blue, Default2=Ann Arbor/Amber.
3. Use visibly different positions and scales.
4. Close in Ann Arbor→Seoul order; reopen twice.
5. Repeat closing Seoul→Ann Arbor; reopen twice.
Expected: no cross-contamination.

### TP-03 Three windows
Add Default3=Saratoga/Green. Repeat close/reopen in varied order.
Expected: all three restore independently.

### TP-04 Cascade
On a clean new second/third slot:
- second appears slightly left/down from first
- third appears slightly left/down from second

## Gate B — scale

### TP-05 Scale matrix
Test -30, -20, -10, 0, +10, +20, +30.
Check time/date/weather clipping, city ellipsis, tooltip, hover toolbar and drag area.

Top-right anchor regression test:
1. Put all clocks at 0% and align their visible positions near the right side.
2. Change one clock 0% → +30% → 0% → -30% → 0%.
3. Repeat for another clock in the opposite order: 0% → -30% → 0% → +30% → 0%.
Expected: the window top-right screen coordinate is unchanged at every step; returning to 0% returns to the exact original position with no cumulative drift.
Diagnostics must contain `ScaleAnchorCaptured` and `ScaleTopRightAnchorApplied`.

## Gate C — Sleep/Wake

### TP-06 Short Sleep
5–10 minutes.
Expected: no missing windows, no position drift, cached weather retained, fresh weather resumes.

### TP-07 Long Sleep
2+ hours.
Expected: same as TP-06.

### TP-08 Repeated Sleep
Five Sleep/Wake cycles.
Expected: no cumulative position drift.

## Gate D — Reboot/startup

Run `Build_EXE.bat` first; its internal C# startup installer performs registration.

### TP-09 Startup with 1 profile
Comment Default2/Default3 in StartupProfiles.txt.
Expected: only Default starts with exact saved UI state.

### TP-10 Startup with 2 profiles
Default + Default2.
Expected: both appear with exact independent city/color/scale/placement. No default-color flash. No random 15-second second-window delay.

### TP-11 Startup with 3 profiles
Enable Default3.
Expected: same guarantee for all three.

## Gate E — resilience

### TP-12 Display topology
If practical, disconnect/reconnect the external monitor.
Expected: laptop-screen placements recover and stored positions are not corrupted.

### TP-13 Network unavailable during Resume
Wake before Wi-Fi is ready.
Expected: last valid weather remains; refresh occurs after connectivity returns; no `--` if cache exists.

### TP-14 Corrupt profile
Back up a profile and deliberately invalidate a copy.
Expected: app stays running, corrupt copy is preserved with timestamp, affected profile only falls back safely.

### TP-15 Log verification
Each run must show: AppStart, ProfileLoaded, WindowCreatedHidden, DisplayTopologySnapshot, PlacementResolved, WindowShown, weather events and AppExit.


## Gate D.1 — Beta 1.9 startup ownership
Before reboot testing:
1. Close all clock windows.
2. Build Beta 1.9.
3. No VBS step is required; the build invokes the internal C# startup installer.
4. Check `%APPDATA%\DesktopClockWeather\V1.1\STARTUP_INSTALL_REPORT.txt`.
5. The report must show exactly one current target under `%LOCALAPPDATA%\DesktopClockWeather\V1.1\DesktopClockWeather_V1.1.exe`.
6. Reboot. Exactly 3 current V1.1 profiles must start for Beta validation.
7. Launching the source EXE manually after reboot should show the max-3 message immediately, proving the reboot windows own the same V1.1 slots.

### TP-05A Visible top-right scale anchor
- Place a 0% clock with its visible right edge close to the screen right edge.
- Test `0 -> +30 -> 0 -> -30 -> 0`.
- Test `0 -> -30 -> 0 -> +30 -> 0`.
Expected:
- the visible `TimeRow` top-right point remains fixed at every transition
- Settings/hover controls remain reachable
- returning to 0% returns to the original visible alignment
- diagnostics contain `ScaleVisualAnchorCaptured` and `ScaleVisualTopRightAnchorApplied`

## V1.1 Fast Startup acceptance test
1. Run `Build_EXE.bat` and confirm `STARTUP_INSTALL_REPORT.txt` says `Success: True`.
2. Confirm `ActiveMode: TaskSchedulerFast`. If it says `StartupFolderFallback`, do not judge Fast Startup timing because the experiment was not activated.
3. Reboot and measure elapsed time from the desktop becoming visible to the first clock window.
4. Target: <= 15 s preferred; <= 25 s maximum acceptable.
5. Confirm second/third saved profiles restore once each (no duplicates).
6. Confirm profile/city/color/scale/position state is unchanged.
7. Run Sleep/Wake once and lid close/open once to check for side effects.
8. If any regression appears, run `Switch_To_Standard_Startup.bat`, reboot, and confirm Beta 1.24-equivalent startup behavior returns.
