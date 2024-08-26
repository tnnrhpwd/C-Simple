using System;
using System.Runtime.InteropServices;

namespace CSimple.Services
{
    public class RawInputHandler
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern int GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint uiSizeHeader);

        private const uint RIM_TYPEMOUSE = 0;
        private const uint RID_INPUT = 0x10000003;
        private const uint RAWINPUT_HEADER_SIZE = 40;

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public MOUSEINPUT mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public uint dwDevice;
            public uint dwReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int lLastX;
            public int lLastY;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsage;
            public ushort usUsagePage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        private IntPtr _hwnd;

        public RawInputHandler(IntPtr hwnd)
        {
            _hwnd = hwnd;
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic Desktop
            rid[0].usUsage = 0x02; // Mouse
            rid[0].dwFlags = 0; // Input from mouse
            rid[0].hwndTarget = hwnd;

            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE)));
        }

        public void ProcessRawInput(IntPtr lParam)
        {
            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, RAWINPUT_HEADER_SIZE);

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, buffer, ref size, RAWINPUT_HEADER_SIZE) != size)
                {
                    return;
                }

                RAWINPUT rawInput = (RAWINPUT)Marshal.PtrToStructure(buffer, typeof(RAWINPUT));
                if (rawInput.header.dwType == RIM_TYPEMOUSE)
                {
                    // Process mouse input
                    Console.WriteLine($"Mouse Input - X: {rawInput.mouse.lLastX}, Y: {rawInput.mouse.lLastY}");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
