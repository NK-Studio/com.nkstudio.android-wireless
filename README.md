# Android Wireless Pairing

Android 11+ 무선 디버깅을 Unity 에디터 안에서 페어링하고 연결하는 에디터 창입니다. Android Studio의 "Pair devices over Wi-Fi"와 같은 흐름으로 동작합니다.

## 기능

- **기기 목록**: 같은 네트워크에서 무선 디버깅이 켜진 기기를 실시간으로 보여 줍니다. 이름 · ADB Wi-Fi 버전 · IP 주소 및 포트 · API를 표시하고, 이미 연결된 기기는 "연결됨"으로 표시합니다. 이름·IP로 검색할 수 있습니다.
- **ADB Wi-Fi 버전 표시**: v1.0은 회색, v2.0(Android 17+)은 초록 원으로 구분합니다.
- **QR 페어링**: 페어링 버튼을 누르면 QR이 바로 뜹니다. 휴대폰 `개발자 옵션 > 무선 디버깅 > QR 코드로 기기 페어링`으로 스캔하면 페어링부터 연결까지 자동으로 진행합니다.
- **페어링 코드**: 휴대폰에서 `페어링 코드로 기기 페어링`을 열면 "페어링 코드로 페어링" 탭에 기기가 나타납니다. 페어링을 누르면 6칸 입력창이 뜨고, 한 칸씩 입력하면 다음 칸으로 넘어갑니다. 아무 칸에나 코드를 붙여넣으면(Ctrl/Cmd+V) 한 번에 채워지고 바로 제출됩니다.
- **Platform-Tools 업데이트 안내**: Unity가 쓰는 Android SDK의 Platform-Tools가 37 미만이면 목록 위에 설치를 제안합니다. 설치를 누르면 Android SDK 라이선스 동의를 확인한 뒤, Unity SDK에 들어 있는 `sdkmanager`로 최신 버전을 설치합니다.
- **테마**: Android Studio / Unity 두 가지. 다크·라이트는 에디터 스킨을 따릅니다. 처음 열 때 테마 선택 화면이 나옵니다.
- **언어**: 한국어 / English.

테마와 언어는 창 오른쪽 위 ⋮ 메뉴나 `Preferences > Android Wireless`에서 바꿉니다. 프로젝트가 아니라 사용자별(EditorPrefs)로 저장됩니다.

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

1. `Tools > Android Wireless Pairing`으로 창을 엽니다.
2. 휴대폰에서 `개발자 옵션 > 무선 디버깅`을 켜면 목록에 나타납니다.
3. 페어링을 누르고 QR을 스캔하거나, "페어링 코드로 페어링" 탭에서 6자리 코드를 입력합니다.

## 요구 사항

- Unity 6000.0+
- **Android Build Support 모듈** (없으면 창에 사용할 수 없다는 안내만 표시됩니다)
- Android 11 이상 기기, PC와 같은 Wi-Fi 네트워크
- 기기 이름·API·ADB Wi-Fi 버전 표시는 Android SDK Platform-Tools 36 이상, ADB Wi-Fi 2.0은 37 이상

## 동작 방식

- 기기 목록과 연결 상태는 실행 중인 adb 서버(기본 `127.0.0.1:5037`)에 직접 붙어 `host:track-mdns-services`, `host:track-devices` 스트림으로 받습니다. 폴링하지 않습니다.
- 페어링과 연결도 adb CLI 대신 서버에 직접 요청합니다(`host:pair`, `host:connect`). 버전이 다른 adb CLI를 실행해서 서버가 재시작되는 일을 줄이기 위해서입니다.
- adb 서버가 6초 넘게 응답하지 않을 때만 직접 서버를 띄웁니다. 이때는 Unity SDK·`ANDROID_HOME`·기본 SDK 위치의 adb 중 가장 최신 버전을 씁니다. adb 경로는 `Preferences > Android Wireless`에서 직접 지정할 수도 있습니다.

## 문제 해결: 목록이 잠깐씩 비거나 연결이 끊길 때

Unity와 다른 도구(Android Studio 등)가 서로 adb 서버를 종료하고 다시 띄우면서 생길 수 있습니다.

- Unity의 `Preferences > External Tools > Kill external ADB instances`가 켜져 있으면, Unity가 자기 SDK가 아닌 adb 서버를 종료합니다. 다른 도구와 같이 쓴다면 이 옵션을 끄세요.
- Unity SDK와 다른 도구의 Platform-Tools 버전이 다르면 서로 서버를 재시작시킵니다. 창의 업데이트 안내로 Unity SDK를 최신으로 올리거나, `Preferences > External Tools > Android SDK`를 같은 SDK로 맞추세요.

## 서드파티

- [QRCoder](https://github.com/codebude/QRCoder) 1.8.0 — MIT (`Editor/Plugins/QRCoder-LICENSE.txt`)
- [IBM Plex Sans KR](https://github.com/IBM/plex) — SIL Open Font License 1.1 (`Editor/UI/Fonts/IBMPlex-LICENSE.txt`)
  - Regular는 원본 그대로 포함합니다.
  - Bold는 이 창에서 쓰는 글자만 남긴 서브셋입니다. OFL의 Reserved Font Name("Plex") 규정에 따라 이름을 `AW Sans KR`로 바꿨습니다.
