# Changelog

## [0.2.0] - 2026-10-09
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

### Removed
- adb 경로 입력란, 직접 연결, 연결된 기기 섹션, 로그 (adb 경로 지정은 Preferences로 이동)

## [0.1.0] - 2026-10-06
### Added
- 최초 릴리스: QR / 페어링 코드 기반 무선 디버깅 페어링 에디터 창
