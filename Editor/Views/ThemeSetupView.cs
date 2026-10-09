using System.Collections.Generic;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>
    /// 처음 창을 열 때(또는 ⋮ 메뉴 > Theme Setup…) 보이는 테마 선택 화면. Android Studio / Unity 중 하나를 고른다.
    /// 다크/라이트는 에디터 스킨을 따르므로 미리보기도 현재 스킨으로 보여준다.
    /// 고르는 즉시 창에 적용되고, Continue로 기기 목록으로 넘어간다.
    /// </summary>
    internal sealed class ThemeSetupView : ViewBase
    {
        private readonly List<(VisualElement card, VisualElement preview, ThemeStyle style)> cards =
            new List<(VisualElement, VisualElement, ThemeStyle)>();

        /// <summary>Unity 카드는 이름 대신 워드마크 로고를 쓴다. 스킨에 맞춰 흰색/검은색을 고른다.</summary>
        private VisualElement unityLogo;

        public ThemeSetupView(WindowContext context) : base("ThemeSetup")
        {
            var grid = Root.Q("grid");
            foreach (var style in new[] { ThemeStyle.AndroidStudio, ThemeStyle.Unity })
            {
                var (card, preview) = CreateCard(style);
                cards.Add((card, preview, style));
                grid.Add(card);
            }

            Root.Q<Button>("continue").clicked += () =>
            {
                ThemeSettings.Chosen = true;
                context.ShowDeviceList();
            };
        }

        public override void OnShow()
        {
            ThemeSettings.Changed += Sync;
            Sync();
        }

        public override void OnHide() => ThemeSettings.Changed -= Sync;

        private void Sync()
        {
            var selected = ThemeSettings.Style;
            foreach (var (card, preview, style) in cards)
            {
                card.EnableInClassList("aw-card--selected", style == selected);
                ThemeSettings.Apply(preview, style, ThemeSettings.IsDark);
            }

            if (unityLogo != null)
                unityLogo.style.backgroundImage = UiAssets.LoadTexture(ThemeSettings.IsDark ? "U_Logo_White_RGB_1C" : "U_Logo_Black_RGB_1C");
        }

        private (VisualElement card, VisualElement preview) CreateCard(ThemeStyle style)
        {
            var card = new VisualElement();
            card.AddToClassList("aw-card");
            card.RegisterCallback<ClickEvent>(_ => ThemeSettings.Style = style);

            // 미리보기 영역에만 해당 테마 토큰을 적용한다.
            var preview = new VisualElement();
            preview.AddToClassList("aw-card__preview");

            var search = Mini("aw-mini__search");
            preview.Add(search);

            for (int i = 0; i < 2; i++)
            {
                var row = Mini("aw-mini__row");
                row.Add(Mini("aw-mini__text"));
                row.Add(Mini("aw-mini__dot " + (i == 0 ? "aw-version-dot--v2" : "aw-version-dot--v1")));
                row.Add(Mini("aw-mini__text aw-mini__text--short"));
                row.Add(Mini("aw-mini__button"));
                preview.Add(row);
            }

            var footer = Mini("aw-mini__footer");
            footer.Add(Mini("aw-mini__primary"));
            preview.Add(footer);

            // 카드 아래: Android Studio는 아이콘 + 이름, Unity는 워드마크 로고만 (색은 Sync에서 스킨에 맞춤)
            var title = new VisualElement { pickingMode = PickingMode.Ignore };
            title.AddToClassList("aw-card__title");
            if (style == ThemeStyle.Unity)
            {
                unityLogo = new VisualElement { pickingMode = PickingMode.Ignore };
                unityLogo.AddToClassList("aw-card__logo");
                title.Add(unityLogo);
            }
            else
            {
                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("aw-card__icon");
                icon.style.backgroundImage = UiAssets.LoadTexture(ThemeSettings.DisplayName(style));
                var label = new Label(ThemeSettings.DisplayName(style)) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("aw-card__label");
                title.Add(icon);
                title.Add(label);
            }

            card.Add(preview);
            card.Add(title);
            return (card, preview);
        }

        private static VisualElement Mini(string classes)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            foreach (var cls in classes.Split(' ')) element.AddToClassList(cls);
            return element;
        }
    }
}
