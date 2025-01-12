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

        private DateTime _mouseLeftButtonDownTimestamp;
        // private DateTime _mouseRightButtonDownTimestamp;
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
#endif
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
        private async void CheckUserLoggedIn()
        {
            if (!await IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                NavigateLogin();
            }
            if (await IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is logged in.");
            }
            else
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                NavigateLogin();
            }
        }

        async void NavigateLogin()
        {
            try
            {
                await Shell.Current.GoToAsync($"///login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to login: {ex.Message}");
            }
        }
        private void OnInputModifierClicked(object sender, EventArgs e)
        {
            InputModifierPopup.IsVisible = true;
        }

        private void OnOkayClicked(object sender, EventArgs e)
        {
            InputModifierPopup.IsVisible = false;
        }
        private void ToggleAllOutputs(bool value)
        {
            if (value)
            {
                TogglePCVisualOutput();
                TogglePCAudibleOutput();
                ToggleUserVisualOutput();
                ToggleUserAudibleOutput();
                ToggleUserTouchOutput();
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
        private async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                // Retrieve stored token
                var userToken = await SecureStorage.GetAsync("userToken");

                // Check if token exists and is not empty
                if (!string.IsNullOrEmpty(userToken))
                {
                    Debug.WriteLine("User token found: " + userToken);
                    return true; // User is logged in
                }
                else
                {
                    Debug.WriteLine("No user token found.");
                    return false; // User is not logged in
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user token: {ex.Message}");
                return false;
            }
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
            // Get the native window handle (HWND) for Windows platform
            var windowHandler = Microsoft.Maui.Controls.Application.Current.Windows[0].Handler;
            if (windowHandler.PlatformView is Microsoft.UI.Xaml.Window window)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                _mouseTrackingService.StartTracking(hwnd);
                GlobalInputCapture.StartHooks();
            }
#else
            // For non-Windows platforms (optional)
            _mouseTrackingService.StartTracking(IntPtr.Zero);
#endif
        }

        private void StopTracking()
        {
            _mouseTrackingService.StopTracking();
            GlobalInputCapture.StopHooks();
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
        private CancellationTokenSource _pcVisualCancellationTokenSource;

        private void TogglePCVisualOutput() // webcam image: record what the webcam sees
        {
            if (PCVisualButtonText == "Read")
            {
                // Start capturing
                PCVisualButtonText = "Stop";
                DebugOutput($"PC Visual Output: {PCVisualButtonText}");
                OnPropertyChanged(nameof(PCVisualButtonText));

                _pcVisualCancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _pcVisualCancellationTokenSource.Token;

                Task.Run(() => CaptureWebcamImages(cancellationToken), cancellationToken);
            }
            else
            {
                // Stop capturing
                PCVisualButtonText = "Read";
                DebugOutput($"PC Visual Output: {PCVisualButtonText}");
                OnPropertyChanged(nameof(PCVisualButtonText));

                _pcVisualCancellationTokenSource?.Cancel();
            }
        }

        private void CaptureWebcamImages(CancellationToken cancellationToken)
        {
            using var capture = new VideoCapture(0); // 0 is the default camera
            using var frame = new Mat();

            if (!capture.IsOpened())
            {
                Console.WriteLine("Failed to open webcam.");
                return;
            }

            // Define the directory path for saving webcam images
            string webcamImagesDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "WebcamImages");

            // Ensure the directory exists
            Directory.CreateDirectory(webcamImagesDirectory);

            while (!cancellationToken.IsCancellationRequested) // Loop to capture frames continuously
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

                    // Update the CapturedImageSource property
                    Dispatcher.Dispatch(() =>
                    {
                        DebugOutput($"Captured image source updated: {filePath}");
                        CapturedImageSource = ImageSource.FromFile(filePath);
                    });
                }
                // Sleep for a short duration to avoid capturing too many frames
                Thread.Sleep(1000); // Adjust the interval as needed
            }
        }

        private void TogglePCAudibleOutput() // Audio Recorder: record the sounds that the computer outputs
        {
            PCAudibleButtonText = PCAudibleButtonText == "Read" ? "Stop" : "Read";
            DebugOutput($"PC Audible Output: {PCAudibleButtonText}");
            OnPropertyChanged(nameof(PCAudibleButtonText));

            if (PCAudibleButtonText == "Stop")
            {
                // Start recording
                try
                {
                    // Define the directory path for saving PC audio
                    string pcAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "PCAudio");
                    DebugOutput($"PC Audio Directory: {pcAudioDirectory}");

                    // Ensure the directory exists
                    Directory.CreateDirectory(pcAudioDirectory);
                    string actionName = ActionNameInput.Text;

                    if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
                    {
                        // Define the output file path once when recording starts
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
                            Console.WriteLine($"Recording saved to: {filePath}");
                            AddFileToLastItem(filePath);
                            // Extract MFCCs
                            ExtractMFCCs(filePath);
                        };

                        _loopbackCapture.StartRecording();
                        Console.WriteLine("Recording PC audio...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }
            else
            {
                // Stop recording
                _loopbackCapture?.StopRecording();
                Console.WriteLine("Stopped recording PC audio.");
            }
        }

        private void ExtractMFCCs(string filePath)
        {
            try
            {
                // Read the recorded audio file
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

                // Define MFCC extractor parameters
                int sampleRate = waveFile.WaveFormat.SampleRate;
                int featureCount = 13; // Number of MFCC coefficients
                int frameSize = 512; // Frame size in samples
                int hopSize = 256; // Hop size in samples

                // Create MFCC extractor
                var mfccExtractor = new MfccExtractor(new MfccOptions
                {
                    SamplingRate = sampleRate,
                    FeatureCount = featureCount,
                    FrameSize = frameSize,
                    HopSize = hopSize
                });

                // Extract MFCCs
                var mfccs = mfccExtractor.ComputeFrom(samples);

                // Define the output file path for MFCCs
                string mfccFilePath = Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_MFCCs.csv");

                // Save MFCCs to a CSV file
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

        private CancellationTokenSource _userVisualCancellationTokenSource;

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
            // Define the directory path for saving screenshots
            string screenshotsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Screenshots");

            // Ensure the directory exists
            Directory.CreateDirectory(screenshotsDirectory);

            while (!cancellationToken.IsCancellationRequested) // Loop to capture screens continuously
            {
                try
                {
                    DateTime captureTime = DateTime.Now; // Capture the time at the beginning of the loop

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
                                // Generate the file path using the captured time
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

                // Sleep for a short duration to avoid capturing too many frames
                Thread.Sleep(1000); // Adjust the interval as needed
            }
        }

        private void ToggleUserAudibleOutput() // webcam audio: record what the webcam hears
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
                    // Define the directory path for saving webcam audio
                    string webcamAudioDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "WebcamAudio");

                    // Ensure the directory exists
                    Directory.CreateDirectory(webcamAudioDirectory);

                    // Define the output file path once when recording starts
                    string filePath = Path.Combine(webcamAudioDirectory, $"WebcamAudio_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

                    _waveIn = new WaveInEvent();

                    // Find the correct input device (your webcam's audio input)
                    var deviceNumber = FindWebcamAudioDevice();
                    if (deviceNumber == -1)
                    {
                        Console.WriteLine("Webcam audio device not found.");
                        return;
                    }
                    string actionName = ActionNameInput.Text;
                    if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(UserTouchInputText))
                    {
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
                            AddFileToLastItem(filePath);
                            Data.Add(new DataItem
                            {
                                Data = new DataObject
                                {
                                    Text = "Webcam Audio",
                                    Files = new List<FileItem> { new FileItem { filename = Path.GetFileName(filePath),
                                contentType = "audio/wav",
                                data = Convert.ToBase64String(File.ReadAllBytes(filePath)) } }
                                }
                            });
                            // Extract MFCCs
                            ExtractMFCCs(filePath);
                        };

                        _waveIn.StartRecording();
                        Console.WriteLine("Recording webcam audio...");
                    }
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
                await SaveNewActionGroup(); // Save the last ActionGroup to the backend
                await SaveDataItemsToFile(); // Save the updated ActionGroups list to the file
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
                var dataItemFiles = Data.Last().Data.Files; // type List<FileItem>
                DebugOutput($"User ID: {userId}, "
                    + $"Action Group: {JsonConvert.SerializeObject(actionGroupObject)}, "
                    + $"Files: {JsonConvert.SerializeObject(dataItemFiles)}");

                var dataItem = new DataObject
                {
                    Text = "Creator:" + userId + "|Action:" + (actionGroupObject.ActionName != null ? actionGroupObject.ActionName : "No Action Name"),
                    ActionGroupObject = actionGroupObject,
                    Files = dataItemFiles // type List<FileItem>
                };

                var response = await _dataService.CreateDataAsync(dataItem, token);
                var serializedData = response.Data != null && response.Data.Any()
                    ? JsonConvert.SerializeObject(response.Data)
                    : "No data available";

                DebugOutput($"4. (ObservePage.SaveNew) New Action Group Saved to Backend: {serializedData}");
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
                // Convert the UserTouchInputText to the new ActionItem format
                var actionItem = JsonConvert.DeserializeObject<ActionItem>(UserTouchInputText);

                // Create ActionModifier from frontend inputs
                var actionModifier = new ActionModifier
                {
                    ModifierName = ModifierNameEntry.Text,
                    Description = DescriptionEntry.Text,
                    Priority = int.TryParse(PriorityEntry.Text, out int priority) ? priority : 0,
                    // Condition = item => true, // Placeholder, you need to parse ConditionEntry.Text to a valid Func<ActionArrayItem, int>
                    // ModifyAction = item => { } // Placeholder, you need to parse ModifyActionEntry.Text to a valid Action<ActionArrayItem>
                };

                // Check if an ActionGroup with the same name already exists
                var existingActionGroup = Data.FirstOrDefault(ag => ag.Data.ActionGroupObject.ActionName == actionName);

                if (existingActionGroup != null)
                {
                    // If it exists, append the new action item to the existing ActionArray
                    existingActionGroup.Data.ActionGroupObject.ActionArray.Add(actionItem);

                    // Check if the ActionModifier already exists before adding it
                    if (!existingActionGroup.Data.ActionGroupObject.ActionModifiers.Any(am => am.ModifierName == actionModifier.ModifierName))
                    {
                        existingActionGroup.Data.ActionGroupObject.ActionModifiers.Add(actionModifier);
                    }

                    DebugOutput($"Updated Action Group: {UserTouchInputText}");
                }
                else
                {
                    // If it doesn't exist, create a new ActionGroup and add it to the list
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
        // Dictionary to track active key presses and mouse button presses with a duration of 0
        private Dictionary<ushort, ActionItem> _activeKeyPresses = new Dictionary<ushort, ActionItem>();

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var currentTime = DateTime.UtcNow.ToString("o"); // Using ISO 8601 format
                var actionItem = new ActionItem
                {
                    Timestamp = DateTime.Parse(currentTime)
                };

                // Process mouse events
                if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    var currentMouseEventTime = DateTime.UtcNow;
                    if ((currentMouseEventTime - lastMouseEventTime).TotalMilliseconds < 500) // Example: 100 milliseconds
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

                    // Track active mouse button press
                    if (!_activeKeyPresses.ContainsKey(buttonCode))
                    {
                        _activeKeyPresses[buttonCode] = actionItem;
                    }

                    UpdateUI(); // Update the UI with the active key/mouse buttons
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_RBUTTONUP)
                {
                    GetCursorPos(out POINT currentMousePos);
                    var duration = (DateTime.UtcNow - _mouseLeftButtonDownTimestamp).TotalMilliseconds;
                    actionItem.Duration = duration > 0 ? (int)duration : 1;
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;

                    var buttonCode = (wParam == (IntPtr)WM_LBUTTONUP) ? (ushort)WM_LBUTTONDOWN : (ushort)WM_RBUTTONDOWN;

                    // Remove the mouse button press from active presses
                    _activeKeyPresses.Remove(buttonCode);

                    UpdateUI(); // Update UI after removing the mouse button
                }

                // Process keyboard events
                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = (ushort)vkCode;

                    // Check if the key is already being pressed (active) with duration 0
                    if (!_activeKeyPresses.ContainsKey(actionItem.KeyCode))
                    {
                        _keyPressDownTimestamps[(ushort)vkCode] = DateTime.UtcNow;
                        actionItem.EventType = WM_KEYDOWN;
                        actionItem.Duration = 0; // Active press (ongoing)

                        // Add the actionArrayItem to track it as active
                        _activeKeyPresses[actionItem.KeyCode] = actionItem;

                        UpdateUI(); // Update UI to display active key presses
                    }
                    else
                    {
                        // Skip recording another keydown with a duration of 0
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

                        // Remove from active key presses once the key is released
                        _activeKeyPresses.Remove(actionItem.KeyCode);
                        _keyPressDownTimestamps.Remove((ushort)vkCode);

                        UpdateUI(); // Update UI after removing the key press
                    }
                }

                // Serialize and save the action
                UserTouchInputText = JsonConvert.SerializeObject(actionItem);
                DebugOutput(UserTouchInputText);
                SaveAction();
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        // Helper function to update the UI with the current active keys and buttons
        private void UpdateUI()
        {
            Dispatcher.Dispatch(() =>
            {
                // Create a formatted string to display active key presses and mouse buttons
                var activeInputsDisplay = new StringBuilder();
                activeInputsDisplay.AppendLine("Active Key/Mouse Presses:");

                foreach (var kvp in _activeKeyPresses)
                {
                    var keycode = kvp.Key;

                    // Format each keycode and mouse event with relevant details
                    activeInputsDisplay.AppendLine($"KeyCode/MouseCode: {keycode}");
                }

                // Update the UI elements with the active key/mouse presses
                ButtonLabel.Text = activeInputsDisplay.ToString(); // Display the active key presses in the ButtonLabel
            });
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
        private void AddFileToLastItem(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            if (!Data.Any()) return;
            if (Data.Last().Data.Files == null)
                Data.Last().Data.Files = new List<FileItem>();

            Data.Last().Data.Files.Add(new FileItem { filename = Path.GetFileName(filePath), 
                contentType = "image/png", 
                // Data = "Convert.ToBase64String(File.ReadAllBytes(filePath)) "});
                data = Convert.ToBase64String(File.ReadAllBytes(filePath)) });
            Debug.WriteLine($"File added: {filePath}");
        }
    }
}
