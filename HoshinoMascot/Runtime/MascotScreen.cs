using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 캐릭터 행동(걷기, 바닥, 떨어지기)에 쓰는 "기기 화면 전체" 좌표입니다 (왼쪽 아래가 원점, 픽셀).
    /// 보통은 Unity 화면과 같습니다. Android 홈 화면에 띄웠을 때는 Unity 창이 캐릭터 주변의 작은 창이라서
    /// MascotOverlay가 기기 화면 크기와 창 위치(Offset)를 채워 넣습니다.
    /// 말풍선, 메뉴, 클릭 판정처럼 Unity 창 안에 그리는 것은 창 좌표(Screen)를 그대로 씁니다.
    /// </summary>
    public static class MascotScreen
    {
        static bool overlay;
        static Vector2 displaySize;
        static Rect displaySafeArea;
        static Vector2 pixelScale = Vector2.one; // Unity 화면 1픽셀이 기기 화면 몇 픽셀인지 (해상도 낮춰 그리기)

        /// <summary>Unity 창의 왼쪽 아래 모서리가 기기 화면 전체에서 어디인지</summary>
        public static Vector2 Offset { get; private set; }
        /// <summary>이번 프레임에 Unity 창이 옮겨지거나 크기가 바뀌었는지</summary>
        public static bool WindowChanged { get; private set; }
        /// <summary>Unity 화면 1픽셀이 기기 화면 몇 픽셀인지 (보통 1)</summary>
        public static Vector2 PixelScale => pixelScale;

        public static float Width => overlay ? displaySize.x : Screen.width;
        public static float Height => overlay ? displaySize.y : Screen.height;
        /// <summary>시스템 표시줄, 노치를 뺀 영역</summary>
        public static Rect SafeArea => overlay ? displaySafeArea : MascotUiSkin.SafeArea;

        public static Vector2 ToDisplay(Vector2 windowPosition) => Offset + Vector2.Scale(windowPosition, pixelScale);
        public static Vector2 ToWindow(Vector2 displayPosition)
        {
            Vector2 local = displayPosition - Offset;
            return new Vector2(local.x / pixelScale.x, local.y / pixelScale.y);
        }

        internal static void SetDisplay(Vector2 size, Rect safeArea)
        {
            overlay = true;
            displaySize = size;
            displaySafeArea = safeArea;
        }

        internal static void SetWindow(Vector2 offset, Vector2 scale, bool changed)
        {
            Offset = offset;
            pixelScale = scale;
            WindowChanged = changed;
        }
    }
}
