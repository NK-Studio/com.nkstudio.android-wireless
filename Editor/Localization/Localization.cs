using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    public enum Language
    {
        English,
        Korean,
    }

    /// <summary>
    /// 창 문구의 한국어/영어 전환. 선택은 EditorPrefs(사용자 Preferences)에 저장해 개발자마다 다르게 쓸 수 있다.
    /// - UXML: text·placeholder-text에 "@키"를 쓰면 <see cref="Localize"/>가 번역해 넣는다.
    /// - 코드: <see cref="Bind"/>로 문구를 만드는 함수를 요소에 묶어 두면 언어가 바뀔 때 다시 그린다.
    /// </summary>
    public static class Localization
    {
        private const string LanguageKey = "AndroidWireless.Language";

        public static event Action Changed;

        private static readonly ConditionalWeakTable<VisualElement, Action> Renderers = new ConditionalWeakTable<VisualElement, Action>();

        public static Language Current
        {
            get
            {
                int stored = EditorPrefs.GetInt(LanguageKey, -1);
                if (stored >= 0) return (Language)stored;
                return Application.systemLanguage == SystemLanguage.Korean ? Language.Korean : Language.English;
            }
            set
            {
                if (EditorPrefs.GetInt(LanguageKey, -1) == (int)value) return;
                EditorPrefs.SetInt(LanguageKey, (int)value);
                Changed?.Invoke();
            }
        }

        public static string DisplayName(Language language) => language == Language.Korean ? "한국어" : "English";

        public static string Get(string key)
        {
            if (!Strings.Table.TryGetValue(key, out var pair)) return key;
            return Current == Language.Korean ? pair.ko : pair.en;
        }

        public static string Format(string key, params object[] args) => string.Format(Get(key), args);

        /// <summary>요소의 문구를 text()로 정하고, 언어가 바뀌면 다시 계산한다.</summary>
        public static void Bind(TextElement element, Func<string> text)
        {
            Renderers.Remove(element);
            Renderers.Add(element, () => element.text = text());
            element.text = text();
        }

        public static void BindKey(TextElement element, string key, params object[] args) =>
            Bind(element, () => args.Length == 0 ? Get(key) : Format(key, args));

        /// <summary>UXML에 적힌 "@키" 문구를 찾아 묶는다.</summary>
        public static void Localize(VisualElement root)
        {
            root.Query<TextElement>().ForEach(element =>
            {
                if (element.text != null && element.text.StartsWith("@", StringComparison.Ordinal))
                    BindKey(element, element.text.Substring(1));
            });
            root.Query<TextField>().ForEach(field =>
            {
                string placeholder = field.textEdition.placeholder;
                if (string.IsNullOrEmpty(placeholder) || !placeholder.StartsWith("@", StringComparison.Ordinal)) return;
                string key = placeholder.Substring(1);
                Renderers.Remove(field);
                Renderers.Add(field, () => field.textEdition.placeholder = Get(key));
                field.textEdition.placeholder = Get(key);
            });
        }

        /// <summary>root 아래의 묶인 문구를 현재 언어로 다시 그린다.</summary>
        public static void Refresh(VisualElement root)
        {
            root.Query<VisualElement>().ForEach(element =>
            {
                if (Renderers.TryGetValue(element, out var render)) render();
            });
        }
    }
}
