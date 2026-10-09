using System;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// Android Studio의 "Pair devices over Wi-Fi"와 같은 흐름의 무선 디버깅 페어링 창.
    /// 화면은 View 단위 UXML(<see cref="ViewBase"/>)로 나뉘고, 이 창은 화면 전환·테마·기기 탐색 수명만 관리한다.
    /// 테마·언어는 창 오른쪽 위 ⋮ 메뉴(<see cref="IHasCustomMenu"/>)와 Preferences > Android Wireless에서 바꾼다.
    /// </summary>
    public class AndroidWirelessWindow : EditorWindow, IHasCustomMenu
    {
        private CancellationTokenSource lifetime;
        private WirelessDiscovery discovery;
        private WindowContext context;

        private VisualElement shell, content, overlayLayer;
        private ViewBase current;
        private bool lastProSkin;

        [MenuItem("Tools/Android Wireless Pairing")]
        public static void Open()
        {
            var window = GetWindow<AndroidWirelessWindow>();
            window.minSize = new Vector2(560, 460);
        }

        private void OnEnable()
        {
            // 도메인 리로드 뒤에도 제목이 클래스 이름으로 바뀌지 않도록 여기서 정한다.
            titleContent = new GUIContent("Android Wireless");
            lifetime = new CancellationTokenSource();
            discovery = new WirelessDiscovery();
            ThemeSettings.Changed += ApplyTheme;
            Localization.Changed += ApplyLanguage;
        }

        private void OnDisable()
        {
            ThemeSettings.Changed -= ApplyTheme;
            Localization.Changed -= ApplyLanguage;
            current?.OnHide();
            current = null;
            lifetime?.Cancel();
            lifetime?.Dispose();
            lifetime = null;
            discovery?.Dispose();
            discovery = null;
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            var common = UiAssets.LoadStyle("Styles/Common.uss");
            if (common != null) root.styleSheets.Add(common);

            shell = new VisualElement();
            shell.AddToClassList("aw-shell");
            content = new VisualElement();
            content.AddToClassList("aw-content");
            overlayLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            overlayLayer.AddToClassList("aw-overlay-layer");
            shell.Add(content);
            shell.Add(overlayLayer);
            root.Add(shell);
            ApplyTheme();

            context = new WindowContext
            {
                Discovery = discovery,
                Lifetime = lifetime.Token,
                OverlayLayer = overlayLayer,
                Navigate = Show,
                CloseWindow = Close,
            };
            context.ShowDeviceList = () => Show(new DeviceListView(context));

            // Esc: 하위 화면이면 기기 목록으로 (오버레이는 자체적으로 먼저 처리)
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || current is DeviceListView || current is ThemeSetupView || current is ModuleMissingView) return;
                context.ShowDeviceList();
                evt.StopPropagation();
            });

            // 에디터 스킨(다크/라이트)이 바뀌면 따라간다.
            lastProSkin = EditorGUIUtility.isProSkin;
            root.schedule.Execute(() =>
            {
                if (lastProSkin == EditorGUIUtility.isProSkin) return;
                lastProSkin = EditorGUIUtility.isProSkin;
                ApplyTheme();
            }).Every(1000);

            // Android 모듈이 없으면 adb·SDK가 없으므로 안내 화면만 보여 주고 기기 탐색은 시작하지 않는다.
            if (!AndroidSdk.IsModuleInstalled)
            {
                Show(new ModuleMissingView());
                return;
            }

            discovery.Start();
            Show(ThemeSettings.Chosen ? (ViewBase)new DeviceListView(context) : new ThemeSetupView(context));
        }

        private void Show(ViewBase view)
        {
            current?.OnHide();
            overlayLayer.Clear();
            overlayLayer.pickingMode = PickingMode.Ignore;

            content.Clear();
            current = view;
            content.Add(view.Root);
            view.OnShow();
        }

        private void ApplyTheme()
        {
            if (shell == null) return;
            ThemeSettings.Apply(shell);
            shell.Query<IconElement>().ForEach(icon => icon.MarkDirtyRepaint());
        }

        private void ApplyLanguage()
        {
            if (shell != null) Localization.Refresh(shell);
        }

        // ---------------------------------------------------------------- ⋮ 메뉴

        public void AddItemsToMenu(GenericMenu menu)
        {
            string theme = Localization.Get("menu.theme");
            foreach (ThemeStyle style in Enum.GetValues(typeof(ThemeStyle)))
                menu.AddItem(new GUIContent($"{theme}/{ThemeSettings.DisplayName(style)}"), ThemeSettings.Style == style,
                    () => ThemeSettings.Style = style);

            string language = Localization.Get("menu.language");
            foreach (Language lang in Enum.GetValues(typeof(Language)))
                menu.AddItem(new GUIContent($"{language}/{Localization.DisplayName(lang)}"), Localization.Current == lang,
                    () => Localization.Current = lang);

            menu.AddSeparator("");
            menu.AddItem(new GUIContent(Localization.Get("menu.themeSetup")), false, () =>
            {
                if (context != null && AndroidSdk.IsModuleInstalled) Show(new ThemeSetupView(context));
            });
            menu.AddItem(new GUIContent(Localization.Get("menu.preferences")), false,
                () => SettingsService.OpenUserPreferences(AndroidWirelessPreferences.Path));
        }
    }
}
