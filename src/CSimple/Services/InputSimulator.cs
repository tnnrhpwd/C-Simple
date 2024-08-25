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

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

        public static void SimulateMouseClick(MouseButton button)
        {
            switch (button)
            {
                case MouseButton.Left:
                    mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
                    break;
                case MouseButton.Right:
                    mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
                    break;
            }
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetCursorPos(int X, int Y);
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
        public ushort wVk;           // Virtual key code
        public ushort wScan;         // Hardware scan code
        public uint dwFlags;         // Flags
        public uint time;            // Timestamp
        public UIntPtr dwExtraInfo;  // Additional info
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

    public enum MouseButton
    {
        Left,
        Right
    }
}
