# Desktop Clock Weather — Project Status

## Current baseline

- Release: **V1.1**
- Platform: Windows x64
- Technology: C# / WPF / .NET 8
- Maximum windows: 3
- Startup: Task Scheduler Fast Startup, with Startup-folder fallback

## Confirmed functionality

- 1/2/3 window working-set persistence: PASS
- Reboot restore: PASS
- Clean uninstall → reinstall → reboot: PASS
- Fast Startup: PASS; observed first window about 21–23 s after desktop on the validation PC, next window about 2 s later
- Sleep/Wake: PASS in repeated tests
- Scale -30% to +30%: PASS
- Top-right visual anchor during scale change: PASS
- City / color / temperature unit persistence: PASS
- City-name tooltip: PASS
- Temperature tooltip Korean/English auto selection by Windows UI language: implemented
- Startup-state/Profile separation: PASS

## Known issue

- Dual-monitor laptop: first clean-install boot or occasional lid close/open can select the external monitor or introduce small position drift. Subsequent saved-position reboot normally restores correctly.

## Release policy

V1.1 is feature-frozen. Only release-blocking defects should change runtime behavior before GitHub publication.

## Development history reference
- `DEVELOPMENT_ISSUE_HISTORY_KO.md`: V1.0~V1.1 문제/원인/조치/교훈 이력.
