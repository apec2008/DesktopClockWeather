# Windows Desktop App Startup Integration Know-How

Desktop Clock Weather V1.1 개발 과정에서 확인한 Windows Startup / 보안 / 재부팅 / 상태복원 문제를 다른 Windows 데스크톱 프로젝트에도 재사용할 수 있도록 정리한 문서이다.

## 1. 핵심 결론

작은 프로그램이라도 아래 항목이 겹치면 난이도가 급격히 올라간다.

- Windows Startup 등록
- 백신/보안 프로그램의 persistence 탐지
- Script Host / VBScript / cscript.exe
- 재부팅과 로그온 타이밍
- `%APPDATA%` / `%LOCALAPPDATA%`의 역할 분리
- 실행 중인 EXE 교체와 파일 잠금
- 여러 Profile의 영속 상태 복원
- Sleep/Wake 및 멀티모니터 topology 변화

핵심 원칙은 다음 세 가지다.

1. **자동 실행 등록 방법과 사용자 상태 저장을 분리한다.**
2. **실패 원인은 로그로 특정한 뒤 해당 경로만 수정한다.**
3. **설치 성공은 "명령을 실행했다"가 아니라 결과물을 재검증한 뒤 판정한다.**

## 2. 최종 구조

### 2.1 파일 역할

| 위치 | 역할 |
|---|---|
| `%LOCALAPPDATA%\\DesktopClockWeather\\V1.1` | 실제 Startup에서 실행할 설치 복사본 EXE |
| `%APPDATA%\\DesktopClockWeather\\V1.1\\profiles` | 도시, 색상, 온도단위, Scale, 위치 등 Profile별 사용자 설정 |
| `%APPDATA%\\DesktopClockWeather\\V1.1\\StartupState.json` | 재부팅 후 어떤 Profile을 다시 열 것인지 저장 |
| `%APPDATA%\\DesktopClockWeather\\V1.1\\logs` | 진단 로그 |
| `%APPDATA%\\DesktopClockWeather\\V1.1\\STARTUP_INSTALL_REPORT.txt` | Startup 등록 결과와 실제 활성 방식 기록 |

### 2.2 Startup 방식

기본은 **Windows Task Scheduler의 사용자 로그온 트리거**를 사용한다.

- 로그온 후 짧은 지연(현재 3초)
- 일반 사용자 권한
- 동일 작업 중복 실행 방지
- 실행 인자: `--startup`

Task Scheduler 등록 또는 검증이 실패하면 **Windows Startup 폴더의 ShellLink(.lnk)** 방식으로 자동 fallback한다.

사용자 상태는 어느 Startup 방식이 활성화되었는지와 무관하다. `StartupState.json`이 Profile working set의 유일한 source of truth이다.

## 3. 문제별 원인 분석과 해결

### 3.1 Startup 창 개수가 과거 설정으로 되살아남

**증상**
- 사용자가 2개 창만 남긴 뒤 재부팅했는데 3번째 과거 Profile이 다시 실행됨.

**원인**
- `StartupProfiles.txt`의 `StartupCount=3` 같은 정적 설정과 실제 사용상태가 동시에 Startup source로 사용됨.
- Profile 저장과 "활성 창 목록" 저장이 혼재함.

**해결**
- `StartupState.json`을 추가하고, 재부팅 시 열 Profile 목록을 오직 이 파일만 사용.
- Profile JSON은 도시/색상/Scale/위치만 담당.
- 최초 설치 시 `Default`, `Default2`만 초기화.
- 사용자가 창을 열거나 닫을 때 `StartupState.json` 즉시 갱신.
- Windows 종료/로그오프에 의한 창 종료는 사용자 close로 오인하지 않도록 상태 갱신을 freeze.

**결과**
- 1/2/3개 창 구성 모두 재부팅 후 동일하게 복원.
- 과거 3번째 Profile이 임의로 되살아나는 문제 제거.

**재사용 Know-How**
- "설정 값"과 "현재 활성 객체 목록"은 다른 상태다. 같은 파일이나 같은 카운터로 관리하지 않는다.

### 3.2 VBScript/cscript 기반 Startup 등록이 보안 프로그램과 충돌

**증상**
- Build 중 백신 경고.
- `cscript.exe` 오류 또는 VBS 실행 중단.
- Startup 등록은 실패했는데 EXE 빌드는 성공하여 원인 혼동.

**원인**
- `BAT -> cscript.exe -> VBS -> Startup 변경`은 악성코드의 persistence 패턴과 유사하다.
- 미서명/신규 EXE는 reputation이 낮아 휴리스틱 탐지 가능성이 더 높다.
- 별도로 VBS UTF-8 BOM 때문에 `(1,1) 유효하지 않은 문자` 컴파일 오류도 발생했다.

**해결**
- VBScript와 cscript.exe 의존성을 완전히 제거.
- C# 코드에서 Windows API/COM을 직접 사용.
- Startup-folder fallback에서는 C#이 ShellLink(.lnk)를 직접 생성.

**결과**
- Script Host 관련 오류/인코딩 문제 제거.
- 설치 경로 단순화.

**재사용 Know-How**
- 설치/자동시작 같은 OS 통합 기능은 중간 스크립트 계층을 불필요하게 늘리지 않는다.
- 스크립트를 꼭 쓴다면 BOM/코드페이지를 빌드 전에 검사한다.

### 3.3 HKCU Run 자가등록이 보안 프로그램에 차단

**증상**
- 프로그램이 `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`에 값을 쓰는 순간 persistence 차단.
- `UnauthorizedAccessException` 발생.

**원인**
- 프로그램 본체가 자기 자신을 자동실행 레지스트리에 등록하는 동작은 보안 제품이 민감하게 보는 전형적 persistence 패턴이다.

**해결**
- HKCU Run 방식 폐기.
- Task Scheduler + ShellLink fallback 구조로 전환.
- 제거 시에는 과거 테스트 버전에서 남겼을 수 있는 legacy Run value도 방어적으로 삭제.

**결과**
- Registry Run 의존 제거.

**재사용 Know-How**
- "기술적으로 가능"한 Startup 등록 방법보다 환경에서 실제로 허용되고 검증 가능한 방법을 선택한다.

### 3.4 Windows Startup 폴더 방식의 1분 가까운 실행 지연

**증상**
- 바탕화면이 보인 뒤 첫 Clock 창까지 약 64초.
- 사용자는 자동실행 실패로 판단할 수준.

**로그 분석**
- Startup Launcher가 시작된 뒤 첫 창은 약 4초 후 표시.
- 두 창 전체는 약 6~7초 내 표시.
- 따라서 약 1분 지연은 앱 내부가 아니라 Windows가 Startup 폴더 항목을 늦게 실행한 구간.

**해결**
- Task Scheduler 로그온 트리거 기반 Fast Startup 도입.
- 3초 deterministic delay 후 `--startup` 실행.
- 실패 시 Startup 폴더 방식 자동 fallback.

**결과**
- 실제 테스트에서 첫 창 약 21~23초, 두 번째 창 약 2초 후 표시.
- 기존 64초 대비 체감 대폭 개선.

**재사용 Know-How**
- 부팅 성능 문제는 반드시 `OS가 앱을 언제 시작했는지`와 `앱 시작 후 창이 언제 떴는지`를 분리 측정한다.
- 앱 내부 최적화로 OS 스케줄링 지연을 해결하려 하지 않는다.

### 3.5 설치 성공/실패 판정이 부정확함

**증상**
- EXE는 생성됐지만 Startup 등록은 실패.
- 오래된 `STARTUP_INSTALL_REPORT.txt`가 남아 새 결과처럼 보임.

**원인**
- Build 성공과 설치 성공을 하나로 취급.
- stale report를 삭제하지 않고 재사용.

**해결**
- Build 시작 시 이전 report 삭제.
- 설치 프로세스 return code 검증.
- 새 report의 Build ID, `Success: True`, EXE target, `--startup`, StartupState 존재, ActiveMode를 모두 재검증.
- 하나라도 실패하면 전체 작업을 실패 처리.

**결과**
- "EXE 빌드 성공"과 "Startup 설치 성공"이 명확하게 분리됨.

**재사용 Know-How**
- Installer는 **fail closed**가 원칙이다. 검증되지 않은 성공은 성공이 아니다.

### 3.6 실행 중 EXE 교체 / 파일 잠금

**증상**
- Startup에서 실행 중인 구버전 EXE를 새 빌드가 덮어쓰지 못함.
- 재부팅 후 구버전 UI가 다시 실행되는 혼재 현상.

**원인**
- Windows에서 실행 중인 EXE를 동일 경로에 덮어쓰는 설계.
- Startup 바로가기와 개발 폴더가 직접 연결됨.

**해결**
- Startup용 설치 복사본을 `%LOCALAPPDATA%`에 별도 저장.
- Beta에서는 버전이 포함된 EXE 파일명을 사용.
- Startup 등록은 항상 해당 설치 복사본을 가리키도록 갱신.

**결과**
- 개발 폴더와 Startup 실행본 분리.
- 구/신버전 혼재 및 파일 lock 문제 감소.

**재사용 Know-How**
- 빌드 산출물과 실제 설치 실행본을 같은 파일로 보지 않는다.

### 3.7 Clean Install인데 과거 Scale/위치가 되살아남

**증상**
- Uninstall 후 재설치했는데 Seoul -10%, Ann Arbor -30% 등 과거 테스트 상태가 복원.

**원인**
- V1.1 Profile이 없으면 V1.0/legacy 설정을 자동 import하는 migration 코드가 살아 있었음.

**해결**
- V1.1 정식 정책에서는 missing V1.1 profile을 true first-run으로 취급.
- legacy auto-import 제거.
- 최초값을 Seoul + Ann Arbor, Scale 0%, Celsius로 명시.

**결과**
- Clean Install과 Upgrade의 의미가 분리됨.

**재사용 Know-How**
- Migration은 영구 기능이 아니다. 제품 전환 기간이 끝나면 제거하거나 명시적 사용자 선택으로 바꾼다.

### 3.8 멀티모니터 / Sleep-Wake 위치 이동

**증상**
- 덮개 Close/Open 시 간헐적으로 확장 모니터로 이동.
- Sleep/Wake 후 몇 픽셀 정도 위치 drift.

**원인**
- 노트북 패널이 사라졌다 나타나면서 Windows display topology가 재구성됨.
- `DISPLAY1/DISPLAY2` 같은 논리 이름은 물리 모니터의 영구 ID가 아니다.
- WPF logical coordinate와 Win32 monitor coordinate, DPI 타이밍이 겹침.

**현재 대응**
- topology snapshot이 안정된 뒤 위치 복원.
- Profile에 monitor/anchor/offset 저장.
- Scale 변경 시 눈에 보이는 Clock content의 오른쪽 위를 시각 anchor로 유지.

**결과**
- Sleep/Wake는 반복 시험에서 정상 수준.
- lid close/open의 간헐적 monitor 선택/소폭 drift는 관찰 항목으로 남음.

**추후 개선 가능성**
- 필요하면 DisplayConfig/EDID 기반 physical monitor identity를 도입.

**재사용 Know-How**
- 멀티모니터 앱에서 `DISPLAYn` 문자열을 영구 하드웨어 ID로 간주하지 않는다.

## 4. 진단 로그 설계 원칙

재현하기 어려운 OS 경계 문제는 로그가 없으면 추측으로 흐르기 쉽다.

### 반드시 기록할 항목

- Build ID / EXE 실제 경로 / PID
- Startup launcher 시작 시각
- Profile별 프로세스 launch 시각
- 창 생성 / 실제 표시 시각
- display topology snapshot
- placement resolve 결과
- Startup registration method와 active mode
- Sleep/Wake, SessionEnding, DisplayChanged 이벤트
- 설정 저장 reason

### 분석 순서

1. **어느 프로세스가 언제 시작됐는가**
2. **OS가 실행을 늦춘 것인가, 앱 내부가 늦은 것인가**
3. **설정 파일에는 무엇이 저장돼 있었는가**
4. **복원 시 어떤 monitor/anchor를 선택했는가**
5. **실패 직전 마지막 성공 로그는 무엇인가**

로그 없이 "아마 ~일 것이다"를 반복하지 않는다.

## 5. 신규 프로젝트에 재사용할 권장 체크리스트

### Startup / Install
- [ ] Startup registration과 사용자 상태 저장을 분리
- [ ] Task Scheduler / Startup folder 중 주 방식과 fallback 정의
- [ ] 설치본은 `%LOCALAPPDATA%` 등 안정된 경로에 복사
- [ ] `--startup` 같은 명시적 startup mode 사용
- [ ] 설치 후 등록 대상을 다시 읽어 target/argument 검증
- [ ] stale report 제거 후 새 report만 사용
- [ ] 성공 조건을 단계별로 명문화

### Persistence
- [ ] Profile 설정과 active working set을 별도 저장
- [ ] Windows shutdown을 user close로 오인하지 않음
- [ ] Clean Install과 Upgrade/Migration 경로 분리
- [ ] schema/version 필드 사용

### Security
- [ ] VBScript/cscript/PowerShell을 불필요하게 체인하지 않음
- [ ] HKCU Run 자가등록은 보안제품 영향 검토
- [ ] 백신 전체 비활성화를 정상 설치 절차로 요구하지 않음
- [ ] 최종 배포 시 code signing/installer 검토

### Reboot / Display
- [ ] 로그인 후 첫 UI 표시시간 측정
- [ ] Startup launcher start와 WindowShown timestamp 분리
- [ ] multi-monitor topology 변화 후 복원 시험
- [ ] Sleep/Wake, lid close/open, reboot를 별도 시나리오로 시험

## 6. 사용자용 설치/삭제 UX 원칙

### 설치
- 사용자가 `Build_EXE.bat`를 실행한 시점에 이미 의사가 명확하므로 중복 Y/N 질문을 두지 않는다.
- 진행 단계를 `[1/5]`처럼 표시한다.
- 마지막에는 `SUCCESS` 또는 `ERROR`를 명확히 표시한다.
- 실패 시 `BUILD_LOG.txt`와 `STARTUP_INSTALL_REPORT.txt` 위치를 안내한다.

### 삭제
- 사용자 설정을 실제로 지우므로 Y/N 확인을 유지한다.
- 삭제 대상과 유지 대상을 실행 전에 명확히 표시한다.
- Startup 등록, 설치본, 사용자 데이터 순으로 제거한다.
- source/build folder는 삭제하지 않는다.

## 7. Desktop Clock Weather V1.1 최종 기준

V1.1은 기능 동결 및 공식 Release 단계로 전환하였다.

검증 완료 항목:

- Reboot 후 1/2/3개 working set 복원
- Task Scheduler Fast Startup + Startup-folder fallback
- Clean Uninstall -> reinstall -> reboot
- Sleep/Wake
- 도시/색상/Scale/온도단위 유지
- ToolTip / 장문 도시명
- StartupState와 Profile persistence 분리

잔여 Known Issue:

- 일부 듀얼 모니터 노트북에서 최초 Clean Install 부팅 또는 lid close/open 직후 외장 모니터가 선택되거나 위치가 소폭 이동할 수 있다. 저장 후 재부팅에서는 대체로 정상 복원된다.

향후 다른 Windows 데스크톱 프로젝트에서는 이 문서의 Startup/Persistence/Logging/Verification 패턴을 기본 템플릿으로 재사용한다.
