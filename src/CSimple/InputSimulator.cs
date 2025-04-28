using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CSimple
{
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }

    public enum VirtualKey
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
        VK_CONTROL = 0x11,
        VK_MENU = 0x12,
        VK_PAUSE = 0x13,
        VK_CAPITAL = 0x14,
        VK_ESCAPE = 0x1B,
        VK_SPACE = 0x20,
        VK_PRIOR = 0x21,
        VK_NEXT = 0x22,
        VK_END = 0x23,
        VK_HOME = 0x24,
        VK_LEFT = 0x25,
        VK_UP = 0x26,
        VK_RIGHT = 0x27,
        VK_DOWN = 0x28,
        VK_SELECT = 0x29,
        VK_PRINT = 0x2A,
        VK_EXECUTE = 0x2B,
        VK_SNAPSHOT = 0x2C,
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
    }

    public static class InputSimulator
    {
        #region P/Invoke declarations and structs
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetMessageExtraInfo();

        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
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
        private struct HARDWAREINPUT
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
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public INPUT_UNION u;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        #endregion

        #region Mouse Input Methods
        public static void MoveMouse(int x, int y)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;

            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            inputs[0].u.mi.dx = (x * 65535) / screenWidth;
            inputs[0].u.mi.dy = (y * 65535) / screenHeight;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo();

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static async Task SmoothMouseMove(int startX, int startY, int endX, int endY, int steps = 20, int delayMs = 2)
        {
            if (startX < 0 || startY < 0)
            {
                GetCursorPos(out POINT currentPos);
                startX = currentPos.X;
                startY = currentPos.Y;
            }

            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            int adaptiveSteps = distance < 100 ? steps : (int)(steps * Math.Sqrt(distance / 100.0));
            adaptiveSteps = Math.Min(adaptiveSteps, 100);

            Random random = new Random();
            int randomOffsetX = random.Next(-10, 10);
            int randomOffsetY = random.Next(-10, 10);

            int controlX = (startX + endX) / 2 + randomOffsetX;
            int controlY = (startY + endY) / 2 + randomOffsetY;

            int prevX = startX;
            int prevY = startY;

            for (int i = 1; i <= adaptiveSteps; i++)
            {
                float t = (float)i / adaptiveSteps;

                float easedT = EaseHumanMove(t);

                float u = 1.0f - easedT;
                float tt = easedT * easedT;
                float uu = u * u;

                int x = (int)(uu * startX + 2 * u * easedT * controlX + tt * endX);
                int y = (int)(uu * startY + 2 * u * easedT * controlY + tt * endY);

                float shakeFactor = 4.0f * easedT * (1.0f - easedT);
                if (distance > 20)
                {
                    x += (int)(random.Next(-2, 3) * shakeFactor);
                    y += (int)(random.Next(-2, 3) * shakeFactor);
                }

                MoveMouse(x, y);

                double segmentDistance = Math.Sqrt(Math.Pow(x - prevX, 2) + Math.Pow(y - prevY, 2));
                prevX = x;
                prevY = y;

                int actualDelay = delayMs;
                if (i < adaptiveSteps * 0.2 || i > adaptiveSteps * 0.8)
                {
                    actualDelay = (int)(delayMs * 1.5);
                }
                else if (segmentDistance > 10)
                {
                    actualDelay = (int)(delayMs * 1.3);
                }

                if (actualDelay > 0)
                    await Task.Delay(actualDelay);
            }

            MoveMouse(endX, endY);
        }

        private static float EaseHumanMove(float t)
        {
            const float power = 2.5f;
            const float overshoot = 0.03f;

            if (t < 0.5f)
            {
                return 0.5f * (float)Math.Pow(t * 2, power);
            }
            else
            {
                float decel = 0.5f + 0.5f * (1 - (float)Math.Pow(2 - t * 2, power));

                if (t > 0.8f && t < 0.95f)
                    decel += overshoot * (t - 0.8f) * (0.95f - t) * 10;

                return decel;
            }
        }

        public static void SimulateMouseClick(MouseButton button, int x, int y)
        {
            MoveMouse(x, y);

            INPUT[] inputs = new INPUT[2];

            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo();

            inputs[1].type = INPUT_MOUSE;
            inputs[1].u.mi.dx = 0;
            inputs[1].u.mi.dy = 0;
            inputs[1].u.mi.mouseData = 0;
            inputs[1].u.mi.time = 0;
            inputs[1].u.mi.dwExtraInfo = GetMessageExtraInfo();

            switch (button)
            {
                case MouseButton.Left:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_LEFTDOWN;
                    inputs[1].u.mi.dwFlags = MOUSEEVENTF_LEFTUP;
                    break;
                case MouseButton.Right:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_RIGHTDOWN;
                    inputs[1].u.mi.dwFlags = MOUSEEVENTF_RIGHTUP;
                    break;
                case MouseButton.Middle:
                    inputs[0].u.mi.dwFlags = MOUSEEVENTF_MIDDLEDOWN;
                    inputs[1].u.mi.dwFlags = MOUSEEVENTF_MIDDLEUP;
                    break;
            }

            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SimulateMouseEvent(MouseButton button, bool isUp)
        {
            GetCursorPos(out POINT currentPos);

            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo();

            switch (button)
            {
                case MouseButton.Left:
                    inputs[0].u.mi.dwFlags = isUp ? (uint)MOUSEEVENTF_LEFTUP : (uint)MOUSEEVENTF_LEFTDOWN;
                    break;
                case MouseButton.Right:
                    inputs[0].u.mi.dwFlags = isUp ? (uint)MOUSEEVENTF_RIGHTUP : (uint)MOUSEEVENTF_RIGHTDOWN;
                    break;
                case MouseButton.Middle:
                    inputs[0].u.mi.dwFlags = isUp ? (uint)MOUSEEVENTF_MIDDLEUP : (uint)MOUSEEVENTF_MIDDLEDOWN;
                    break;
            }

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
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
        #endregion

        #region Keyboard Input Methods
        public static void SimulateKeyDown(VirtualKey key)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)key;
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = KEYEVENTF_KEYDOWN;
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = GetMessageExtraInfo();

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SimulateKeyUp(VirtualKey key)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)key;
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = KEYEVENTF_KEYUP;
            inputs[0].u.ki.time = 0;
            inputs[0].u.ki.dwExtraInfo = GetMessageExtraInfo();

            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SimulateKeyPress(VirtualKey key)
        {
            SimulateKeyDown(key);
            SimulateKeyUp(key);
        }
        #endregion

        #region Advanced Game Input Methods
        private static bool _gameEnhancedMode = true;
        private static int _gameMouseSensitivity = 50;
        private static int _gameMovementSteps = 40;
        private static int _gameMovementDelay = 8;

        public static void SetGameEnhancedMode(bool enabled, int mouseSensitivity = 50)
        {
            _gameEnhancedMode = enabled;
            _gameMouseSensitivity = Math.Clamp(mouseSensitivity, 1, 200);
        }

        public static async Task GameMouseMove(int deltaX, int deltaY)
        {
            await GameMouseMove(deltaX, deltaY, _gameMovementSteps, _gameMovementDelay);
        }

        public static async Task GameMouseMove(int deltaX, int deltaY, int steps, int delayMs)
        {
            GetCursorPos(out POINT startPos);

            float sensitivityFactor = _gameMouseSensitivity / 100f;
            deltaX = (int)(deltaX * sensitivityFactor);
            deltaY = (int)(deltaY * sensitivityFactor);

            int targetX = startPos.X + deltaX;
            int targetY = startPos.Y + deltaY;

            await SmoothGameMouseMove(startPos.X, startPos.Y, targetX, targetY, steps, delayMs);
        }

        private static async Task SmoothGameMouseMove(int startX, int startY, int endX, int endY, int steps = 40, int delayMs = 8)
        {
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            Random random = new Random();

            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;

                float easedT = 1 - (float)Math.Pow(1 - t, 3);

                int x = (int)(startX + (endX - startX) * easedT);
                int y = (int)(startY + (endY - startY) * easedT);

                if (i > 1 && i < steps - 1 && distance > 50)
                {
                    x += random.Next(-1, 2);
                    y += random.Next(-1, 2);
                }

                SetCursorPos(x, y);

                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }

            SetCursorPos(endX, endY);
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        #endregion

        public static async Task MoveDirectlyAsync(int startX, int startY, int endX, int endY, int steps = 20)
        {
            int stepX = (endX - startX) / steps;
            int stepY = (endY - startY) / steps;

            for (int i = 0; i < steps; i++)
            {
                MoveMouse(startX + stepX * i, startY + stepY * i);
                await Task.Delay(10);
            }

            MoveMouse(endX, endY);
        }

        public static bool BringWindowToForeground(IntPtr hWnd)
        {
            if (IsIconic(hWnd))
            {
                ShowWindow(hWnd, SW_RESTORE);
            }

            return SetForegroundWindow(hWnd);
        }

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;
    }
}
