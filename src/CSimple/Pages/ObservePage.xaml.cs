using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using OpenCvSharp;
using NAudio.Wave;
using Microsoft.Maui.Storage;
#if WINDOWS
using System.Text;
using System.Windows.Forms;
#endif
using NWaves.FeatureExtractors;
using NWaves.FeatureExtractors.Options;
using CSimple.Services;

namespace CSimple.Pages
{
    public partial class ObservePage : ContentPage
    {
        private bool _isReadAllToggled;
        public bool IsReadAllToggled
        {
            get => _isReadAllToggled;
            set
            {
                if (_isReadAllToggled != value)
                {
                    _isReadAllToggled = value;
                    OnPropertyChanged();
                    ToggleAllOutputs(value);
                }
            }
        }
        public ObservableCollection<DataItem> Data { get; set; } = new ObservableCollection<DataItem>();
        private CancellationTokenSource _userVisualCancellationTokenSource;
        private CancellationTokenSource _pcVisualCancellationTokenSource;
        private Dictionary<ushort, ActionItem> _activeKeyPresses = new Dictionary<ushort, ActionItem>();

        public Command TogglePCVisualCommand { get; }
        public Command TogglePCAudibleCommand { get; }
        public Command ToggleUserVisualCommand { get; }
        public Command ToggleUserAudibleCommand { get; }
        public Command ToggleUserTouchCommand { get; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }
        private RawInputService _rawInputService;
        private readonly MouseTrackingService _mouseTrackingService;
        private readonly UserService _userService;
        private readonly UserLoginService _userLoginService;

        private DateTime _mouseLeftButtonDownTimestamp;
        private Dictionary<ushort, DateTime> _keyPressDownTimestamps = new Dictionary<ushort, DateTime>();

        public string PCVisualButtonText { get; set; } = "Read";
        public string PCAudibleButtonText { get; set; } = "Read";
        public string UserVisualButtonText { get; set; } = "Read";
        public string UserAudibleButtonText { get; set; } = "Read";
        public string UserTouchButtonText { get; set; } = "Read";
        public string UserTouchInputText { get; set; } = "";
        public ImageSource CapturedImageSource { get; set; }

#if WINDOWS
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _keyboardProc;
        private LowLevelKeyboardProc _mouseProc;
        private IntPtr _keyboardHookID = IntPtr.Zero;
        private IntPtr _mouseHookID = IntPtr.Zero;
        private bool isUserTouchActive = false;
        private POINT lastMousePos;

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int HORZRES = 8;
        private const int VERTRES = 10;

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
        private List<string> _recordedActions;
        private readonly DataService _dataService;
        private FileService _fileService;
        private WaveInEvent _waveIn;
        private WaveFileWriter _writer;
        private WasapiLoopbackCapture _loopbackCapture;
        private WaveFileWriter _loopbackWriter;

        public ObservePage()
        {
            InitializeComponent();
            _fileService = new FileService();
            _dataService = new DataService();
            _userService = new UserService();
            _userLoginService = new UserLoginService();
            _recordedActions = new List<string>();
            var window = Microsoft.Maui.Controls.Application.Current.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
            _mouseTrackingService = new MouseTrackingService();
            _mouseTrackingService.MouseMoved += OnMouseMoved;
            CheckUserLoggedIn();

            TogglePCVisualCommand = new Command(TogglePCVisualOutput);
            TogglePCAudibleCommand = new Command(TogglePCAudibleOutput);
            ToggleUserVisualCommand = new Command(ToggleUserVisualOutput);
            ToggleUserAudibleCommand = new Command(ToggleUserAudibleOutput);
            ToggleUserTouchCommand = new Command(ToggleUserTouchOutput);
            SaveActionCommand = new Command(SaveAction);
            SaveToFileCommand = new Command(async () => await SaveDataItemsToFile());
            LoadFromFileCommand = new Command(async () => await LoadDataItemsFromFile());

            BindingContext = this;
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadDataItemsFromFile();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        private async void CheckUserLoggedIn()
        {
            await _userLoginService.CheckUserLoggedInAsync();
        }

        private void OnInputModifierClicked(object sender, EventArgs e)
        {
            InputModifierPopup.IsVisible = true;
        }

        private void OnOkayClicked(object sender, EventArgs e)
        {
            InputModifierPopup.IsVisible = false;
        }

        private async void ToggleAllOutputs(bool value)
        {
            if (value)
            {
                TogglePCVisualOutput();
                TogglePCAudibleOutput();
                ToggleUserVisualOutput();
                ToggleUserAudibleOutput();
                ToggleUserTouchOutput();
                await CompressAndUploadAsync();
            }
            else
            {
                if (PCVisualButtonText == "Stop")
                {
                    TogglePCVisualOutput();
                }
                if (PCAudibleButtonText == "Stop")
                {
                    TogglePCAudibleOutput();
                }
                if (UserVisualButtonText == "Stop")
                {
                    ToggleUserVisualOutput();
                }
                if (UserAudibleButtonText == "Stop")
                {
                    ToggleUserAudibleOutput();
                }
                if (UserTouchButtonText == "Stop")
                {
                    ToggleUserTouchOutput();
                }
            }
        }

        private async Task CompressAndUploadAsync()
        {
            var token = await SecureStorage.GetAsync("userToken");
            var compressedData = CompressData(Data.ToList());

            bool meetsCriteria = CheckPriority(compressedData);

            if (meetsCriteria && NetworkIsSuitable())
            {
                // await _dataService.CreateDataAsync(compressedData, token); 
            }
            else
            {
                // StoreDataLocally(compressedData);
            }
        }

        private object CompressData(List<DataItem> dataItems)
        {
            return dataItems;
        }

        private bool CheckPriority(object compressedData)
        {
            return true;
        }

        private bool NetworkIsSuitable()
        {
            return false;
        }

        private void OnMouseMoved(Microsoft.Maui.Graphics.Point delta)
        {
            Dispatcher.Dispatch(() =>
            {
                DebugOutput($"observepage Mouse Movement: X={delta.X}, Y={delta.Y}");
            });
        }

        private void StartTracking()
        {
#if WINDOWS
            var windowHandler = Microsoft.Maui.Controls.Application.Current.Windows[0].Handler;
            if (windowHandler.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                _mouseTrackingService.StartTracking(hwnd);
                GlobalInputCapture.StartHooks();
            }
#else
            _mouseTrackingService.StartTracking(IntPtr.Zero);
#endif
        }

        private void StopTracking()
        {
            _mouseTrackingService.StopTracking();
            GlobalInputCapture.StopHooks();
        }

        private void AddFileToLastItem(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (!Data.Any()) return;
            if (Data.Last().Data.Files == null)
                Data.Last().Data.Files = new List<FileItem>();

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var contentType = extension == ".wav" ? "audio/wav" : "image/png";

            if (!File.Exists(filePath))
            {
                File.WriteAllBytes(filePath, Convert.FromBase64String(File.ReadAllText(filePath)));
            }

            Data.Last().Data.Files.Add(new FileItem
            {
                filename = Path.GetFileName(filePath),
                contentType = contentType,
                data = Convert.ToBase64String(File.ReadAllBytes(filePath))
            });
            Debug.WriteLine($"File added: {filePath}");
        }

        private void TogglePCVisualOutput()
        {
            if (PCVisualButtonText == "Read")
            {
                PCVisualButtonText = "Stop";
                DebugOutput($"PC Visual Output: {PCVisualButtonText}");
                OnPropertyChanged(nameof(PCVisualButtonText));

                _pcVisualCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _pcVisualCancellationTokenSource.Token;

                Task.Run(() => CaptureWebcamImages(cancellationToken), cancellationToken);
            }
            else
            {
                PCVisualButtonText = "Read";
                DebugOutput($"PC Visual Output: {PCVisualButtonText}");
                OnPropertyChanged(nameof(PCVisualButtonText));

                _pcVisualCancellationTokenSource?.Cancel();
            }
        }

        private void CaptureWebcamImages(CancellationToken cancellationToken)
        {
            using var capture = new VideoCapture(0);
            using var frame = new Mat();

            if (!capture.IsOpened())
            {
                Console.WriteLine("Failed to open webcam.");
                return;
            }

            string webcamImagesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "WebcamImages");
            Directory.CreateDirectory(webcamImagesDirectory);

            while (!cancellationToken.IsCancellationRequested)
            {
                capture.Read(frame);

                if (frame.Empty())
                {
                    continue;
                }

                string actionName = ActionNameInput.Text;
                if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
                {
                    string filePath = Path.Combine(webcamImagesDirectory, $"WebcamImage_{DateTime.Now:yyyyMMdd_HHmmss}.jpg");
                    Cv2.ImWrite(filePath, frame);
                    AddFileToLastItem(filePath);
                    DebugOutput($"Webcam image saved to: {filePath}");

                    Dispatcher.Dispatch(() =>
                    {
                        DebugOutput($"Captured image source updated: {filePath}");
                        CapturedImageSource = ImageSource.FromFile(filePath);
                    });
                }
                Thread.Sleep(1000);
            }
        }

        private void TogglePCAudibleOutput()
        {
            PCAudibleButtonText = PCAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"PC Audible Output: {PCAudibleButtonText}");
            OnPropertyChanged(nameof(PCAudibleButtonText));

            if (PCAudibleButtonText == "Stop")
            {
                try
                {
                    string pcAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "PCAudio");
                    DebugOutput($"PC Audio Directory: {pcAudioDirectory}");

                    Directory.CreateDirectory(pcAudioDirectory);
                    string actionName = ActionNameInput.Text;

                    DebugOutput("Recording PC audio...");
                    string filePath = Path.Combine(pcAudioDirectory, $"PCAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                    _loopbackCapture = new WasapiLoopbackCapture();
                    _loopbackWriter = new WaveFileWriter(filePath, _loopbackCapture.WaveFormat);

                    _loopbackCapture.DataAvailable += (s, a) =>
                    {
                        _loopbackWriter.Write(a.Buffer, 0, a.BytesRecorded);
                    };

                    _loopbackCapture.RecordingStopped += (s, a) =>
                    {
                        _loopbackWriter?.Dispose();
                        _loopbackWriter = null;
                        _loopbackCapture.Dispose();
                        DebugOutput($"Recording saved to: {filePath}");
                        AddFileToLastItem(filePath);
                        ExtractMFCCs(filePath);
                    };

                    _loopbackCapture.StartRecording();
                    DebugOutput("Recording PC audio...");
                }
                catch (Exception ex)
                {
                    DebugOutput($"Error: {ex.Message}");
                }
            }
            else
            {
                _loopbackCapture?.StopRecording();
                DebugOutput("Stopped recording PC audio.");
            }
        }

        private void ExtractMFCCs(string filePath)
        {
            try
            {
                using var waveFile = new WaveFileReader(filePath);
                var samples = new float[waveFile.SampleCount];
                int sampleIndex = 0;
                var buffer = new byte[waveFile.WaveFormat.SampleRate * waveFile.WaveFormat.BlockAlign];
                int samplesRead;
                while ((samplesRead = waveFile.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < samplesRead; i += waveFile.WaveFormat.BlockAlign)
                    {
                        samples[sampleIndex++] = BitConverter.ToSingle(buffer, i);
                    }
                }

                int sampleRate = waveFile.WaveFormat.SampleRate;
                int featureCount = 13;
                int frameSize = 512;
                int hopSize = 256;

                var mfccExtractor = new MfccExtractor(new MfccOptions
                {
                    SamplingRate = sampleRate,
                    FeatureCount = featureCount,
                    FrameSize = frameSize,
                    HopSize = hopSize
                });

                var mfccs = mfccExtractor.ComputeFrom(samples);

                string mfccFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_MFCCs.csv");

                using (var writer = new StreamWriter(mfccFilePath))
                {
                    foreach (var vector in mfccs)
                    {
                        writer.WriteLine(string.Join(",", vector));
                    }
                }

                Console.WriteLine($"MFCCs saved to: {mfccFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error extracting MFCCs: {ex.Message}");
            }
        }

        private void ToggleUserVisualOutput()
        {
            UserVisualButtonText = UserVisualButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"User Visual Output: {UserVisualButtonText}");
            OnPropertyChanged(nameof(UserVisualButtonText));

            if (UserVisualButtonText == "Stop")
            {
                _userVisualCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _userVisualCancellationTokenSource.Token;

                Task.Run(() => CaptureAllScreens(cancellationToken), cancellationToken);
            }
            else
            {
                _userVisualCancellationTokenSource?.Cancel();
            }
        }

        private void CaptureAllScreens(CancellationToken cancellationToken)
        {
            string screenshotsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Screenshots");
            Directory.CreateDirectory(screenshotsDirectory);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    DateTime captureTime = DateTime.Now;

                    foreach (var screen in Screen.AllScreens)
                    {
                        var bounds = screen.Bounds;
                        using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
                        {
                            using (var g = Graphics.FromImage(bitmap))
                            {
                                g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                            }
                            string actionName = ActionNameInput.Text;
                            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
                            {
                                string filePath = Path.Combine(screenshotsDirectory, $"ScreenCapture_{captureTime:yyyyMMdd_HHmmss_fff}_{screen.DeviceName.Replace("\\", "").Replace(":", "")}.png");
                                bitmap.Save(filePath, ImageFormat.Png);
                                AddFileToLastItem(filePath);
                                DebugOutput($"Screenshot saved to: {filePath}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    DebugOutput($"Error capturing screen: {ex.Message}");
                }

                Thread.Sleep(1000);
            }
        }

        private void ToggleUserAudibleOutput()
        {
            UserAudibleButtonText = UserAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"User Audible Output: {UserAudibleButtonText}");
            OnPropertyChanged(nameof(UserAudibleButtonText));

            if (UserAudibleButtonText == "Stop")
            {
                try
                {
                    string webcamAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "WebcamAudio");

                    Directory.CreateDirectory(webcamAudioDirectory);

                    string filePath = Path.Combine(webcamAudioDirectory, $"WebcamAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                    _waveIn = new WaveInEvent();

                    var deviceNumber = FindWebcamAudioDevice();
                    if (deviceNumber == -1)
                    {
                        DebugOutput("Webcam audio device not found.");
                        return;
                    }

                    _waveIn.DeviceNumber = deviceNumber;
                    _waveIn.WaveFormat = new WaveFormat(44100, 1);
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
                        DebugOutput($"Recording saved to: {filePath}");
                        AddFileToLastItem(filePath);
                        ExtractMFCCs(filePath);
                    };

                    _waveIn.StartRecording();
                    DebugOutput("Recording webcam audio...");
                }
                catch (Exception ex)
                {
                    DebugOutput($"Error: {ex.Message}");
                }
            }
            else
            {
                _waveIn?.StopRecording();
                DebugOutput("Stopped recording webcam audio.");
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
            return -1;
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
                await SaveNewActionGroup();
                await SaveDataItemsToFile();
                await SaveLocalRichDataAsync();
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
        }

        private async Task SaveNewActionGroup()
        {
            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    DebugOutput("User is not logged in.");
                    return;
                }

                var userId = await SecureStorage.GetAsync("userID");
                var actionGroupObject = Data.Last().Data.ActionGroupObject;
                var dataItemFiles = Data.Last().Data.Files;
                DebugOutput($"User ID: {userId}, "
                    + $"Action Group: {JsonConvert.SerializeObject(actionGroupObject)}, "
                    + $"Files: {dataItemFiles.Count}");

                dataItemFiles.ForEach(file =>
                {
                    DebugOutput($"File: {file.filename}, {file.contentType}, {file.data.Length} bytes");
                });

                var dataItem = new DataObject
                {
                    Text = "Creator:" + userId + "|Action:" + (actionGroupObject.ActionName != null ? actionGroupObject.ActionName : "No Action Name"),
                    ActionGroupObject = actionGroupObject,
                    Files = dataItemFiles
                };

                // var response = await _dataService.CreateDataAsync(dataItem, token);
                // var serializedData = response.Data != null && response.Data.Any()
                //     ? JsonConvert.SerializeObject(response.Data)
                //     : "No data available";

                // DebugOutput($"4. (ObservePage.SaveNew) New Action Group Saved to Backend: {serializedData}");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error saving new action group: {ex.Message}");
            }
        }

        private async Task SaveDataItemsToFile()
        {
            try
            {
                await _fileService.SaveDataItemsAsync(Data.ToList());
                DebugOutput("Data items Saved to File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error Data items groups: {ex.Message}");
            }
        }

        private async Task LoadDataItemsFromFile()
        {
            try
            {
                var loadedActionGroups = await _fileService.LoadDataItemsAsync();
                Data = new ObservableCollection<DataItem>(loadedActionGroups.Select(ag => new DataItem { Data = new DataObject { ActionGroupObject = ag.Data.ActionGroupObject } }));
                DebugOutput("Data items Saved to File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading Data items from file: {ex.Message}");
            }
        }

        private void SaveAction()
        {
            string actionName = ActionNameInput.Text;
            if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
            {
                var actionItem = JsonConvert.DeserializeObject<ActionItem>(UserTouchInputText);

                var actionModifier = new ActionModifier
                {
                    ModifierName = ModifierNameEntry.Text,
                    Description = DescriptionEntry.Text,
                    Priority = int.TryParse(PriorityEntry.Text, out int priority) ? priority : 0,
                };

                var existingActionGroup = Data.FirstOrDefault(ag => ag.Data.ActionGroupObject.ActionName == actionName);

                if (existingActionGroup != null)
                {
                    existingActionGroup.Data.ActionGroupObject.ActionArray.Add(actionItem);

                    if (!existingActionGroup.Data.ActionGroupObject.ActionModifiers.Any(am => am.ModifierName == actionModifier.ModifierName))
                    {
                        existingActionGroup.Data.ActionGroupObject.ActionModifiers.Add(actionModifier);
                    }

                    DebugOutput($"Updated Action Group: {UserTouchInputText}");
                }
                else
                {
                    var newActionGroup = new ActionGroup
                    {
                        ActionName = actionName,
                        ActionArray = new List<ActionItem> { actionItem },
                        ActionModifiers = new List<ActionModifier> { actionModifier },
                        IsSimulating = false
                    };
                    Data.Add(new DataItem { Data = new DataObject { ActionGroupObject = newActionGroup } });
                    DebugOutput($"Saved Action Group: {actionName}");
                }

                _recordedActions.Clear();
            }
        }

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
                var currentTime = DateTime.UtcNow.ToString("o");
                var actionItem = new ActionItem
                {
                    Timestamp = DateTime.Parse(currentTime)
                };

                if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    var currentMouseEventTime = DateTime.UtcNow;
                    if ((currentMouseEventTime - lastMouseEventTime).TotalMilliseconds < 500)
                    {
                        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                    }
                    lastMouseEventTime = currentMouseEventTime;

                    GetCursorPos(out POINT currentMousePos);
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = WM_MOUSEMOVE;
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    GetCursorPos(out POINT currentMousePos);
                    var buttonCode = (wParam == (IntPtr)WM_LBUTTONDOWN) ? (ushort)WM_LBUTTONDOWN : (ushort)WM_RBUTTONDOWN;
                    _mouseLeftButtonDownTimestamp = DateTime.UtcNow;
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;
                    actionItem.Duration = 0;

                    if (!_activeKeyPresses.ContainsKey(buttonCode))
                    {
                        _activeKeyPresses[buttonCode] = actionItem;
                    }

                    UpdateUI();
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_RBUTTONUP)
                {
                    GetCursorPos(out POINT currentMousePos);
                    var duration = (DateTime.UtcNow - _mouseLeftButtonDownTimestamp).TotalMilliseconds;
                    actionItem.Duration = duration > 0 ? (int)duration : 1;
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;

                    var buttonCode = (wParam == (IntPtr)WM_LBUTTONUP) ? (ushort)WM_LBUTTONDOWN : (ushort)WM_RBUTTONDOWN;

                    _activeKeyPresses.Remove(buttonCode);

                    UpdateUI();
                }

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = (ushort)vkCode;

                    if (!_activeKeyPresses.ContainsKey(actionItem.KeyCode))
                    {
                        _keyPressDownTimestamps[(ushort)vkCode] = DateTime.UtcNow;
                        actionItem.EventType = WM_KEYDOWN;
                        actionItem.Duration = 0;

                        _activeKeyPresses[actionItem.KeyCode] = actionItem;

                        UpdateUI();
                    }
                    else
                    {
                        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = (ushort)vkCode;

                    if (_keyPressDownTimestamps.TryGetValue((ushort)vkCode, out DateTime keyDownTimestamp))
                    {
                        var duration = (DateTime.UtcNow - keyDownTimestamp).TotalMilliseconds;
                        actionItem.Duration = duration > 0 ? (int)duration : 1;
                        actionItem.EventType = WM_KEYUP;

                        _activeKeyPresses.Remove(actionItem.KeyCode);
                        _keyPressDownTimestamps.Remove((ushort)vkCode);

                        UpdateUI();
                    }
                }

                UserTouchInputText = JsonConvert.SerializeObject(actionItem);
                DebugOutput(UserTouchInputText);
                SaveAction();
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void UpdateUI()
        {
            Dispatcher.Dispatch(() =>
            {
                var activeInputsDisplay = new StringBuilder();
                activeInputsDisplay.AppendLine("Active Key/Mouse Presses:");

                foreach (var kvp in _activeKeyPresses)
                {
                    var keycode = kvp.Key;

                    activeInputsDisplay.AppendLine($"KeyCode/MouseCode: {keycode}");
                }

                ButtonLabel.Text = activeInputsDisplay.ToString();
            });
        }

        private async Task SaveLocalRichDataAsync()
        {
            try
            {
                await _fileService.SaveLocalDataItemsAsync(Data.ToList());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving local rich data: {ex.Message}");
            }
        }

        private async Task LoadLocalRichDataAsync()
        {
            try
            {
                var localData = await _fileService.LoadLocalDataItemsAsync();
                Data.Clear();
                foreach (var item in localData) Data.Add(item);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading local rich data: {ex.Message}");
            }
        }
    }
}
