using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// 창 전체를 어둡게 덮고 가운데에 6자리 입력 다이얼로그를 띄운다.
    /// 6칸이 차거나 Enter·붙여넣기로 제출되면 페어링 → 연결까지 진행한다.
    /// Esc·Cancel·바깥 클릭으로 닫히고, 진행 중이면 취소된다.
    /// </summary>
    internal sealed class PairingCodeOverlay : ViewBase
    {
        public event Action Closed;

        private readonly WindowContext context;
        private readonly string address;
        private readonly string deviceName;
        private readonly PairingCodeInput input;
        private readonly VisualElement dialog, busy;
        private readonly Label error, hint;

        private CancellationTokenSource cts;
        private bool isOpen;

        public PairingCodeOverlay(WindowContext context, string address, string deviceName) : base("PairingCodeOverlay")
        {
            this.context = context;
            this.address = address;
            this.deviceName = deviceName;

            Root.AddToClassList("aw-overlay-root");
            var message = Root.Q<Label>("message");
            message.enableRichText = true;
            Localization.BindKey(message, "overlay.message", address);

            dialog = Root.Q("dialog");
            input = Root.Q<PairingCodeInput>("code");
            busy = Root.Q("busy");
            error = Root.Q<Label>("error");
            hint = Root.Q<Label>("hint");
            input.Submitted += code => _ = SubmitAsync(code);
            Root.Q<Button>("cancel").clicked += Close;

            // 다이얼로그 바깥(어두운 배경)을 누르면 닫는다.
            Root.Q("dim").RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == evt.currentTarget) Close();
            });
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                Close();
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);

            SetState(null, null);
        }

        public void Open()
        {
            if (isOpen) return;
            isOpen = true;
            context.OverlayLayer.Add(Root);
            context.OverlayLayer.pickingMode = PickingMode.Position;
            // 레이아웃이 잡힌 뒤에 포커스해야 첫 칸이 키 입력을 받는다.
            Root.schedule.Execute(input.FocusFirst);
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            cts?.Cancel();
            cts?.Dispose();
            cts = null;
            Root.RemoveFromHierarchy();
            if (context.OverlayLayer.childCount == 0) context.OverlayLayer.pickingMode = PickingMode.Ignore;
            Closed?.Invoke();
        }

        private async Task SubmitAsync(string code)
        {
            if (cts != null) return; // 이미 진행 중
            cts = CancellationTokenSource.CreateLinkedTokenSource(context.Lifetime);
            var token = cts.Token;

            SetState(Localization.Get("overlay.pairing"), null);
            input.SetEnabled(false);
            try
            {
                var outcome = await PairingSession.PairAndConnectAsync(context.Discovery, address, code, deviceName, token);
                if (!isOpen || token.IsCancellationRequested) return;

                if (outcome.Success)
                {
                    Close();
                    context.Navigate(new PairingResultView(context, outcome.DeviceName));
                    return;
                }

                FailWith(outcome.Message);
            }
            catch (OperationCanceledException)
            {
                // 닫힘
            }
            catch (Exception e)
            {
                if (isOpen) FailWith(e.Message);
            }
        }

        private void FailWith(string message)
        {
            cts?.Dispose();
            cts = null;
            input.SetEnabled(true);
            input.ClearCode();
            input.SetError(true);
            SetState(null, message);
            Root.schedule.Execute(input.FocusFirst);
        }

        private void SetState(string busyMessage, string errorMessage)
        {
            busy.style.display = busyMessage != null ? DisplayStyle.Flex : DisplayStyle.None;
            busy.Q<Label>("busy-text").text = busyMessage ?? "";
            error.style.display = errorMessage != null ? DisplayStyle.Flex : DisplayStyle.None;
            error.text = errorMessage ?? "";
            hint.style.display = busyMessage == null && errorMessage == null ? DisplayStyle.Flex : DisplayStyle.None;
            dialog.EnableInClassList("aw-dialog--busy", busyMessage != null);
        }
    }
}
