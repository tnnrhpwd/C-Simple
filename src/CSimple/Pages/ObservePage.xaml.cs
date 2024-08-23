using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
#if WINDOWS
using System.Windows.Input;
#endif
using System.Linq;

namespace CSimple.Pages
{
    public partial class ObservePage : ContentPage
    {
        public Command ReadPCVisualCommand { get; }
        public Command ReadPCAudibleCommand { get; }
        public Command ReadUserVisualCommand { get; }
        public Command ReadUserAudibleCommand { get; }
        public Command ReadUserTouchCommand { get; }

#if WINDOWS
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;
#endif

        public ObservePage()
        {
            InitializeComponent();
            ReadPCVisualCommand = new Command(async () => InitializePCVisualOutput());
            ReadPCAudibleCommand = new Command(InitializePCAudibleOutput);
            ReadUserVisualCommand = new Command(InitializeUserVisualOutput);
            ReadUserAudibleCommand = new Command(InitializeUserAudibleOutput);
            ReadUserTouchCommand = new Command(InitializeUserTouchOutput);

            BindingContext = this;
        }

        private void InitializePCVisualOutput()
        {
            DebugOutput("Starting PC Visual Output capture.");
        }

        private void InitializePCAudibleOutput()
        {
            DebugOutput("Starting PC Audible Output capture.");
        }

        private void InitializeUserVisualOutput()
        {
            DebugOutput("Starting User Visual Output capture.");
        }

        private void InitializeUserAudibleOutput()
        {
            DebugOutput("Starting User Audible Output capture.");
        }

        private void InitializeUserTouchOutput()
        {
#if WINDOWS
            _proc = HookCallback;
            _hookID = SetHook(_proc);
            DebugOutput("User Touch Output capture initialized.");
#endif
        }

        private void DebugOutput(string message)
        {
            Debug.WriteLine(message);
            // Optionally update a UI label or text area with the debug message
        }

#if WINDOWS
        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);

                string keyPressed = string.Empty;

                switch ((VirtualKey)vkCode)
                {
                    case VirtualKey.VK_ESCAPE:
                        keyPressed = "Escape";
                        break;
                    case VirtualKey.VK_LWIN:
                    case VirtualKey.VK_RWIN:
                        keyPressed = "Windows";
                        break;
                    case VirtualKey.VK_F5:
                        keyPressed = "F5";
                        break;
                    default:
                        keyPressed = ((VirtualKey)vkCode).ToString();
                        break;
                }

                // Update the UI with the keypress
                Dispatcher.Dispatch(() =>
                {
                    UserTouchOutput.Text += keyPressed + " key pressed\n";
                });

                DebugOutput($"{keyPressed} key pressed.");
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private const int WH_KEYBOARD_LL = 13;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private enum VirtualKey
        {
            VK_ESCAPE = 0x1B,
            VK_LWIN = 0x5B,
            VK_RWIN = 0x5C,
            VK_F5 = 0x74,
            // Add more keys as needed
        }
#endif
    }
}
