using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>시작 화면: 무선 디버깅이 켜진 기기를 실시간으로 나열하고 Pair로 넘어간다.</summary>
    internal sealed class DeviceListView : ViewBase
    {
        private readonly WindowContext context;
        private readonly VisualTreeAsset rowTemplate;
        private readonly TextField searchField;
        private readonly ScrollView rows;
        private readonly VisualElement empty;
        private readonly Spinner emptySpinner;
        private readonly Label emptyTitle, emptyDesc, footerNote;
        private readonly Button emptyButton, restartButton;
        private bool restartingAdb, restartedAdb;
        private string restartError;

        public DeviceListView(WindowContext context) : base("DeviceList")
        {
            this.context = context;
            rowTemplate = UiAssets.LoadView("DeviceRow");

            BindLink(Root.Q<Label>("step2-desc"),
                "list.step2.desc", "common.learnMore", () => Application.OpenURL(WindowContext.LearnMoreUrl));

            var search = Root.Q("search");
            searchField = Root.Q<TextField>("search-field");
            searchField.RegisterValueChangedCallback(_ => Rebuild());
            searchField.RegisterCallback<FocusInEvent>(_ => search.AddToClassList("aw-search--focused"));
            searchField.RegisterCallback<FocusOutEvent>(_ => search.RemoveFromClassList("aw-search--focused"));

            rows = Root.Q<ScrollView>("rows");
            empty = Root.Q("empty");
            emptySpinner = Root.Q<Spinner>("empty-spinner");
            emptyTitle = Root.Q<Label>("empty-title");
            emptyDesc = Root.Q<Label>("empty-desc");
            emptyButton = Root.Q<Button>("empty-adb");
            emptyButton.clicked += () => SettingsService.OpenUserPreferences(AndroidWirelessPreferences.Path);
            footerNote = Root.Q<Label>("footer-note");
            restartButton = Root.Q<Button>("restart-adb");
            restartButton.clicked += RestartAdbServer;

            Root.Q<Button>("close").clicked += () => context.CloseWindow();

            SetupUpdateBanner();
        }

        // ---------------------------------------------------------------- Platform-Tools 업데이트 배너

        private const string LaterKey = "AndroidWireless.PlatformToolsLater";
        private static bool installing;

        /// <summary>Unity SDK의 Platform-Tools가 ADB Wi-Fi 2.0 기준(37)보다 낮으면 설치를 제안한다.</summary>
        private void SetupUpdateBanner()
        {
            var banner = Root.Q("update-banner");
            var text = Root.Q<Label>("update-text");
            var spinner = Root.Q<Spinner>("update-spinner");
            var later = Root.Q<Button>("update-later");
            var install = Root.Q<Button>("update-install");
            var close = Root.Q<Button>("update-close");
            close.style.display = DisplayStyle.None;
            close.clicked += () => banner.style.display = DisplayStyle.None;

            void SetState(Func<string> message, bool busy, bool error, bool showButtons)
            {
                banner.style.display = DisplayStyle.Flex;
                Localization.Bind(text, message);
                banner.EnableInClassList("aw-banner--error", error);
                spinner.style.display = busy ? DisplayStyle.Flex : DisplayStyle.None;
                later.style.display = showButtons ? DisplayStyle.Flex : DisplayStyle.None;
                install.style.display = showButtons ? DisplayStyle.Flex : DisplayStyle.None;
                // 설치 결과(완료·실패)는 X로 닫을 수 있다.
                close.style.display = !busy && !showButtons || error ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var version = AndroidSdk.PlatformToolsVersion;
            bool hidden = SessionState.GetBool(LaterKey, false);
            if (installing)
            {
                SetState(() => Localization.Get("update.installing"), true, false, false);
            }
            else if (!AndroidSdk.NeedsPlatformToolsUpdate || hidden)
            {
                banner.style.display = DisplayStyle.None;
                return;
            }
            else
            {
                SetState(() => Localization.Format("update.message", version, AndroidSdk.RequiredPlatformToolsMajor), false, false, true);
            }

            later.clicked += () =>
            {
                SessionState.SetBool(LaterKey, true);
                banner.style.display = DisplayStyle.None;
            };

            install.clicked += async () =>
            {
                if (installing) return;
                bool agreed = await new InstallConfirmOverlay(context, AndroidSdk.SdkRoot).ShowAsync();
                if (!agreed || banner.panel == null) return;

                installing = true;
                SetState(() => Localization.Get("update.installing"), true, false, false);
                var result = await AndroidSdk.InstallPlatformToolsAsync(context.Lifetime);
                installing = false;
                if (banner.panel == null) return; // 화면이 바뀜

                var installed = AndroidSdk.PlatformToolsVersion;
                if (result.Success && !AndroidSdk.NeedsPlatformToolsUpdate)
                {
                    // 설치해도 이미 떠 있던 이전 버전 서버는 그대로 남는다(macOS는 교체된 파일로 계속 실행됨) → 새 adb로 교체한다.
                    installing = true;
                    SetState(() => Localization.Get("list.restartingAdb"), true, false, false);
                    try
                    {
                        await AdbServer.RestartAsync(context.Lifetime);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                    finally
                    {
                        installing = false;
                    }
                    context.Discovery.Restart();
                    if (banner.panel == null) return;

                    SetState(() => Localization.Format("update.done", installed), false, false, false);
                }
                else
                {
                    string reason = LastLine(result.Output);
                    SetState(() => Localization.Format("update.failed", reason), false, true, true);
                }
            };
        }

        private static string LastLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "?";
            if (text.IndexOf("license is not accepted", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("licenses or those of the packages they depend on were not accepted", StringComparison.OrdinalIgnoreCase) >= 0)
                return Localization.Get("update.licenseNotAccepted");

            // sdkmanager는 진행률을 "\r[====] 100% …"로 덮어쓰므로 진행률 줄을 건너뛰고 마지막 메시지를 쓴다.
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToArray();
            if (lines.Length == 0) return "?";
            return lines.LastOrDefault(l => !l.StartsWith("[", StringComparison.Ordinal)) ?? lines[lines.Length - 1];
        }

        public override void OnShow()
        {
            context.Discovery.Changed += Rebuild;
            Localization.Changed += Rebuild;
            Rebuild();
            searchField.schedule.Execute(() => searchField.Focus());
        }

        public override void OnHide()
        {
            context.Discovery.Changed -= Rebuild;
            Localization.Changed -= Rebuild;
        }

        private void Rebuild()
        {
            var discovery = context.Discovery;
            string filter = searchField.value?.Trim() ?? "";
            var devices = discovery.Devices.Where(d => MatchesFilter(d, filter)).ToList();

            rows.Clear();
            foreach (var device in devices)
                rows.Add(CreateRow(device));

            bool showEmpty = devices.Count == 0;
            empty.style.display = showEmpty ? DisplayStyle.Flex : DisplayStyle.None;
            rows.style.display = showEmpty ? DisplayStyle.None : DisplayStyle.Flex;
            emptyButton.style.display = DisplayStyle.None;
            emptySpinner.style.display = DisplayStyle.None;

            if (showEmpty)
            {
                if (discovery.Error != null)
                {
                    emptyTitle.text = Localization.Get("list.empty.adbError");
                    emptyDesc.text = discovery.Error;
                    emptyButton.style.display = DisplayStyle.Flex;
                }
                else if (discovery.IsStarting)
                {
                    emptySpinner.style.display = DisplayStyle.Flex;
                    emptyTitle.text = Localization.Get("list.empty.looking");
                    emptyDesc.text = "";
                }
                else if (filter.Length > 0 && discovery.Devices.Count > 0)
                {
                    emptyTitle.text = Localization.Format("list.empty.noMatch", filter);
                    emptyDesc.text = "";
                }
                else
                {
                    emptySpinner.style.display = DisplayStyle.Flex;
                    emptyTitle.text = Localization.Get("list.empty.none");
                    emptyDesc.text = Localization.Get("list.empty.noneDesc");
                }
            }

            UpdateFooter(discovery);
        }

        // ---------------------------------------------------------------- 오래된 adb 서버

        /// <summary>
        /// 상세 정보가 안 오면 실행 중인 서버가 오래된 것이다. 설치된 adb 중 최신이 37 이상이면
        /// 서버만 바꾸면 되므로 "adb 서버 다시 시작"을 보여 주고, 아니면 Platform-Tools가 필요하다고 안내한다.
        /// </summary>
        private void UpdateFooter(WirelessDiscovery discovery)
        {
            if (discovery.HasDetailedInfo)
            {
                restartError = null;
                footerNote.text = "";
                restartButton.style.display = DisplayStyle.None;
                return;
            }

            bool canRestart = NewestAdbSupportsDetails();
            restartButton.style.display = canRestart ? DisplayStyle.Flex : DisplayStyle.None;
            restartButton.SetEnabled(!restartingAdb);
            restartButton.text = Localization.Get(restartingAdb ? "list.restartingAdb" : "list.restartAdb");

            if (!canRestart) footerNote.text = Localization.Get("list.note.oldAdb");
            else if (restartError != null) footerNote.text = restartError;
            else footerNote.text = Localization.Get(restartedAdb ? "list.note.oldServerAgain" : "list.note.oldServer");
        }

        /// <summary>서버를 띄울 때 쓸 adb의 Platform-Tools 버전이 상세 정보 스트림을 지원하는지. 알 수 없으면 true.</summary>
        private static bool NewestAdbSupportsDetails()
        {
            string adb = AdbClient.ResolveAdbPath();
            if (string.IsNullOrEmpty(adb)) return false;
            string sdkRoot = Path.GetDirectoryName(Path.GetDirectoryName(adb));
            var version = AndroidSdk.ReadPlatformToolsVersion(sdkRoot);
            return version == null || version.Major >= AndroidSdk.RequiredPlatformToolsMajor;
        }

        private async void RestartAdbServer()
        {
            if (restartingAdb) return;
            bool confirmed = await new ConfirmOverlay(context, "restart.confirmTitle", "restart.confirmBody", "restart.confirmOk").ShowAsync();
            if (!confirmed || Root.panel == null) return;

            restartingAdb = true;
            restartError = null;
            Rebuild();
            try
            {
                string error = await AdbServer.RestartAsync(context.Lifetime);
                restartError = error;
            }
            catch (OperationCanceledException)
            {
                return; // 창이 닫힘
            }
            catch (Exception e)
            {
                restartError = Localization.Format("error.startServer", e.Message);
            }
            finally
            {
                restartingAdb = false;
            }

            if (restartError != null)
            {
                // 실패 이유를 안내 자리에 남긴다. 서버가 계속 없으면 WirelessDiscovery가 알아서 다시 띄운다.
                Rebuild();
                return;
            }

            restartedAdb = true;
            context.Discovery.Restart();
        }

        private static bool MatchesFilter(WirelessDevice device, string filter)
        {
            if (filter.Length == 0) return true;
            return Contains(device.DisplayName, filter) || Contains(device.Model, filter)
                || Contains(device.Serial, filter) || Contains(device.Address, filter);
        }

        private static bool Contains(string text, string filter) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;

        private VisualElement CreateRow(WirelessDevice device)
        {
            var row = rowTemplate.Instantiate();
            Localization.Localize(row);
            row.AddToClassList("aw-table__row-container");

            row.Q<Label>("name").text = device.DisplayName;
            row.Q<Label>("address").text = device.Address;
            row.Q<Label>("api").text = string.IsNullOrEmpty(device.Api) ? "—" : device.Api;

            int version = device.AdbWifiVersion;
            var dot = row.Q("version-dot");
            dot.EnableInClassList("aw-version-dot--v1", version == 1);
            dot.EnableInClassList("aw-version-dot--v2", version >= 2);
            dot.EnableInClassList("aw-version-dot--unknown", version == 0);
            row.Q<Label>("version").text = version == 0 ? "—" : $"v{version}.0";
            dot.tooltip = version == 0 ? Localization.Get("list.version.unknown") : $"ADB Wi-Fi v{version}.0";

            var pair = row.Q<Button>("pair");
            var connected = row.Q("connected");
            pair.style.display = device.IsConnected ? DisplayStyle.None : DisplayStyle.Flex;
            connected.style.display = device.IsConnected ? DisplayStyle.Flex : DisplayStyle.None;
            connected.tooltip = device.ConnectedSerial;

            string key = device.Key;
            pair.clicked += () =>
            {
                var target = context.Discovery.FindDevice(key) ?? device;
                context.Navigate(new PairDeviceView(context, target));
            };
            return row;
        }
    }
}
