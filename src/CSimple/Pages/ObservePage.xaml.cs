﻿using Microsoft.Maui.Controls;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CSimple.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System;
// using Windows.Graphics.Display;
using OpenCvSharp;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Threading.Tasks;
using YourNamespace.Services;

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
        // private RawInputHandler _rawInputHandler;
        private readonly MouseTrackingService _mouseTrackingService;

        private DateTime _mouseLeftButtonDownTimestamp;
        private DateTime _mouseRightButtonDownTimestamp;
        private Dictionary<ushort, DateTime> _keyPressDownTimestamps = new Dictionary<ushort, DateTime>();

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
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;

        public ObservePage()
        {
            InitializeComponent();
            _fileService = new FileService();
            _recordedActions = new List<string>();
            // var hwnd = ((MauiWinUIWindow)App.Current.Windows[0].Handler.PlatformView).WindowHandle;
            // _rawInputHandler = new RawInputHandler(hwnd);
            _mouseTrackingService = new MouseTrackingService();
            _mouseTrackingService.MouseMoved += OnMouseMoved;

            TogglePCVisualCommand = new Command(TogglePCVisualOutput);
            TogglePCAudibleCommand = new Command(TogglePCAudibleOutput);
            ToggleUserVisualCommand = new Command(ToggleUserVisualOutput);
            ToggleUserAudibleCommand = new Command(ToggleUserAudibleOutput);
            ToggleUserTouchCommand = new Command(ToggleUserTouchOutput);
            SaveActionCommand = new Command(SaveAction);
            SaveToFileCommand = new Command(async () => await SaveActionGroupsToFile());
            LoadFromFileCommand = new Command(async () => await LoadActionGroupsFromFile());
            // _rawInputHandler = new RawInputHandler(this.Handle); // 'ObservePage' does not contain a definition for 'Handle' and no accessible extension method 'Handle' accepting a first argument of type 'ObservePage' could be found (are you missing a using directive or an assembly reference?)CS1061

            _ = LoadActionGroups();

            BindingContext = this;
        }
        private void OnMouseMoved(Microsoft.Maui.Graphics.Point delta)
        {
            Dispatcher.Dispatch(() =>
            {
                DebugOutput($"Mouse Movement: X={delta.X}, Y={delta.Y}");
            });
        }
        private void StartTracking()
        {
            _mouseTrackingService.StartTracking();
        }
        private void StopTracking()
        {
            _mouseTrackingService.StopTracking();
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadActionGroupsFromFile();
        }
        private void TogglePCVisualOutput() // webcam image: record what else the human hears
        {
            PCVisualButtonText = PCVisualButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"PC Visual Output: {PCVisualButtonText}");
            OnPropertyChanged(nameof(PCVisualButtonText));
            using var capture = new VideoCapture(0); // 0 is the default camera
            using var frame = new Mat();

            if (!capture.IsOpened())
            {
                Console.WriteLine("Failed to open webcam.");
                return;
            }

            while (true) // Loop to capture frames continuously
            {
                capture.Read(frame);

                if (frame.Empty() && PCVisualButtonText == "Stop")
                    break;
                
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", $"WebcamImage_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                Cv2.ImWrite(filePath, frame);

                // For toggling, you can break out of the loop based on user input or a flag
            }
        }

        private void TogglePCAudibleOutput() // Audio Recorder: record the sounds that the human hears
        {
            PCAudibleButtonText = PCAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"PC Audible Output: {PCAudibleButtonText}");
            OnPropertyChanged(nameof(PCAudibleButtonText));
        }

        private void ToggleUserVisualOutput() // Screen Recorder: record what the human sees on the screen
        {
            UserVisualButtonText = UserVisualButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"User Visual Output: {UserVisualButtonText}");
            OnPropertyChanged(nameof(UserVisualButtonText));
        }

        private void ToggleUserAudibleOutput() // webcam audio: record what is going on around the computer
        {
            // Toggle the button text between "Read" and "Stop"
            UserAudibleButtonText = UserAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"User Audible Output: {UserAudibleButtonText}");
            OnPropertyChanged(nameof(UserAudibleButtonText));

            if (UserAudibleButtonText == "Stop")
            {
                // Start recording
                try
                {
                    // Define the output file path once when recording starts
                    string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", $"WebcamAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                    // Ensure the directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));

                    _waveIn = new WaveInEvent();

                    // Find the correct input device (your webcam's audio input)
                    var deviceNumber = FindWebcamAudioDevice();
                    if (deviceNumber == -1)
                    {
                        Console.WriteLine("Webcam audio device not found.");
                        return;
                    }

                    _waveIn.DeviceNumber = deviceNumber;
                    _waveIn.WaveFormat = new WaveFormat(44100, 1); // Set appropriate format for your webcam audio
                    _writer = new WaveFileWriter(filePath, _waveIn.WaveFormat);

                    _waveIn.DataAvailable += (s, a) =>
                    {
                        _writer.Write(a.Buffer, 0, a.BytesRecorded);
                    };

                    _waveIn.RecordingStopped += (s, a) =>
                    {
                        _writer?.Dispose();
                        _writer = null;
                        _waveIn.Dispose();
                        Console.WriteLine($"Recording saved to: {filePath}");
                    };

                    _waveIn.StartRecording();
                    Console.WriteLine("Recording webcam audio...");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            else
            {
                // Stop recording
                _waveIn?.StopRecording();
                Console.WriteLine("Stopped recording webcam audio.");
            }
        }


        private int FindWebcamAudioDevice()
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++) 
            {
                var deviceInfo = WaveIn.GetCapabilities(i);
                if (deviceInfo.ProductName.Contains("Webcam")) 
                {
                    return i;
                }
            }
            return -1; // Not found
        }

        private async void ToggleUserTouchOutput() // mouse &  key logger: record when human presses buttons
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
                StartTracking();
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
                // CaptureScreen("C:\\Path\\To\\Your\\File\\screenshot.png"); // Capture the screen and save to a file
                await SaveActionGroupsToFile(); // Save the updated ActionGroups list to the file
                UserTouchButtonText = "Read";
                isUserTouchActive = false;
                StopTracking();
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
            DebugOutput("saving action...");
            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
            {
                // Convert the UserTouchInputText to the new ActionArrayItem format
                var actionArrayItem = JsonConvert.DeserializeObject<ActionArrayItem>(UserTouchInputText);

                // Check if an ActionGroup with the same name already exists
                var existingActionGroup = ActionGroups.FirstOrDefault(ag => ag.ActionName == actionName);

                if (existingActionGroup != null)
                {
                    // If it exists, append the new action item to the existing ActionArray
                    existingActionGroup.ActionArray.Add(actionArrayItem);
                    DebugOutput($"Updated Action Group: {UserTouchInputText}");
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
                // DebugOutput("Please enter both Action Name and Action Array.");
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
        
                // if (_rawInputHandler == null)
                // {
                //     DebugOutput("Error: _rawInputHandler is not initialized.");
                //     return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                // }
        
                // _rawInputHandler.ProcessRawInput(lParam);
                DebugOutput(nCode.ToString()+","+wParam+","+lParam);
                if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    GetCursorPos(out POINT currentMousePos);
                    _mouseLeftButtonDownTimestamp = DateTime.UtcNow;
                    actionArrayItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionArrayItem.EventType = WM_MOUSEMOVE;
                    actionArrayItem.KeyCode = 0;
                }
                if (wParam == (IntPtr)WM_LBUTTONDOWN)
                {
                    GetCursorPos(out POINT currentMousePos);
                    _mouseLeftButtonDownTimestamp = DateTime.UtcNow;
                    actionArrayItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionArrayItem.EventType = WM_LBUTTONDOWN;
                    actionArrayItem.KeyCode = 0;
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP)
                {
                    var duration = (DateTime.UtcNow - _mouseLeftButtonDownTimestamp).TotalMilliseconds;
                    actionArrayItem.Duration = duration > 0 ? (int)duration : 1;
                    GetCursorPos(out POINT currentMousePos);
                    actionArrayItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionArrayItem.EventType = WM_LBUTTONUP;
                    actionArrayItem.KeyCode = 0;
                }
                else if (wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    GetCursorPos(out POINT currentMousePos);
                    _mouseRightButtonDownTimestamp = DateTime.UtcNow;
                    actionArrayItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionArrayItem.EventType = WM_RBUTTONDOWN;
                    actionArrayItem.KeyCode = 0;
                }
                else if (wParam == (IntPtr)WM_RBUTTONUP)
                {
                    var duration = (DateTime.UtcNow - _mouseRightButtonDownTimestamp).TotalMilliseconds;
                    actionArrayItem.Duration = duration > 0 ? (int)duration : 1;
                    GetCursorPos(out POINT currentMousePos);
                    actionArrayItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionArrayItem.EventType = WM_RBUTTONUP;
                    actionArrayItem.KeyCode = 0;
                }
                else if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionArrayItem.KeyCode = (ushort)vkCode;
                    _keyPressDownTimestamps[(ushort)vkCode] = DateTime.UtcNow;
                    actionArrayItem.EventType = WM_KEYDOWN;
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionArrayItem.KeyCode = (ushort)vkCode;
        
                    if (_keyPressDownTimestamps.TryGetValue((ushort)vkCode, out DateTime keyDownTimestamp))
                    {
                        var duration = (DateTime.UtcNow - keyDownTimestamp).TotalMilliseconds;
                        actionArrayItem.Duration = duration > 0 ? (int)duration : 1;
                        _keyPressDownTimestamps.Remove((ushort)vkCode);
                    }
                    actionArrayItem.EventType = WM_KEYUP;
                }
        
                Dispatcher.Dispatch(() =>
                {
                    UserTouchOutput.Text += $"{currentTime} - {((VirtualKey)actionArrayItem.KeyCode).ToString()} {actionArrayItem.EventType} - Duration: {actionArrayItem.Duration}ms\n";
                });
        
                UserTouchInputText = JsonConvert.SerializeObject(actionArrayItem);
                SaveAction();

                #if WINDOWS
                if (UserVisualButtonText == "Stop")
                {
                    string fileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", $"ScreenCapture_{DateTime.Now.ToString("yyyyMMdd_HHmmss")}.png");
                    DebugOutput(fileName);
                    CaptureScreen(fileName);
                }
                #endif
            }
            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
        
        private void CaptureScreen(string filePath)
        {
            try
            {
                IntPtr hDesktopWnd = GetDesktopWindow();
                IntPtr hDesktopDC = GetDC(hDesktopWnd);
                IntPtr hMemoryDC = CreateCompatibleDC(hDesktopDC);

                // Get screen dimensions using native Windows API
                Rectangle screenBounds = GetScreenBounds();
                IntPtr hBitmap = CreateCompatibleBitmap(hDesktopDC, screenBounds.Width, screenBounds.Height);
                IntPtr hOldBitmap = SelectObject(hMemoryDC, hBitmap);

                BitBlt(hMemoryDC, 0, 0, screenBounds.Width, screenBounds.Height, hDesktopDC, 0, 0, SRCCOPY);

                using (Bitmap bitmap = Bitmap.FromHbitmap(hBitmap))
                {
                    bitmap.Save(filePath, ImageFormat.Png);
                }

                SelectObject(hMemoryDC, hOldBitmap);
                DeleteObject(hBitmap);
                ReleaseDC(hDesktopWnd, hDesktopDC);
                ReleaseDC(hDesktopWnd, hMemoryDC);
            }
            catch (Exception ex)
            {
                DebugOutput($"Error capturing screen: {ex.Message}");
            }
        }

        private Rectangle GetScreenBounds()
        {
            // Using P/Invoke to get the screen dimensions
            IntPtr hDesktopDC = GetDC(GetDesktopWindow());
            int screenWidth = GetDeviceCaps(hDesktopDC, 118); // HORZRES
            int screenHeight = GetDeviceCaps(hDesktopDC, 117); // VERTRES
            ReleaseDC(GetDesktopWindow(), hDesktopDC);
            return new Rectangle(0, 0, screenWidth, screenHeight);
        }

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int HORZRES = 8;
        private const int VERTRES = 10;

        private DateTime lastMouseEventTime = DateTime.MinValue;
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

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

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(IntPtr hDestDC, int xDest, int yDest, int nWidth, int nHeight, IntPtr hSrcDC, int xSrc, int ySrc, int dwRop);

        private const int SRCCOPY = 0x00CC0020;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
        #endif
    }
}
