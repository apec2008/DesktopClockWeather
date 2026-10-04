# Desktop Clock Weather 개발 단계 문제점 이력

기준 버전: **V1.1**  
목적: V1.0~V1.1 개발 과정에서 실제로 발생했던 문제를 증상, 원인, 조치, 교훈 중심으로 정리한다. 향후 유사한 Windows Desktop Utility 개발 시 재사용하는 참고 기록이다.

## 1. Sleep/Wake 이후 날씨 갱신 정지
- **증상**: 장시간 Sleep 후 온도/날씨가 갱신되지 않거나 이전 값에 머무름.
- **원인**: 일반 타이머만으로는 장시간 Suspend/Resume 경계를 안정적으로 감지하기 어려웠고 네트워크 복귀 시점도 일정하지 않음.
- **조치**: heartbeat gap 기반 Resume 감지, 마지막 정상 날씨 cache 유지, 재시도/timeout, Resume 후 지연 복구 로직 도입.
- **결과**: 반복 Sleep/Wake 및 장시간 Sleep에서 대부분 정상 복구.
- **교훈**: Windows lifecycle 이벤트 하나에 의존하지 말고 시간 간격, 네트워크 지연, cache를 함께 고려한다.

## 2. Sleep/Lid Close 후 창 위치 이동
- **증상**: Resume 후 창이 다른 모니터로 이동하거나 위치가 조금씩 달라짐.
- **원인**: Windows가 Display Topology를 재구성하면서 `DISPLAYn` 논리 이름, 좌표, Primary 상태가 달라질 수 있음.
- **조치**: Display topology snapshot, monitor/work-area 기반 위치 복구, visible clock anchor 모델 적용.
- **현재 상태**: 기능상 큰 문제는 없으나 부팅 또는 Lid Close/Open 후 소폭 위치 drift와 다른 모니터 선택이 간헐적으로 남음.
- **향후 개선**: 필요 시 DisplayConfig/EDID 기반 물리 모니터 식별 도입 검토.

## 3. Scale 변경 시 창이 오른쪽으로 밀리거나 원위치 복귀 실패
- **증상**: 0%→+30%→0% 등의 확대/축소 후 시계가 조금씩 이동하거나 화면 밖으로 밀림.
- **원인**: 투명 Window 전체 영역의 좌표를 기준으로 보정하여 실제 보이는 시계 content의 기준점과 불일치.
- **조치**: visible clock content의 **Top-Right anchor**를 기준으로 확대/축소 전후 좌표를 보정.
- **결과**: 0→+30→0→-30→0 반복에서도 위치 복귀 안정화.
- **교훈**: 투명 UI는 Window 경계보다 실제 visual content anchor를 기준으로 배치해야 한다.

## 4. 재부팅 후 원하지 않는 세 번째 창 재생성
- **증상**: 사용자가 2개만 사용하도록 닫았는데 재부팅하면 과거 세 번째 Profile이 다시 실행됨.
- **원인**: Profile 정보와 Startup 대상 정보가 여러 위치에 중복 저장되어 서로 충돌.
- **조치**: `StartupState.json`의 `DesiredProfiles`를 Startup working set의 **Single Source of Truth**로 지정.
- **결과**: 1/2/3개 창 상태가 재부팅 후 그대로 유지됨.
- **교훈**: 같은 상태를 여러 파일/레지스트리에서 동시에 결정하지 않는다.

## 5. 시스템 종료 시 창 Close가 사용자 Close로 오인됨
- **증상**: Windows 종료 과정에서 창이 닫히면서 다음 부팅 대상 Profile이 사라질 위험.
- **원인**: 사용자 Close와 SessionEnding Close를 동일하게 처리.
- **조치**: `SystemSessionEnding` 시 startup-state 변경을 freeze.
- **결과**: 종료/재부팅에 의한 Close는 사용자 의도로 기록되지 않음.

## 6. VBS Startup 등록이 보안 프로그램에 감지됨
- **증상**: Startup shortcut 생성용 VBS/cscript 실행 시 백신 경고 또는 차단.
- **원인**: Script Host + Startup persistence 조합이 보안 제품의 heuristic 탐지 대상이 되기 쉬움.
- **조치**: VBS/cscript 제거, C# 기반 Windows ShellLink COM 방식으로 전환.
- **교훈**: 정상 프로그램이어도 persistence + script 조합은 보안 제품에서 민감하다.

## 7. VBS UTF-8 BOM으로 Windows Script Host 실행 실패
- **증상**: `(1,1) invalid character` 오류.
- **원인**: VBS 파일의 UTF-8 BOM(`EF BB BF`)을 해당 실행 환경에서 정상 처리하지 못함.
- **조치**: ASCII/no-BOM 패키징으로 수정했으나 최종적으로 VBS 자체를 제거.
- **교훈**: 스크립트 배포에서는 내용뿐 아니라 파일 encoding도 실행환경의 일부다.

## 8. HKCU Run 자동등록이 보안 제품에 차단됨
- **증상**: Registry `HKCU\...\Run` 자동등록 시 보안 경고/UnauthorizedAccessException.
- **원인**: 프로그램이 자신의 자동실행 persistence를 직접 추가하는 행동이 민감하게 평가됨.
- **조치**: HKCU Run 방식을 폐기. Task Scheduler 및 Startup-folder ShellLink 방식 사용.

## 9. C# ShellLink 전환 직후 Compile Error
- **증상**: `Path`, `Directory`, `File`, `IOException` 등의 타입을 찾지 못함.
- **원인**: `using System.IO;` 누락.
- **조치**: 필요한 namespace 추가 후 재빌드.
- **교훈**: 대규모 오류 목록에서도 최초의 직접 오류와 연쇄(cascading) 오류를 분리해서 본다.

## 10. Startup Folder 방식의 긴 실행 지연
- **증상**: Windows 바탕화면 표시 후 첫 Clock이 약 1분 뒤 나타남.
- **로그 분석**: 앱 내부에서는 launcher 시작 후 첫 창까지 약 4초 수준. 실제 긴 지연은 Windows가 Startup Folder 항목을 늦게 실행한 시간.
- **조치**: Task Scheduler Logon Trigger + 약 3초 delay를 Fast Startup으로 도입, 실패 시 Startup Folder 자동 fallback.
- **결과**: 실측 첫 창 약 21~23초, 두 번째 창 약 2초 후 표시.
- **교훈**: 체감 지연을 앱 실행시간으로 추정하지 말고 OS 호출 시점부터 로그로 분리해야 한다.

## 11. Clean Install인데 과거 Scale/Position 복원
- **증상**: 삭제 후 새 설치인데 이전 -10%, -30% scale과 위치가 다시 나타남.
- **원인**: V1.1 Profile이 없을 때 V1.0/legacy 설정을 자동 import하는 migration 로직.
- **조치**: true clean install에서는 legacy 자동 migration 제거.
- **결과**: 기본 Seoul + Ann Arbor, Scale 0%, fresh placement로 시작.

## 12. Clean Install 후 자동실행은 되지만 창이 0개 표시
- **증상**: Task Scheduler 등록과 실행은 성공했는데 재부팅 후 아무 창도 나타나지 않음.
- **로그 원인**: `StartupLauncherStart ... profiles=` → `StartupNoProfilesRequested`.
- **직접 원인**: 마지막 창을 닫는 과정에서 `DesiredProfiles=[]`가 저장되었고, 재빌드가 이를 기존 working set으로 오인해 보존.
- **조치**:
  - Clean uninstall 삭제 검증 강화.
  - `CLEAN_REINSTALL_REQUESTED.flag` 추가.
  - clean install 시 `Default`, `Default2`를 강제 초기화.
  - 기본 Profile이 모두 없으면 stale/empty StartupState를 clean install로 판정.
- **결과**: `Uninstall → Build → Reboot` 정상, Seoul + Ann Arbor 복원.

## 13. Startup 설치가 성공한 것처럼 보이지만 실제 상태 불확실
- **증상**: 설치 명령 성공 메시지와 실제 등록 상태가 불일치할 가능성.
- **조치**: `STARTUP_INSTALL_REPORT.txt`, return code, active mode, state/report/shortcut 존재 여부를 Build 단계에서 검증하는 fail-closed 방식 도입.
- **교훈**: Installer는 “명령 실행 성공”보다 “최종 상태 검증”이 중요하다.

## 14. Startup EXE와 개발 EXE의 버전 혼재
- **증상**: 재부팅에서는 구버전 UI가 뜨고 수동 실행은 신버전이 떠 총 5개 창까지 보임.
- **원인**: Startup 등록이 이전 EXE 경로를 계속 가리킴.
- **조치**: `%LOCALAPPDATA%\DesktopClockWeather\V1.1`에 설치용 EXE를 별도 복사하고 Startup 등록은 그 설치본만 참조.
- **교훈**: 개발 source 위치와 production installed location을 분리한다.

## 15. 실행 중인 고정 파일명 EXE 덮어쓰기 위험
- **증상 가능성**: 정식 V1.1부터 EXE 이름을 고정하면 실행 중인 파일이 교체되지 않거나 설치 실패 가능.
- **조치**: `Install.bat` 실행 시 Desktop Clock Weather가 실행 중이면 설치를 중단하고 모든 창을 닫으라는 메시지를 표시.
- **교훈**: Installer는 file-lock 상태를 사용자가 이해할 수 있는 단계에서 사전 차단해야 한다.

## 16. 정식 V1.1 Source ZIP Build Error — 동일 소스가 두 벌 포함
- **증상**: `CS0101`, `CS0111`, duplicate attribute, `IComponentConnector.Connect` 중복 등 대량 Compile Error.
- **원인**: ZIP 루트의 source와 `DesktopClockWeather_V1.1_Release` 하위 source를 .NET SDK가 동시에 자동 compile.
- **조치**: nested duplicate source directory 제거, 단일 source tree로 package 재구성.
- **교훈**: SDK-style `.csproj`는 기본적으로 하위 `*.cs`를 자동 포함하므로 archive/backup source를 project root 아래에 두면 안 된다.

## 17. Settings Window 버전 표기가 과거 Beta로 남음
- **증상**: 실제 Beta가 진행되었는데 Settings title은 오래된 Beta 번호 표시.
- **원인**: UI 문자열이 별도 hard-coded되어 release version과 동기화되지 않음.
- **조치**: 정식 V1.1에서 Settings title을 `Desktop Clock Weather - Settings V1.1`로 정리.
- **향후 개선**: Assembly InformationalVersion에서 UI 버전을 읽도록 하면 manual mismatch를 예방할 수 있음.

## 18. Settings 하단 안내문 정렬/배치 문제
- **증상**: `Up to 3 clock windows can be displayed.`가 설명문과 다른 왼쪽 기준선에 보이거나 설명문은 Scroll 영역인데 안내문만 항상 보이는 하단 영역에 분리됨.
- **원인**: 해당 문구가 ScrollViewer 밖 Bottom DockPanel에 배치됨.
- **최종 조치**: 일반 설명문 바로 아래 같은 ScrollViewer 내부로 이동하고 동일한 10px left margin 사용. `Developed by D.J. Lee`만 고정 하단에 유지.

## 19. 도시명이 긴 경우 UI 폭 초과
- **증상**: 긴 도시명이 Weather row 레이아웃을 침범.
- **조치**: ellipsis 적용 + city ToolTip으로 전체 이름 제공.

## 20. 온도 ToolTip 언어
- **요구사항**: Windows UI culture가 한국어이면 `현재/최고/최저`, 그 외에는 `Current/High/Low`.
- **조치**: 설정 toggle을 추가하지 않고 OS UI culture 기반 자동 localize.

---

# 개발 과정에서 확인된 공통 원칙

1. **로그가 추측보다 우선** — 발생 시각, 실행 EXE, Profile, startup mode, display topology를 기록한다.
2. **OS Boundary를 별도 subsystem으로 본다** — Startup, Sleep/Wake, Display, Security는 일반 UI logic과 성격이 다르다.
3. **Single Source of Truth** — 같은 상태를 여러 저장소에서 동시에 판단하지 않는다.
4. **Fail-closed verification** — 설치/삭제는 command return만 믿지 않고 최종 상태를 검증한다.
5. **Source / Installed Binary / User State 분리** — 각각 개발폴더, LOCALAPPDATA, APPDATA로 역할을 나눈다.
6. **Clean Install과 Update를 구분** — migration, profile preservation, startup-state 초기화 정책을 별도로 둔다.
7. **Release 직전에는 기능 추가보다 회귀 안정성 우선** — V1.1 동결 이후 수정은 UI/packaging처럼 영향 범위가 작은 항목 중심으로 한다.

# V1.1 현재 Known Issue

- 부팅 후 저장 위치가 몇 픽셀 또는 소폭 이동하는 경우가 있음.
- Dual-monitor 환경에서 clean-install 첫 부팅이나 Lid Close/Open 후 다른 모니터가 선택되는 경우가 드물게 있음.
- 현재 기능 사용에는 큰 문제가 없으며, 향후 필요 시 physical monitor ID(DisplayConfig/EDID) 기반 보강 후보로 유지한다.
