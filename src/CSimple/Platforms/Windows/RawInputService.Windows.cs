#if WINDOWS
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace YourNamespace.Services
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

        public event Action<int, int> MouseMoved;

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
            rid[0].usUsagePage = 0x01;
            rid[0].usUsage = 0x02;
            rid[0].dwFlags = 0;
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
                        int x = raw.mouse.lLastX;
                        int y = raw.mouse.lLastY;
                        MouseMoved?.Invoke(x, y);
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

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(rid[0])))
            {
                // Handle error
            }
        }
        public void Dispose()
        {
            // Unregister raw input devices if needed
            UnregisterRawInput();

            // Dispose the native window if necessary
            // _nativeWindow?.Dispose();
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
