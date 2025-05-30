#if WINDOWS
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CSimple.Services
{
    public class RawInputService : IDisposable
    {
        private const int WM_INPUT = 0x00FF;
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RIM_TYPEKEYBOARD = 1;
        private const uint RIM_TYPEHID = 2; // HID devices including touchpads and touch screens
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

        // Constants for mouse button flags
        private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;

        // Events for both mouse movement and buttons
        public event Action<int, int> MouseMoved;   // For relative mouse movement (3D controls)
        public event Action<string> ButtonDown; // For mouse button presses
        public event Action<string> ButtonUp;   // Added for mouse button releases
        public event Action<TouchData> TouchEvent; // For touch events
        public event Action<TrackpadData> TrackpadEvent; // For trackpad events
        public event Action<MouseButtonData> MouseButtonEvent; // New event for detailed mouse button state

        // Touch data structure
        public class TouchData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public uint TouchId { get; set; }
            public TouchAction Action { get; set; }
        }

        public enum TouchAction
        {
            Down,
            Move,
            Up,
            Cancel
        }

        // New mouse button data structure
        public class MouseButtonData
        {
            public int X { get; set; }
            public int Y { get; set; }
            public bool IsLeftDown { get; set; }
            public bool IsRightDown { get; set; }
            public bool IsMiddleDown { get; set; }
            public MouseButtonAction Action { get; set; }
        }

        public enum MouseButtonAction
        {
            None,
            LeftDown,
            LeftUp,
            RightDown,
            RightUp,
            MiddleDown,
            MiddleUp
        }

        // Trackpad data structure
        public class TrackpadData
        {
            public int DeltaX { get; set; }
            public int DeltaY { get; set; }
            public bool IsMultiTouch { get; set; }
            public int Fingers { get; set; } // Number of fingers detected
        }

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
            // Resize array to accommodate more device types
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[3]; // Changed from 1 to 3

            // Mouse
            rid[0].usUsagePage = 0x01;     // Generic Desktop Controls
            rid[0].usUsage = 0x02;         // Mouse
            rid[0].dwFlags = 0;            // No flags for now, could adjust
            rid[0].hwndTarget = _hwnd;

            // Touch Screen
            rid[1].usUsagePage = 0x0D;     // Digitizer
            rid[1].usUsage = 0x04;         // Touch screen
            rid[1].dwFlags = 0;            // No flags
            rid[1].hwndTarget = _hwnd;

            // Trackpad/Touchpad
            rid[2].usUsagePage = 0x0D;     // Digitizer
            rid[2].usUsage = 0x05;         // Touch pad
            rid[2].dwFlags = 0;            // No flags
            rid[2].hwndTarget = _hwnd;

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
                        MouseMoved?.Invoke(deltaX, deltaY);

                        if (raw.mouse.usButtonFlags != 0)
                        {
                            string buttonState = "";

                            if ((raw.mouse.usButtonFlags & RI_MOUSE_LEFT_BUTTON_DOWN) != 0)
                            {
                                buttonState = "Left button down";
                                ButtonDown?.Invoke(buttonState);
                                MouseButtonEvent?.Invoke(new MouseButtonData
                                {
                                    X = raw.mouse.lLastX,
                                    Y = raw.mouse.lLastY,
                                    IsLeftDown = true,
                                    Action = MouseButtonAction.LeftDown
                                });
                            }
                            else if ((raw.mouse.usButtonFlags & RI_MOUSE_LEFT_BUTTON_UP) != 0)
                            {
                                buttonState = "Left button up";
                                ButtonUp?.Invoke(buttonState);
                                MouseButtonEvent?.Invoke(new MouseButtonData
                                {
                                    X = raw.mouse.lLastX,
                                    Y = raw.mouse.lLastY,
                                    IsLeftDown = false,
                                    Action = MouseButtonAction.LeftUp
                                });
                            }
                            else if ((raw.mouse.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_DOWN) != 0)
                            {
                                buttonState = "Right button down";
                                ButtonDown?.Invoke(buttonState);
                                MouseButtonEvent?.Invoke(new MouseButtonData
                                {
                                    X = raw.mouse.lLastX,
                                    Y = raw.mouse.lLastY,
                                    IsRightDown = true,
                                    Action = MouseButtonAction.RightDown
                                });
                            }
                            else if ((raw.mouse.usButtonFlags & RI_MOUSE_RIGHT_BUTTON_UP) != 0)
                            {
                                buttonState = "Right button up";
                                ButtonUp?.Invoke(buttonState);
                                MouseButtonEvent?.Invoke(new MouseButtonData
                                {
                                    X = raw.mouse.lLastX,
                                    Y = raw.mouse.lLastY,
                                    IsRightDown = false,
                                    Action = MouseButtonAction.RightUp
                                });
                            }
                            else if ((raw.mouse.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_DOWN) != 0)
                            {
                                buttonState = "Middle button down";
                                ButtonDown?.Invoke(buttonState);
                                MouseButtonEvent?.Invoke(new MouseButtonData
                                {
                                    X = raw.mouse.lLastX,
                                    Y = raw.mouse.lLastY,
                                    IsMiddleDown = true,
                                    Action = MouseButtonAction.MiddleDown
                                });
                            }
                            else if ((raw.mouse.usButtonFlags & RI_MOUSE_MIDDLE_BUTTON_UP) != 0)
                            {
                                buttonState = "Middle button up";
                                ButtonUp?.Invoke(buttonState);
                                MouseButtonEvent?.Invoke(new MouseButtonData
                                {
                                    X = raw.mouse.lLastX,
                                    Y = raw.mouse.lLastY,
                                    IsMiddleDown = false,
                                    Action = MouseButtonAction.MiddleUp
                                });
                            }
                        }
                    }
                    else if (raw.header.dwType == RIM_TYPEHID)
                    {
                        ProcessHidInput(raw, buffer);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
        }

        private void ProcessHidInput(RAWINPUT raw, IntPtr buffer)
        {
            var deviceInfo = GetRawInputDeviceInfo(raw.header.hDevice);

            if (deviceInfo.IsTouch)
            {
                ProcessTouchInput(raw, buffer);
            }
            else if (deviceInfo.IsTrackpad)
            {
                ProcessTrackpadInput(raw, buffer);
            }
        }

        private void ProcessTouchInput(RAWINPUT raw, IntPtr buffer)
        {
            var touchData = new TouchData
            {
                X = GetTouchXCoordinate(buffer),
                Y = GetTouchYCoordinate(buffer),
                TouchId = GetTouchId(buffer),
                Action = GetTouchAction(buffer)
            };

            TouchEvent?.Invoke(touchData);
        }

        private void ProcessTrackpadInput(RAWINPUT raw, IntPtr buffer)
        {
            var trackpadData = new TrackpadData
            {
                DeltaX = GetTrackpadDeltaX(buffer),
                DeltaY = GetTrackpadDeltaY(buffer),
                IsMultiTouch = GetIsMultiTouch(buffer),
                Fingers = GetFingerCount(buffer)
            };

            TrackpadEvent?.Invoke(trackpadData);
        }

        private int GetTouchXCoordinate(IntPtr buffer) => 0;
        private int GetTouchYCoordinate(IntPtr buffer) => 0;
        private uint GetTouchId(IntPtr buffer) => 0;
        private TouchAction GetTouchAction(IntPtr buffer) => TouchAction.Down;
        private int GetTrackpadDeltaX(IntPtr buffer) => 0;
        private int GetTrackpadDeltaY(IntPtr buffer) => 0;
        private bool GetIsMultiTouch(IntPtr buffer) => false;
        private int GetFingerCount(IntPtr buffer) => 1;

        private RawInputDeviceInfo GetRawInputDeviceInfo(IntPtr hDevice)
        {
            return new RawInputDeviceInfo { IsTouch = false, IsTrackpad = false };
        }

        private class RawInputDeviceInfo
        {
            public bool IsTouch { get; set; }
            public bool IsTrackpad { get; set; }
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
