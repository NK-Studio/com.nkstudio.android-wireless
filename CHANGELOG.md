# Changelog

## [1.0.1] - 2026-10-09
### Changed
- Platform-Tools 설치 확인을 OS 대화상자 대신 창 안의 팝업으로 표시 (설치 위치·관리자 권한 안내, 라이선스 링크)

### Fixed
- Windows: Platform-Tools 설치
  - 설치 전에 adb 서버를 내리고, 설치 중에는 다시 띄우지 않도록 수정 (실행 중인 `adb.exe`가 잠겨 교체에 실패하던 문제)
  - Unity SDK 폴더에 쓸 수 없으면(Program Files) 관리자 권한(UAC)으로 sdkmanager를 실행
  - 공백이 있는 경로의 `sdkmanager.bat`을 `cmd /s /c`로 감싸 실행
  - 취소·시간 초과 시 sdkmanager의 자식 java 프로세스까지 종료
  - Windows의 sdkmanager가 stdin의 "y" 응답을 읽지 않아 라이선스 미동의로 아무것도 설치하지 않던 문제: 동의 후 `licenses/android-sdk-license`에 동의 기록을 남긴 뒤 실행
  - 실패 시 진행률 줄 대신 실제 오류 메시지를 표시
- 기기 이름·API 표시 요구 버전 안내를 Platform-Tools 37 이상으로 정정 (36.0.0 서버는 `host:track-mdns-services` 미지원)
- adb 서버 시작: 서버 데몬이 출력 파이프를 물고 있어도 멈추지 않도록 출력은 종료 후 최대 1초만 기다림. 종료 코드가 실패여도 서버가 떠 있으면 성공으로 처리

## [1.0.0] - 2026-10-09
### Changed
- Android Studio의 "Pair devices over Wi-Fi"와 같은 흐름으로 창을 새로 구성 (View 단위 UXML)
  - 시작하면 무선 디버깅이 켜진 기기를 실시간 목록으로 표시 (이름 · ADB Wi-Fi 버전 · IP:포트 · API)
  - Pair → 기기별 화면: QR 탭(기본) / 페어링 코드 탭
  - 페어링 코드는 딤 오버레이의 6칸 입력. 칸 자동 이동, 붙여넣기 시 자동 채움 + 자동 제출
  - 연결 성공 화면
- 기기 정보는 adb 서버 소켓의 `host:track-mdns-services` 스트림으로 받음 (폴링 없음, 구버전 서버는 텍스트 폴백)
- 페어링·연결도 adb CLI 대신 서버 소켓(`host:pair`, `host:connect`)으로 처리해 adb 버전 차이로 서버가 재시작되는 문제를 줄임
- 서버는 6초 이상 응답이 없을 때만 직접 시작하고, 설치된 adb 중 최신 버전을 사용

### Added
- ADB Wi-Fi 버전 원형 표시: v1.0 회색 · v2.0 초록 (Android 17 대비)
- 테마: Android Studio / Unity (다크·라이트는 에디터 스킨을 따름), 첫 실행 시 테마 선택 화면
- 언어: 한국어 / English
- 테마·언어는 창 오른쪽 위 ⋮ 메뉴와 Preferences > Android Wireless에서 변경 (사용자별 EditorPrefs 저장)
- Platform-Tools 37 미만이면 설치 제안 배너 (Unity SDK의 sdkmanager로 설치, 라이선스 동의 확인)
- Android Build Support 모듈이 없으면 사용할 수 없다는 안내 화면
- IBM Plex Sans KR 폰트 (Bold는 사용 글자 서브셋 `AW Sans KR`)
- protobuf 파싱 EditMode 테스트

### Removed
- adb 경로 입력란, 직접 연결, 연결된 기기 섹션, 로그 (adb 경로 지정은 Preferences로 이동)

## [0.1.0] - 2026-10-06
### Added
- 최초 릴리스: QR / 페어링 코드 기반 무선 디버깅 페어링 에디터 창
