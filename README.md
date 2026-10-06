# Android Wireless Pairing

Android 11+ 무선 디버깅을 유니티 에디터 안에서 페어링/연결하는 에디터 창입니다.

- **QR 페어링**: 에디터에 표시된 QR을 휴대폰 `개발자 옵션 > 무선 디버깅 > QR 코드로 기기 페어링`으로 스캔하면, mDNS로 페어링 서비스를 찾아 `adb pair` → `adb connect`까지 자동으로 진행합니다.
- **페어링 코드**: 휴대폰에 표시된 `IP:포트`와 6자리 코드로 페어링합니다.
- 수동 `adb connect`, 연결된 기기 목록, 로그 표시
- adb 경로 자동 탐지 (Unity Android 모듈 SDK) + 수동 지정

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

## 서드파티

- [QRCoder](https://github.com/codebude/QRCoder) 1.8.0 — MIT (`Editor/Plugins/QRCoder-LICENSE.txt`)
