using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace AndroidWireless
{
    /// <summary>뷰들이 공유하는 창 상태와 화면 전환 수단.</summary>
    internal sealed class WindowContext
    {
        public const string LearnMoreUrl = "https://developer.android.com/studio/run/device#wireless";

        public WirelessDiscovery Discovery;
        public CancellationToken Lifetime;
        public VisualElement OverlayLayer;
        public Action<ViewBase> Navigate;
        public Action ShowDeviceList;
        public Action CloseWindow;
    }

    /// <summary>UXML 하나를 루트로 가지는 화면 단위.</summary>
    internal abstract class ViewBase
    {
        public VisualElement Root { get; }

        protected ViewBase(string uxmlName)
        {
            var tree = UiAssets.LoadView(uxmlName);
            Root = tree != null ? tree.Instantiate() : new Label($"{uxmlName}.uxml not found");
            Root.AddToClassList("aw-view");
            Localization.Localize(Root);
        }

        public virtual void OnShow() { }
        public virtual void OnHide() { }

        /// <summary>
        /// 문장 안의 "Learn more" 같은 링크. 링크 색은 .aw-has-link 규칙의 --aw-label-link(= 테마 토큰 --aw-link)를 읽어 rich text로 칠한다.
        /// textKey 문구 안의 {link}가 linkKey 문구로 바뀐다. 언어가 바뀌면 다시 그린다.
        /// </summary>
        protected static void BindLink(Label label, string textKey, string linkKey, Action onClick)
        {
            var linkColor = new CustomStyleProperty<Color>("--aw-label-link");
            label.enableRichText = true;

            string Render()
            {
                var color = label.customStyle.TryGetValue(linkColor, out var c) ? c : new Color(0.33f, 0.54f, 0.97f);
                string link = $"<link=\"go\"><color=#{ColorUtility.ToHtmlStringRGB(color)}>{Localization.Get(linkKey)}</color></link>";
                return Localization.Get(textKey).Replace("{link}", link);
            }

            label.AddToClassList("aw-has-link");
            label.RegisterCallback<CustomStyleResolvedEvent>(_ => label.text = Render());
            label.RegisterCallback<PointerUpLinkTagEvent>(_ => onClick());
            label.RegisterCallback<PointerOverLinkTagEvent>(_ => label.AddToClassList("aw-has-link--hover"));
            label.RegisterCallback<PointerOutLinkTagEvent>(_ => label.RemoveFromClassList("aw-has-link--hover"));
            Localization.Bind(label, Render);
        }
    }
}
