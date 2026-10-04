# Desktop Clock Weather V1.1 — GitHub Release Guide

## 1. 최종 빌드

Windows 개발 PC에서 `Build_EXE.bat`를 실행합니다.

성공 문구:

```text
SUCCESS - V1.1 build/startup registration verified.
```

생성 파일:

```text
DesktopClockWeather_V1.1.exe
```

## 2. GitHub Release에 올릴 파일

일반 사용자용으로 다음 4개를 같은 폴더/ZIP에 넣는 것을 권장합니다.

- `DesktopClockWeather_V1.1.exe`
- `Install.bat`
- `Uninstall_DesktopClockWeather.bat`
- `README_KO.md` 또는 간단한 설치 안내

소스도 공개할 경우 별도로:

- `DesktopClockWeather_V1.1_Source.zip`

## 3. GitHub Release 생성

- Tag: `v1.1`
- Release title: `Desktop Clock Weather V1.1`
- V1.0은 삭제하지 않고 기존 Release로 유지
- V1.1을 새 Release로 추가

## 4. 일반 사용자 설치 방법

1. Release ZIP을 한 폴더에 압축 해제
2. `Install.bat` 실행
3. `SUCCESS` 확인
4. `DesktopClockWeather_V1.1.exe`를 실행하거나 다음 로그인/재부팅 시 자동 실행 확인

일반 사용자는 .NET SDK나 소스 코드가 필요하지 않습니다. EXE는 self-contained single-file입니다.

## 5. 삭제

`Uninstall_DesktopClockWeather.bat`를 실행하고 `Y`를 선택합니다.

삭제 대상:

- 자동 시작 등록
- `%LOCALAPPDATA%\DesktopClockWeather\V1.1` 설치본
- `%APPDATA%\DesktopClockWeather\V1.1` 사용자 설정/Profile/로그

압축을 풀어 놓은 원본 Release 폴더는 삭제하지 않습니다.

## 6. Release 설명에 넣을 Known Issue

일부 듀얼 모니터 노트북 환경에서 최초 Clean Install 부팅 또는 덮개 Close/Open 직후 외장 모니터가 선택되거나 창 위치가 소폭 이동할 수 있습니다. 저장 후 다음 부팅에서는 대체로 정상 복원됩니다.
