# Android Wireless Pairing

Android 11+ 무선 디버깅을 유니티 에디터 안에서 페어링/연결하는 에디터 창입니다. Android Studio의 "Pair devices over Wi-Fi"와 같은 흐름으로 동작합니다.

- **기기 목록**: 무선 디버깅이 켜진 기기를 실시간으로 표시합니다 (이름 · ADB Wi-Fi 버전 · IP 주소 및 포트 · API). 이미 연결된 기기는 `Connected`로 표시됩니다.
- **ADB Wi-Fi 버전**: v1.0은 회색, v2.0(Android 17+)은 초록 원으로 구분합니다.
- **QR 페어링**: Pair를 누르면 QR이 바로 뜹니다. 휴대폰 `개발자 옵션 > 무선 디버깅 > QR 코드로 기기 페어링`으로 스캔하면 페어링 → 연결까지 자동으로 진행합니다.
- **페어링 코드**: 휴대폰에서 `페어링 코드로 기기 페어링`을 열면 목록에 나타나고, Pair를 누르면 6칸 입력창이 뜹니다. 한 칸씩 입력하면 다음 칸으로 넘어가고, 아무 칸에나 코드를 붙여넣으면(Ctrl/Cmd+V) 바로 채워지고 제출됩니다.
- **테마 / 언어**: Android Studio · Unity 테마(다크/라이트는 에디터 스킨을 따름), 한국어 · English. 창 오른쪽 위 ⋮ 메뉴나 `Preferences > Android Wireless`에서 바꿉니다. 사용자별(EditorPrefs)로 저장되어 팀원마다 다르게 쓸 수 있습니다.

기기 정보는 adb 서버의 `host:track-mdns-services` 스트림으로 받습니다 (Android SDK Platform-Tools 36+). 그보다 오래된 adb 서버에서는 이름·주소만 표시됩니다.

## 설치

`Window > Package Manager > + > Install package from git URL...`

```
https://github.com/NK-Studio/com.nkstudio.android-wireless.git
```

또는 `Packages/manifest.json`:

```json
"com.nkstudio.android-wireless": "https://github.com/NK-Studio/com.nkstudio.android-wireless.git"
```

## 사용법

`Tools > Android Wireless Pairing`

## 요구 사항

- Unity 6000.0+
- Android 11 이상 기기, PC와 같은 Wi-Fi 네트워크
- adb (Unity Android Build Support 모듈 또는 Android SDK platform-tools)

## 참고: adb 서버가 계속 재시작될 때

Unity 내장 SDK의 adb와 Android Studio 등 다른 도구의 adb 버전이 다르면, 두 도구가 서로 서버를 죽이고 다시 띄우면서 목록이 잠깐씩 비거나 연결이 끊길 수 있습니다. `Preferences > External Tools > Android SDK`를 Android Studio와 같은 SDK로 맞추면 해결됩니다.

## 서드파티

- [QRCoder](https://github.com/codebude/QRCoder) 1.8.0 — MIT (`Editor/Plugins/QRCoder-LICENSE.txt`)
