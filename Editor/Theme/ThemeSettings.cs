using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    public enum ThemeStyle
    {
        Unity,
        AndroidStudio,
    }

    /// <summary>
    /// 테마 설정. 프로젝트가 아니라 EditorPrefs(사용자 Preferences)에 저장하므로
    /// 같은 프로젝트를 쓰는 팀원마다 다르게 고를 수 있다.
    /// </summary>
    public static class ThemeSettings
    {
        private const string StyleKey = "AndroidWireless.Style";
        private const string ChosenKey = "AndroidWireless.ThemeChosen";

        private const string ThemeClassPrefix = "aw-theme--";

        public static event Action Changed;

        public static ThemeStyle Style
        {
            get => (ThemeStyle)EditorPrefs.GetInt(StyleKey, (int)ThemeStyle.AndroidStudio);
            set
            {
                if (Style == value) return;
                EditorPrefs.SetInt(StyleKey, (int)value);
                Changed?.Invoke();
            }
        }

        /// <summary>처음 창을 열 때 테마 선택 화면을 이미 거쳤는지.</summary>
        public static bool Chosen
        {
            get => EditorPrefs.GetBool(ChosenKey, false);
            set => EditorPrefs.SetBool(ChosenKey, value);
        }

        /// <summary>다크/라이트는 항상 Unity 에디터 스킨을 따른다.</summary>
        public static bool IsDark => EditorGUIUtility.isProSkin;

        public static string DisplayName(ThemeStyle style) => style == ThemeStyle.Unity ? "Unity" : "Android Studio";

        /// <summary>element 아래에만 해당 테마 토큰을 적용한다(창 전체 또는 미리보기 카드).</summary>
        public static void Apply(VisualElement element, ThemeStyle style, bool dark)
        {
            string name = (style == ThemeStyle.Unity ? "Unity" : "As") + (dark ? "Dark" : "Light");

            for (int i = element.styleSheets.count - 1; i >= 0; i--)
            {
                var existing = element.styleSheets[i];
                if (existing != null && Array.IndexOf(ThemeSheetNames, existing.name) >= 0)
                    element.styleSheets.Remove(existing);
            }

            var sheet = UiAssets.LoadStyle($"Themes/{name}.uss");
            if (sheet != null) element.styleSheets.Add(sheet);

            foreach (var n in ThemeSheetNames)
                element.RemoveFromClassList(ThemeClassPrefix + n);
            element.AddToClassList(ThemeClassPrefix + name);
            element.EnableInClassList(ThemeClassPrefix + "dark", dark);
            element.EnableInClassList(ThemeClassPrefix + "light", !dark);
        }

        public static void Apply(VisualElement element) => Apply(element, Style, IsDark);

        private static readonly string[] ThemeSheetNames = { "UnityDark", "UnityLight", "AsDark", "AsLight" };
    }
}
