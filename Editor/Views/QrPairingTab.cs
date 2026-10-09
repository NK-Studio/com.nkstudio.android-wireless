using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// QR 페어링 탭. 보이는 동안 QR을 띄워 두고, 휴대폰이 스캔해 mDNS 페어링 서비스(studio-…)를 열면
    /// 페어링 → 연결까지 진행한다.
    /// </summary>
    internal sealed class QrPairingTab : ViewBase
    {
        private readonly WindowContext context;
        private readonly string deviceName;
        private readonly Image qrImage;
        private readonly VisualElement busy, error;
        private readonly Label busyText, errorText;

        private CancellationTokenSource cts;
        private Texture2D qrTexture;

        public QrPairingTab(WindowContext context, string deviceName) : base("QrPairingTab")
        {
            this.context = context;
            this.deviceName = deviceName;

            Localization.BindKey(Root.Q<Label>("caption"), "qr.caption", deviceName);
            qrImage = Root.Q<Image>("qr-image");
            busy = Root.Q("qr-busy");
            busyText = Root.Q<Label>("qr-busy-text");
            error = Root.Q("error");
            errorText = Root.Q<Label>("error-text");
            Root.Q<Button>("retry").clicked += Restart;
        }

        public override void OnShow() => Restart();

        public override void OnHide()
        {
            Cancel();
            DestroyQr();
        }

        private void Restart()
        {
            Cancel();
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime);
            _ = RunAsync(cts.Token);
        }

        private void Cancel()
        {
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
        }

        private async Task RunAsync(CancellationToken token)
        {
            var (payload, serviceName, password) = PairingSession.CreateQr();
            ShowQr(payload);
            SetBusy(null);
            SetError(null);

            try
            {
                var service = await context.Discovery.WaitForAsync(
                    d => FindQrService(d, serviceName), token);

                SetBusy(Localization.Format("qr.pairing", string.IsNullOrEmpty(service.ProductModel) ? deviceName : service.ProductModel));
                var outcome = await PairingSession.PairAndConnectAsync(
                    context.Discovery, service.Address, password, deviceName, token);
                if (token.IsCancellationRequested) return;

                if (outcome.Success)
                {
                    context.Navigate(new PairingResultView(context, outcome.DeviceName));
                    return;
                }

                SetBusy(null);
                SetError(outcome.Message);
            }
            catch (OperationCanceledException)
            {
                // 탭 전환·창 닫힘
            }
        }

        private static MdnsServiceInfo FindQrService(WirelessDiscovery discovery, string serviceName)
        {
            foreach (var s in discovery.Services)
                if (s.IsPairing && s.Instance == serviceName) return s;
            return null;
        }

        private void ShowQr(string payload)
        {
            DestroyQr();
            qrTexture = QrTextureBuilder.Build(payload);
            qrImage.image = qrTexture;
        }

        private void DestroyQr()
        {
            qrImage.image = null;
            if (qrTexture != null) UnityEngine.Object.DestroyImmediate(qrTexture);
            qrTexture = null;
        }

        private void SetBusy(string message)
        {
            busy.style.display = message != null ? DisplayStyle.Flex : DisplayStyle.None;
            busyText.text = message ?? "";
        }

        private void SetError(string message)
        {
            error.style.display = message != null ? DisplayStyle.Flex : DisplayStyle.None;
            errorText.text = message ?? "";
            qrImage.EnableInClassList("aw-qr__image--stale", message != null);
        }
    }
}
