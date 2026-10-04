# Desktop Clock Weather V1.1 Architecture

V1.1 is an internal redesign. The visual concept is retained, but state ownership is separated so Sleep/Wake, Reboot, multi-instance operation, weather, and settings do not compete with one another.

## Modules

- `ProfileManager` — the only owner of profile JSON load/save/migration. Uses atomic temp-file replacement and a backup.
- `WindowPlacementManager` — the only owner of persisted user placement. Saves placement only after a user drag. Stores monitor-edge anchors plus absolute fallback coordinates.
- `DisplayTopologyService` — enumerates monitor work areas/DPI and produces a topology fingerprint.
- `LifecycleCoordinator` — coordinates Cold Start, Display Change, Resume, and recovery ordering. It waits for display topology stability before placement.
- `WeatherCoordinator` — serializes/cancels weather transactions and preserves last-known-good cache.
- `WeatherService` — Open-Meteo HTTP/geocoding only.
- `ClockViewModel` — current display state only.
- `MainWindow` — view interaction, rendering, drag/settings/hover. It does not load JSON or decide recovery policy.
- `StartupLauncher` — hidden in-process launcher for 1–3 configured persistent profiles. No BAT/CMD window and no `WaitForInputIdle` dependency.
- `DiagnosticsLog` — structured daily logs with PID, profile, lifecycle state, event and details. Seven-day retention.

## Startup sequence

1. Resolve a persistent slot (`Default`, `Default2`, `Default3`).
2. `ProfileManager` loads/migrates settings before any visible window appears.
3. `MainWindow` is created at opacity 0.
4. Cached city/weather/color/scale are applied.
5. `LifecycleCoordinator` samples display topology until stable.
6. `WindowPlacementManager` resolves the final location.
7. Window becomes visible.
8. Weather refresh starts asynchronously.

This prevents placeholder city/color/position flashes.

## Placement model

V1.0 stored absolute `Left/Top` coordinates only. V1.1 adds:

- monitor device name
- horizontal anchor (`Left` or `Right`)
- vertical anchor (`Top` or `Bottom`)
- offsets from those work-area edges
- absolute `Left/Top` fallback

The model is migrated from existing V1.0/V1.0.1 settings on first use. OS-driven temporary moves are not saved. Only explicit user drag persists a new placement.

## Scale model

The canonical widget layout remains 720×238 DIPs. `ScalePercent` applies a uniform transform and derives window size from the canonical dimensions.

Allowed values: -30, -20, -10, 0, +10, +20, +30 percent.

The V1.1 default is -10%.


## Beta 1.25 clean-install policy
- V1.1 profile storage is authoritative. Missing V1.1 profiles are created from V1.1 defaults; legacy root-folder profiles are not auto-imported.
- Existing V1.1 profile files continue to migrate in place and are never reset by a normal build/update.
