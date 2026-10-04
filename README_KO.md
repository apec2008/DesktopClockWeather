# Desktop Clock Weather V1.1

Windows 바탕화면에서 시간·날짜·도시·날씨를 표시하는 C# / WPF / .NET 8 기반 데스크톱 위젯입니다.

## 주요 기능

- 최대 **3개의 독립 Clock 창**
- 도시 선택에 따른 시간대 + Open-Meteo 날씨 연동
- Celsius / Fahrenheit 선택
- 창 크기 `-30% ~ +30%`
- 각 창별 도시·색상·크기·위치 독립 저장
- 온도 및 전체 도시명 ToolTip
- Sleep/Wake 복구와 Profile persistence
- Windows Task Scheduler 기반 Fast Startup
- Task Scheduler 사용 불가 시 Startup-folder 방식 자동 fallback
- Clean uninstall / reinstall 지원

## 최초 설치 기본값

- Default: Seoul
- Default2: Ann Arbor
- 두 창 모두 0% scale

세 번째 창이 필요하면 EXE를 한 번 더 실행하면 됩니다.

## 일반 사용자 설치

Release 패키지에서 `Install.bat`를 실행합니다. 일반 사용자는 .NET SDK가 필요하지 않습니다.

## 개발자 빌드

소스에서 빌드할 때만 Windows x64와 .NET 8 SDK가 필요합니다.

```text
Build_EXE.bat
```

최종 생성 파일:

```text
DesktopClockWeather_V1.1.exe
```

마지막에 다음 문구가 표시된 경우에만 설치가 정상 완료된 것입니다.

```text
SUCCESS - V1.1 build/startup registration verified.
```

## 삭제

```text
Uninstall_DesktopClockWeather.bat
```

Clean uninstall이므로 V1.1의 자동시작 등록, 설치본, Profile, StartupState, 로그와 리포트를 삭제합니다. 압축을 풀어 놓은 소스/빌드 폴더는 삭제하지 않습니다.

## 알려진 잔여 이슈

듀얼 모니터 노트북 환경에서 **최초 Clean Install 부팅 또는 덮개 Close/Open 직후** 간헐적으로 외장 모니터가 선택되거나 위치가 소폭 이동할 수 있습니다. 이후 저장·재부팅 시에는 대체로 정상 복원됩니다.

## 문서

- `REQUIREMENTS.md`
- `ARCHITECTURE.md`
- `TEST_PLAN.md`
- `RELEASE_NOTES.md`
- `WINDOWS_STARTUP_INTEGRATION_KNOWHOW_KO.md`
- `PROJECT_STATUS.md`
