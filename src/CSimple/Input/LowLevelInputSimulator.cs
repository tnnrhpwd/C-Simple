using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CSimple.Input
{
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }

    public static class LowLevelInputSimulator
    {
        #region P/Invoke declarations, constants and structs

        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint); // Made public if needed elsewhere

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex); // Made public if needed elsewhere

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetMessageExtraInfo();

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_SCANCODE = 0x0008; // Added if needed for scan codes
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;
        private const uint MOUSEEVENTF_MOVE_NOCOALESCING = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT // Keep if needed, otherwise remove
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUT_UNION
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi; // Keep if needed
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUT_UNION u;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT // Made public if needed elsewhere
        {
            public int X;
            public int Y;
        }

        #endregion

        #region Low-Level Input Methods

        public static void SendLowLevelMouseMove(int x, int y)
        {
            try
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;

                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                inputs[0].u.mi.dx = (x * 65535) / screenWidth;
                inputs[0].u.mi.dy = (y * 65535) / screenHeight;
                inputs[0].u.mi.mouseData = 0;
                inputs[0].u.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
                inputs[0].u.mi.time = 0;
                inputs[0].u.mi.dwExtraInfo = IntPtr.Zero; // Using IntPtr.Zero instead of GetMessageExtraInfo

                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

                if (result != 1)
                {
                    Debug.WriteLine($"⚠️ SendInput failed for mouse move to ({x},{y}), error code: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendLowLevelMouseMove: {ex.Message}");
            }
        }

        public static void SendLowLevelMouseClick(MouseButton button, bool isUp, int x, int y)
        {
            try
            {
                // Ensure position before click event
                SendLowLevelMouseMove(x, y);

                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;
                inputs[0].u.mi.dx = 0; // Position is set by the preceding move
                inputs[0].u.mi.dy = 0;
                inputs[0].u.mi.mouseData = 0;
                inputs[0].u.mi.time = 0;
                inputs[0].u.mi.dwExtraInfo = IntPtr.Zero; // Using IntPtr.Zero

                switch (button)
                {
                    case MouseButton.Left:
                        inputs[0].u.mi.dwFlags = (uint)(isUp ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN);
                        break;
                    case MouseButton.Right:
                        inputs[0].u.mi.dwFlags = (uint)(isUp ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_RIGHTDOWN);
                        break;
                    case MouseButton.Middle:
                        inputs[0].u.mi.dwFlags = (uint)(isUp ? MOUSEEVENTF_MIDDLEUP : MOUSEEVENTF_MIDDLEDOWN);
                        break;
                }

                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

                if (result != 1)
                {
                    Debug.WriteLine($"⚠️ SendInput failed for mouse {button} {(isUp ? "UP" : "DOWN")}, error code: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendLowLevelMouseClick: {ex.Message}");
            }
        }

        public static void SendKeyboardInput(ushort key, bool isKeyUp)
        {
            try
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = key;
                inputs[0].u.ki.wScan = 0; // Use 0 for virtual key codes

                if (IsExtendedKey(key))
                {
                    inputs[0].u.ki.dwFlags = isKeyUp ?
                        KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY :
                        KEYEVENTF_KEYDOWN | KEYEVENTF_EXTENDEDKEY;
                }
                else
                {
                    inputs[0].u.ki.dwFlags = isKeyUp ? KEYEVENTF_KEYUP : KEYEVENTF_KEYDOWN;
                }

                inputs[0].u.ki.time = 0;
                inputs[0].u.ki.dwExtraInfo = IntPtr.Zero; // Using IntPtr.Zero

                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

                if (result != 1)
                {
                    // Consider logging the specific key and error code
                    Debug.WriteLine($"⚠️ SendInput failed for key {key}, isKeyUp={isKeyUp}, error code: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendKeyboardInput: {ex.Message}");
            }
        }

        public static void SendRawMouseInput(int deltaX, int deltaY)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = deltaX;
            inputs[0].u.mi.dy = deltaY;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_MOVE_NOCOALESCING;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = IntPtr.Zero;

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private static bool IsExtendedKey(ushort vkCode)
        {
            // Common extended keys (add others if needed)
            return vkCode == (ushort)VirtualKey.VK_RCONTROL ||
                   vkCode == (ushort)VirtualKey.VK_RMENU || // Right Alt
                   vkCode == (ushort)VirtualKey.VK_INSERT ||
                   vkCode == (ushort)VirtualKey.VK_DELETE ||
                   vkCode == (ushort)VirtualKey.VK_HOME ||
                   vkCode == (ushort)VirtualKey.VK_END ||
                   vkCode == (ushort)VirtualKey.VK_PRIOR || // Page Up
                   vkCode == (ushort)VirtualKey.VK_NEXT || // Page Down
                   vkCode == (ushort)VirtualKey.VK_LEFT ||
                   vkCode == (ushort)VirtualKey.VK_UP ||
                   vkCode == (ushort)VirtualKey.VK_RIGHT ||
                   vkCode == (ushort)VirtualKey.VK_DOWN ||
                   vkCode == (ushort)VirtualKey.VK_NUMLOCK ||
                   vkCode == (ushort)VirtualKey.VK_SNAPSHOT || // Print Screen
                   vkCode == (ushort)VirtualKey.VK_CANCEL; // Break / Pause
                                                           // Note: Left/Right Win keys (VK_LWIN, VK_RWIN) are often handled specially
        }

        // Add VirtualKey enum here if it's only used by this class,
        // otherwise keep it in InputSimulator.cs or move to a shared location.
        // For now, assuming it might be used elsewhere, keep it in InputSimulator.cs
        // or move it to its own file like `VirtualKeys.cs`.

        #endregion
    }

    // Consider moving VirtualKey enum here or to its own file if not needed by InputSimulator.cs
    public enum VirtualKey : ushort // Changed to ushort to match KEYBDINPUT.wVk
    {
        VK_LBUTTON = 0x01,
        VK_RBUTTON = 0x02,
        VK_CANCEL = 0x03,
        VK_MBUTTON = 0x04,
        VK_BACK = 0x08,
        VK_TAB = 0x09,
        VK_CLEAR = 0x0C,
        VK_RETURN = 0x0D,
        VK_SHIFT = 0x10,
        VK_CONTROL = 0x11, // Left Control
        VK_MENU = 0x12,    // Left Alt
        VK_PAUSE = 0x13,
        VK_CAPITAL = 0x14, // Caps Lock
        VK_ESCAPE = 0x1B,
        VK_SPACE = 0x20,
        VK_PRIOR = 0x21,   // Page Up
        VK_NEXT = 0x22,    // Page Down
        VK_END = 0x23,
        VK_HOME = 0x24,
        VK_LEFT = 0x25,
        VK_UP = 0x26,
        VK_RIGHT = 0x27,
        VK_DOWN = 0x28,
        VK_SELECT = 0x29,
        VK_PRINT = 0x2A,
        VK_EXECUTE = 0x2B,
        VK_SNAPSHOT = 0x2C, // Print Screen
        VK_INSERT = 0x2D,
        VK_DELETE = 0x2E,
        VK_HELP = 0x2F,
        KEY_0 = 0x30,
        KEY_1 = 0x31,
        KEY_2 = 0x32,
        KEY_3 = 0x33,
        KEY_4 = 0x34,
        KEY_5 = 0x35,
        KEY_6 = 0x36,
        KEY_7 = 0x37,
        KEY_8 = 0x38,
        KEY_9 = 0x39,
        KEY_A = 0x41,
        KEY_B = 0x42,
        KEY_C = 0x43,
        KEY_D = 0x44,
        KEY_E = 0x45,
        KEY_F = 0x46,
        KEY_G = 0x47,
        KEY_H = 0x48,
        KEY_I = 0x49,
        KEY_J = 0x4A,
        KEY_K = 0x4B,
        KEY_L = 0x4C,
        KEY_M = 0x4D,
        KEY_N = 0x4E,
        KEY_O = 0x4F,
        KEY_P = 0x50,
        KEY_Q = 0x51,
        KEY_R = 0x52,
        KEY_S = 0x53,
        KEY_T = 0x54,
        KEY_U = 0x55,
        KEY_V = 0x56,
        KEY_W = 0x57,
        KEY_X = 0x58,
        KEY_Y = 0x59,
        KEY_Z = 0x5A,
        VK_LWIN = 0x5B,    // Left Windows key
        VK_RWIN = 0x5C,    // Right Windows key
        VK_APPS = 0x5D,    // Applications key
        VK_SLEEP = 0x5F,
        VK_NUMPAD0 = 0x60,
        VK_NUMPAD1 = 0x61,
        VK_NUMPAD2 = 0x62,
        VK_NUMPAD3 = 0x63,
        VK_NUMPAD4 = 0x64,
        VK_NUMPAD5 = 0x65,
        VK_NUMPAD6 = 0x66,
        VK_NUMPAD7 = 0x67,
        VK_NUMPAD8 = 0x68,
        VK_NUMPAD9 = 0x69,
        VK_MULTIPLY = 0x6A,
        VK_ADD = 0x6B,
        VK_SEPARATOR = 0x6C,
        VK_SUBTRACT = 0x6D,
        VK_DECIMAL = 0x6E,
        VK_DIVIDE = 0x6F,
        VK_F1 = 0x70,
        VK_F2 = 0x71,
        VK_F3 = 0x72,
        VK_F4 = 0x73,
        VK_F5 = 0x74,
        VK_F6 = 0x75,
        VK_F7 = 0x76,
        VK_F8 = 0x77,
        VK_F9 = 0x78,
        VK_F10 = 0x79,
        VK_F11 = 0x7A,
        VK_F12 = 0x7B,
        VK_NUMLOCK = 0x90,
        VK_SCROLL = 0x91,  // Scroll Lock
        VK_LSHIFT = 0xA0,
        VK_RSHIFT = 0xA1,
        VK_LCONTROL = 0xA2,
        VK_RCONTROL = 0xA3,
        VK_LMENU = 0xA4,   // Left Alt
        VK_RMENU = 0xA5,   // Right Alt
        VK_OEM_1 = 0xBA,      // ';:' for US
        VK_OEM_PLUS = 0xBB,   // '+' any country
        VK_OEM_COMMA = 0xBC,  // ',' any country
        VK_OEM_MINUS = 0xBD,  // '-' any country
        VK_OEM_PERIOD = 0xBE, // '.' any country
        VK_OEM_2 = 0xBF,      // '/?' for US
        VK_OEM_3 = 0xC0,      // '`~' for US
        VK_OEM_4 = 0xDB,      // '[{' for US
        VK_OEM_5 = 0xDC,      // '\|' for US
        VK_OEM_6 = 0xDD,      // ']}' for US
        VK_OEM_7 = 0xDE,      // ''"' for US
        VK_VOLUME_MUTE = 0xAD,
        VK_VOLUME_DOWN = 0xAE,
        VK_VOLUME_UP = 0xAF,
        // Add other keys as needed
    }
}
