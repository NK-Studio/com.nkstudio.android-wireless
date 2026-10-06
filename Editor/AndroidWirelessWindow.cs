using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// 안드로이드 11+ 무선 디버깅 페어링 창.
    /// QR: WIFI:T:ADB;S:{name};P:{password};; 를 띄우고, 휴대폰이 mDNS로 알리는 페어링 서비스를 찾아 adb pair → adb connect.
    /// 페어링 코드: 휴대폰에 표시된 IP:포트와 6자리 코드로 adb pair → adb connect.
    /// </summary>
    public class AndroidWirelessWindow : EditorWindow
    {
        private const string TabPrefKey = "AndroidWireless.Tab";
        private const int ScanTimeoutSeconds = 120;
        private const int ConnectTimeoutSeconds = 20;
        private const int MaxLogLines = 300;

        private static readonly Regex AddressRegex = new Regex(@"^[\w\.\-]+:\d{1,5}$");
        private static readonly Regex CodeRegex = new Regex(@"^\d{6}$");

        // 스크립트 Inspector의 기본 참조로 지정된다. 비어 있으면 같은 이름의 에셋을 찾는다.
        [SerializeField] private VisualTreeAsset windowTemplate;
        [SerializeField] private VisualTreeAsset deviceRowTemplate;

        private enum Status { Idle, Busy, Ok, Error }

        private Label adbStatusLabel;
        private TextField adbOverrideField;
        private Button tabQr, tabCode;
        private VisualElement pageQr, pageCode;
        private Image qrImage;
        private Label qrPlaceholder;
        private Button qrStartButton;
        private TextField codeAddressField, codeCodeField;
        private Button codePairButton;
        private VisualElement statusBar;
        private Label statusText;
        private TextField connectAddressField;
        private Button connectButton;
        private VisualElement deviceList;
        private Label deviceEmptyLabel;
        private Label logLabel;
        private ScrollView logScroll;

        private readonly StringBuilder log = new StringBuilder();
        private int logLineCount;

        private CancellationTokenSource windowCts;
        private CancellationTokenSource operationCts;
        private Texture2D qrTexture;

        private bool IsOperationRunning => operationCts != null;

        [MenuItem("Tools/Android Wireless Pairing")]
        public static void Open()
        {
            var window = GetWindow<AndroidWirelessWindow>();
            window.titleContent = new GUIContent("Android Wireless");
            window.minSize = new Vector2(360, 520);
        }

        private void OnEnable()
        {
            windowCts = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            CancelOperation();
            windowCts?.Cancel();
            windowCts?.Dispose();
            windowCts = null;
            DestroyQrTexture();
        }

        // ---------------------------------------------------------------- UI 구성

        public void CreateGUI()
        {
            var template = windowTemplate != null ? windowTemplate : FindAsset<VisualTreeAsset>("AndroidWirelessWindow");
            if (deviceRowTemplate == null) deviceRowTemplate = FindAsset<VisualTreeAsset>("DeviceRow");
            if (template == null)
            {
                rootVisualElement.Add(new Label("AndroidWirelessWindow.uxml을 찾을 수 없습니다."));
                return;
            }

            template.CloneTree(rootVisualElement);
            var root = rootVisualElement;
            root.style.flexGrow = 1;

            adbStatusLabel = root.Q<Label>("adb-status");
            adbOverrideField = root.Q<TextField>("adb-override");
            tabQr = root.Q<Button>("tab-qr");
            tabCode = root.Q<Button>("tab-code");
            pageQr = root.Q("page-qr");
            pageCode = root.Q("page-code");
            qrImage = root.Q<Image>("qr-image");
            qrPlaceholder = root.Q<Label>("qr-placeholder");
            qrStartButton = root.Q<Button>("qr-start");
            codeAddressField = root.Q<TextField>("code-address");
            codeCodeField = root.Q<TextField>("code-code");
            codePairButton = root.Q<Button>("code-pair");
            statusBar = root.Q("status-bar");
            statusText = root.Q<Label>("status-text");
            connectAddressField = root.Q<TextField>("connect-address");
            connectButton = root.Q<Button>("connect-button");
            deviceList = root.Q("device-list");
            deviceEmptyLabel = root.Q<Label>("device-empty");
            logLabel = root.Q<Label>("log");
            logScroll = root.Q<ScrollView>("log-scroll");

            adbOverrideField.SetValueWithoutNotify(AdbClient.OverridePath);
            adbOverrideField.RegisterCallback<FocusOutEvent>(_ => ApplyAdbOverride(adbOverrideField.value));
            root.Q<Button>("adb-browse").clicked += BrowseAdb;
            root.Q<Button>("adb-reset").clicked += () => ApplyAdbOverride("");
            root.Q<Button>("adb-refresh").clicked += () => _ = RefreshEnvironmentAsync();

            tabQr.clicked += () => ShowTab(0);
            tabCode.clicked += () => ShowTab(1);
            ShowTab(EditorPrefs.GetInt(TabPrefKey, 0));

            qrStartButton.clicked += OnQrButtonClicked;
            codePairButton.clicked += () => _ = PairWithCodeAsync();
            connectButton.clicked += () => _ = ConnectManuallyAsync();
            root.Q<Button>("devices-refresh").clicked += () => _ = RefreshDevicesAsync();
            root.Q<Button>("log-clear").clicked += ClearLog;

            SetStatus(Status.Idle, "대기 중");
            _ = RefreshEnvironmentAsync();
            _ = RefreshDevicesAsync();
        }

        private static T FindAsset<T>(string name) where T : UnityEngine.Object
        {
            foreach (var guid in AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                    return AssetDatabase.LoadAssetAtPath<T>(path);
            }
            return null;
        }

        private void ShowTab(int index)
        {
            if (IsOperationRunning) return;
            EditorPrefs.SetInt(TabPrefKey, index);
            tabQr.EnableInClassList("aw-tab--selected", index == 0);
            tabCode.EnableInClassList("aw-tab--selected", index == 1);
            pageQr.EnableInClassList("aw-page--hidden", index != 0);
            pageCode.EnableInClassList("aw-page--hidden", index != 1);
        }

        private void SetStatus(Status status, string message)
        {
            statusBar.EnableInClassList("aw-status--idle", status == Status.Idle);
            statusBar.EnableInClassList("aw-status--busy", status == Status.Busy);
            statusBar.EnableInClassList("aw-status--ok", status == Status.Ok);
            statusBar.EnableInClassList("aw-status--error", status == Status.Error);
            statusText.text = message;
            Log(message);
        }

        private void SetInputsEnabled(bool enabled)
        {
            tabQr.SetEnabled(enabled);
            tabCode.SetEnabled(enabled);
            codePairButton.SetEnabled(enabled);
            connectButton.SetEnabled(enabled);
        }

        // ---------------------------------------------------------------- ADB 환경

        private void BrowseAdb()
        {
            string path = EditorUtility.OpenFilePanel("adb 선택", "", "");
            if (!string.IsNullOrEmpty(path)) ApplyAdbOverride(path);
        }

        private void ApplyAdbOverride(string path)
        {
            path = path?.Trim() ?? "";
            if (path == AdbClient.OverridePath) return;
            AdbClient.OverridePath = path;
            adbOverrideField.SetValueWithoutNotify(path);
            _ = RefreshEnvironmentAsync();
        }

        private async Task RefreshEnvironmentAsync()
        {
            string adb = AdbClient.ResolveAdbPath();
            if (string.IsNullOrEmpty(adb))
            {
                adbStatusLabel.text = "adb를 찾을 수 없습니다. Unity Android 모듈을 설치하거나 adb 경로를 지정하세요.";
                return;
            }

            adbStatusLabel.text = $"{adb}\n확인 중…";
            string version = await AdbClient.GetVersionAsync();
            var mdns = await AdbClient.RunAsync("mdns check");
            if (adbStatusLabel == null) return; // 창이 닫힘

            string mdnsText = mdns.Success ? mdns.Output.Split('\n')[0] : "mDNS 사용 불가 — QR 페어링이 동작하지 않습니다. 페어링 코드 방식을 사용하세요.";
            adbStatusLabel.text = $"{adb}\nVersion {version ?? "?"}  ·  {mdnsText}";
        }

        // ---------------------------------------------------------------- QR 페어링

        private void OnQrButtonClicked()
        {
            if (IsOperationRunning)
            {
                CancelOperation();
                return;
            }
            _ = PairWithQrAsync();
        }

        private async Task PairWithQrAsync()
        {
            var token = BeginOperation();
            qrStartButton.text = "취소";

            // Android Studio와 같은 형식의 서비스 이름. 휴대폰은 이 이름으로 mDNS 페어링 서비스를 연다.
            string serviceName = "studio-" + RandomString(10);
            string password = RandomString(12);
            ShowQr($"WIFI:T:ADB;S:{serviceName};P:{password};;");

            try
            {
                SetStatus(Status.Busy, "휴대폰으로 QR 코드를 스캔하세요…");
                MdnsService? pairing = await WaitForMdnsAsync(
                    s => s.IsPairing && s.Name == serviceName, ScanTimeoutSeconds, token);

                if (pairing == null)
                {
                    SetStatus(Status.Error, "시간 초과: 스캔이 감지되지 않았습니다. 같은 Wi-Fi인지 확인하고 QR을 다시 만드세요.");
                    return;
                }

                HideQr("스캔 완료");
                await PairAndConnectAsync(pairing.Value.Address, password, token);
            }
            catch (OperationCanceledException)
            {
                SetStatus(Status.Idle, "취소됨");
            }
            finally
            {
                HideQr("'QR 만들기'를 누르세요");
                EndOperation();
                qrStartButton.text = "QR 만들기";
            }
        }

        private void ShowQr(string payload)
        {
            DestroyQrTexture();
            qrTexture = QrTextureBuilder.Build(payload);
            qrImage.image = qrTexture;
            qrPlaceholder.style.display = DisplayStyle.None;
        }

        private void HideQr(string placeholder)
        {
            if (qrImage == null) return;
            qrImage.image = null;
            qrPlaceholder.text = placeholder;
            qrPlaceholder.style.display = DisplayStyle.Flex;
            DestroyQrTexture();
        }

        private void DestroyQrTexture()
        {
            if (qrTexture != null) DestroyImmediate(qrTexture);
            qrTexture = null;
        }

        // ---------------------------------------------------------------- 페어링 코드

        private async Task PairWithCodeAsync()
        {
            string address = codeAddressField.value.Trim();
            string code = codeCodeField.value.Trim();

            if (!AddressRegex.IsMatch(address))
            {
                SetStatus(Status.Error, "IP 주소 및 포트 형식이 올바르지 않습니다. 예: 192.168.0.12:37123");
                return;
            }
            if (!CodeRegex.IsMatch(code))
            {
                SetStatus(Status.Error, "페어링 코드는 6자리 숫자입니다.");
                return;
            }

            var token = BeginOperation();
            try
            {
                await PairAndConnectAsync(address, code, token);
                if (statusBar.ClassListContains("aw-status--ok")) codeCodeField.value = "";
            }
            catch (OperationCanceledException)
            {
                SetStatus(Status.Idle, "취소됨");
            }
            finally
            {
                EndOperation();
            }
        }

        // ---------------------------------------------------------------- 공통 흐름

        private async Task PairAndConnectAsync(string pairAddress, string secret, CancellationToken token)
        {
            SetStatus(Status.Busy, $"페어링 중… ({pairAddress})");
            var (paired, guid, message) = await AdbClient.PairAsync(pairAddress, secret, token);
            Log(message);
            if (!paired)
            {
                SetStatus(Status.Error, "페어링 실패: " + FirstLine(message));
                return;
            }

            string host = pairAddress.Substring(0, pairAddress.LastIndexOf(':'));
            SetStatus(Status.Busy, "페어링 완료. 연결 중…");
            string connected = await WaitForConnectionAsync(guid, host, token);

            if (connected != null)
            {
                SetStatus(Status.Ok, $"연결됨: {connected}");
                connectAddressField.value = connected;
            }
            else
            {
                connectAddressField.value = host + ":";
                SetStatus(Status.Error, "페어링은 됐지만 자동 연결에 실패했습니다. 무선 디버깅 화면의 'IP 주소 및 포트'를 아래 '직접 연결'에 입력하세요.");
            }
            await RefreshDevicesAsync();
        }

        /// <summary>
        /// 페어링 후 휴대폰이 _adb-tls-connect 서비스를 알리면 연결한다.
        /// adb 서버가 mDNS로 자동 연결하는 경우도 있으므로 기기 목록도 함께 확인한다.
        /// </summary>
        private async Task<string> WaitForConnectionAsync(string guid, string host, CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddSeconds(ConnectTimeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();

                var devices = await AdbClient.GetDevicesAsync(token);
                var already = devices.FirstOrDefault(d => d.State == "device" &&
                    ((guid != null && d.Serial.StartsWith(guid, StringComparison.Ordinal)) ||
                     d.Serial.StartsWith(host + ":", StringComparison.Ordinal)));
                if (already.Serial != null) return already.Serial;

                var services = await AdbClient.GetMdnsServicesAsync(token);
                var connect = services.FirstOrDefault(s => s.IsConnect && (s.Name == guid || s.Host == host));
                if (connect.Name != null)
                {
                    var (ok, message) = await AdbClient.ConnectAsync(connect.Address, token);
                    Log(message);
                    if (ok) return connect.Address;
                }

                await Task.Delay(1000, token);
            }
            return null;
        }

        private async Task<MdnsService?> WaitForMdnsAsync(Func<MdnsService, bool> match, int timeoutSeconds, CancellationToken token)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < deadline)
            {
                token.ThrowIfCancellationRequested();
                var services = await AdbClient.GetMdnsServicesAsync(token);
                foreach (var s in services)
                    if (match(s)) return s;
                await Task.Delay(1000, token);
            }
            return null;
        }

        private async Task ConnectManuallyAsync()
        {
            string address = connectAddressField.value.Trim();
            if (!AddressRegex.IsMatch(address))
            {
                SetStatus(Status.Error, "IP 주소 및 포트 형식이 올바르지 않습니다. 예: 192.168.0.12:41234");
                return;
            }

            var token = BeginOperation();
            try
            {
                SetStatus(Status.Busy, $"연결 중… ({address})");
                var (ok, message) = await AdbClient.ConnectAsync(address, token);
                Log(message);
                if (ok) SetStatus(Status.Ok, $"연결됨: {address}");
                else SetStatus(Status.Error, "연결 실패: " + FirstLine(message) + " (처음 연결하는 기기라면 먼저 페어링하세요)");
                await RefreshDevicesAsync();
            }
            catch (OperationCanceledException)
            {
                SetStatus(Status.Idle, "취소됨");
            }
            finally
            {
                EndOperation();
            }
        }

        private CancellationToken BeginOperation()
        {
            CancelOperation();
            operationCts = CancellationTokenSource.CreateLinkedTokenSource(windowCts.Token);
            SetInputsEnabled(false);
            return operationCts.Token;
        }

        private void EndOperation()
        {
            operationCts?.Dispose();
            operationCts = null;
            if (statusBar != null) SetInputsEnabled(true);
        }

        private void CancelOperation()
        {
            operationCts?.Cancel();
        }

        // ---------------------------------------------------------------- 기기 목록

        private async Task RefreshDevicesAsync()
        {
            var devices = await AdbClient.GetDevicesAsync(windowCts?.Token ?? default);
            if (deviceList == null) return;

            deviceList.Clear();
            deviceEmptyLabel.style.display = devices.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (var device in devices)
            {
                var row = deviceRowTemplate.Instantiate();
                row.Q<Label>("device-model").text = string.IsNullOrEmpty(device.Model) ? "(알 수 없는 기기)" : device.Model;
                row.Q<Label>("device-serial").text = $"{device.Serial}  ·  {device.State}";
                row.Q<Label>("device-badge").text = device.IsWireless ? "Wi-Fi" : "USB";
                row.Q("device-dot").EnableInClassList("aw-device-dot--offline", device.State != "device");

                var disconnect = row.Q<Button>("device-disconnect");
                disconnect.style.display = device.IsWireless ? DisplayStyle.Flex : DisplayStyle.None;
                string serial = device.Serial;
                disconnect.clicked += () => _ = DisconnectAsync(serial);

                deviceList.Add(row);
            }
        }

        private async Task DisconnectAsync(string serial)
        {
            var result = await AdbClient.DisconnectAsync(serial);
            Log(result.Output);
            if (statusBar == null) return;
            SetStatus(result.Success ? Status.Idle : Status.Error, result.Success ? $"연결 해제: {serial}" : "연결 해제 실패: " + FirstLine(result.Output));
            await RefreshDevicesAsync();
        }

        // ---------------------------------------------------------------- 로그 / 유틸

        private void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || logLabel == null) return;

            foreach (var line in message.Trim().Split('\n'))
            {
                log.Append('[').Append(DateTime.Now.ToString("HH:mm:ss")).Append("] ").AppendLine(line.TrimEnd());
                logLineCount++;
            }

            while (logLineCount > MaxLogLines)
            {
                int cut = log.ToString().IndexOf('\n');
                if (cut < 0) break;
                log.Remove(0, cut + 1);
                logLineCount--;
            }

            logLabel.text = log.ToString();
            logScroll.schedule.Execute(() => logScroll.scrollOffset = new Vector2(0, float.MaxValue));
        }

        private void ClearLog()
        {
            log.Clear();
            logLineCount = 0;
            logLabel.text = "";
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "(출력 없음)";
            return text.Trim().Split('\n')[0];
        }

        private static string RandomString(int length)
        {
            const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);

            var sb = new StringBuilder(length);
            foreach (var b in bytes) sb.Append(alphabet[b % alphabet.Length]);
            return sb.ToString();
        }
    }
}
