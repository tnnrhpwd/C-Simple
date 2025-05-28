using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;
using System.IO;

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

                    // Enable previews when "Record All" is toggled on
                    if (value && CapturePreviewCard != null)
                    {
                        // Enable preview regardless of previous user preference
                        CapturePreviewCard.IsPreviewEnabled = true;
                        CapturePreviewCard.IsUserToggledOff = false;
                    }

                    ToggleAllOutputs(value);
                }
            }
        }

        public ObservableCollection<DataItem> Data { get; set; } = new();
        private CancellationTokenSource _userVisualCts, _pcVisualCts;

        // Button states
        public string PCVisualButtonText { get; set; } = "Read";
        public string PCAudibleButtonText { get; set; } = "Read";
        public string UserVisualButtonText { get; set; } = "Read";
        public string UserAudibleButtonText { get; set; } = "Read";
        public string UserTouchButtonText { get; set; } = "Read";
        public string UserTouchInputText { get; set; } = "";
        public ImageSource CapturedImageSource { get; set; }

        // Services
        private readonly InputCaptureService _inputService;
        private readonly ScreenCaptureService _screenService;
        private readonly AudioCaptureService _audioService;
        private readonly ObserveDataService _dataService;
        private readonly MouseTrackingService _mouseService;
        private readonly ActionService _actionService; // Add ActionService

        // Commands
        public ICommand TogglePCVisualCommand { get; }
        public ICommand TogglePCAudibleCommand { get; }
        public ICommand ToggleUserVisualCommand { get; }
        public ICommand ToggleUserAudibleCommand { get; }
        public ICommand ToggleUserTouchCommand { get; }
        public ICommand SaveActionCommand { get; }
        public ICommand SaveToFileCommand { get; }
        public ICommand LoadFromFileCommand { get; }

        // Save options - Change defaults to true to ensure actions are always saved
        private bool _saveRecord = true; // Changed from false to true
        public bool SaveRecord
        {
            get => _saveRecord;
            set
            {
                if (_saveRecord != value)
                {
                    _saveRecord = value;
                    OnPropertyChanged();

                    // If not saving at all, disable the other options
                    if (!value)
                    {
                        SaveLocally = false;
                        UploadToBackend = false;
                    }
                }
            }
        }

        private bool _saveLocally = true; // Changed from false to true
        public bool SaveLocally
        {
            get => _saveLocally;
            set
            {
                if (_saveLocally != value)
                {
                    _saveLocally = value;
                    OnPropertyChanged();

                    // If saving locally, we need to save the record
                    if (value && !SaveRecord)
                        SaveRecord = true;
                }
            }
        }

        private bool _uploadToBackend = false;
        public bool UploadToBackend
        {
            get => _uploadToBackend;
            set
            {
                if (_uploadToBackend != value)
                {
                    _uploadToBackend = value;
                    OnPropertyChanged();

                    // If uploading to backend, we need to save the record
                    if (value && !SaveRecord)
                        SaveRecord = true;
                }
            }
        }

        // Audio levels for progress bars
        private float _pcAudioLevel;
        public float PCAudioLevel
        {
            get => _pcAudioLevel;
            set
            {
                if (_pcAudioLevel != value)
                {
                    _pcAudioLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _userAudioLevel;
        public float UserAudioLevel
        {
            get => _userAudioLevel;
            set
            {
                if (_userAudioLevel != value)
                {
                    _userAudioLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        // Add new properties for progress bar levels
        private float _userTouchLevel = 0.0f;
        public float UserTouchLevel
        {
            get => _userTouchLevel;
            set
            {
                if (_userTouchLevel != value)
                {
                    _userTouchLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _userVisualLevel = 0.0f;
        public float UserVisualLevel
        {
            get => _userVisualLevel;
            set
            {
                if (_userVisualLevel != value)
                {
                    _userVisualLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        private float _pcVisualLevel = 0.0f;
        public float PCVisualLevel
        {
            get => _pcVisualLevel;
            set
            {
                if (_pcVisualLevel != value)
                {
                    _pcVisualLevel = value;
                    OnPropertyChanged();
                }
            }
        }

        // Scaled version of PC audio level to prevent always showing max
        public float PCAudioScaledLevel => Math.Min(PCAudioLevel * 0.5f, 1.0f);

        // Debouncing timer
        private Timer _debounceTimer;
        private const int DebounceInterval = 200; // milliseconds

        // Add flag to track if we're currently in recording mode
        private bool _isRecording = false;
        private List<ActionItem> _currentRecordingBuffer = new List<ActionItem>();

        // New helper property to track the last recorded action for deduplication
        private ActionItem _lastRecordedAction;

        // Modify the constructor to accept ActionService
        public ObservePage(InputCaptureService inputService, ScreenCaptureService screenService,
                           AudioCaptureService audioService, ObserveDataService dataService,
                           MouseTrackingService mouseService, ActionService actionService)
        {
            InitializeComponent();

            // Init services
            _inputService = inputService;
            _screenService = screenService;
            _audioService = audioService;
            _dataService = dataService;
            _mouseService = mouseService;
            _actionService = actionService; // Assign ActionService

            // Set up events
            _inputService.InputCaptured += OnInputCaptured;
            _screenService.FileCaptured += OnFileCaptured;
            _audioService.FileCaptured += OnFileCaptured;
            _audioService.PCLevelChanged += level => Dispatcher.Dispatch(() => PCAudioLevel = level);
            _audioService.WebcamLevelChanged += level => Dispatcher.Dispatch(() => UserAudioLevel = level);
            _mouseService.MouseMoved += OnMouseMoved;

            // Add preview frame handlers
            _screenService.ScreenPreviewFrameReady += OnScreenPreviewFrameReady;
            _screenService.WebcamPreviewFrameReady += OnWebcamPreviewFrameReady;

            // Init commands with visual state handling
            TogglePCVisualCommand = new Command(() =>
            {
                PCVisualButtonText = ToggleOutput(PCVisualButtonText, StartPCVisual, StopPCVisual);
                OnPropertyChanged(nameof(PCVisualButtonText));
                UpdateButtonVisualState(PCVisualButtonText);
            });

            TogglePCAudibleCommand = new Command(() =>
            {
                PCAudibleButtonText = ToggleOutput(PCAudibleButtonText,
                    () => _audioService.StartPCAudioRecording(SaveRecord),
                    _audioService.StopPCAudioRecording);
                OnPropertyChanged(nameof(PCAudibleButtonText));
                UpdateButtonVisualState(PCAudibleButtonText);
            });

            ToggleUserVisualCommand = new Command(() =>
            {
                UserVisualButtonText = ToggleOutput(UserVisualButtonText, StartUserVisual, StopUserVisual);
                OnPropertyChanged(nameof(UserVisualButtonText));
                UpdateButtonVisualState(UserVisualButtonText);
            });

            ToggleUserAudibleCommand = new Command(() =>
            {
                UserAudibleButtonText = ToggleOutput(UserAudibleButtonText,
                    () => _audioService.StartWebcamAudioRecording(SaveRecord),
                    _audioService.StopWebcamAudioRecording);
                OnPropertyChanged(nameof(UserAudibleButtonText));
                UpdateButtonVisualState(UserAudibleButtonText);
            });

            ToggleUserTouchCommand = new Command(() =>
            {
                UserTouchButtonText = ToggleOutput(UserTouchButtonText, StartUserTouch, StopUserTouch);
                OnPropertyChanged(nameof(UserTouchButtonText));
                UpdateButtonVisualState(UserTouchButtonText);
            });

            SaveActionCommand = new Command(SaveAction, () => !string.IsNullOrEmpty(ActionConfigCard?.ActionName));
            SaveToFileCommand = new Command(async () => await SaveDataItemsToFile());
            LoadFromFileCommand = new Command(async () => await LoadDataItemsFromFile());

            CheckUserLoggedIn();
            BindingContext = this;
        }

        private void UpdateButtonVisualState(string buttonText)
        {
            // This would normally update button styles based on state
            // The style changes are handled by XAML in this redesign
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            try
            {
                // Load data items from file to ensure local actions are available
                await LoadDataItemsFromFile();

                // Add welcome animation
                await this.FadeTo(0, 0);
                await this.FadeTo(1, 400, Easing.CubicOut);

                // Register for preview toggle events
                if (CapturePreviewCard != null)
                {
                    CapturePreviewCard.PreviewEnabledChanged += OnPreviewEnabledChanged;

                    // Ensure preview is disabled by default
                    CapturePreviewCard.IsPreviewEnabled = false;
                    CapturePreviewCard.IsUserToggledOff = true;
                }

                // Start preview sources with a slight delay to ensure UI is ready
                await Task.Delay(500);
                UpdatePreviewSources(true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading ObservePage: {ex.Message}");
            }
        }

        protected override async void OnDisappearing()
        {
            // Stop all captures first
            StopAllCaptures();
            UpdatePreviewSources(false);

            // Always try to save data before navigating away, regardless of SaveLocally flag
            // This ensures we don't lose data when changing pages
            try
            {
                if (Data.Count > 0)
                {
                    Debug.WriteLine("OnDisappearing: Saving all data before navigation");
                    await _dataService.SaveDataItemsToFile(Data.ToList());
                    await _dataService.SaveLocalRichDataAsync(Data.ToList());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data on page disappearing: {ex.Message}");
            }

            base.OnDisappearing();
        }

        private void OnPreviewEnabledChanged(object sender, bool isEnabled)
        {
            // When preview is disabled, we'll still keep the preview service running
            // but the CapturePreviewCard will not update its UI elements
            Debug.WriteLine($"Preview toggled to: {isEnabled}");

            // Always track the user toggle state
            if (CapturePreviewCard != null)
            {
                CapturePreviewCard.IsUserToggledOff = !isEnabled && CapturePreviewCard.IsToggleChangedByUser;
                Debug.WriteLine($"CapturePreviewCard.IsUserToggledOff set to: {CapturePreviewCard.IsUserToggledOff}");
            }

            // Optionally pause/resume preview generation to save resources
            if (_screenService != null)
            {
                if (isEnabled)
                {
                    // Force refresh of previews when enabled
                    RefreshPreviewFrames();
                }
            }
        }

        // Update the ToggleOutput method to return a string instead of using ref
        private string ToggleOutput(string buttonText, Action startAction, Action stopAction)
        {
            if (buttonText == "Read")
            {
                buttonText = "Stop";
                startAction();

                // Update visual levels when starting
                if (startAction == StartUserVisual)
                {
                    UserVisualLevel = 1.0f;
                    // Enable previews when starting screen capture
                    EnablePreviewIfNeeded();
                }
                else if (startAction == StartPCVisual)
                {
                    PCVisualLevel = 1.0f;
                    // Enable previews when starting webcam capture
                    EnablePreviewIfNeeded();
                }
                else if (startAction == StartUserTouch)
                {
                    // Set recording flag when starting touch capture
                    _isRecording = true;
                    _currentRecordingBuffer.Clear(); // Clear previous recording buffer
                }
            }
            else
            {
                buttonText = "Read";
                stopAction();

                // Update visual levels when stopping
                if (stopAction == StopUserVisual)
                {
                    UserVisualLevel = 0.0f;
                    // Check if we should disable previews
                    CheckAndDisablePreviewIfNeeded();
                }
                else if (stopAction == StopPCVisual)
                {
                    PCVisualLevel = 0.0f;
                    // Check if we should disable previews
                    CheckAndDisablePreviewIfNeeded();
                }
                else if (stopAction == StopUserTouch)
                {
                    // Reset recording flag when stopping touch capture
                    _isRecording = false;
                }
            }
            return buttonText;
        }

        // Helper method to enable previews when visual captures are active
        private void EnablePreviewIfNeeded()
        {
            if (CapturePreviewCard != null)
            {
                // Only enable if the user hasn't explicitly disabled it with the toggle
                if (!CapturePreviewCard.IsUserToggledOff)
                {
                    // Enable preview if it's not already enabled
                    if (!CapturePreviewCard.IsPreviewEnabled)
                    {
                        CapturePreviewCard.IsPreviewEnabled = true;
                        // StartPreviewMode handled by the property change event
                    }
                }
            }
        }

        // Helper method to check if we should disable previews
        private void CheckAndDisablePreviewIfNeeded()
        {
            // Only disable previews if both visual captures are off and we're not toggled to "Record All"
            if (!IsRecordingActive && !IsReadAllToggled)
            {
                if (CapturePreviewCard != null && !CapturePreviewCard.IsUserToggledOff)
                {
                    CapturePreviewCard.IsPreviewEnabled = false;
                }
            }
        }

        // Add properties for tracking active streams
        public bool IsRecordingActive =>
            UserVisualButtonText == "Stop" ||
            PCVisualButtonText == "Stop";

        private void StartPCVisual()
        {
            _pcVisualCts = new CancellationTokenSource();
            Debug.WriteLine("[ObservePage] StartPCVisual - Calling _screenService.StartWebcamCapture");
            Task.Run(() => _screenService.StartWebcamCapture(_pcVisualCts.Token, ActionConfigCard?.ActionName, UserTouchInputText), _pcVisualCts.Token);
            Debug.WriteLine("[ObservePage] StartPCVisual - _screenService.StartWebcamCapture task started"); // Added debug print
        }

        private void StopPCVisual() => _pcVisualCts?.Cancel();

        private void StartUserVisual()
        {
            _userVisualCts = new CancellationTokenSource();
            Task.Run(() => _screenService.StartScreenCapture(_userVisualCts.Token, ActionConfigCard?.ActionName, UserTouchInputText), _userVisualCts.Token);
            Debug.WriteLine("[ObservePage] StartUserVisual - _screenService.StartScreenCapture task started"); // Added debug print
        }

        private void StopUserVisual() => _userVisualCts?.Cancel();

        // Updated StartUserTouch method to reset touch level and recording state
        private void StartUserTouch()
        {
            _inputService.StartCapturing();
            // Start mouse tracking with the window handle
            Microsoft.UI.Xaml.Window window = (Microsoft.UI.Xaml.Window)App.Current.Windows.First().Handler.PlatformView;
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            _mouseService.StartTracking(hwnd);
            UserTouchLevel = 0.0f; // Reset to zero when starting

            // Reset recording state
            _currentRecordingBuffer = new List<ActionItem>(10000); // Pre-allocate larger capacity
            _lastRecordedAction = null; // Reset last action for deduplication
            Debug.WriteLine("Started input recording with high-frequency mouse tracking");
        }

        private async void StopUserTouch()
        {
            _inputService.StopCapturing();
            _mouseService.StopTracking();
            _isRecording = false; // Ensure recording flag is set to false

            Debug.WriteLine($"Stopping touch recording. Buffer has {_currentRecordingBuffer.Count} items.");
            Debug.WriteLine($"ActionName={ActionConfigCard?.ActionName ?? "null"}, SaveRecord={SaveRecord}, SaveLocally={SaveLocally}");

            // Count keyboard events for diagnostics
            int keyDownEvents = _currentRecordingBuffer.Count(a => a.EventType == 0x0100);
            int keyUpEvents = _currentRecordingBuffer.Count(a => a.EventType == 0x0101);
            Debug.WriteLine($"Keyboard events in buffer - Down: {keyDownEvents}, Up: {keyUpEvents}");

            try
            {
                // Only save if we're saving records or if we have keyboard events (always save keyboard events)
                if (SaveRecord || keyDownEvents > 0 || keyUpEvents > 0)
                {
                    // Process all buffered actions at once now that we're done recording
                    await SaveAllBufferedActions();

                    // Ensure actions are saved to the file system before navigating
                    if (SaveLocally)
                    {
                        Debug.WriteLine("Explicitly saving to local files...");
                        await SaveDataItemsToFile();
                    }

                    // Log the final state of Data collection
                    Debug.WriteLine($"Data collection now has {Data.Count} items");
                }
                else
                {
                    Debug.WriteLine("Skipping save because SaveRecord is false and no keyboard events detected");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in StopUserTouch: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private void OnInputCaptured(string inputJson)
        {
            try
            {
                // Process immediately instead of debouncing
                var actionItem = JsonConvert.DeserializeObject<ActionItem>(inputJson);

                // Check if it's a mouse movement event
                bool isMouseMovement = actionItem.EventType == 512 || // Mouse move
                                       actionItem.EventType == 0x0200; // WM_MOUSEMOVE

                // Always process keyboard events immediately without debouncing
                bool isKeyboardEvent = actionItem.EventType == 0x0100 || // WM_KEYDOWN
                                       actionItem.EventType == 0x0101;   // WM_KEYUP

                // Check if it's a mouse button event
                bool isMouseButtonEvent = IsMouseClickEvent(actionItem.EventType);

                if (isMouseMovement)
                {
                    // Process mouse movements directly without debounce
                    ProcessMouseMovement(actionItem);
                }
                else if (isKeyboardEvent)
                {
                    // Process keyboard events immediately to ensure they're recorded
                    ProcessKeyboardEvent(actionItem);
                }
                else if (isMouseButtonEvent)
                {
                    // Process mouse button events immediately to ensure they're recorded
                    ProcessMouseButtonEvent(actionItem);
                }
                else
                {
                    // Only debounce non-mouse, non-keyboard inputs
                    _debounceTimer?.Dispose();
                    _debounceTimer = new Timer(DebouncedInputCaptured, inputJson, DebounceInterval, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing input: {ex.Message}");
            }
        }

        // New method to process mouse button events directly
        private void ProcessMouseButtonEvent(ActionItem actionItem)
        {
            if (_isRecording)
            {
                // Always record mouse button events (never consider them duplicates)
                _currentRecordingBuffer.Add(actionItem);
                _lastRecordedAction = actionItem;

                // Update UI
                Dispatcher.Dispatch(() =>
                {
                    // Update touch level for visual feedback only
                    UserTouchLevel = 0.8f; // Show activity

                    if (CapturePreviewCard != null)
                    {
                        // Update input activity on the preview card
                        CapturePreviewCard.UpdateInputActivity((ushort)actionItem.EventType, true);
                    }

                    // Debug logging
                    Debug.WriteLine($"Directly recorded mouse button event: EventType: {actionItem.EventType}");
                });
            }
        }

        // New method to process keyboard events directly to ensure they're recorded
        private void ProcessKeyboardEvent(ActionItem actionItem)
        {
            if (_isRecording)
            {
                // Never consider keyboard events as duplicates - always record them
                _currentRecordingBuffer.Add(actionItem);
                _lastRecordedAction = actionItem;

                bool isKeyDown = actionItem.EventType == 0x0100; // WM_KEYDOWN

                // Update UI
                Dispatcher.Dispatch(() =>
                {
                    // Update touch level based on active key count
                    int activeKeyCount = _inputService.GetActiveKeyCount();
                    UserTouchLevel = Math.Min(activeKeyCount / 5.0f, 1.0f);

                    if (CapturePreviewCard != null)
                    {
                        CapturePreviewCard.UpdateInputActivity((ushort)actionItem.KeyCode, isKeyDown);
                    }

                    // Debug logging
                    Debug.WriteLine($"Directly recorded keyboard event: {(isKeyDown ? "DOWN" : "UP")} KeyCode: {actionItem.KeyCode}");
                });

                // For important key events, force save more frequently
                if (_currentRecordingBuffer.Count % 10 == 0 &&
                    IsKeyboardEvent(actionItem.EventType))
                {
                    // Consider periodic auto-saving for keyboard events
                    // This is optional but can help ensure keyboard events are saved
                    Dispatcher.Dispatch(async () =>
                    {
                        // We can optionally save periodically during recording
                        // but it might impact performance
                        // await SaveAllBufferedActions();
                    });
                }
            }
        }

        // New method to save all buffered actions at once
        private async Task SaveAllBufferedActions()
        {
            string actionName = ActionConfigCard?.ActionName;
            if (string.IsNullOrEmpty(actionName))
            {
                // Add a default name if none is provided
                actionName = $"Action-{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                Debug.WriteLine($"No action name provided, using default: {actionName}");
                // Update the ActionConfigCard if available
                if (ActionConfigCard != null)
                {
                    ActionConfigCard.ActionName = actionName;
                }
            }

            if (_currentRecordingBuffer.Count == 0)
            {
                Debug.WriteLine("Recording buffer is empty, nothing to save");
                return;
            }

            // Diagnostic information
            int keyDownEvents = _currentRecordingBuffer.Count(a => a.EventType == 0x0100);
            int keyUpEvents = _currentRecordingBuffer.Count(a => a.EventType == 0x0101);
            int mouseEvents = _currentRecordingBuffer.Count(a => a.EventType == 0x0200 || a.EventType == 512);
            Debug.WriteLine($"Event breakdown - KeyDown: {keyDownEvents}, KeyUp: {keyUpEvents}, Mouse: {mouseEvents}");

            // Create priority and modifier configuration
            int priority = 0;
            if (!string.IsNullOrEmpty(ActionConfigCard?.Priority))
            {
                int.TryParse(ActionConfigCard.Priority, out priority);
            }

            var actionModifier = new ActionModifier
            {
                ModifierName = ActionConfigCard?.ModifierName ?? string.Empty,
                Description = InputModifierPopupControl?.Description ?? string.Empty,
                Priority = priority
            };

            // Ensure unique identifier for the action group
            var newActionGroupId = Guid.NewGuid();
            var existingGroup = Data.FirstOrDefault(ag =>
                ag.Data?.ActionGroupObject?.ActionName == actionName);

            if (existingGroup != null)
            {
                // Update the existing action group with all buffered items
                existingGroup.Data.ActionGroupObject.ActionArray.AddRange(_currentRecordingBuffer);

                if (!existingGroup.Data.ActionGroupObject.ActionModifiers.Any(am => am.ModifierName == actionModifier.ModifierName))
                    existingGroup.Data.ActionGroupObject.ActionModifiers.Add(actionModifier);

                // Make sure it's marked as local
                existingGroup.Data.ActionGroupObject.IsLocal = true;

                // Make sure timestamp is updated
                existingGroup.updatedAt = DateTime.Now;

                Debug.WriteLine($"Updated Action Group with {_currentRecordingBuffer.Count} items");

                // Save the updated action
                await SaveNewActionToFile(existingGroup);
            }
            else
            {
                // Create a new action group with a unique ID and all buffered items
                var newActionGroup = new ActionGroup
                {
                    Id = newActionGroupId,
                    ActionName = actionName,
                    ActionArray = new List<ActionItem>(_currentRecordingBuffer), // Copy all buffered items
                    ActionModifiers = new List<ActionModifier> { actionModifier },
                    CreatedAt = DateTime.Now,
                    IsLocal = true  // Explicitly mark as local
                };

                var newItem = new DataItem
                {
                    Data = new DataObject { ActionGroupObject = newActionGroup },
                    createdAt = DateTime.Now,  // Set the timestamp on the DataItem
                    _id = newActionGroupId.ToString(), // Ensure the _id field is set
                    deleted = false // Explicitly set not deleted
                };

                Data.Add(newItem);

                Debug.WriteLine($"Created New Action Group: {actionName} with {_currentRecordingBuffer.Count} items");

                // Save the new action to files - only when explicitly stopping
                await SaveNewActionToFile(newItem);
            }

            // Clear buffer after saving
            _currentRecordingBuffer.Clear();
        }

        // Update StopAllCaptures method
        private void StopAllCaptures()
        {
            if (PCVisualButtonText == "Stop")
            {
                PCVisualButtonText = ToggleOutput(PCVisualButtonText, StartPCVisual, StopPCVisual);
                OnPropertyChanged(nameof(PCVisualButtonText));
            }
            if (PCAudibleButtonText == "Stop")
            {
                PCAudibleButtonText = ToggleOutput(PCAudibleButtonText,
                    () => _audioService.StartPCAudioRecording(SaveRecord),
                    _audioService.StopPCAudioRecording);
                OnPropertyChanged(nameof(PCAudibleButtonText));
            }
            if (UserVisualButtonText == "Stop")
            {
                UserVisualButtonText = ToggleOutput(UserVisualButtonText, StartUserVisual, StopUserVisual);
                OnPropertyChanged(nameof(UserVisualButtonText));
            }
            if (UserAudibleButtonText == "Stop")
            {
                UserAudibleButtonText = ToggleOutput(UserAudibleButtonText,
                    () => _audioService.StartWebcamAudioRecording(SaveRecord),
                    _audioService.StopWebcamAudioRecording);
                OnPropertyChanged(nameof(UserAudibleButtonText));
            }
            if (UserTouchButtonText == "Stop")
            {
                UserTouchButtonText = ToggleOutput(UserTouchButtonText, StartUserTouch, StopUserTouch);
                OnPropertyChanged(nameof(UserTouchButtonText));
            }
        }

        // Update ToggleAllOutputs method
        private async void ToggleAllOutputs(bool value)
        {
            if (value)
            {
                // First enable preview mode if Record All is enabled
                if (CapturePreviewCard != null)
                {
                    CapturePreviewCard.IsPreviewEnabled = true;
                    CapturePreviewCard.IsUserToggledOff = false;
                }

                // Then toggle all the recording buttons
                PCVisualButtonText = ToggleOutput(PCVisualButtonText, StartPCVisual, StopPCVisual);
                PCAudibleButtonText = ToggleOutput(PCAudibleButtonText,
                    () => _audioService.StartPCAudioRecording(SaveRecord),
                    _audioService.StopPCAudioRecording);
                UserVisualButtonText = ToggleOutput(UserVisualButtonText, StartUserVisual, StopUserVisual);
                UserAudibleButtonText = ToggleOutput(UserAudibleButtonText,
                    () => _audioService.StartWebcamAudioRecording(SaveRecord),
                    _audioService.StopWebcamAudioRecording);
                UserTouchButtonText = ToggleOutput(UserTouchButtonText, StartUserTouch, StopUserTouch);

                OnPropertyChanged(nameof(PCVisualButtonText));
                OnPropertyChanged(nameof(PCAudibleButtonText));
                OnPropertyChanged(nameof(UserVisualButtonText));
                OnPropertyChanged(nameof(UserAudibleButtonText));
                OnPropertyChanged(nameof(UserTouchButtonText));

                await _dataService.CompressAndUploadAsync(Data.ToList());

                // Update preview sources regardless of save settings
                UpdatePreviewSources(true);
            }
            else
            {
                StopAllCaptures();

                // Clear preview sources when stopping all recordings
                UpdatePreviewSources(false);
            }
        }

        private void UpdatePreviewSources(bool isActive)
        {
            if (CapturePreviewCard != null)
            {
                CapturePreviewCard.SetPreviewActive(isActive);
                Debug.WriteLine($"CapturePreviewCard.SetPreviewActive({isActive}) called");

                // Default to disabled on page load
                // Only auto-enable if user hasn't explicitly turned it off AND we have active captures
                if (isActive && !CapturePreviewCard.IsUserToggledOff && IsRecordingActive)
                {
                    CapturePreviewCard.IsPreviewEnabled = true;
                    Debug.WriteLine($"CapturePreviewCard.IsPreviewEnabled set to true (auto-enable)");
                }
                else if (!isActive)
                {
                    // When stopping all captures, respect user's toggle preference
                    if (!CapturePreviewCard.IsUserToggledOff)
                    {
                        CapturePreviewCard.IsPreviewEnabled = false;
                        Debug.WriteLine($"CapturePreviewCard.IsPreviewEnabled set to false (respecting user toggle)");
                    }
                }
                else
                {
                    Debug.WriteLine($"CapturePreviewCard.IsPreviewEnabled not changed");
                }

                // Start the capture service - it will always run, but preview updates depend on IsPreviewEnabled
                if (isActive)
                {
                    _screenService.StartPreviewMode();
                    Debug.WriteLine("Started screen and webcam preview service");
                }
                else
                {
                    _screenService.StopPreviewMode();
                    Debug.WriteLine("Stopped screen and webcam preview service");
                }
            }
            else
            {
                Debug.WriteLine("CapturePreviewCard is null in UpdatePreviewSources");
            }
        }

        private async void CheckUserLoggedIn()
        {
            if (!await _dataService.IsUserLoggedInAsync())
            {
                Debug.WriteLine("User not logged in, navigating to login...");
                await Shell.Current.GoToAsync("///login");
            }
        }

        private void OnInputModifierClicked(object sender, EventArgs e) => InputModifierPopupControl.Show();
        private void OnOkayClicked(object sender, EventArgs e) => InputModifierPopupControl.Hide();

        private void OnMouseMoved(Microsoft.Maui.Graphics.Point delta)
        {
            Dispatcher.Dispatch(() =>
            {
                if (CapturePreviewCard != null)
                {
                    CapturePreviewCard.UpdateMouseMovement(delta);
                }
                Debug.WriteLine($"Mouse Movement: X={delta.X}, Y={delta.Y}");
            });
        }

        private bool IsMouseClickEvent(int eventType)
        {
            return eventType == 0x0201 || // WM_LBUTTONDOWN
                   eventType == 0x0202 || // WM_LBUTTONUP
                   eventType == 0x0204 || // WM_RBUTTONDOWN
                   eventType == 0x0205 || // WM_RBUTTONUP
                   eventType == 0x0207 || // WM_MBUTTONDOWN
                   eventType == 0x0208;   // WM_MBUTTONUP
        }

        // Checks if two actions are duplicates (same properties)
        private bool IsDuplicateAction(ActionItem newAction, ActionItem lastAction)
        {
            if (lastAction == null)
                return false;

            // Always record mouse click events (never consider them duplicates)
            if (IsMouseClickEvent(newAction.EventType) || IsMouseClickEvent(lastAction.EventType))
                return false;

            // For mouse movements, compare important properties
            if (newAction.EventType == 512 && lastAction.EventType == 512)  // Mouse move
            {
                // Allow small movements to be filtered out (reduce noise)
                if (Math.Abs(newAction.DeltaX) <= 1 && Math.Abs(newAction.DeltaY) <= 1 &&
                    Math.Abs(lastAction.DeltaX) <= 1 && Math.Abs(lastAction.DeltaY) <= 1)
                {
                    return true; // Filter tiny movements as duplicates
                }

                // If exact same coordinates and delta, it's a duplicate
                if (newAction.Coordinates?.X == lastAction.Coordinates?.X &&
                    newAction.Coordinates?.Y == lastAction.Coordinates?.Y &&
                    newAction.DeltaX == lastAction.DeltaX &&
                    newAction.DeltaY == lastAction.DeltaY)
                {
                    return true;
                }
            }

            // For keyboard events, compare key code and event type
            if ((newAction.EventType == 256 || newAction.EventType == 257) &&
                (lastAction.EventType == 256 || lastAction.EventType == 257))
            {
                return newAction.EventType == lastAction.EventType &&
                       newAction.KeyCode == lastAction.KeyCode;
            }

            return false;
        }

        // Modified ProcessMouseMovement to prevent duplicate actions
        private void ProcessMouseMovement(ActionItem actionItem)
        {
            if (_isRecording)
            {
                // Check if this is a duplicate of the last recorded action
                bool isDuplicate = IsDuplicateAction(actionItem, _lastRecordedAction);

                if (!isDuplicate)
                {
                    // Add to recording buffer only if not a duplicate
                    _currentRecordingBuffer.Add(actionItem);
                    _lastRecordedAction = actionItem;

                    // Periodically update UI to show activity without slowing down recording
                    if (_currentRecordingBuffer.Count % 50 == 0)
                    {
                        Dispatcher.Dispatch(() =>
                        {
                            // Update touch level for visual feedback only
                            UserTouchLevel = 0.6f; // Show activity

                            // Update capture preview if available (minimal UI update)
                            if (CapturePreviewCard != null && CapturePreviewCard.IsPreviewEnabled)
                            {
                                CapturePreviewCard.UpdateMouseMovement(new Point(
                                    actionItem.DeltaX,
                                    actionItem.DeltaY
                                ));
                            }
                        });
                    }
                }
            }
        }

        private bool IsKeyboardEvent(int eventType)
        {
            // WM_KEYDOWN = 0x0100 (256)
            // WM_KEYUP = 0x0101 (257)
            return eventType == 256 || eventType == 257;
        }

        private async void DebouncedInputCaptured(object state)
        {
            string inputJson = (string)state;
            Dispatcher.Dispatch(async () =>
            {
                UserTouchInputText = inputJson;

                try
                {
                    var actionItem = JsonConvert.DeserializeObject<ActionItem>(inputJson);

                    // Always process keyboard events and non-mouse-move events
                    if (IsKeyboardEvent(actionItem.EventType) || actionItem.EventType != 512)
                    {
                        // Update touch level based on active key count
                        int activeKeyCount = _inputService.GetActiveKeyCount();
                        UserTouchLevel = Math.Min(activeKeyCount / 5.0f, 1.0f);

                        if (CapturePreviewCard != null)
                        {
                            bool isPressed = actionItem.EventType == 256; // WM_KEYDOWN
                            CapturePreviewCard.UpdateInputActivity((ushort)actionItem.KeyCode, isPressed);
                        }

                        // Add to recording buffer
                        if (_isRecording)
                        {
                            bool isDuplicate = IsDuplicateAction(actionItem, _lastRecordedAction);
                            if (!isDuplicate)
                            {
                                _currentRecordingBuffer.Add(actionItem);
                                _lastRecordedAction = actionItem;

                                // Debug logging
                                if (IsKeyboardEvent(actionItem.EventType))
                                {
                                    Debug.WriteLine($"Recorded keyboard event: {(actionItem.EventType == 256 ? "DOWN" : "UP")} KeyCode: {actionItem.KeyCode}");
                                }
                            }
                        }
                    }
                    // Handle mouse events
                    else if (actionItem.EventType == 512) // Mouse move
                    {
                        // ... existing mouse handling code ...
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating input visualization: {ex.Message}");
                }
            });
        }

        private void SaveAction()
        {
            // This method is kept for backward compatibility
            // but should not be called during active recording
            if (!SaveRecord || _isRecording) return;

            // Process any buffered inputs
            if (_currentRecordingBuffer.Count > 0)
            {
                _ = SaveAllBufferedActions();
            }
        }

        private void OnFileCaptured(string filePath)
        {
            Dispatcher.Dispatch(() =>
            {
                Debug.WriteLine($"OnFileCaptured: FilePath={filePath}");

                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    Debug.WriteLine($"OnFileCaptured: File exists at {filePath}");
                }
                else
                {
                    Debug.WriteLine($"OnFileCaptured: File does not exist or path is invalid");
                }

                if (Data.Any())
                {
                    var lastActionGroup = Data.Last().Data.ActionGroupObject;

                    // Add the captured file to the ActionGroup's Files property
                    lastActionGroup.Files.Add(new ActionFile
                    {
                        Filename = System.IO.Path.GetFileName(filePath),
                        ContentType = GetFileContentType(filePath),
                        Data = filePath // Store the file path as the data
                    });

                    // Update preview image with animation if it's an image file
                    if (filePath.EndsWith(".jpg") || filePath.EndsWith(".png"))
                    {
                        var image = ImageSource.FromFile(filePath);
                        AnimateImageChange(image);
                    }
                }
            });
        }

        private string GetFileContentType(string filePath)
        {
            if (filePath.EndsWith(".mp3") || filePath.EndsWith(".wav"))
                return "Audio";
            if (filePath.EndsWith(".png") || filePath.EndsWith(".jpg"))
                return "Image";
            if (filePath.EndsWith(".txt"))
                return "Text";
            return "Unknown";
        }

        private async void AnimateImageChange(ImageSource newImage)
        {
            await this.ScaleTo(0.95, 150, Easing.CubicInOut);
            CapturedImageSource = newImage;
            OnPropertyChanged(nameof(CapturedImageSource));
            await this.ScaleTo(1, 150, Easing.CubicInOut);
        }

        private async Task SaveNewActionGroup()
        {
            try
            {
                if (Data.Any() && UploadToBackend)
                {
                    // Only upload if explicitly requested
                    await _dataService.CompressAndUploadAsync(new List<DataItem> { Data.Last() });
                    Debug.WriteLine("Uploaded action group to backend");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving action group: {ex.Message}");
            }
        }

        private async Task SaveDataItemsToFile()
        {
            if (SaveLocally)
            {
                try
                {
                    Debug.WriteLine($"SaveDataItemsToFile: Saving {Data.Count} action groups to local storage");
                    if (Data.Count > 0)
                    {
                        // Save all data to both storage locations to ensure consistency
                        await _dataService.SaveDataItemsToFile(Data.ToList());
                        await _dataService.SaveLocalRichDataAsync(Data.ToList());
                        Debug.WriteLine("Successfully saved all action groups to both storage locations");
                    }
                    else
                    {
                        Debug.WriteLine("No data to save to local storage");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving data to files: {ex.Message}");
                    Debug.WriteLine(ex.StackTrace);
                }
            }
            else
            {
                Debug.WriteLine("SaveLocally is false - not saving action groups to local storage");
            }
        }

        private async Task LoadDataItemsFromFile()
        {
            var items = await _dataService.LoadDataItemsFromFile();
            Data = new ObservableCollection<DataItem>(items);
            OnPropertyChanged(nameof(Data));
        }

        private void OnScreenPreviewFrameReady(ImageSource source)
        {
            if (source == null)
            {
                Debug.WriteLine("[ObservePage] OnScreenPreviewFrameReady - ImageSource is null"); // Added debug print
                return;
            }

            // Use Dispatcher to update UI from background thread
            Dispatcher.Dispatch(() =>
            {
                try
                {
                    if (CapturePreviewCard != null)
                    {
                        CapturePreviewCard.UpdateScreenCapture(source);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating screen preview: {ex.Message}");
                }
            });
        }

        private void OnWebcamPreviewFrameReady(ImageSource source)
        {
            if (source == null)
            {
                Debug.WriteLine("[ObservePage] OnWebcamPreviewFrameReady - ImageSource is null"); // Added debug print
                return;
            }

            // Use Dispatcher to update UI from background thread
            Dispatcher.Dispatch(() =>
            {
                try
                {
                    if (CapturePreviewCard != null)
                    {
                        CapturePreviewCard.UpdateWebcamCapture(source);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating webcam preview: {ex.Message}");
                }
            });
        }

        // Add a method to explicitly trigger preview updates
        private void RefreshPreviewFrames()
        {
            if (_screenService != null && CapturePreviewCard != null && CapturePreviewCard.IsScreenCaptureInactive)
            {
                // Force an initial screen capture
                var screenImage = _screenService.GetSingleScreenshot();
                if (screenImage != null)
                {
                    CapturePreviewCard.UpdateScreenCapture(screenImage);
                }
            }
        }

        // Improved SaveNewActionToFile to guarantee the action is saved to both files
        private async Task SaveNewActionToFile(DataItem newItem)
        {
            try
            {
                Debug.WriteLine($"SaveNewActionToFile: Saving action '{newItem?.Data?.ActionGroupObject?.ActionName ?? "Unknown"}'");

                // Make sure IsLocal flag is set
                if (newItem?.Data?.ActionGroupObject != null)
                {
                    newItem.Data.ActionGroupObject.IsLocal = true;
                }

                // Step 1: Load & update the regular dataitems.json file
                var existingItems = await _dataService.LoadDataItemsFromFile();
                Debug.WriteLine($"Loaded {existingItems.Count} existing items from dataitems.json");

                // Remove any existing item with the same name to prevent duplicates
                existingItems.RemoveAll(item =>
                    item?.Data?.ActionGroupObject?.ActionName == newItem?.Data?.ActionGroupObject?.ActionName);

                // Add the new item
                existingItems.Add(newItem);
                Debug.WriteLine($"Added new action to regular items list (now {existingItems.Count} items)");

                // Save to regular data file
                await _dataService.SaveDataItemsToFile(existingItems);
                Debug.WriteLine("Saved action to dataitems.json");

                // Step 2: Also save to local data store (localdataitems.json)
                // Make a new list with just the action we want to save
                var localItems = new List<DataItem> { newItem };
                await _dataService.SaveLocalRichDataAsync(localItems);
                Debug.WriteLine("Saved action to localdataitems.json");

                // Verify file paths before saving
                if (newItem?.Data?.ActionGroupObject?.Files != null)
                {
                    foreach (var file in newItem.Data.ActionGroupObject.Files)
                    {
                        if (!string.IsNullOrEmpty(file.Data) && File.Exists(file.Data))
                        {
                            Debug.WriteLine($"File path verified: {file.Data}");
                        }
                        else
                        {
                            Debug.WriteLine($"WARNING: Invalid file path: {file.Data}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving action to files: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }
        }

        private async Task ButtonColorAnimation(Color color, int duration = 200)
        {
            // This is a placeholder for button color animation
            // Since we can't directly access ButtonLabel anymore
            await Task.Delay(duration);
        }
    }

    // Extension methods for animations
    public static class AnimationExtensions
    {
        public static Task<bool> BackgroundColorTo(this View view, Microsoft.Maui.Graphics.Color color, uint length = 250, Easing easing = null)
        {
            var tcs = new TaskCompletionSource<bool>();
            var animationName = "BackgroundColorAnimation";

            view.Animate(animationName, new Animation((d) =>
            {
                view.BackgroundColor = color;
            }), 16, length, easing, (d, b) =>
            {
                tcs.SetResult(true);
            });

            return tcs.Task;
        }
    }
}
