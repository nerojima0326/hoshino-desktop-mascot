using System.Collections;
using UnityEngine;

namespace HoshinoMascot
{
    /// <summary>
    /// Android에서 호시노를 홈 화면(다른 앱 위)에 띄웁니다. Android가 아니면 아무 일도 하지 않습니다.
    ///
    /// - 앱 화면: 메뉴의 "홈 화면에 띄우기"를 누르면 MascotOverlayService(Java)가 별도 프로세스에서
    ///   Unity를 하나 더 띄우고, 앱은 닫힙니다. 앱을 다시 열면 홈 화면의 호시노를 앱으로 데려옵니다.
    /// - 홈 화면: Android 12부터는 다른 앱 위의 창이 터치를 아래 앱으로 넘기지 못하므로, 창을 캐릭터와
    ///   말풍선이 들어갈 만큼만 작게 만들어 캐릭터를 따라 옮깁니다. 카메라는 그 창이 보는 부분만 그립니다.
    ///   들고 있거나, 떨어지거나, 메뉴가 열려 있거나, 두 손가락으로 크기를 바꿀 때는 창을 화면 전체로 넓힙니다.
    ///
    /// 없으면 MascotController가 Android에서 저절로 붙입니다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // 창 위치를 MascotController보다 먼저 읽습니다
    public class MascotOverlay : MonoBehaviour
    {
        const string ServiceClass = "com.hoshino.mascot.MascotOverlayService";

        [Header("홈 화면에 띄웠을 때")]
        [Tooltip("말풍선 최대 너비(dp). 창이 이만큼 넓어지고 창 안은 터치가 아래 앱으로 가지 않으므로, 좁게 둡니다.")]
        public float bubbleMaxWidth = 200f;
        [Tooltip("가만히 있거나 걸을 때 초당 프레임 (배터리 절약)")]
        public int idleFrameRate = 30;
        [Tooltip("들고 있거나 떨어질 때 초당 프레임")]
        public int activeFrameRate = 60;

        static bool? isOverlay;
        static bool hasPlugin;

        /// <summary>지금 이 Unity가 Android 홈 화면에 떠 있는 쪽인지</summary>
        public static bool IsOverlay
        {
            get
            {
                if (isOverlay == null) isOverlay = QueryOverlay();
                return isOverlay.Value;
            }
        }

        /// <summary>앱 화면에서 "홈 화면에 띄우기"를 쓸 수 있는지 (Android 빌드에서만 true)</summary>
        public static bool CanStart => !IsOverlay && hasPlugin;

        MascotController controller;
        MascotSpeech speech;
        MascotMenu menu;
        bool waitingForPermission;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass service;
        Camera targetCamera;
        Vector3 baseCameraPosition;
        float unitsPerPixel;
        Vector2 displaySize;
        Rect displaySafeArea; // 기기 화면 전체 좌표 (왼쪽 아래가 원점)
        float nextDisplayCheck;
        RectInt window; // 지금 맞춰 둔 창 (Android 좌표: 화면 왼쪽 위가 원점)
        RectInt sentWindow;
#endif

        static bool QueryOverlay()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var javaClass = new AndroidJavaClass(ServiceClass))
                {
                    bool hosting = javaClass.CallStatic<bool>("isHosting");
                    hasPlugin = true;
                    return hosting;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Hoshino Mascot] 홈 화면 띄우기 플러그인(MascotOverlayService.java)을 찾지 못했습니다: " + e.Message);
            }
#endif
            return false;
        }

        void Awake()
        {
            controller = GetComponent<MascotController>();
            speech = GetComponent<MascotSpeech>();
            menu = GetComponent<MascotMenu>();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsOverlay) SetUpOverlay();
#endif
        }

        void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // 앱을 열면 홈 화면에 있던 호시노를 앱으로 데려옵니다 (떠 있지 않으면 아무 일도 없습니다)
            if (CanStart) CallWithActivity("stop");
#endif
        }

        // ───────── 앱 화면 쪽 ─────────

        /// <summary>메뉴의 "홈 화면에 띄우기". 권한이 없으면 설정 화면을 엽니다.</summary>
        public void RequestStart()
        {
            if (!CanStart) return;
            if (HasPermission())
            {
                StartCoroutine(StartOverlay());
                return;
            }
            if (speech != null) speech.SayRandom(speech.overlayPermissionLines);
            StartCoroutine(OpenPermissionSettings());
        }

        IEnumerator OpenPermissionSettings()
        {
            yield return new WaitForSeconds(1.5f); // 말풍선을 읽을 틈을 줍니다
            waitingForPermission = true;
            CallWithActivity("requestPermission");
        }

        void OnApplicationPause(bool paused)
        {
            if (paused || !waitingForPermission) return;
            // 설정 화면에서 돌아왔을 때 허용했으면 바로 띄웁니다
            waitingForPermission = false;
            if (HasPermission()) StartCoroutine(StartOverlay());
            else if (speech != null) speech.SayRandom(speech.overlayDeniedLines);
        }

        IEnumerator StartOverlay()
        {
            // 홈 화면 쪽 Unity가 같은 위치와 크기로 시작하도록 먼저 저장합니다
            if (controller != null) controller.SaveState();
            CallWithActivity("start");
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaObject activity = CurrentActivity()) activity.Call<bool>("moveTaskToBack", true);
#endif
            // 홈 화면 쪽은 별도 프로세스라서 앱은 닫아도 됩니다
            yield return new WaitForSeconds(1f);
            Application.Quit();
        }

        bool HasPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var javaClass = new AndroidJavaClass(ServiceClass))
            using (AndroidJavaObject activity = CurrentActivity())
                return javaClass.CallStatic<bool>("canDrawOverlays", activity);
#else
            return false;
#endif
        }

        /// <summary>MascotOverlayService의 static 메서드를 현재 Activity를 넘겨서 부릅니다.</summary>
        static void CallWithActivity(string method)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var javaClass = new AndroidJavaClass(ServiceClass))
            using (AndroidJavaObject activity = CurrentActivity())
                javaClass.CallStatic(method, activity);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                return player.GetStatic<AndroidJavaObject>("currentActivity");
        }
#endif

        // ───────── 홈 화면 쪽 ─────────

        /// <summary>홈 화면의 호시노를 내립니다 (홈 화면 쪽 Unity가 종료할 때 부릅니다).</summary>
        public static void StopOverlay()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var javaClass = new AndroidJavaClass(ServiceClass)) javaClass.CallStatic("stopFromUnity");
#endif
        }

        /// <summary>창을 화면 전체로 넓혀야 하는지</summary>
        bool Expanded
        {
            get
            {
                if (controller == null) return false;
                MascotController.State state = controller.CurrentState;
                return state == MascotController.State.Held
                       || state == MascotController.State.Fall
                       || controller.IsPinching
                       || (menu != null && menu.IsOpen);
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void SetUpOverlay()
        {
            service = new AndroidJavaClass(ServiceClass);
            targetCamera = controller != null && controller.TargetCamera != null ? controller.TargetCamera : Camera.main;
            if (targetCamera == null)
            {
                Debug.LogWarning("[Hoshino Mascot] 카메라가 없어서 홈 화면 창을 맞출 수 없습니다.");
                service = null;
                return;
            }

            // 창 안의 캐릭터가 없는 곳은 투명하게 해서 홈 화면이 보이게 합니다
            targetCamera.orthographic = true;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            baseCameraPosition = targetCamera.transform.position;
            if (speech != null) speech.maxWidth = Mathf.Min(speech.maxWidth, bubbleMaxWidth);

            ReadDisplay();
            // 씬의 카메라가 화면 높이에 맞춰 둔 크기를 기기 화면의 짧은 변에 맞춥니다. 화면을 돌려도 캐릭터 크기가 그대로입니다
            unitsPerPixel = targetCamera.orthographicSize * 2f / Mathf.Max(1f, Mathf.Min(displaySize.x, displaySize.y));
            ReadAppliedWindow();
        }

        void Update()
        {
            if (service == null) return;
            // 화면을 돌렸는지 가끔 확인합니다
            if (Time.unscaledTime >= nextDisplayCheck)
            {
                nextDisplayCheck = Time.unscaledTime + 0.5f;
                ReadDisplay();
            }
            // 손가락 위치는 실제로 놓인 창 기준이므로, 이번 프레임의 행동은 실제 창 위치로 계산합니다
            ReadAppliedWindow();
            Application.targetFrameRate = Expanded ? activeFrameRate : idleFrameRate;
        }

        void LateUpdate()
        {
            if (service == null) return;
            RectInt next = DesiredWindow();
            if (!next.Equals(sentWindow))
            {
                sentWindow = next;
                service.CallStatic("setWindowRect", next.x, next.y, next.width, next.height);
            }
            // 이번 프레임은 옮길 창 위치에 맞춰 그립니다
            ApplyWindow(next, !next.Equals(window));
        }

        void ReadDisplay()
        {
            int[] info = service.CallStatic<int[]>("getDisplayInfo");
            if (info == null || info.Length < 6 || info[0] <= 0 || info[1] <= 0) return;
            displaySize = new Vector2(info[0], info[1]);
            // [너비, 높이, 왼쪽, 위, 오른쪽, 아래 표시줄 두께]를 왼쪽 아래 원점의 영역으로 바꿉니다
            displaySafeArea = Rect.MinMaxRect(info[2], info[5], info[0] - info[4], info[1] - info[3]);
            MascotScreen.SetDisplay(displaySize, displaySafeArea);
        }

        void ReadAppliedWindow()
        {
            int[] rect = service.CallStatic<int[]>("getWindowRect");
            if (rect == null || rect.Length < 4 || rect[2] <= 0 || rect[3] <= 0) return;
            var applied = new RectInt(rect[0], rect[1], rect[2], rect[3]);
            bool changed = !applied.Equals(window);
            window = applied;
            ApplyWindow(applied, changed);
        }

        /// <summary>창 위치에 맞춰 화면 좌표 변환과 카메라를 맞춥니다. 기기 화면 1픽셀은 늘 unitsPerPixel만큼입니다.</summary>
        void ApplyWindow(RectInt rect, bool changed)
        {
            var offset = new Vector2(rect.x, displaySize.y - rect.y - rect.height);
            // Unity가 창보다 낮은 해상도로 그리거나 창 크기가 막 바뀐 참이면 Unity 화면 1픽셀이 창 1픽셀과 다릅니다
            var pixelScale = new Vector2(rect.width / (float)Mathf.Max(1, Screen.width), rect.height / (float)Mathf.Max(1, Screen.height));
            MascotScreen.SetWindow(offset, pixelScale, changed);

            // 카메라를 창이 보는 부분으로 옮깁니다 (기기 화면 한가운데가 씬의 원래 카메라 위치)
            var size = new Vector2(rect.width, rect.height);
            Vector2 center = offset + size * 0.5f - displaySize * 0.5f;
            Transform cameraTransform = targetCamera.transform;
            targetCamera.orthographicSize = size.y * 0.5f * unitsPerPixel;
            targetCamera.aspect = size.x / size.y;
            cameraTransform.position = baseCameraPosition
                                       + cameraTransform.right * (center.x * unitsPerPixel)
                                       + cameraTransform.up * (center.y * unitsPerPixel);
        }

        /// <summary>이번 프레임에 맞출 창 (Android 좌표: 화면 왼쪽 위가 원점)</summary>
        RectInt DesiredWindow()
        {
            Rect area = displaySafeArea;
            if (!Expanded && controller != null)
            {
                // 캐릭터와 그 위의 말풍선(3줄)이 들어갈 만큼만 (dp를 기기 화면 픽셀로)
                float scale = MascotUiSkin.Scale * MascotScreen.PixelScale.x;
                float character = controller.Height / unitsPerPixel;
                float bubbleWidth = speech != null ? (speech.maxWidth + 32f) * scale : 0f;
                float bubbleHeight = speech != null ? (speech.fontSize * 4f + 32f) * scale : 0f;
                float width = Mathf.Min(Mathf.Max(character * 1.2f, bubbleWidth), area.width);
                float height = Mathf.Min(character * 1.25f + bubbleHeight, area.height);

                Vector2 feet = controller.FeetDisplayPosition;
                float x = Mathf.Clamp(feet.x - width * 0.5f, area.xMin, area.xMax - width);
                float y = Mathf.Clamp(feet.y - 8f * scale, area.yMin, area.yMax - height);
                area = new Rect(x, y, width, height);
            }
            return new RectInt(
                Mathf.RoundToInt(area.xMin),
                Mathf.RoundToInt(displaySize.y - area.yMax),
                Mathf.RoundToInt(area.width),
                Mathf.RoundToInt(area.height));
        }

        void OnDestroy()
        {
            service?.Dispose();
            service = null;
        }
#endif
    }
}
