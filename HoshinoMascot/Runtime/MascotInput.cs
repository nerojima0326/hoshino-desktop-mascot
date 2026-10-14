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
    /// 마우스 입력을 한곳에서 읽습니다. 예전 Input Manager와 새 Input System을 둘 다 지원합니다.
    /// 투명 창이 클릭 통과 상태일 때는 Unity가 마우스 이벤트를 못 받으므로,
    /// Windows 빌드에서는 커서 위치와 버튼 상태를 OS에서 직접 읽습니다.
    /// </summary>
    public static class MascotInput
    {
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

        static Vector2 UnityCursorPosition()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#elif ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
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
            Mouse mouse = Mouse.current;
            if (mouse == null) return null;
            return button == 0 ? mouse.leftButton : mouse.rightButton;
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
