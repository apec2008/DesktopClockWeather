# Development Guide — V1.1

V1.1 development rule: do not add cross-module shortcuts.

- Persistent JSON: ProfileManager only
- Window position: WindowPlacementManager only
- Display topology: DisplayTopologyService only
- Sleep/Wake/Reboot ordering: LifecycleCoordinator only
- HTTP/weather retry/cache: WeatherCoordinator + WeatherService
- Visual state: ClockViewModel/MainWindow
- Startup process creation: StartupLauncher only

Every bug fix that changes lifecycle or placement must add/adjust a test point and a diagnostic event before release.

Version flow:
- V1.1-beta.1: architecture refactor validation
- V1.1-beta.2+: bug fixes found by the test matrix
- V1.1-rc.1: all acceptance tests pass
- V1.1: stable

### Beta 1.25 diagnostic discipline
Beta 1.21 never reached startup registration: compilation failed because `StartupRegistrationManager.cs` referenced `Path`, `File`, `Directory`, `IOException`, and `FileNotFoundException` without importing `System.IO`. Beta 1.25 makes this dependency explicit. Do not infer Startup-registration behavior from a stale report when publish fails; the build now deletes the prior report before starting and captures installer output in `BUILD_LOG.txt`.
