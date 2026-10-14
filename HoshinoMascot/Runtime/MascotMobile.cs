using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// 모바일(Android/iOS)에서만 쓰는 화면 설정입니다. 데스크톱에서는 아무 일도 하지 않습니다.
    /// 모바일은 다른 앱 위에 투명하게 띄울 수 없어서, 앱 화면에 배경을 깔고 그 위에 캐릭터를 세웁니다.
    /// 없으면 MascotController가 모바일에서 기본값으로 붙입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class MascotMobile : MonoBehaviour
    {
        [Header("배경")]
        public Color backgroundColor = new Color(0.85f, 0.92f, 0.98f);
        [Tooltip("배경 그림. 비워 두면 단색입니다. 화면을 빈틈없이 채우도록 늘리고, 넘치는 부분은 잘립니다.")]
        public Texture2D backgroundImage;

        [Header("화면")]
        [Tooltip("켜 두면 화면이 저절로 꺼지지 않습니다. 배터리를 더 씁니다.")]
        public bool keepScreenOn;

        Camera targetCamera;
        Transform background;
        Sprite backgroundSprite;

        void Start()
        {
            // 홈 화면에 띄운 쪽은 배경 없이 투명하게 둡니다 (MascotOverlay가 맡습니다)
            if (!MascotInput.IsMobile || MascotOverlay.IsOverlay)
            {
                enabled = false;
                return;
            }

            var controller = GetComponent<MascotController>();
            targetCamera = controller != null && controller.TargetCamera != null ? controller.TargetCamera : Camera.main;
            if (targetCamera == null)
            {
                enabled = false;
                return;
            }

            // 투명 창용 배경(알파 0)은 모바일에서 검게 나오므로 불투명한 색으로 바꿉니다
            Color color = backgroundColor;
            color.a = 1f;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = color;
            Screen.sleepTimeout = keepScreenOn ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;

            if (backgroundImage != null) CreateBackground();
        }

        // 화면을 돌려도 배경이 화면을 꽉 채우게 합니다
        void LateUpdate()
        {
            if (background != null) FitBackground();
        }

        void OnDestroy()
        {
            if (background != null) Destroy(background.gameObject);
            if (backgroundSprite != null) Destroy(backgroundSprite);
        }

        void CreateBackground()
        {
            var rect = new Rect(0f, 0f, backgroundImage.width, backgroundImage.height);
            backgroundSprite = Sprite.Create(backgroundImage, rect, new Vector2(0.5f, 0.5f), 100f);

            // 카메라 맨 뒤쪽에 붙여 두어 캐릭터 뒤에 그려지게 합니다. 콜라이더가 없어서 클릭을 막지 않습니다
            var backgroundObject = new GameObject("Mascot Background");
            background = backgroundObject.transform;
            background.SetParent(targetCamera.transform, false);
            backgroundObject.AddComponent<SpriteRenderer>().sprite = backgroundSprite;
            FitBackground();
        }

        void FitBackground()
        {
            float distance = targetCamera.farClipPlane * 0.9f;
            float height = targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float width = height * targetCamera.aspect;
            Vector3 size = backgroundSprite.bounds.size;
            float scale = Mathf.Max(width / size.x, height / size.y);

            background.localPosition = new Vector3(0f, 0f, distance);
            background.localRotation = Quaternion.identity;
            background.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
