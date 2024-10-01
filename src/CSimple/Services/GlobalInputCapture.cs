using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
Console.WriteLine("Getting love.");
public class GlobalInputCapture : Form
{
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static HookProc _keyboardProc = KeyboardHookCallback;
    private static HookProc _mouseProc = MouseHookCallback;
    private static IntPtr _keyboardHookID = IntPtr.Zero;
    private static IntPtr _mouseHookID = IntPtr.Zero;

    // Constants for hook types
    private const int WH_KEYBOARD_LL = 13;
    private const int WH_MOUSE_LL = 14;
    private const int WM_INPUT = 0x00FF;

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
        // Add necessary fields for touch, pen, keyboard, and mouse
        // This structure varies based on the input type.
    }

    // Hook setup
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

    // Callback functions for keyboard and mouse hooks
    private static IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)0x0100) // WM_KEYDOWN
        {
            int vkCode = Marshal.ReadInt32(lParam);
            Console.WriteLine("Key Pressed: " + ((Keys)vkCode)); // Log keypress information
        }
        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
    }

    private static IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            Console.WriteLine("Mouse Event: " + wParam); // Log mouse event information
        }
        return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
    }

    // Register Touch and Pen devices
    public static void RegisterTouchAndPenDevices(IntPtr hwnd)
    {
        RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];

        // Touchscreen
        rid[0].usUsagePage = 0x0D;  // Touchscreen usage page
        rid[0].usUsage = 0x04;      // Touchscreen usage
        rid[0].dwFlags = 0;         // Flags (0 = default behavior)
        rid[0].hwndTarget = hwnd;   // Handle to the window receiving input

        // Pen input
        rid[1].usUsagePage = 0x0D;  // Digitizer usage page
        rid[1].usUsage = 0x02;      // Pen usage
        rid[1].dwFlags = 0;
        rid[1].hwndTarget = hwnd;

        if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
        {
            Console.WriteLine("Failed to register raw input devices.");
            return;
        }
        
        Console.WriteLine("Touch and pen devices registered successfully.");
    }

    // Handling raw input messages for touch and pen devices
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
        {
            Console.WriteLine("WM_INPUT message received.");
            
            uint size = 0;
            GetRawInputData(m.LParam, 0x10000003, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            IntPtr buffer = Marshal.AllocHGlobal((int)size);
            if (buffer != IntPtr.Zero)
            {
                GetRawInputData(m.LParam, 0x10000003, buffer, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

                RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buffer);

                // Handle touch input
                if (raw.header.dwType == 0x00000004) // RIM_TYPEHID for touch
                {
                    Console.WriteLine("Touch Event Detected.");
                }
                // Handle pen input
                else if (raw.header.dwType == 0x00000002) // RIM_TYPEHID for pen
                {
                    Console.WriteLine("Pen Event Detected.");
                }

                Marshal.FreeHGlobal(buffer);
            }
            else
            {
                Console.WriteLine("Failed to allocate buffer for raw input.");
            }
        }

        base.WndProc(ref m);
    }
}
