using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>연결 성공 화면.</summary>
    internal sealed class PairingResultView : ViewBase
    {
        public PairingResultView(WindowContext context, string deviceName) : base("PairingResult")
        {
            Localization.BindKey(Root.Q<Label>("title"), "result.connected", deviceName);
            BindLink(Root.Q<Label>("another"), "result.linkOnly", "result.another", () => context.ShowDeviceList());
            Root.Q<Button>("close").clicked += () => context.ShowDeviceList();
        }
    }
}
