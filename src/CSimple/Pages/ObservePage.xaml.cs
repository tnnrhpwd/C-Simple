using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CSimple.Services;
#if WINDOWS
using System.Windows.Input;
#endif

namespace CSimple.Pages
{
    public partial class ObservePage : ContentPage
    {
        public Command TogglePCVisualCommand { get; }
        public Command TogglePCAudibleCommand { get; }
        public Command ToggleUserVisualCommand { get; }
        public Command ToggleUserAudibleCommand { get; }
        public Command ToggleUserTouchCommand { get; }

        public string PCVisualButtonText { get; set; } = "Read";
        public string PCAudibleButtonText { get; set; } = "Read";
        public string UserVisualButtonText { get; set; } = "Read";
        public string UserAudibleButtonText { get; set; } = "Read";
        public string UserTouchButtonText { get; set; } = "Read";

#if WINDOWS
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _keyboardProc;
        private LowLevelKeyboardProc _mouseProc;
        private IntPtr _keyboardHookID = IntPtr.Zero;
        private IntPtr _mouseHookID = IntPtr.Zero;
        private bool isUserTouchActive = false;

        private POINT lastMousePos;
#endif
        private List<string> _recordedActions;
        private FileService _fileService;

        public ObservePage()
        {
            InitializeComponent();
            _fileService = new FileService();
            _recordedActions = new List<string>();

            TogglePCVisualCommand = new Command(TogglePCVisualOutput);
            TogglePCAudibleCommand = new Command(TogglePCAudibleOutput);
            ToggleUserVisualCommand = new Command(ToggleUserVisualOutput);
            ToggleUserAudibleCommand = new Command(ToggleUserAudibleOutput);
            ToggleUserTouchCommand = new Command(ToggleUserTouchOutput);

            _ = LoadRecordedActions(); // Ignore the returned task since we only need to ensure it's running

            BindingContext = this;
        }

        private void TogglePCVisualOutput()
        {
            PCVisualButtonText = PCVisualButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"PC Visual Output: {PCVisualButtonText}");
            OnPropertyChanged(nameof(PCVisualButtonText));
        }

        private void TogglePCAudibleOutput()
        {
            PCAudibleButtonText = PCAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"PC Audible Output: {PCAudibleButtonText}");
            OnPropertyChanged(nameof(PCAudibleButtonText));
        }

        private void ToggleUserVisualOutput()
        {
            UserVisualButtonText = UserVisualButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"User Visual Output: {UserVisualButtonText}");
            OnPropertyChanged(nameof(UserVisualButtonText));
        }

        private void ToggleUserAudibleOutput()
        {
            UserAudibleButtonText = UserAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"User Audible Output: {UserAudibleButtonText}");
            OnPropertyChanged(nameof(UserAudibleButtonText));
        }

        private void ToggleUserTouchOutput()
        {
#if WINDOWS
            if (!isUserTouchActive)
            {
                _keyboardProc = HookCallback;
                _mouseProc = HookCallback;
                _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
                GetCursorPos(out lastMousePos);
                DebugOutput("User Touch Output capture initialized.");
                UserTouchButtonText = "Stop";
                isUserTouchActive = true;
            }
            else
            {
                if (_keyboardHookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHookID);
                    _keyboardHookID = IntPtr.Zero;
                }
                if (_mouseHookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookID);
                    _mouseHookID = IntPtr.Zero;
                }
                DebugOutput("User Touch Output capture stopped.");
                UserTouchButtonText = "Read";
                isUserTouchActive = false;
            }

            OnPropertyChanged(nameof(UserTouchButtonText));
#endif
        }

        private void DebugOutput(string message)
        {
            Debug.WriteLine(message);
            // Optionally update a UI label or text area with the debug message
        }

        private async Task LoadRecordedActions()
        {
            _recordedActions = await _fileService.LoadRecordedActionsAsync();
        }

        private async Task SaveRecordedActions()
        {
            await _fileService.SaveRecordedActionsAsync(_recordedActions);
        }

        public void AddRecordedAction(string action)
        {
            _recordedActions.Add(action);
            // Save changes to file
            _ = SaveRecordedActions(); // Ignore the returned task since we only need to ensure it's running
        }

#if WINDOWS
        private IntPtr SetHook(LowLevelKeyboardProc proc, int hookType)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                string currentTime = DateTime.Now.ToString("HH:mm:ss.fff");

                if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    GetCursorPos(out POINT currentMousePos);
                    Dispatcher.Dispatch(() =>
                    {
                        if (wParam == (IntPtr)WM_LBUTTONDOWN)
                        {
                            UserTouchOutput.Text += $"{currentTime} - Left Mouse Button clicked at ({currentMousePos.X}, {currentMousePos.Y})\n";
                        }
                        else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                        {
                            UserTouchOutput.Text += $"{currentTime} - Right Mouse Button clicked at ({currentMousePos.X}, {currentMousePos.Y})\n";
                        }
                        else if (wParam == (IntPtr)WM_MOUSEMOVE)
                        {
                            UserTouchOutput.Text += $"{currentTime} - Mouse Moved to ({currentMousePos.X}, {currentMousePos.Y})\n";
                        }
                    });
                    DebugOutput($"{currentTime} {wParam} {currentMousePos.X} {currentMousePos.Y}");
                    AddRecordedAction($"{currentTime} {wParam} {currentMousePos.X} {currentMousePos.Y}");
                    lastMousePos = currentMousePos;
                }
                else
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    string keyPressed = ((VirtualKey)vkCode).ToString();

                    Dispatcher.Dispatch(() =>
                    {
                        UserTouchOutput.Text += $"{currentTime} - {keyPressed} key pressed\n";
                    });
                    DebugOutput($"{currentTime} {vkCode}");
                    AddRecordedAction($"{currentTime} {vkCode}");

                }
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
        #endif
    }
}
