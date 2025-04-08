using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class GlobalInputCapture : Form
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static HookProc _keyboardProc = KeyboardHookCallback;
    private static HookProc _mouseProc = MouseHookCallback;
    private static IntPtr _keyboardHookID = IntPtr.Zero;
    private static IntPtr _mouseHookID = IntPtr.Zero;

    // Event handlers for service integration
    public static event Action<IntPtr, int> TouchInputReceived;
    public static event Action<TOUCHINPUT[]> TouchDataProcessed;

    // Constants for hook types
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_INPUT = 0x00FF;
    private const int WM_TOUCH = 0x0240;
    private const int WM_GESTURE = 0x0119;
    private const int WM_POINTERDOWN = 0x0246;
    private const int WM_POINTERUP = 0x0247;
    private const int WM_POINTERUPDATE = 0x0245;

    // DLL Imports
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] pRawInputDevice, uint uiNumDevices, uint cbSize);

    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

    [DllImport("user32.dll")]
    private static extern bool RegisterTouchWindow(IntPtr hWnd, uint ulFlags);

    [DllImport("user32.dll")]
    private static extern bool GetTouchInputInfo(IntPtr hTouchInput, uint cInputs, [Out] TOUCHINPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern bool CloseTouchInputHandle(IntPtr hTouchInput);

    [DllImport("user32.dll")]
    private static extern bool EnableMouseInPointer(bool fEnable);

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Explicit)]
    struct RAWINPUT
    {
        [FieldOffset(0)]
        public RAWINPUTHEADER header;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TOUCHINPUT
    {
        public int x;
        public int y;
        public IntPtr hSource;
        public int dwID;
        public int dwFlags;
        public int dwMask;
        public int dwTime;
        public IntPtr dwExtraInfo;
        public int cxContact;
        public int cyContact;
    }

    private const int TOUCHEVENTF_DOWN = 0x0001;
    private const int TOUCHEVENTF_UP = 0x0002;
    private const int TOUCHEVENTF_MOVE = 0x0004;
    private const int TOUCHEVENTF_INRANGE = 0x0008;
    private const int TOUCHEVENTF_PRIMARY = 0x0010;
    private const int TOUCHEVENTF_NOCOALESCE = 0x0020;
    private const int TOUCHEVENTF_PALM = 0x0080;

    private const uint TWF_WANTPALM = 0x00000002;
    private const uint TWF_FINETOUCH = 0x00000001;

    public static void StartHooks()
    {
        _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
        _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
        Console.WriteLine("Hooks started.");
    }

    public static void StopHooks()
    {
        UnhookWindowsHookEx(_keyboardHookID);
        UnhookWindowsHookEx(_mouseHookID);
        Console.WriteLine("Hooks stopped.");
    }

    private static IntPtr SetHook(HookProc proc, int hookType)
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
        }
    }

    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)0x0100)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Console.WriteLine("Key Pressed: " + ((Keys)vkCode));
        }
        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
    }

    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            Console.WriteLine("Mouse Event: " + wParam);
        }
        return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
    }

    public static void RegisterTouchAndPenDevices(IntPtr hwnd)
    {
        RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[3];

        rid[0].usUsagePage = 0x0D;
        rid[0].usUsage = 0x04;
        rid[0].dwFlags = 0;
        rid[0].hwndTarget = hwnd;

        rid[1].usUsagePage = 0x0D;
        rid[1].usUsage = 0x02;
        rid[1].dwFlags = 0;
        rid[1].hwndTarget = hwnd;

        rid[2].usUsagePage = 0x0D;
        rid[2].usUsage = 0x05;
        rid[2].dwFlags = 0;
        rid[2].hwndTarget = hwnd;

        if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
        {
            Console.WriteLine("Failed to register raw input devices.");
            return;
        }

        RegisterTouchWindow(hwnd, TWF_FINETOUCH | TWF_WANTPALM);
        EnableMouseInPointer(true);

        Console.WriteLine("Touch and pen devices registered successfully.");
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_INPUT:
                Console.WriteLine("WM_INPUT message received.");
                ProcessRawInput(m);
                break;

            case WM_TOUCH:
                Console.WriteLine("WM_TOUCH message received.");
                ProcessTouchInput(m);
                break;

            case WM_GESTURE:
                Console.WriteLine("WM_GESTURE message received.");
                break;

            case WM_POINTERDOWN:
            case WM_POINTERUP:
            case WM_POINTERUPDATE:
                Console.WriteLine($"Pointer message received: {m.Msg}");
                ProcessPointerInput(m);
                break;
        }

        base.WndProc(ref m);
    }

    private void ProcessTouchInput(Message m)
    {
        int inputCount = (int)m.WParam & 0xFFFF;
        Console.WriteLine($"Touch input count: {inputCount}");

        if (inputCount > 0)
        {
            TouchInputReceived?.Invoke(m.LParam, inputCount);

            TOUCHINPUT[] inputs = new TOUCHINPUT[inputCount];

            if (GetTouchInputInfo(m.LParam, (uint)inputCount, inputs, Marshal.SizeOf(typeof(TOUCHINPUT))))
            {
                try
                {
                    foreach (var input in inputs)
                    {
                        string action = "Unknown";
                        if ((input.dwFlags & TOUCHEVENTF_DOWN) != 0)
                            action = "Down";
                        else if ((input.dwFlags & TOUCHEVENTF_UP) != 0)
                            action = "Up";
                        else if ((input.dwFlags & TOUCHEVENTF_MOVE) != 0)
                            action = "Move";

                        Console.WriteLine($"Touch {action} at ({input.x}, {input.y}), ID: {input.dwID}");
                    }

                    TouchDataProcessed?.Invoke(inputs);
                }
                finally
                {
                    CloseTouchInputHandle(m.LParam);
                }
            }
            else
            {
                Console.WriteLine("Failed to get touch input info");
            }
        }
    }

    private void ProcessPointerInput(Message m)
    {
        uint pointerId = (uint)m.WParam & 0xFFFF;
        Console.WriteLine($"Pointer ID: {pointerId}");
    }

    private void ProcessRawInput(Message m)
    {
        uint size = 0;
        GetRawInputData(m.LParam, 0x10000003, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

        IntPtr buffer = Marshal.AllocHGlobal((int)size);
        if (buffer != IntPtr.Zero)
        {
            GetRawInputData(m.LParam, 0x10000003, buffer, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

            if (raw.header.dwType == 0x00000004)
            {
                Console.WriteLine("Touch Event Detected.");
                ProcessTouchRawInput(buffer, size);
            }
            else if (raw.header.dwType == 0x00000002)
            {
                Console.WriteLine("Pen Event Detected.");
                ProcessPenRawInput(buffer, size);
            }

            Marshal.FreeHGlobal(buffer);
        }
        else
        {
            Console.WriteLine("Failed to allocate buffer for raw input.");
        }
    }

    private void ProcessTouchRawInput(IntPtr buffer, uint size)
    {
        Console.WriteLine("Processing raw touch input");
    }

    private void ProcessPenRawInput(IntPtr buffer, uint size)
    {
        Console.WriteLine("Processing raw pen input");
    }
}
