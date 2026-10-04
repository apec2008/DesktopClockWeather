# Desktop ClockWeather V1.1 – Requirements & Redesign Specification

Version: Draft 0.1  
Purpose: Architecture refactor before further feature development

---

## 1. Objective

Rebuild the internal architecture while preserving the proven UI concept.

The V1.1 redesign must solve:
- unreliable window placement after Sleep/Wake
- unreliable window placement after Reboot
- profile/state contamination between multiple windows
- initialization race conditions
- insufficient diagnostic visibility

The redesign must also support:
- 1, 2, or 3 independently configured clock windows
- future extension to more windows without redesign
- user-selectable UI scale from -30% to +30% in 10% steps
- Celsius as default temperature unit; Fahrenheit selectable manually
- silent Windows startup without CMD windows

---

## 2. Scope Freeze

The following functionality is considered part of V1.1.

### 2.1 Core clock
- Local time for selected city
- 12/24-hour display
- optional seconds
- year/date/day
- manual time correction: -2, -1, 0, +1, +2 hours

### 2.2 City / time zone
- City lookup through Open-Meteo geocoding
- one selected city determines both time zone and weather location
- invalid city must never overwrite the current valid profile

### 2.3 Weather
- current temperature
- weather icon
- high/low values in tooltip
- weather refresh interval, minimum 10 minutes
- Celsius default
- Fahrenheit manual option
- no automatic unit mode

### 2.4 Appearance
- font color
- city color
- glow color
- independent settings per window/profile
- user scale:
  - -30%
  - -20%
  - -10%
  - 0%
  - +10%
  - +20%
  - +30%
- New default scale: 0% (canonical 100% size)

### 2.5 Multiple windows
- must work correctly with 1, 2, or 3 simultaneous windows
- each window has an independent profile and state
- profile data must never be shared accidentally
- closing order must not affect subsequent restoration
- startup order must not affect profile identity
- new windows should cascade from the immediately preceding active window
- default cascade offset: approximately 28 logical pixels left and down

### 2.6 Sleep / Wake
- preserve all running instances
- preserve city/profile identity
- preserve user-selected colors and scale
- restore saved position
- retain last valid weather while the network is unavailable
- refresh weather after network recovery
- detect resume via Windows events and independent heartbeat-gap detection

### 2.7 Windows Reboot / Login startup
- no CMD window
- no visible intermediate/default state
- startup must restore configured profiles only
- display must remain hidden until profile settings are loaded
- window must not become visible until initial placement is ready
- startup must support configuration for 1, 2, or 3 windows
- default startup configuration:
  - Default = Seoul
  - Default2 = Ann Arbor
- optional:
  - Default3 = Saratoga

---

## 3. Architecture

### 3.1 ProfileManager
Single owner of persistent profile data.

Responsibilities:
- load/save JSON
- profile identity
- schema migration
- default profile creation
- atomic writes
- validation
- no UI code

Profiles:
- Default
- Default2
- Default3
- extensible later

### 3.2 WindowPlacementManager
Single owner of placement.

Responsibilities:
- user-drag position capture
- startup restore
- Sleep/Wake restore
- display-change handling
- monitor/work-area validation
- cascade placement for newly created profiles
- never save OS-forced temporary relocation as a user position

Position model should store:
- target monitor identity
- absolute fallback Left/Top
- normalized/relative position within monitor work area
- anchor offsets where useful

### 3.3 LifecycleCoordinator
Single state machine for:
- ColdStart
- StartupRestore
- Running
- Sleeping
- Resuming
- DisplayReconfiguration
- Ready

Only this component determines when window placement and weather refresh may run.

### 3.4 WeatherService
Responsibilities:
- API request
- request timeout
- cancellation
- retry
- cache
- last-known-good data

Weather failure must not blank the last valid display.

### 3.5 ClockViewModel
Owns current UI state only:
- time
- date
- city
- temperature
- symbols
- colors
- scale

No file I/O, monitor enumeration, startup process control, or HTTP logic.

### 3.6 MainWindow
View only.
- bind to ViewModel
- drag interaction
- settings interaction
- tooltip/hover behavior
- no profile migration or recovery policy

### 3.7 StartupLauncher
Prefer an in-process hidden launcher mode rather than BAT/CMD.

Configuration file example:

StartupProfiles.txt
```
Profile=Default
Profile=Default2
# Profile=Default3
```

StartupLauncher must:
- start selected profiles
- use a fixed short interval
- not depend on WaitForInputIdle
- never show a console window

---

## 4. Initialization Sequence

### 4.1 Normal interactive launch
1. determine free profile slot
2. load complete profile
3. build ViewModel
4. create hidden window
5. apply appearance/scale
6. resolve final window placement
7. show window
8. start weather refresh asynchronously

No placeholder city, default color, or incorrect position may be briefly visible.

### 4.2 Windows startup launch
1. Windows invokes hidden startup launcher
2. read StartupProfiles.txt
3. launch each configured profile
4. each profile loads settings completely
5. keep each window hidden while display topology is unstable
6. wait for a short display-stability interval
7. resolve position
8. show
9. begin weather refresh

### 4.3 Resume
1. detect resume
2. keep last-known-good weather on screen
3. wait for display topology stabilization
4. restore position
5. restart/cancel stale weather request
6. refresh weather with retry
7. mark recovery complete only when placement is valid and lifecycle is stable

---

## 5. Scale Feature

### 5.1 Decision
Include the scale feature in V1.1 redesign.

Reason:
- scale is a display-only concern
- if implemented at the View/ViewModel boundary, it does not need to interact with profile identity, weather, startup, or lifecycle logic
- adding it now is safer than retrofitting it after another layout freeze

### 5.2 Rules
- internal layout keeps one canonical design size
- scale is applied uniformly to the entire widget
- Width and Height are derived from base dimensions × scale
- fonts/icons/margins scale together
- placement uses the final scaled window dimensions
- profile stores scale percentage only, not independent font-size mutations

Allowed values:
-70%, -60% ... not allowed
-30%, -20%, -10%, 0%, +10%, +20%, +30% only

Default:
0% (100%)

---

## 6. Logging

Log path:
`%APPDATA%\DesktopClockWeather\logs\DesktopClockWeather-yyyyMMdd.log`

Each log record must include:
- timestamp with milliseconds
- process ID
- profile ID
- lifecycle state
- event name
- important values

Example:
`2026-09-13 08:15:21.337 [PID:1234] [Default2] [Resuming] PlacementRestore OK monitor=DISPLAY2 x=3039 y=97`

Required events:
- AppStart
- ProfileSelected
- ProfileLoaded
- ProfileSaved
- WindowCreatedHidden
- DisplayTopologySnapshot
- PlacementResolved
- WindowShown
- UserDragStart
- UserDragSaved
- SleepDetected
- ResumeDetected
- HeartbeatResumeDetected
- DisplayChanged
- WeatherRequestStart
- WeatherRequestSuccess
- WeatherRequestTimeout
- WeatherRetry
- WeatherCacheUsed
- StartupLauncherStart
- StartupProfileLaunch
- AppExit
- UnhandledException

Keep at least 7 days of logs.
Delete older logs automatically.

---

## 7. Test Points

Each build intended for validation must have an internal build ID shown in Settings/About and recorded in logs.

### TP-01 Single-window persistence
- run one window
- configure city/color/scale/position
- close
- reopen
Expected: exact restoration

### TP-02 Two-window independence
- Default = Seoul
- Default2 = Ann Arbor
- different colors
- different positions
- different scales
- close in both possible orders
Expected: no cross contamination

### TP-03 Three-window independence
- add Default3 = Saratoga
- distinct colors/positions/scales
- close/reopen in varied order
Expected: exact restoration

### TP-04 Cascade creation
- create 2nd and 3rd new windows
Expected:
- window 2 cascades from window 1
- window 3 cascades from window 2

### TP-05 Scale matrix
Test each:
-30, -20, -10, 0, +10, +20, +30
Check:
- clipping
- ellipsis
- hover toolbar
- tooltip
- drag area
- weather row
- screen-edge placement

### TP-06 Short Sleep
- 5–10 minutes
Expected:
- no missing windows
- positions unchanged
- weather retained/refreshed

### TP-07 Long Sleep
- 2+ hours
Expected: same as TP-06

### TP-08 Repeated Sleep
- 5 consecutive Sleep/Wake cycles
Expected: no positional drift

### TP-09 Reboot with one window
Expected:
- correct profile
- correct color
- correct scale
- correct position

### TP-10 Reboot with two windows
Expected:
- both restored independently
- no overlap unless user intentionally saved overlap
- no default-color flash

### TP-11 Reboot with three windows
Expected: same guarantees for all three

### TP-12 Display topology transition
- external monitor connected
- disconnect/reconnect if practical
Expected:
- laptop-screen clock positions recover
- no saved position corruption

### TP-13 Network unavailable on resume
- resume before Wi-Fi is ready
Expected:
- cached temperature remains
- retry updates later
- never show `--` if valid cache exists

### TP-14 Corrupt profile file
Expected:
- app does not crash
- backup/error log generated
- only affected profile falls back safely

### TP-15 Startup timing
Record:
- Windows login time
- launcher start
- first window shown
- second window shown
- third window shown
Expected:
- deterministic profile order
- no 15-second random delay

---

## 8. Acceptance Criteria

V1.1 may be called stable only when:
- TP-01 through TP-05 pass
- TP-06 and TP-07 pass
- TP-09, TP-10 pass
- TP-11 passes if 3-window startup is enabled
- no profile/color/scale cross contamination
- no CMD window on startup
- no placeholder/default UI flash
- no lost weather after long Sleep
- no saved position corruption after display changes
- diagnostics sufficient to identify failure state without guessing

---

## 9. Deferred Features

Not part of V1.1:
- installer/MSIX
- auto-update
- online sync
- alarms
- world-clock grid
- themes marketplace
- advertising
- cloud profile backup

These may be considered only after V1.1 stability is established.
