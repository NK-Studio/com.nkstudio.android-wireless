using System.Collections.Generic;

namespace AndroidWireless
{
    /// <summary>문구 표. 키 → (영어, 한국어). {link}는 링크 문구, {0}…은 string.Format 인자.</summary>
    internal static class Strings
    {
        public static readonly Dictionary<string, (string en, string ko)> Table = new Dictionary<string, (string, string)>
        {
            // 공통
            ["common.close"] = ("Close", "닫기"),
            ["common.cancel"] = ("Cancel", "취소"),
            ["common.tryAgain"] = ("Try again", "다시 시도"),
            ["common.learnMore"] = ("Learn more", "자세히 알아보기"),

            // ⋮ 메뉴 / Preferences
            ["menu.theme"] = ("Theme", "테마"),
            ["menu.language"] = ("Language", "언어"),
            ["menu.themeSetup"] = ("Theme Setup…", "테마 선택 화면…"),
            ["menu.preferences"] = ("Preferences…", "환경설정…"),
            ["prefs.adbPath"] = ("adb Path Override", "adb 경로 지정"),
            ["prefs.browse"] = ("Browse…", "찾아보기…"),
            ["prefs.selectAdb"] = ("Select adb", "adb 선택"),
            ["prefs.adbNote"] = (
                "Used only to start the adb server when it isn't running. Leave empty to pick the newest adb automatically.",
                "adb 서버가 실행 중이 아닐 때 서버를 시작하는 데만 씁니다. 비워 두면 가장 최신 adb를 자동으로 고릅니다."),
            ["prefs.showSetup"] = ("Show Theme Setup Next Time", "다음에 테마 선택 화면 표시"),

            // 기기 목록
            ["list.step1.title"] = ("1. Connect to the Same Network", "1. 같은 네트워크에 연결"),
            ["list.step1.desc"] = ("Ensure your workstation and device are on the same wireless network.", "PC와 기기가 같은 Wi-Fi 네트워크에 연결되어 있는지 확인하세요."),
            ["list.step2.title"] = ("2. Enable Wireless Debugging", "2. 무선 디버깅 사용"),
            ["list.step2.desc"] = ("On your Android 11+ device, go to Developer Options > Wireless debugging. {link}.", "Android 11 이상 기기에서 개발자 옵션 > 무선 디버깅을 켜세요. {link}"),
            ["list.search"] = ("Search for a device by name", "기기 이름으로 검색"),
            ["list.col.name"] = ("Name", "이름"),
            ["list.col.version"] = ("ADB Wi-Fi", "ADB Wi-Fi"),
            ["list.col.address"] = ("IP Address & Port", "IP 주소 및 포트"),
            ["list.col.api"] = ("API", "API"),
            ["list.pair"] = ("Pair", "페어링"),
            ["list.connected"] = ("Connected", "연결됨"),
            ["list.version.unknown"] = ("Unknown ADB Wi-Fi version", "ADB Wi-Fi 버전을 알 수 없음"),
            ["list.openPreferences"] = ("Open Preferences…", "환경설정 열기…"),
            ["list.empty.adbError"] = ("Can't reach the adb server", "adb 서버에 연결할 수 없습니다"),
            ["list.empty.looking"] = ("Looking for devices…", "기기를 찾는 중…"),
            ["list.empty.noMatch"] = ("No devices match \"{0}\"", "\"{0}\"와(과) 일치하는 기기가 없습니다"),
            ["list.empty.none"] = ("No devices found", "기기를 찾지 못했습니다"),
            ["list.empty.noneDesc"] = ("Turn on Wireless debugging on your device. It will show up here automatically.", "기기에서 무선 디버깅을 켜면 여기에 자동으로 나타납니다."),
            ["list.note.oldAdb"] = (
                "Device name, API and ADB Wi-Fi version need Android SDK Platform-Tools 37 or newer.",
                "기기 이름·API·ADB Wi-Fi 버전 표시는 Android SDK Platform-Tools 37 이상이 필요합니다."),

            // Pair 화면
            ["pair.title"] = ("Pair {0} over Wi-Fi", "Wi-Fi로 {0} 페어링"),
            ["pair.desc"] = (
                "Pair devices to enable wireless debugging. Other devices can be paired using a pairing code. {link}.",
                "기기를 페어링해 무선 디버깅을 사용하세요. 다른 기기는 페어링 코드로 페어링할 수 있습니다. {link}"),
            ["pair.tab.qr"] = ("Pair using QR code", "QR 코드로 페어링"),
            ["pair.tab.code"] = ("Pair using pairing code", "페어링 코드로 페어링"),

            // QR 탭
            ["qr.caption"] = ("Scan the QR code from your {0}", "{0}에서 QR 코드를 스캔하세요"),
            ["qr.pairing"] = ("Pairing with {0}…", "{0}와(과) 페어링 중…"),
            ["qr.hintTitle"] = ("QR scanner available at:", "QR 스캐너 위치:"),
            ["qr.hint"] = ("Developer options > Wireless debugging > Pair device with QR code", "개발자 옵션 > 무선 디버깅 > QR 코드로 기기 페어링"),

            // 페어링 코드 탭
            ["code.waiting"] = ("Waiting for {0} to enter pairing mode…", "{0}이(가) 페어링 모드로 들어오기를 기다리는 중…"),
            ["code.available"] = ("Available Wi-Fi devices", "페어링 가능한 Wi-Fi 기기"),
            ["code.deviceAt"] = ("Device at <b>{0}</b>", "<b>{0}</b>의 기기"),
            ["code.availableToPair"] = ("Available to pair", "페어링 가능"),
            ["code.hintTitle"] = ("Set your {0} to pairing mode", "{0}을(를) 페어링 모드로 설정하세요"),
            ["code.hint"] = ("Go to Developer options > Wireless debugging > Pair device with pairing code", "개발자 옵션 > 무선 디버깅 > 페어링 코드로 기기 페어링"),

            // 6자리 입력 오버레이
            ["overlay.message"] = ("Enter the 6 digit code shown on the device at <b>{0}</b> to pair.", "<b>{0}</b> 기기에 표시된 6자리 코드를 입력하세요."),
            ["overlay.pairing"] = ("Pairing…", "페어링 중…"),
            ["overlay.tip"] = ("Tip: paste the code anywhere to fill it in.", "팁: 아무 칸에나 코드를 붙여넣으면 한 번에 채워집니다."),

            // 결과
            ["result.connected"] = ("{0} connected", "{0} 연결됨"),
            ["result.linkOnly"] = ("{link}", "{link}"),
            ["result.another"] = ("Pair another device", "다른 기기 페어링"),

            // 테마 선택
            ["setup.title"] = ("Choose a look", "테마 선택"),
            ["setup.desc"] = (
                "Choose a theme for this window.\nDark / Light follows the Unity editor skin. You can change it later from the ⋮ menu at the top right.",
                "이 창의 테마를 고르세요.\n다크/라이트는 Unity 에디터 스킨을 따르고, 나중에 오른쪽 위 ⋮ 메뉴에서 바꿀 수 있습니다."),
            ["setup.continue"] = ("Continue", "계속"),

            // Android 모듈 없음
            ["module.title"] = ("Android Build Support isn't installed", "Android Build Support 모듈이 설치되어 있지 않습니다"),
            ["module.desc"] = (
                "This tool finds and pairs devices with the adb in Unity's Android module, so it can't be used in this editor. Add Android Build Support to this editor version in Unity Hub, then restart the editor.",
                "이 도구는 Unity Android 모듈의 adb로 기기를 찾고 페어링하기 때문에 이 에디터에서는 사용할 수 없습니다. Unity Hub에서 이 에디터 버전에 Android Build Support를 추가한 뒤 에디터를 다시 시작하세요."),

            // Platform-Tools 업데이트
            ["update.message"] = (
                "Unity's Android SDK has Platform-Tools {0}. ADB Wi-Fi 2.0 needs {1} or newer.",
                "Unity Android SDK의 Platform-Tools가 {0}입니다. ADB Wi-Fi 2.0을 쓰려면 {1} 이상이 필요합니다."),
            ["update.install"] = ("Install", "설치"),
            ["update.later"] = ("Later", "나중에"),
            ["update.installing"] = ("Installing the latest Platform-Tools…", "최신 Platform-Tools 설치 중…"),
            ["update.done"] = ("Platform-Tools {0} installed.", "Platform-Tools {0} 설치를 마쳤습니다."),
            ["update.failed"] = ("Install failed: {0}", "설치하지 못했습니다: {0}"),
            ["update.noSdkManager"] = ("sdkmanager wasn't found in Unity's Android SDK.", "Unity Android SDK에서 sdkmanager를 찾지 못했습니다."),
            ["update.confirmTitle"] = ("Install Android SDK Platform-Tools", "Android SDK Platform-Tools 설치"),
            ["update.confirmBody"] = (
                "The latest Android SDK Platform-Tools will be downloaded with sdkmanager and installed into Unity's Android SDK:",
                "sdkmanager로 최신 Android SDK Platform-Tools를 받아 Unity Android SDK에 설치합니다:"),
            ["update.confirmNote"] = (
                "The running adb server will be restarted. On Windows, administrator permission may be requested if this folder is read-only (e.g. under Program Files).",
                "실행 중인 adb 서버는 다시 시작됩니다. Windows에서 이 폴더에 쓸 권한이 없으면(Program Files 등) 관리자 권한을 요청합니다."),
            ["update.licenseNote"] = ("By installing, you agree to the {link}.", "설치하면 {link}에 동의하게 됩니다."),
            ["update.licenseLink"] = ("Android SDK License Agreement", "Android SDK 라이선스 계약"),
            ["update.confirmOk"] = ("Agree and Install", "동의하고 설치"),
            ["update.elevationDenied"] = ("Administrator permission was denied.", "관리자 권한 요청이 거부되었습니다."),
            ["update.licenseNotAccepted"] = ("sdkmanager didn't accept the Android SDK license.", "sdkmanager가 Android SDK 라이선스 동의를 받지 못했습니다."),

            // 오류
            ["error.pairTimeout"] = ("Pairing timed out. Make sure the device is still in pairing mode.", "페어링 시간이 초과되었습니다. 기기가 아직 페어링 모드인지 확인하세요."),
            ["error.notConnected"] = (
                "Paired, but the device didn't connect. Turn Wireless debugging off and on, then try again.",
                "페어링은 됐지만 기기가 연결되지 않았습니다. 무선 디버깅을 껐다 켠 뒤 다시 시도하세요."),
            ["error.pairFailed"] = ("Pairing failed.", "페어링에 실패했습니다."),
            ["error.lostServer"] = ("Lost connection to the adb server. Reconnecting…", "adb 서버 연결이 끊겼습니다. 다시 연결하는 중…"),
            ["error.noServer"] = ("Can't reach the adb server.", "adb 서버에 연결할 수 없습니다."),
            ["error.noAdb"] = ("The adb server isn't running and adb couldn't be found.", "adb 서버가 실행 중이 아니고 adb를 찾을 수 없습니다."),
            ["error.startServer"] = ("Couldn't start the adb server: {0}", "adb 서버를 시작하지 못했습니다: {0}"),
        };
    }
}
