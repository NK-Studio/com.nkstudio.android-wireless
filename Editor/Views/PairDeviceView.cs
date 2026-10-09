using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>"Pair {기기} over Wi-Fi" 화면. QR 탭이 기본이고 페어링 코드 탭으로 바꿀 수 있다.</summary>
    internal sealed class PairDeviceView : ViewBase
    {
        private readonly WindowContext context;
        private readonly Button tabQr, tabCode;
        private readonly VisualElement tabContent;
        private readonly ViewBase qrTab, codeTab;
        private ViewBase currentTab;

        public PairDeviceView(WindowContext context, WirelessDevice device) : base("PairDevice")
        {
            this.context = context;
            string name = device.DisplayName;

            Localization.BindKey(Root.Q<Label>("title"), "pair.title", name);
            BindLink(Root.Q<Label>("desc"),
                "pair.desc", "common.learnMore", () => Application.OpenURL(WindowContext.LearnMoreUrl));

            tabQr = Root.Q<Button>("tab-qr");
            tabCode = Root.Q<Button>("tab-code");
            tabContent = Root.Q("tab-content");
            qrTab = new QrPairingTab(context, name);
            codeTab = new CodePairingTab(context, device);

            tabQr.clicked += () => ShowTab(qrTab);
            tabCode.clicked += () => ShowTab(codeTab);
            Root.Q<Button>("close").clicked += () => context.ShowDeviceList();
        }

        public override void OnShow() => ShowTab(qrTab);

        public override void OnHide()
        {
            currentTab?.OnHide();
            currentTab = null;
        }

        private void ShowTab(ViewBase tab)
        {
            if (currentTab == tab) return;
            currentTab?.OnHide();
            tabContent.Clear();

            currentTab = tab;
            tabQr.EnableInClassList("aw-tab--selected", tab == qrTab);
            tabCode.EnableInClassList("aw-tab--selected", tab == codeTab);
            tabContent.Add(tab.Root);
            tab.OnShow();
        }
    }
}
