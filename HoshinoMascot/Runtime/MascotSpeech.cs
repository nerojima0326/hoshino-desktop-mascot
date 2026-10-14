using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 머리 위에 말풍선을 띄우고 상황에 맞는 대사를 고릅니다.
    /// 대사는 인스펙터에서 자유롭게 고칠 수 있습니다.
    /// (코드에 적힌 기본값은 컴포넌트를 처음 붙일 때만 들어갑니다.)
    /// </summary>
    [DisallowMultipleComponent]
    public class MascotSpeech : MonoBehaviour
    {
        [Header("켰을 때 인사 (시간대별)")]
        public string[] morningGreetings =
        {
            "으헤~ 선생님, 좋은 아침~ 아저씨는 아직 졸려…",
            "벌써 아침이야? 5분만 더 자면 안 될까~",
        };
        public string[] afternoonGreetings =
        {
            "으헤~ 선생님 왔구나~ 오늘도 적당히 힘내자~",
            "점심은 먹었어? 아저씨는 먹고 나니까 졸리네~",
        };
        public string[] eveningGreetings =
        {
            "오늘 하루도 수고했어, 선생님~",
            "슬슬 쉴 시간 아니야? 으헤~",
        };
        public string[] nightGreetings =
        {
            "이 시간까지 일하는 거야? 무리하면 안 돼~",
            "으헤~ 아저씨는 벌써 잘 시간인데~",
        };

        [Header("상황별 대사")]
        public string[] pokeLines =
        {
            "으헤? 왜~ 선생님?",
            "아저씨 지금 쉬는 중이야~",
            "응~? 무슨 일 있어?",
            "으헤~ 간지러워~",
            "선생님도 같이 쉬자~",
        };
        public string[] annoyedLines =
        {
            "으헤~ 그만 찔러~ 아저씨 괴롭히는 거야?",
            "선생님~ 자꾸 그러면 아저씨도 삐진다~?",
        };
        public string[] pickUpLines =
        {
            "으헤?! 어, 어디 데려가는 거야~",
            "높은 데는 좀 무서운데~",
            "살살 다뤄줘~ 아저씨 몸이 예전 같지 않다구~",
        };
        public string[] landLines =
        {
            "으헤~ 착지 성공~",
            "휴~ 내려왔다~",
        };
        public string[] hardLandingLines =
        {
            "아야야… 아저씨 허리…",
            "으헤… 좀 더 살살 내려줘~",
        };
        public string[] fallAsleepLines =
        {
            "으헤~ 아저씨는 잠깐 낮잠 좀…",
            "조금만… 눈 좀 붙일게…",
        };
        public string[] sleepTalkLines =
        {
            "쿨… 쿨…",
            "Zzz…",
            "음냐… 선생님… 5분만…",
        };
        public string[] wakeUpLines =
        {
            "으헤?! 안 잤어~ 눈만 감고 있었어~",
            "응…? 벌써 일어날 시간이야~?",
        };
        public string[] sitDownLines =
        {
            "으헤~ 잠깐 앉아서 쉴게~",
            "역시 앉는 게 편하다니까~",
            "다리 아파~ 아저씨는 좀 앉을래~",
        };
        public string[] standUpLines =
        {
            "영차~ 슬슬 일어나 볼까~",
            "으헤~ 엉덩이가 저려~",
        };
        public string[] yawnLines =
        {
            "하아암~ 으헤~ 졸려~",
            "하암… 아저씨는 벌써 졸려~",
            "으헤~ 하품이 자꾸 나오네~",
        };
        public string[] dozeWakeLines =
        {
            "으헤?! 안 졸았어~ 진짜야~",
            "…앗, 방금 잠깐 졸았나~?",
        };
        public string[] chatterLines =
        {
            "오늘 날씨, 낮잠 자기 딱 좋네~",
            "선생님, 일은 적당히 하고 쉬어~",
            "아저씨는 여기서 지켜보고 있을게~",
            "물 좀 마시면서 해, 선생님~",
            "고래 보러 가고 싶다~",
        };
        public string[] lateNightChatterLines =
        {
            "선생님, 너무 늦게까지 무리하지 마~",
            "졸리면 자는 게 제일이야~ 으헤~",
        };
        public string[] goodbyeLines =
        {
            "그럼 아저씨는 먼저 갈게~ 또 봐, 선생님~",
        };

        [Header("홈 화면에 띄우기 (Android)")]
        public string[] overlayPermissionLines =
        {
            "홈 화면에 놀러 가려면 '다른 앱 위에 표시'를 허용해 줘~",
        };
        public string[] overlayDeniedLines =
        {
            "으헤~ 허용 안 해 주면 홈 화면엔 못 가~",
        };

        [Header("말풍선 모양 (100% 배율 기준 픽셀)")]
        [Tooltip("말풍선과 메뉴에 쓸 글꼴 (TTF). 비워 두면 OS 글꼴(맑은 고딕 등)을 씁니다. 한글이 네모로 나오면 한글 글꼴을 넣으세요.")]
        public Font font;
        public int fontSize = 16;
        public float maxWidth = 260f;
        public Color fillColor = Color.white;
        public Color borderColor = new Color(0.96f, 0.62f, 0.75f);
        public Color textColor = new Color(0.25f, 0.22f, 0.3f);

        [Header("표시 시간")]
        [Tooltip("글자가 하나씩 나타나는 속도 (초당 글자 수)")]
        public float charactersPerSecond = 30f;
        public float minSeconds = 2.5f;
        [Tooltip("글자 하나당 더 보여주는 시간")]
        public float secondsPerCharacter = 0.08f;

        const int CornerRadius = 12;
        const float BorderWidth = 2f;
        const float TailWidth = 18f;
        const float TailHeight = 10f;
        static readonly Vector2 Padding = new Vector2(14f, 9f);

        MascotController controller;
        string text;
        float shownAt;
        float hideAt;
        string lastLine;
        Rect bubbleRect;

        GUIStyle bubbleStyle;
        GUIStyle textStyle;
        Texture2D bubbleTexture;
        Texture2D tailTexture;
        float builtScale;

        public bool IsShowing => text != null && Time.time < hideAt;
        /// <summary>글자가 아직 하나씩 나타나는 중인지 (입을 움직일 때 씁니다)</summary>
        public bool IsTalking => IsShowing && Time.time - shownAt < text.Length / Mathf.Max(1f, charactersPerSecond);

        void Awake()
        {
            controller = GetComponent<MascotController>();
            if (font != null) MascotUiSkin.CustomFont = font;
        }

        void OnDestroy() => DestroyTextures();

        public void Say(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            text = line;
            shownAt = Time.time;
            float typing = line.Length / Mathf.Max(1f, charactersPerSecond);
            hideAt = Time.time + typing + Mathf.Max(minSeconds, line.Length * secondsPerCharacter);
        }

        public void SayRandom(string[] lines)
        {
            if (lines == null || lines.Length == 0) return;
            int index = Random.Range(0, lines.Length);
            // 같은 대사가 연달아 나오지 않게 합니다
            if (lines.Length > 1 && lines[index] == lastLine) index = (index + 1) % lines.Length;
            lastLine = lines[index];
            Say(lastLine);
        }

        public void SayGreeting()
        {
            int hour = System.DateTime.Now.Hour;
            if (hour >= 5 && hour < 11) SayRandom(morningGreetings);
            else if (hour >= 11 && hour < 17) SayRandom(afternoonGreetings);
            else if (hour >= 17 && hour < 22) SayRandom(eveningGreetings);
            else SayRandom(nightGreetings);
        }

        public void SayChatter()
        {
            int hour = System.DateTime.Now.Hour;
            bool lateNight = hour >= 23 || hour < 5;
            SayRandom(lateNight && Random.value < 0.5f ? lateNightChatterLines : chatterLines);
        }

        public void Dismiss() => text = null;

        /// <summary>Unity 화면 좌표(왼쪽 아래가 원점)가 말풍선 위인지</summary>
        public bool ContainsScreenPoint(Vector2 screenPosition)
        {
            return IsShowing && bubbleRect.Contains(new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        }

        void OnGUI()
        {
            if (!IsShowing || controller == null || controller.TargetCamera == null) return;
            GUI.depth = 0;
            float scale = MascotUiSkin.Scale;
            EnsureStyles(scale);

            var content = new GUIContent(text);
            Vector2 padding = Padding * scale;
            float textWidth = Mathf.Min(textStyle.CalcSize(content).x, maxWidth * scale);
            float textHeight = textStyle.CalcHeight(content, textWidth);
            float width = textWidth + padding.x * 2f;
            float height = textHeight + padding.y * 2f;
            float tailWidth = TailWidth * scale;
            float tailHeight = TailHeight * scale;

            // 머리 위에 띄우되 화면(휴대폰은 노치를 뺀 곳) 밖으로 나가지 않게 합니다 (GUI 좌표는 위쪽이 0)
            Vector2 anchor = controller.BubbleAnchorScreen;
            float anchorY = Screen.height - anchor.y;
            Rect safe = MascotUiSkin.SafeGuiRect;
            float x = Mathf.Clamp(anchor.x - width * 0.5f, safe.xMin + 4f, Mathf.Max(safe.xMin + 4f, safe.xMax - width - 4f));
            float y = Mathf.Clamp(anchorY - height - tailHeight, safe.yMin + 4f, Mathf.Max(safe.yMin + 4f, safe.yMax - height - tailHeight - 4f));
            bubbleRect = new Rect(x, y, width, height);

            Event current = Event.current;
            if (current.type == EventType.MouseDown && bubbleRect.Contains(current.mousePosition))
            {
                Dismiss(); // 말풍선을 클릭하면 닫습니다
                current.Use();
                return;
            }
            if (current.type != EventType.Repaint) return;

            float fadeIn = Mathf.Clamp01((Time.time - shownAt) / 0.12f);
            float fadeOut = Mathf.Clamp01((hideAt - Time.time) / 0.25f);
            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, fadeIn * fadeOut);

            bubbleStyle.Draw(bubbleRect, false, false, false, false);
            float corner = CornerRadius * scale;
            float tailX = Mathf.Clamp(anchor.x - tailWidth * 0.5f, x + corner, x + width - tailWidth - corner);
            // 꼬리 윗부분을 말풍선 테두리에 겹쳐서 이음새가 안 보이게 합니다
            GUI.DrawTexture(new Rect(tailX, y + height - BorderWidth * scale, tailWidth, tailHeight), tailTexture);

            int shown = Mathf.Clamp(Mathf.CeilToInt((Time.time - shownAt) * charactersPerSecond), 1, text.Length);
            GUI.Label(new Rect(x + padding.x, y + padding.y, textWidth, textHeight), text.Substring(0, shown), textStyle);

            GUI.color = previousColor;
        }

        void EnsureStyles(float scale)
        {
            if (bubbleStyle != null && Mathf.Approximately(builtScale, scale)) return;
            DestroyTextures();
            builtScale = scale;

            int radius = Mathf.RoundToInt(CornerRadius * scale);
            float border = BorderWidth * scale;
            bubbleTexture = MascotUiSkin.RoundedRect(fillColor, borderColor, radius, border);
            tailTexture = MascotUiSkin.Tail(fillColor, borderColor, Mathf.RoundToInt(TailWidth * scale), Mathf.RoundToInt(TailHeight * scale), border);

            bubbleStyle = new GUIStyle { border = new RectOffset(radius, radius, radius, radius) };
            bubbleStyle.normal.background = bubbleTexture;

            textStyle = new GUIStyle
            {
                font = MascotUiSkin.UiFont,
                fontSize = Mathf.RoundToInt(fontSize * scale),
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
            };
            textStyle.normal.textColor = textColor;
        }

        void DestroyTextures()
        {
            if (bubbleTexture != null) Destroy(bubbleTexture);
            if (tailTexture != null) Destroy(tailTexture);
        }
    }
}
