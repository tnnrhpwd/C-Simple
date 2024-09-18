using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

#if WINDOWS
using Microsoft.Maui.Dispatching;
#endif

namespace CSimple.Services
{
    public class MouseTrackingService
    {
        // Constants for raw input
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RID_INPUT = 0x10000003;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        // Raw input structures
        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        private bool _isTracking;
        private Point _lastPosition;

        public List<Point> MouseMovements { get; private set; } = new List<Point>();

        public event Action<Point> MouseMoved;

        public MouseTrackingService()
        {
            _lastPosition = new Point(0, 0);
        }

        public void StartTracking(IntPtr hwnd)
        {
            _isTracking = true;
            RegisterForRawInput(hwnd);

            // Start tracking loop
            _ = TrackMouseMovementAsync();
        }

        public void StopTracking()
        {
            _isTracking = false;
        }

        // Register the application to receive raw mouse input
        private void RegisterForRawInput(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];

            // Register for mouse input (UsagePage = 1, Usage = 2)
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = 0;        // No flags for relative movement
            rid[0].hwndTarget = hwnd;  // Handle of the window to receive the input

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new Exception("Failed to register raw input devices.");
            }
        }

        // Capture the raw input and process mouse movement
        public void ProcessRawInput(IntPtr lParam)
        {
            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            IntPtr rawData = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, rawData, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == size)
                {
                    RAWINPUT rawInput = Marshal.PtrToStructure<RAWINPUT>(rawData);
                    if (rawInput.header.dwType == RIM_TYPEMOUSE)
                    {
                        // Capture relative movement
                        int deltaX = rawInput.mouse.lLastX;
                        int deltaY = rawInput.mouse.lLastY;

                        // Call the event with the new mouse movement
                        var delta = new Point(deltaX, deltaY);
                        MouseMovements.Add(delta);
                        MouseMoved?.Invoke(delta);

                        // Log for testing
                        Console.WriteLine($"Mouse moved: X = {deltaX}, Y = {deltaY}");
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rawData);
            }
        }

        // This would be your main loop to track mouse inputs in case no raw input message is received
        private async Task TrackMouseMovementAsync()
        {
            while (_isTracking)
            {
                await Task.Delay(16); // approximately 60 frames per second
            }
        }
    }

#if WINDOWS
    public static class WindowsMouseHelper
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static POINT GetMousePosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }
    }
#endif
}
