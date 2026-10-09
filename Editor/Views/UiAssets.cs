using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AndroidWireless
{
    /// <summary>패키지 안의 UXML/USS를 경로로 불러온다.</summary>
    internal static class UiAssets
    {
        public const string Root = "Packages/com.nkstudio.android-wireless/Editor/UI/";

        public static VisualTreeAsset LoadView(string name) => Load<VisualTreeAsset>($"Views/{name}.uxml");

        public static StyleSheet LoadStyle(string relativePath) => Load<StyleSheet>(relativePath);

        public static Texture2D LoadTexture(string name) => Load<Texture2D>($"Textures/{name}.png");

        private static T Load<T>(string relativePath) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(Root + relativePath);
            if (asset == null) Debug.LogError($"[Android Wireless] {Root + relativePath}을(를) 찾을 수 없습니다.");
            return asset;
        }
    }
}
