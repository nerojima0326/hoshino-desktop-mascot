using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 캐릭터를 오른쪽 클릭하면 나오는 메뉴입니다. 모바일에서는 캐릭터를 길게 누르면 나옵니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MascotController))]
    public class MascotMenu : MonoBehaviour
    {
        [Header("모양 (100% 배율 기준 픽셀)")]
        public int fontSize = 15;
        public float width = 150f;
        public float itemHeight = 32f;
        [Tooltip("모바일에서 쓰는 항목 높이 (손가락으로 누르기 좋게)")]
        public float touchItemHeight = 44f;
        public Color fillColor = Color.white;
        public Color borderColor = new Color(0.96f, 0.62f, 0.75f);
        public Color hoverColor = new Color(1f, 0.9f, 0.94f);
        public Color textColor = new Color(0.25f, 0.22f, 0.3f);

        [Header("크기 조절")]
        [Tooltip("크게/작게를 한 번 누를 때 바뀌는 배율")]
        public float scaleStep = 1.15f;

        const int CornerRadius = 10;
        const float HoverRadius = 6f;
        const float BorderWidth = 2f;
        const float Padding = 6f;

        MascotController controller;
        bool open;
        bool openedByTouch;
        Vector2 openPosition; // GUI 좌표 (위쪽이 0)
        Rect panelRect;

        GUIStyle panelStyle;
        GUIStyle itemStyle;
        Texture2D panelTexture;
        Texture2D hoverTexture;
        float builtScale;

        public bool IsOpen => open;

        // iOS는 앱이 스스로 종료하면 안 되므로 "종료"를 뺍니다
        static bool CanQuit => UnityEngine.Device.Application.platform != RuntimePlatform.IPhonePlayer;
        static int ItemCount => CanQuit ? 6 : 5;
        float ItemHeight => MascotInput.IsMobile ? touchItemHeight : itemHeight;

        void Awake() => controller = GetComponent<MascotController>();

        void OnDestroy() => DestroyTextures();

        // 다른 창을 클릭하면 이 창이 포커스를 잃으므로 그때 메뉴를 닫습니다
        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) open = false;
        }

        /// <summary>Unity 화면 좌표(왼쪽 아래가 원점)가 메뉴 위인지</summary>
        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            return open && panelRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        void Update()
        {
            Vector2 cursor = MascotInput.CursorPosition;
            if (ContainsScreenPoint(cursor)) return;

            if (MascotInput.RightDown)
            {
                if (controller.IsPointerOnCharacter(cursor)) Open(cursor);
                else open = false;
            }
            else if (MascotInput.LeftDown)
            {
                open = false;
            }
        }

        /// <summary>
        /// Unity 화면 좌표(왼쪽 아래가 원점)에 메뉴를 엽니다.
        /// byTouch면 손가락에 가리지 않게 손가락 위쪽에 엽니다.
        /// </summary>
        public void Open(Vector2 screenPosition, bool byTouch = false)
        {
            openPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            openedByTouch = byTouch;
            open = true;
            LayoutPanel(MascotUiSkin.Scale);
        }

        public void Close() => open = false;

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -10; // 말풍선보다 위에 그립니다
            float scale = MascotUiSkin.Scale;
            EnsureStyles(scale);
            LayoutPanel(scale);

            GUI.Box(panelRect, GUIContent.none, panelStyle);

            float padding = Padding * scale;
            var row = new Rect(panelRect.x + padding, panelRect.y + padding, panelRect.width - padding * 2f, ItemHeight * scale);
            if (Item(ref row, "말 걸기")) controller.Talk();
            if (Item(ref row, controller.IsSleeping ? "깨우기" : "낮잠 재우기")) controller.ToggleSleep();
            if (Item(ref row, controller.IsSitting ? "일어나기" : "앉기")) controller.ToggleSit();
            if (Item(ref row, "크게")) controller.ChangeScale(scaleStep);
            if (Item(ref row, "작게")) controller.ChangeScale(1f / scaleStep);
            if (CanQuit && Item(ref row, "종료")) controller.Quit();
        }

        bool Item(ref Rect row, string label)
        {
            bool clicked = GUI.Button(row, label, itemStyle);
            row.y += row.height;
            if (clicked) open = false;
            return clicked;
        }

        void LayoutPanel(float scale)
        {
            float panelWidth = width * scale;
            float panelHeight = ItemCount * ItemHeight * scale + Padding * scale * 2f;
            Rect safe = MascotUiSkin.SafeGuiRect;
            float x = openPosition.x;
            float y = openPosition.y;
            if (openedByTouch)
            {
                // 손가락 위쪽 가운데에 엽니다. 위에 자리가 없으면 아래쪽에 엽니다.
                float gap = 24f * scale;
                x -= panelWidth * 0.5f;
                y = y - gap - panelHeight >= safe.yMin + 4f ? y - gap - panelHeight : y + gap;
            }
            // 화면(휴대폰은 노치를 뺀 곳) 밖으로 나가지 않게 합니다
            x = Mathf.Max(safe.xMin + 4f, Mathf.Min(x, safe.xMax - panelWidth - 4f));
            y = Mathf.Max(safe.yMin + 4f, Mathf.Min(y, safe.yMax - panelHeight - 4f));
            panelRect = new Rect(x, y, panelWidth, panelHeight);
        }

        void EnsureStyles(float scale)
        {
            if (panelStyle != null && Mathf.Approximately(builtScale, scale)) return;
            DestroyTextures();
            builtScale = scale;

            int radius = Mathf.RoundToInt(CornerRadius * scale);
            int hoverRadius = Mathf.RoundToInt(HoverRadius * scale);
            panelTexture = MascotUiSkin.RoundedRect(fillColor, borderColor, radius, BorderWidth * scale);
            hoverTexture = MascotUiSkin.RoundedRect(hoverColor, hoverColor, hoverRadius, 0f);

            panelStyle = new GUIStyle { border = new RectOffset(radius, radius, radius, radius) };
            panelStyle.normal.background = panelTexture;

            itemStyle = new GUIStyle
            {
                font = MascotUiSkin.UiFont,
                fontSize = Mathf.RoundToInt(fontSize * scale),
                alignment = TextAnchor.MiddleLeft,
                border = new RectOffset(hoverRadius, hoverRadius, hoverRadius, hoverRadius),
                padding = new RectOffset(Mathf.RoundToInt(12f * scale), 0, 0, 0),
            };
            itemStyle.normal.textColor = textColor;
            itemStyle.hover.textColor = textColor;
            itemStyle.active.textColor = textColor;
            itemStyle.hover.background = hoverTexture;
            itemStyle.active.background = hoverTexture;
        }

        void DestroyTextures()
        {
            if (panelTexture != null) Destroy(panelTexture);
            if (hoverTexture != null) Destroy(hoverTexture);
        }
    }
}
