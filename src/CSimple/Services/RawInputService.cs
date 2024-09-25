#if WINDOWS
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CSimple.Services
{
    public class RawInputService : IDisposable
    {
        private const int WM_INPUT = 0x00FF;
        private const uint RIM_TYPEMOUSE = 0;
        private IntPtr _hwnd;
        private RawInputNativeWindow _nativeWindow;

        [DllImport("user32.dll")]
        private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll")]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;
            public ushort usButtonData;
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        // Events for both mouse movement and buttons
        public event Action<int, int> MouseMoved;   // For relative mouse movement (3D controls)
        public event Action<string> ButtonDown; // For mouse button presses

        public RawInputService(Microsoft.UI.Xaml.Window window)
        {
            _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            RegisterRawInput();

            _nativeWindow = new RawInputNativeWindow();
            _nativeWindow.MessageReceived += OnMessageReceived;
            _nativeWindow.AssignHandle(_hwnd);
        }

        private void RegisterRawInput()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01; // Generic desktop controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = 0;        // No flags for now, could adjust
            rid[0].hwndTarget = _hwnd;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
            {
                throw new Exception("Failed to register raw input device(s).");
            }
        }

        private void OnMessageReceived(object sender, Message m)
        {
            if (m.Msg == WM_INPUT)
            {
                uint dwSize = 0;
                GetRawInputData(m.LParam, 0x10000003, IntPtr.Zero, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                IntPtr buffer = Marshal.AllocHGlobal((int)dwSize);
                try
                {
                    if (GetRawInputData(m.LParam, 0x10000003, buffer, ref dwSize, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) != dwSize)
                        return;

                    RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                    if (raw.header.dwType == RIM_TYPEMOUSE)
                    {
                        int deltaX = raw.mouse.lLastX; // Relative movement in X
                        int deltaY = raw.mouse.lLastY; // Relative movement in Y

                        // Trigger mouse movement event
                        MouseMoved?.Invoke(deltaX, deltaY);

                    // Track mouse button states and assign descriptive strings
                    if (raw.mouse.usButtonFlags != 0)
                    {
                        string buttonState = "";

                        if ((raw.mouse.usButtonFlags & 0x0001) != 0)
                        {
                            buttonState = "Left button down";
                        }
                        else if ((raw.mouse.usButtonFlags & 0x0002) != 0)
                        {
                            buttonState = "Left button up";
                        }
                        else if ((raw.mouse.usButtonFlags & 0x0004) != 0)
                        {
                            buttonState = "Right button down";
                        }
                        else if ((raw.mouse.usButtonFlags & 0x0008) != 0)
                        {
                            buttonState = "Right button up";
                        }
                        else if ((raw.mouse.usButtonFlags & 0x0010) != 0)
                        {
                            buttonState = "Middle button down";
                        }
                        else if ((raw.mouse.usButtonFlags & 0x0020) != 0)
                        {
                            buttonState = "Middle button up";
                        }

                        // Invoke the event with the descriptive string
                        ButtonDown?.Invoke(buttonState);
                    }

                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        private const uint RIDEV_REMOVE = 0x00000001;
        private void UnregisterRawInput()
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[1];
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02;
            rid[0].dwFlags = RIDEV_REMOVE;
            rid[0].hwndTarget = _hwnd;

            RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0]));
        }

        public void Dispose()
        {
            UnregisterRawInput();
        }

        public class RawInputNativeWindow : NativeWindow
        {
            public event EventHandler<Message> MessageReceived;

            protected override void WndProc(ref Message m)
            {
                MessageReceived?.Invoke(this, m);
                base.WndProc(ref m);
            }
        }
    }
}
#endif
