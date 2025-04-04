using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class InputSimulator
    {
        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, [In] ref INPUT pInputs, int cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        private const int INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        // New static properties for game-enhanced mode
        private static bool _gameEnhancedMode = false;
        private static int _mouseSensitivity = 100; // 1-200%

        // Static properties for state tracking to prevent conflicts
        private static DateTime _lastMoveTime = DateTime.MinValue;
        private static POINT _lastPosition;
        private static readonly object _moveLock = new object();

        public static void SetGameEnhancedMode(bool enabled, int sensitivity)
        {
            _gameEnhancedMode = enabled;
            _mouseSensitivity = Math.Clamp(sensitivity, 1, 200);
        }

        public static void SimulateMouseClick(MouseButton button, int x, int y)
        {
            Console.WriteLine($"Simulating Mouse Click at X: {x}, Y: {y}");

            // Set cursor position
            SetCursorPos(x, y);

            // Create input structure
            INPUT mouseDownInput = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = 0,
                        dwFlags = button == MouseButton.Left ? MOUSEEVENTF_LEFTDOWN : MOUSEEVENTF_RIGHTDOWN,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            INPUT mouseUpInput = new INPUT
            {
                type = INPUT_MOUSE,
                U = new InputUnion
                {
                    mi = new MOUSEINPUT
                    {
                        dx = 0,
                        dy = 0,
                        mouseData = 0,
                        dwFlags = button == MouseButton.Left ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_RIGHTUP,
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            // Simulate mouse click
            uint resultDown = SendInput(1, ref mouseDownInput, Marshal.SizeOf(typeof(INPUT)));
            uint resultUp = SendInput(1, ref mouseUpInput, Marshal.SizeOf(typeof(INPUT)));

            Console.WriteLine($"SendInput results - Down: {resultDown}, Up: {resultUp}");
        }

        public static void SimulateKeyDown(VirtualKey key)
        {
            INPUT input = new INPUT
            {
                type = 1, // Input type: Keyboard
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0, // Hardware scan code for key
                        dwFlags = 0, // 0 for key press
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            // Simulate key down
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void SimulateKeyUp(VirtualKey key)
        {
            INPUT input = new INPUT
            {
                type = 1, // Input type: Keyboard
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0, // Hardware scan code for key
                        dwFlags = 2, // 2 for key up
                        time = 0,
                        dwExtraInfo = UIntPtr.Zero
                    }
                }
            };

            // Simulate key up
            SendInput(1, ref input, Marshal.SizeOf(typeof(INPUT)));
        }

        public static void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
        }

        // Enhanced smooth movement that prevents jumping
        public static async Task MoveSmoothlyAsync(int startX, int startY, int endX, int endY, int steps = 40, int delayMs = 1)
        {
            // Synchronize movements to prevent conflicts
            lock (_moveLock)
            {
                // If there was a very recent move, ensure we're using the correct start position
                if ((DateTime.Now - _lastMoveTime).TotalMilliseconds < 100)
                {
                    startX = _lastPosition.X;
                    startY = _lastPosition.Y;
                }
                else
                {
                    // Get current mouse position to ensure we start from the right spot
                    GetCursorPos(out _lastPosition);
                    startX = _lastPosition.X;
                    startY = _lastPosition.Y;
                }
            }

            // Apply sensitivity adjustment if needed
            if (_gameEnhancedMode)
            {
                float factor = _mouseSensitivity / 100f;
                int deltaX = endX - startX;
                int deltaY = endY - startY;

                deltaX = (int)(deltaX * factor);
                deltaY = (int)(deltaY * factor);

                endX = startX + deltaX;
                endY = startY + deltaY;
            }

            // Calculate distance for proper step count
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            // Optimize steps based on distance
            if (distance < 10) steps = 3;  // Very few steps for tiny movements
            else if (distance < 50) steps = Math.Max(5, steps / 4);
            else if (distance > 500) steps = Math.Min(100, steps * 2);

            // Define control points for perfect cubic bezier curve
            int control1X = startX + (endX - startX) / 3;
            int control1Y = startY + (endY - startY) / 3;
            int control2X = startX + 2 * (endX - startX) / 3;
            int control2Y = startY + 2 * (endY - startY) / 3;

            // Track last position to avoid duplicates
            int lastX = startX;
            int lastY = startY;

            for (int i = 1; i <= steps; i++)
            {
                // Calculate progress
                float t = (float)i / steps;

                // Sine-based easing - perfect smoothness
                float easedT = (float)(Math.Sin(Math.PI * (t - 0.5)) / 2 + 0.5);

                // Calculate bezier curve position
                float u = 1.0f - easedT;
                float u2 = u * u;
                float u3 = u2 * u;
                float t2 = easedT * easedT;
                float t3 = t2 * easedT;

                int x = (int)Math.Round(u3 * startX +
                                  3 * u2 * easedT * control1X +
                                  3 * u * t2 * control2X +
                                  t3 * endX);

                int y = (int)Math.Round(u3 * startY +
                                  3 * u2 * easedT * control1Y +
                                  3 * u * t2 * control2Y +
                                  t3 * endY);

                // Only move if position changed
                if (x != lastX || y != lastY)
                {
                    SetCursorPos(x, y);
                    lastX = x;
                    lastY = y;

                    // Update last position for synchronization
                    lock (_moveLock)
                    {
                        _lastPosition.X = x;
                        _lastPosition.Y = y;
                        _lastMoveTime = DateTime.Now;
                    }
                }

                // Use minimal delay for smoother motion
                if (i < steps && delayMs > 0)
                    await Task.Delay(delayMs);
            }

            // Always ensure final position is exact
            SetCursorPos(endX, endY);

            // Update last position for synchronization
            lock (_moveLock)
            {
                _lastPosition.X = endX;
                _lastPosition.Y = endY;
                _lastMoveTime = DateTime.Now;
            }
        }

        // Direct movement with no curves - useful for straight line movements
        public static async Task MoveDirectlyAsync(int startX, int startY, int endX, int endY, int steps = 20)
        {
            // Synchronize with other movement methods
            lock (_moveLock)
            {
                if ((DateTime.Now - _lastMoveTime).TotalMilliseconds < 100)
                {
                    startX = _lastPosition.X;
                    startY = _lastPosition.Y;
                }
                else
                {
                    // Get current position to ensure accuracy
                    GetCursorPos(out _lastPosition);
                    startX = _lastPosition.X;
                    startY = _lastPosition.Y;
                }
            }

            // Calculate distance and optimize steps
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            // For very short movements, use fewer steps
            if (distance < 10) steps = 2;
            else if (distance < 50) steps = 5;
            else if (distance < 100) steps = 10;

            int lastX = startX;
            int lastY = startY;

            for (int i = 1; i <= steps; i++)
            {
                // Linear interpolation (no easing)
                float t = (float)i / steps;

                // Calculate position with linear interpolation
                int x = (int)Math.Round(startX + (endX - startX) * t);
                int y = (int)Math.Round(startY + (endY - startY) * t);

                // Only move if position changed
                if (x != lastX || y != lastY)
                {
                    SetCursorPos(x, y);
                    lastX = x;
                    lastY = y;

                    // Update tracking info
                    lock (_moveLock)
                    {
                        _lastPosition.X = x;
                        _lastPosition.Y = y;
                        _lastMoveTime = DateTime.Now;
                    }
                }

                // Minimal delay for smoothness
                if (i < steps)
                    await Task.Delay(1);
            }

            // Ensure final position is reached
            SetCursorPos(endX, endY);

            lock (_moveLock)
            {
                _lastPosition.X = endX;
                _lastPosition.Y = endY;
                _lastMoveTime = DateTime.Now;
            }
        }

        public static bool BringWindowToForeground(IntPtr hWnd)
        {
            // Check if the window is already the foreground window
            if (GetForegroundWindow() == hWnd)
                return true;

            // Attempt to bring the window to the foreground
            return SetForegroundWindow(hWnd);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    public enum MouseButton
    {
        Left,
        Right
    }
}
