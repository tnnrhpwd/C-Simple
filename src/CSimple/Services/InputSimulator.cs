using System;
using System.Runtime.InteropServices;

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
        private const int INPUT_MOUSE = 0;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

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
