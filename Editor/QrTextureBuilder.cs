using QRCoder;
using UnityEngine;

namespace AndroidWireless
{
    /// <summary>QRCoder의 모듈 행렬을 Texture2D로 그린다. (QRCoder 렌더러는 System.Drawing 의존이라 사용하지 않음)</summary>
    public static class QrTextureBuilder
    {
        public static Texture2D Build(string text, int pixelsPerModule = 8)
        {
            QRCodeData data = QRCodeGenerator.GenerateQrCode(text, QRCodeGenerator.ECCLevel.M);
            var matrix = data.ModuleMatrix; // 기본 quiet zone(여백 4모듈) 포함
            int modules = matrix.Count;
            int size = modules * pixelsPerModule;

            var pixels = new Color32[size * size];
            var dark = new Color32(0, 0, 0, 255);
            var light = new Color32(255, 255, 255, 255);

            for (int row = 0; row < modules; row++)
            {
                // Texture2D는 아래쪽이 y=0이므로 위아래를 뒤집어 쓴다.
                int yBase = (modules - 1 - row) * pixelsPerModule;
                for (int col = 0; col < modules; col++)
                {
                    var color = matrix[row][col] ? dark : light;
                    int xBase = col * pixelsPerModule;
                    for (int y = 0; y < pixelsPerModule; y++)
                    {
                        int offset = (yBase + y) * size + xBase;
                        for (int x = 0; x < pixelsPerModule; x++)
                            pixels[offset + x] = color;
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
