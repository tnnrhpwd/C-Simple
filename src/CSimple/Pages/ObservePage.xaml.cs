using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CSimple.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Newtonsoft.Json;

namespace CSimple.Pages
{
    public partial class ObservePage : ContentPage
    {
        public ObservableCollection<ActionGroup> ActionGroups { get; set; } = new ObservableCollection<ActionGroup>();
        public Command TogglePCVisualCommand { get; }
        public Command TogglePCAudibleCommand { get; }
        public Command ToggleUserVisualCommand { get; }
        public Command ToggleUserAudibleCommand { get; }
        public Command ToggleUserTouchCommand { get; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }

        public string PCVisualButtonText { get; set; } = "Read";
        public string PCAudibleButtonText { get; set; } = "Read";
        public string UserVisualButtonText { get; set; } = "Read";
        public string UserAudibleButtonText { get; set; } = "Read";
        public string UserTouchButtonText { get; set; } = "Read";
        public string UserTouchInputText { get; set; } = "";

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
            SaveActionCommand = new Command(SaveAction);
            SaveToFileCommand = new Command(async () => await SaveActionGroupsToFile());
            LoadFromFileCommand = new Command(async () => await LoadActionGroupsFromFile());

            _ = LoadActionGroups();

            BindingContext = this;
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadActionGroupsFromFile();
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

        private async void ToggleUserTouchOutput()
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
                // Save the updated ActionGroups list to the file
                await SaveActionGroupsToFile();
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

        private async Task LoadActionGroups()
        {
            try
            {
                var actionGroups = await _fileService.LoadActionGroupsAsync();
                ActionGroups.Clear();
                foreach (var actionGroup in actionGroups)
                {
                    ActionGroups.Add(actionGroup);
                }
                DebugOutput("Action Groups Loaded");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups: {ex.Message}");
            }
        }
        private async Task SaveActionGroupsToFile()
        {
            try
            {
                // var actionGroupsToSave = ActionGroups.Cast<object>().ToList();
                await _fileService.SaveActionGroupsAsync(ActionGroups);
                DebugOutput("Action Groups Saved to File");
                _recordedActions.Clear();
                _ = LoadActionGroupsFromFile();
                UserTouchOutput.Text = "";
            }
            catch (Exception ex)
            {
                DebugOutput($"Error saving action groups: {ex.Message}");
            }
        }



        private async Task LoadActionGroupsFromFile()
        {
            try
            {
                var loadedActionGroups = await _fileService.LoadActionGroupsAsync();
                ActionGroups = new ObservableCollection<ActionGroup>((IEnumerable<ActionGroup>)loadedActionGroups);
                DebugOutput("Action Groups Loaded from File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups from file: {ex.Message}");
            }
        }

        private void SaveAction()
        {
            string actionName = ActionNameInput.Text;

            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
            {
                // Convert the UserTouchInputText to the new ActionArrayItem format
                var actionArrayItem = JsonConvert.DeserializeObject<ActionArrayItem>(UserTouchInputText); //The name 'JsonConvert' does not exist in the current contextCS0103

                // Check if an ActionGroup with the same name already exists
                var existingActionGroup = ActionGroups.FirstOrDefault(ag => ag.ActionName == actionName);

                if (existingActionGroup != null)
                {
                    // If it exists, append the new action item to the existing ActionArray
                    existingActionGroup.ActionArray.Add(actionArrayItem);
                    DebugOutput($"Updated Action Group: {actionName}");
                }
                else
                {
                    // If it doesn't exist, create a new ActionGroup and add it to the list
                    var newActionGroup = new ActionGroup
                    {
                        ActionName = actionName,
                        ActionArray = new List<ActionArrayItem> { actionArrayItem }
                    };
                    ActionGroups.Add(newActionGroup);
                    DebugOutput($"Saved Action Group: {actionName}");
                }

                // Clear the recorded actions after saving to prevent duplicates
                _recordedActions.Clear();
            }
            else
            {
                DebugOutput("Please enter both Action Name and Action Array.");
            }
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
                var currentTime = DateTime.UtcNow.ToString("o"); // Using ISO 8601 format
                var actionArrayItem = new ActionArrayItem
                {
                    Timestamp = currentTime
                };

                if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    GetCursorPos(out POINT currentMousePos);
                    actionArrayItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };

                    // Set event type and category
                    if (wParam == (IntPtr)WM_LBUTTONDOWN)
                    {
                        actionArrayItem.Type = 513;
                        actionArrayItem.Category = 1; // Example category
                    }
                    else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                    {
                        actionArrayItem.Type = 516;
                        actionArrayItem.Category = 1; // Example category
                    }
                    else if (wParam == (IntPtr)WM_MOUSEMOVE)
                    {
                        actionArrayItem.Type = 512;
                        actionArrayItem.Category = 0; // Example category
                    }

                    Dispatcher.Dispatch(() =>
                    {
                        UserTouchOutput.Text += $"{currentTime} - Mouse Event at ({currentMousePos.X}, {currentMousePos.Y})\n";
                    });
                    DebugOutput($"{currentTime} {wParam} {currentMousePos.X} {currentMousePos.Y}");
                }
                else
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionArrayItem.Type = 256;
                    actionArrayItem.KeyCode = vkCode;

                    Dispatcher.Dispatch(() =>
                    {
                        UserTouchOutput.Text += $"{currentTime} - Key {((VirtualKey)vkCode).ToString()} pressed\n";
                    });
                    DebugOutput($"{currentTime} {vkCode}");
                }

                // Serialize the action item to JSON and save it
                UserTouchInputText = JsonConvert.SerializeObject(actionArrayItem);
                SaveAction();
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
        private DateTime lastMouseEventTime = DateTime.MinValue;
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
        private static extern bool GetCursorPos(out POINT lpPoint);
        [StructLayout(LayoutKind.Sequential)]

        private struct POINT
        {
            public int X;
            public int Y;
        }
        #endif
    }
}
