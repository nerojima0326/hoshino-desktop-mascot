using System;
using System.Runtime.InteropServices;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace HoshinoMascot
{
    /// <summary>
    /// 마우스와 터치 입력을 한곳에서 읽습니다. 예전 Input Manager와 새 Input System을 둘 다 지원합니다.
    /// 투명 창이 클릭 통과 상태일 때는 Unity가 마우스 이벤트를 못 받으므로,
    /// Windows 빌드에서는 커서 위치와 버튼 상태를 OS에서 직접 읽습니다.
    /// 모바일에서는 첫 번째 손가락이 "왼쪽 버튼"이고, 커서 위치는 마지막으로 만진 곳입니다.
    /// </summary>
    public static class MascotInput
    {
        /// <summary>모바일(Android/iOS)인지. 에디터의 Device Simulator에서도 true입니다.</summary>
        public static bool IsMobile => UnityEngine.Device.Application.isMobilePlatform;

        /// <summary>Unity 화면 좌표(왼쪽 아래가 원점) 기준 커서 위치</summary>
        public static Vector2 CursorPosition
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                if (Native.TryGetClientCursor(out Vector2 position)) return position;
#endif
                return UnityCursorPosition();
            }
        }

        public static bool LeftDown => ButtonDown(0);
        public static bool LeftUp => ButtonUp(0);
        public static bool RightDown => ButtonDown(1);

        /// <summary>
        /// 왼쪽 버튼이 실제로 눌려 있는지. 끌다가 커서가 투명한 곳에 걸쳐서
        /// 버튼 떼기 이벤트를 놓쳐도 캐릭터가 손에 붙어 있지 않게 하는 데 씁니다.
        /// </summary>
        public static bool LeftHeld
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                return Native.IsLeftButtonPressed();
#else
                return ButtonHeld(0);
#endif
            }
        }

        /// <summary>Android의 뒤로 가기 버튼. 데스크톱의 Esc 키는 무시합니다.</summary>
        public static bool BackPressed
        {
            get
            {
                if (!IsMobile) return false;
#if ENABLE_LEGACY_INPUT_MANAGER
                return Input.GetKeyDown(KeyCode.Escape);
#elif ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
                return false;
#endif
            }
        }

        /// <summary>손가락 두 개가 화면에 닿아 있으면 그 위치를 줍니다 (핀치로 크기 조절).</summary>
        public static bool TryGetTwoTouches(out Vector2 first, out Vector2 second)
        {
            first = second = default;
            int found = 0;
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < Input.touchCount && found < 2; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) continue;
                if (found == 0) first = touch.position;
                else second = touch.position;
                found++;
            }
#elif ENABLE_INPUT_SYSTEM
            Touchscreen screen = Touchscreen.current;
            if (screen == null) return false;
            foreach (TouchControl touch in screen.touches)
            {
                if (!touch.press.isPressed) continue;
                if (found == 0) first = touch.position.ReadValue();
                else second = touch.position.ReadValue();
                if (++found == 2) break;
            }
#endif
            return found >= 2;
        }

        static Vector2 UnityCursorPosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition; // 터치도 마우스처럼 들어옵니다
#elif ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current; // 마우스나 터치 화면 중 마지막으로 쓴 것
            return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
            return Vector2.zero;
#endif
        }

        static bool ButtonDown(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(button);
#elif ENABLE_INPUT_SYSTEM
            ButtonControl control = GetButton(button);
            return control != null && control.wasPressedThisFrame;
#else
            return false;
#endif
        }

        static bool ButtonUp(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(button);
#elif ENABLE_INPUT_SYSTEM
            ButtonControl control = GetButton(button);
            return control != null && control.wasReleasedThisFrame;
#else
            return false;
#endif
        }

        static bool ButtonHeld(int button)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(button);
#elif ENABLE_INPUT_SYSTEM
            ButtonControl control = GetButton(button);
            return control != null && control.isPressed;
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        static ButtonControl GetButton(int button)
        {
            // 왼쪽 버튼은 터치도 받도록 Pointer.press를 씁니다 (마우스면 leftButton과 같습니다)
            if (button == 0) return Pointer.current?.press;
            return Mouse.current?.rightButton;
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        static class Native
        {
            [StructLayout(LayoutKind.Sequential)]
            struct POINT
            {
                public int x;
                public int y;
            }

            [DllImport("user32.dll")]
            static extern bool GetCursorPos(out POINT point);

            [DllImport("user32.dll")]
            static extern bool ScreenToClient(IntPtr hWnd, ref POINT point);

            [DllImport("user32.dll")]
            static extern short GetAsyncKeyState(int virtualKey);

            [DllImport("user32.dll")]
            static extern int GetSystemMetrics(int index);

            [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)]
            static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

            [DllImport("user32.dll")]
            static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

            [DllImport("kernel32.dll")]
            static extern uint GetCurrentProcessId();

            const int SM_SWAPBUTTON = 23;
            const int VK_LBUTTON = 0x01;
            const int VK_RBUTTON = 0x02;

            static IntPtr window;

            public static bool TryGetClientCursor(out Vector2 position)
            {
                position = default;
                if (window == IntPtr.Zero) window = FindOwnWindow();
                if (window == IntPtr.Zero) return false;
                if (!GetCursorPos(out POINT point) || !ScreenToClient(window, ref point)) return false;
                position = new Vector2(point.x, Screen.height - point.y);
                return true;
            }

            public static bool IsLeftButtonPressed()
            {
                // 왼손잡이용으로 버튼을 바꾼 경우 물리적인 오른쪽 버튼이 "왼쪽 클릭"입니다
                int key = GetSystemMetrics(SM_SWAPBUTTON) != 0 ? VK_RBUTTON : VK_LBUTTON;
                return (GetAsyncKeyState(key) & 0x8000) != 0;
            }

            // 이 프로세스가 만든 Unity 창을 찾습니다. (GetActiveWindow는 창이 포커스를 잃으면 못 씁니다)
            static IntPtr FindOwnWindow()
            {
                uint processId = GetCurrentProcessId();
                IntPtr candidate = IntPtr.Zero;
                while ((candidate = FindWindowEx(IntPtr.Zero, candidate, "UnityWndClass", null)) != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(candidate, out uint owner);
                    if (owner == processId) return candidate;
                }
                return IntPtr.Zero;
            }
        }
#endif
    }
}
