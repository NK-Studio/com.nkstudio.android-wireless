using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// 제목·본문·확인 버튼 문구만 바꿔 쓰는 확인 다이얼로그. 창 전체를 어둡게 덮고 가운데에 띄운다.
    /// 확인이면 true, 취소·Esc·바깥 클릭이면 false로 끝난다.
    /// </summary>
    internal sealed class ConfirmOverlay : ViewBase
    {
        private readonly WindowContext context;
        private readonly TaskCompletionSource<bool> result = new TaskCompletionSource<bool>();
        private bool isOpen;

        public ConfirmOverlay(WindowContext context, string titleKey, string bodyKey, string confirmKey) : base("ConfirmOverlay")
        {
            this.context = context;

            Root.AddToClassList("aw-overlay-root");
            Localization.BindKey(Root.Q<Label>("title"), titleKey);
            Localization.BindKey(Root.Q<Label>("body"), bodyKey);
            Localization.BindKey(Root.Q<Button>("confirm"), confirmKey);

            Root.Q<Button>("cancel").clicked += () => Close(false);
            Root.Q<Button>("confirm").clicked += () => Close(true);

            // 다이얼로그 바깥(어두운 배경)을 누르면 취소한다.
            Root.Q("dim").RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.target == evt.currentTarget) Close(false);
            });
            Root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape) return;
                Close(false);
                evt.StopPropagation();
            }, TrickleDown.TrickleDown);
        }

        /// <summary>다이얼로그를 띄우고 사용자가 고를 때까지 기다린다.</summary>
        public Task<bool> ShowAsync()
        {
            if (isOpen) return result.Task;
            isOpen = true;
            context.OverlayLayer.Add(Root);
            context.OverlayLayer.pickingMode = PickingMode.Position;
            // Esc를 받으려면 포커스가 다이얼로그 안에 있어야 한다.
            Root.schedule.Execute(() => Root.Q<Button>("confirm").Focus());
            return result.Task;
        }

        public void Close(bool confirmed)
        {
            if (!isOpen) return;
            isOpen = false;
            Root.RemoveFromHierarchy();
            if (context.OverlayLayer.childCount == 0) context.OverlayLayer.pickingMode = PickingMode.Ignore;
            result.TrySetResult(confirmed);
        }
    }
}
