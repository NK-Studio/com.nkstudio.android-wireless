using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AndroidWireless
{
    /// <summary>Preferences > Android Wireless. 사용자별 설정(EditorPrefs)이라 팀원마다 다르게 둘 수 있다.</summary>
    internal static class AndroidWirelessPreferences
    {
        public const string Path = "Preferences/Android Wireless";

        [SettingsProvider]
        private static SettingsProvider Create()
        {
            return new SettingsProvider(Path, SettingsScope.User)
            {
                label = "Android Wireless",
                keywords = new HashSet<string> { "android", "adb", "wireless", "theme", "android studio", "language", "언어", "테마" },
                guiHandler = _ => DrawGui(),
            };
        }

        private static void DrawGui()
        {
            EditorGUIUtility.labelWidth = 160;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(Localization.Get("menu.theme"), EditorStyles.boldLabel);

            ThemeSettings.Style = (ThemeStyle)EditorGUILayout.Popup(Localization.Get("menu.theme"),
                (int)ThemeSettings.Style, new[] { ThemeSettings.DisplayName(ThemeStyle.Unity), ThemeSettings.DisplayName(ThemeStyle.AndroidStudio) });
            Localization.Current = (Language)EditorGUILayout.Popup(Localization.Get("menu.language"),
                (int)Localization.Current, new[] { Localization.DisplayName(Language.English), Localization.DisplayName(Language.Korean) });

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("adb", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                string current = AdbClient.OverridePath;
                string next = EditorGUILayout.DelayedTextField(Localization.Get("prefs.adbPath"), current);
                if (GUILayout.Button(Localization.Get("prefs.browse"), GUILayout.Width(90)))
                {
                    string picked = EditorUtility.OpenFilePanel(Localization.Get("prefs.selectAdb"), "", "");
                    if (!string.IsNullOrEmpty(picked)) next = picked;
                }
                if (next != current) AdbClient.OverridePath = next.Trim();
            }
            EditorGUILayout.LabelField(Localization.Get("prefs.adbNote"), EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(10);
            if (GUILayout.Button(Localization.Get("prefs.showSetup"), GUILayout.Width(240)))
                ThemeSettings.Chosen = false;
        }
    }
}
