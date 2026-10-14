using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 말풍선과 메뉴에 쓰는 글꼴과 텍스처를 코드로 만듭니다.
    /// 별도 이미지 에셋 없이 스크립트만 복사해도 동작하게 하기 위해서입니다.
    /// </summary>
    public static class MascotUiSkin
    {
        static readonly string[] FontNames = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Segoe UI", "Arial" };
        static Font font;

        /// <summary>Windows 화면 배율 (100% = 1). 픽셀 단위 설정값에 곱해서 씁니다.</summary>
        public static float Scale => Screen.dpi > 0f ? Mathf.Max(1f, Screen.dpi / 96f) : 1f;

        /// <summary>한글이 나오는 OS 글꼴 (맑은 고딕)</summary>
        public static Font UiFont
        {
            get
            {
                if (font == null) font = Font.CreateDynamicFontFromOSFont(FontNames, 16);
                return font;
            }
        }

        /// <summary>
        /// 테두리가 있는 둥근 사각형. GUIStyle.border를 radius로 두고 9-slice로 늘려서 씁니다.
        /// </summary>
        public static Texture2D RoundedRect(Color fill, Color border, int radius, float borderWidth)
        {
            int size = radius * 2 + 2;
            Texture2D texture = CreateTexture(size, size);
            var pixels = new Color[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 둥근 사각형 가장자리까지의 부호 있는 거리 (안쪽이 음수)
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    float distance = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                                     + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    pixels[y * size + x] = Shade(fill, border, distance, distance, borderWidth);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 아래를 가리키는 말풍선 꼬리. 윗변은 말풍선 몸통에 겹쳐 그리므로 테두리를 넣지 않습니다.
        /// </summary>
        public static Texture2D Tail(Color fill, Color border, int width, int height, float borderWidth)
        {
            Texture2D texture = CreateTexture(width, height);
            var pixels = new Color[width * height];
            float center = width * 0.5f;
            float edgeLength = Mathf.Sqrt(center * center + height * height);
            // 텍스처는 y = 0이 아래쪽이므로 꼭짓점이 y = 0에 옵니다
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float left = ((px - center) * height + py * center) / edgeLength;
                    float right = (-(px - center) * height + py * center) / edgeLength;
                    float top = height - py;
                    float slanted = -Mathf.Min(left, right);
                    float outline = -Mathf.Min(Mathf.Min(left, right), top);
                    pixels[y * width + x] = Shade(fill, border, outline, slanted, borderWidth);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        static Color Shade(Color fill, Color border, float coverageDistance, float borderDistance, float borderWidth)
        {
            float coverage = Mathf.Clamp01(0.5f - coverageDistance);
            float borderMix = Mathf.Clamp01(borderDistance + borderWidth + 0.5f);
            Color color = Color.Lerp(fill, border, borderMix);
            color.a *= coverage;
            return color;
        }

        static Texture2D CreateTexture(int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }
    }
}
