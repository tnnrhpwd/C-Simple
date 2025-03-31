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
        // Standard key codes
        VK_LBUTTON = 0x01,      // Left mouse button
        VK_RBUTTON = 0x02,      // Right mouse button
        VK_CANCEL = 0x03,       // Control-break processing
        VK_MBUTTON = 0x04,      // Middle mouse button
        VK_BACK = 0x08,         // BACKSPACE key
        VK_TAB = 0x09,          // TAB key
        VK_CLEAR = 0x0C,        // CLEAR key
        VK_RETURN = 0x0D,       // ENTER key
        VK_SHIFT = 0x10,        // SHIFT key
        VK_CONTROL = 0x11,      // CTRL key
        VK_MENU = 0x12,         // ALT key
        VK_PAUSE = 0x13,        // PAUSE key
        VK_CAPITAL = 0x14,      // CAPS LOCK key
        VK_ESCAPE = 0x1B,       // ESC key
        VK_SPACE = 0x20,        // SPACEBAR
        VK_PRIOR = 0x21,        // PAGE UP key
        VK_NEXT = 0x22,         // PAGE DOWN key
        VK_END = 0x23,          // END key
        VK_HOME = 0x24,         // HOME key
        VK_LEFT = 0x25,         // LEFT ARROW key
        VK_UP = 0x26,           // UP ARROW key
        VK_RIGHT = 0x27,        // RIGHT ARROW key
        VK_DOWN = 0x28,         // DOWN ARROW key
        VK_SELECT = 0x29,       // SELECT key
        VK_PRINT = 0x2A,        // PRINT key
        VK_EXECUTE = 0x2B,      // EXECUTE key
        VK_SNAPSHOT = 0x2C,     // PRINT SCREEN key
        VK_INSERT = 0x2D,       // INS key
        VK_DELETE = 0x2E,       // DEL key
        VK_HELP = 0x2F,         // HELP key
        // Number keys
        KEY_0 = 0x30,           // 0 key
        KEY_1 = 0x31,           // 1 key
        KEY_2 = 0x32,           // 2 key
        KEY_3 = 0x33,           // 3 key
        KEY_4 = 0x34,           // 4 key
        KEY_5 = 0x35,           // 5 key
        KEY_6 = 0x36,           // 6 key
        KEY_7 = 0x37,           // 7 key
        KEY_8 = 0x38,           // 8 key
        KEY_9 = 0x39,           // 9 key
        // Letter keys
        KEY_A = 0x41,           // A key
        KEY_B = 0x42,           // B key
        KEY_C = 0x43,           // C key
        KEY_D = 0x44,           // D key
        KEY_E = 0x45,           // E key
        KEY_F = 0x46,           // F key
        KEY_G = 0x47,           // G key
        KEY_H = 0x48,           // H key
        KEY_I = 0x49,           // I key
        KEY_J = 0x4A,           // J key
        KEY_K = 0x4B,           // K key
        KEY_L = 0x4C,           // L key
        KEY_M = 0x4D,           // M key
        KEY_N = 0x4E,           // N key
        KEY_O = 0x4F,           // O key
        KEY_P = 0x50,           // P key
        KEY_Q = 0x51,           // Q key
        KEY_R = 0x52,           // R key
        KEY_S = 0x53,           // S key
        KEY_T = 0x54,           // T key
        KEY_U = 0x55,           // U key
        KEY_V = 0x56,           // V key
        KEY_W = 0x57,           // W key
        KEY_X = 0x58,           // X key
        KEY_Y = 0x59,           // Y key
        KEY_Z = 0x5A,           // Z key
        // Function keys
        VK_F1 = 0x70,           // F1 key
        VK_F2 = 0x71,           // F2 key
        VK_F3 = 0x72,           // F3 key
        VK_F4 = 0x73,           // F4 key
        VK_F5 = 0x74,           // F5 key
        VK_F6 = 0x75,           // F6 key
        VK_F7 = 0x76,           // F7 key
        VK_F8 = 0x77,           // F8 key
        VK_F9 = 0x78,           // F9 key
        VK_F10 = 0x79,          // F10 key
        VK_F11 = 0x7A,          // F11 key
        VK_F12 = 0x7B,          // F12 key
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

        // Constants for SendInput
        private const int INPUT_MOUSE = 0;
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        // Structures for SendInput
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
        /// <summary>
        /// Moves the mouse to absolute screen coordinates using low-level input
        /// </summary>
        public static void MoveMouse(int x, int y)
        {
            // Create INPUT structure
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;

            // Get screen dimensions for absolute positioning
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Convert to normalized coordinates (0-65535)
            inputs[0].u.mi.dx = (x * 65535) / screenWidth;
            inputs[0].u.mi.dy = (y * 65535) / screenHeight;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo();

            // Send input
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Smooth mouse movement with interpolation (best for games)
        /// </summary>
        public static async Task SmoothMouseMove(int startX, int startY, int endX, int endY, int steps = 20, int delayMs = 2)
        {
            // If starting position is not specified, get current position
            if (startX < 0 || startY < 0)
            {
                GetCursorPos(out POINT currentPos);
                startX = currentPos.X;
                startY = currentPos.Y;
            }

            for (int i = 1; i <= steps; i++)
            {
                // Calculate intermediate position
                float t = (float)i / steps;
                int x = (int)(startX + (endX - startX) * t);
                int y = (int)(startY + (endY - startY) * t);

                // Move to the intermediate position
                MoveMouse(x, y);

                // Small delay for smoothness
                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }

            // Ensure final position
            MoveMouse(endX, endY);
        }

        /// <summary>
        /// Simulates a mouse click at the specified coordinates
        /// </summary>
        public static void SimulateMouseClick(MouseButton button, int x, int y)
        {
            // First move the mouse to the position
            MoveMouse(x, y);

            // Then perform the click
            INPUT[] inputs = new INPUT[2];

            // Set up for button down
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo();

            // Set up for button up
            inputs[1].type = INPUT_MOUSE;
            inputs[1].u.mi.dx = 0;
            inputs[1].u.mi.dy = 0;
            inputs[1].u.mi.mouseData = 0;
            inputs[1].u.mi.time = 0;
            inputs[1].u.mi.dwExtraInfo = GetMessageExtraInfo();

            // Set specific button flags
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

            // Send inputs
            SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        /// <summary>
        /// Sends a mouse down or up event at the current mouse position
        /// </summary>
        public static void SimulateMouseEvent(MouseButton button, bool isUp)
        {
            // Get current mouse position
            GetCursorPos(out POINT currentPos);

            // Create input structure
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = GetMessageExtraInfo();

            // Set specific button flags
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

            // Send input
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }
        #endregion

        #region Keyboard Input Methods
        /// <summary>
        /// Simulates pressing a key down
        /// </summary>
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

        /// <summary>
        /// Simulates releasing a key
        /// </summary>
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

        /// <summary>
        /// Simulates pressing and releasing a key
        /// </summary>
        public static void SimulateKeyPress(VirtualKey key)
        {
            SimulateKeyDown(key);
            SimulateKeyUp(key);
        }
        #endregion

        #region Advanced Game Input Methods
        // Game-specific settings
        private static bool _gameEnhancedMode = true;
        private static int _gameMouseSensitivity = 50; // Reduced from 100 to 50 (slower by default)
        private static int _gameMovementSteps = 40; // Increase steps for smoother movement
        private static int _gameMovementDelay = 8; // Longer delay between movements

        /// <summary>
        /// Sets enhanced mode for games
        /// </summary>
        public static void SetGameEnhancedMode(bool enabled, int mouseSensitivity = 50)
        {
            _gameEnhancedMode = enabled;
            _gameMouseSensitivity = Math.Clamp(mouseSensitivity, 1, 200);
        }

        /// <summary>
        /// Game-optimized mouse movement (accounts for in-game sensitivity)
        /// </summary>
        public static async Task GameMouseMove(int deltaX, int deltaY)
        {
            // Use the class fields for steps and delay
            await GameMouseMove(deltaX, deltaY, _gameMovementSteps, _gameMovementDelay);
        }

        /// <summary>
        /// Game-optimized mouse movement with custom steps and delay
        /// </summary>
        public static async Task GameMouseMove(int deltaX, int deltaY, int steps, int delayMs)
        {
            // Get current cursor position
            GetCursorPos(out POINT startPos);

            // Apply sensitivity adjustment - lower value means slower camera movement
            float sensitivityFactor = _gameMouseSensitivity / 100f;
            deltaX = (int)(deltaX * sensitivityFactor);
            deltaY = (int)(deltaY * sensitivityFactor);

            // Calculate target position
            int targetX = startPos.X + deltaX;
            int targetY = startPos.Y + deltaY;

            // Use smooth movement with custom easing for better game feel
            await SmoothGameMouseMove(startPos.X, startPos.Y, targetX, targetY, steps, delayMs);
        }

        /// <summary>
        /// Special smooth movement specifically for game camera control
        /// </summary>
        private static async Task SmoothGameMouseMove(int startX, int startY, int endX, int endY, int steps = 40, int delayMs = 8)
        {
            for (int i = 1; i <= steps; i++)
            {
                // Calculate intermediate position with custom easing for games
                float t = (float)i / steps;

                // Apply ease-out cubic function for deceleration (better for cameras)
                // Fix: Explicitly cast Math.Pow result to float
                t = 1 - (float)Math.Pow(1 - t, 3);

                int x = (int)(startX + (endX - startX) * t);
                int y = (int)(startY + (endY - startY) * t);

                // Move to the intermediate position (use direct Windows API for games)
                SetCursorPos(x, y);

                // Small delay between moves for smoothness
                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }
        }

        // Add this P/Invoke for direct cursor positioning (better for games)
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        #endregion
    }
}
