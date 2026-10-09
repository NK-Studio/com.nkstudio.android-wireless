using System.Collections.Generic;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// 페어링 코드 탭. 기기가 "페어링 코드로 기기 페어링" 화면을 열면(_adb-tls-pairing 광고)
    /// "Device at IP:port" 행이 나타나고, Pair를 누르면 6자리 입력 오버레이가 뜬다.
    /// </summary>
    internal sealed class CodePairingTab : ViewBase
    {
        private readonly WindowContext context;
        private readonly WirelessDevice device;
        private readonly string deviceName;
        private readonly VisualElement waiting, available;
        private readonly ScrollView list;
        private PairingCodeOverlay overlay;

        public CodePairingTab(WindowContext context, WirelessDevice device) : base("CodePairingTab")
        {
            this.context = context;
            this.device = device;
            deviceName = device.DisplayName;

            Localization.BindKey(Root.Q<Label>("waiting-text"), "code.waiting", deviceName);
            Localization.BindKey(Root.Q<Label>("hint-title"), "code.hintTitle", deviceName);
            waiting = Root.Q("waiting");
            available = Root.Q("available");
            list = Root.Q<ScrollView>("available-list");
        }

        public override void OnShow()
        {
            context.Discovery.Changed += Refresh;
            Refresh();
        }

        public override void OnHide()
        {
            context.Discovery.Changed -= Refresh;
            overlay?.Close();
            overlay = null;
        }

        private void Refresh()
        {
            var services = new List<MdnsServiceInfo>();
            foreach (var s in context.Discovery.Services)
                if (s.IsPairing && !s.IsQrPairing && device.Matches(s)) services.Add(s);

            waiting.style.display = services.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            available.style.display = services.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            list.Clear();
            foreach (var service in services)
                list.Add(CreateRow(service));
        }

        private VisualElement CreateRow(MdnsServiceInfo service)
        {
            var row = new VisualElement();
            row.AddToClassList("aw-available__row");

            var text = new VisualElement();
            text.AddToClassList("aw-available__text");
            var title = new Label { enableRichText = true };
            Localization.BindKey(title, "code.deviceAt", service.Address);
            title.AddToClassList("aw-available__address");
            var sub = new Label();
            Localization.BindKey(sub, "code.availableToPair");
            sub.AddToClassList("aw-available__sub");
            text.Add(title);
            text.Add(sub);

            var pair = new Button(() => OpenOverlay(service.Address));
            Localization.BindKey(pair, "list.pair");
            pair.AddToClassList("aw-btn");
            pair.AddToClassList("aw-btn--secondary");

            row.Add(text);
            row.Add(pair);
            return row;
        }

        private void OpenOverlay(string address)
        {
            overlay?.Close();
            overlay = new PairingCodeOverlay(context, address, deviceName);
            overlay.Closed += () => overlay = null;
            overlay.Open();
        }
    }
}
