using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 말풍선과 메뉴에 쓰는 글꼴과 텍스처를 코드로 만듭니다.
    /// 별도 이미지 에셋 없이 스크립트만 복사해도 동작하게 하기 위해서입니다.
    /// </summary>
    public static class MascotUiSkin
    {
        // 한글이 들어 있는 글꼴을 앞에 둡니다 (Windows, macOS·iOS, Android 순)
        static readonly string[] FontNames =
        {
            "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "AppleSDGothicNeo-Regular",
            "Noto Sans CJK KR", "Noto Sans KR", "NotoSansCJK-Regular", "Segoe UI", "Roboto", "Arial",
        };
        static Font font;

        /// <summary>
        /// 화면 배율. 픽셀 단위 설정값에 곱해서 씁니다.
        /// Windows는 96dpi가 100%이고, 모바일은 160dpi가 기준(dp)입니다.
        /// </summary>
        public static float Scale
        {
            get
            {
                bool mobile = MascotInput.IsMobile;
                float dpi = UnityEngine.Device.Screen.dpi;
                if (dpi > 0f) return Mathf.Max(1f, dpi / (mobile ? 160f : 96f));
                // dpi를 못 읽는 기기는 화면 짧은 변을 360dp로 봅니다
                return mobile ? Mathf.Max(1f, Mathf.Min(MascotScreen.Width, MascotScreen.Height) / 360f) : 1f;
            }
        }

        /// <summary>
        /// 노치, 둥근 모서리, 홈 표시줄을 뺀 영역 (Unity 화면 좌표, 왼쪽 아래가 원점).
        /// 데스크톱과 Android 홈 화면 창에서는 Unity 화면 전체입니다.
        /// </summary>
        public static Rect SafeArea
        {
            get
            {
                // 홈 화면 창은 MascotOverlay가 표시줄을 피해서 놓습니다
                if (MascotOverlay.IsOverlay) return new Rect(0f, 0f, Screen.width, Screen.height);
                Rect area = UnityEngine.Device.Screen.safeArea;
                return area.width > 0f && area.height > 0f ? area : new Rect(0f, 0f, Screen.width, Screen.height);
            }
        }

        /// <summary>SafeArea를 GUI 좌표(위쪽이 0)로 바꾼 것</summary>
        public static Rect SafeGuiRect
        {
            get
            {
                Rect area = SafeArea;
                return new Rect(area.xMin, Screen.height - area.yMax, area.width, area.height);
            }
        }

        /// <summary>인스펙터에서 고른 글꼴 (MascotSpeech.font). 없으면 OS 글꼴을 씁니다.</summary>
        public static Font CustomFont { get; set; }

        /// <summary>한글이 나오는 글꼴 (고른 글꼴이 없으면 맑은 고딕 같은 OS 글꼴)</summary>
        public static Font UiFont
        {
            get
            {
                if (CustomFont != null) return CustomFont;
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
