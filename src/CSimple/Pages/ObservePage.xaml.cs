﻿using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Newtonsoft.Json;
using System.Threading;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

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

        // Commands
        public ICommand TogglePCVisualCommand { get; }
        public ICommand TogglePCAudibleCommand { get; }
        public ICommand ToggleUserVisualCommand { get; }
        public ICommand ToggleUserAudibleCommand { get; }
        public ICommand ToggleUserTouchCommand { get; }
        public ICommand SaveActionCommand { get; }
        public ICommand SaveToFileCommand { get; }
        public ICommand LoadFromFileCommand { get; }

        // Save options
        private bool _saveRecord = false; // Changed from true to false
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

        private bool _saveLocally = false; // Changed from true to false
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

        public ObservePage()
        {
            InitializeComponent();

            // Init services
            _inputService = new InputCaptureService();
            _screenService = new ScreenCaptureService();
            _audioService = new AudioCaptureService();
            _dataService = new ObserveDataService();
            _mouseService = new MouseTrackingService();

            // Set up events
            _inputService.InputCaptured += OnInputCaptured;
            _inputService.DebugMessageLogged += message => Debug.WriteLine(message);
            _screenService.FileCaptured += OnFileCaptured;
            _screenService.DebugMessageLogged += message => Debug.WriteLine(message);
            _audioService.FileCaptured += OnFileCaptured;
            _audioService.DebugMessageLogged += message => Debug.WriteLine(message);
            _audioService.PCLevelChanged += level => Dispatcher.Dispatch(() => PCAudioLevel = level);
            _audioService.WebcamLevelChanged += level => Dispatcher.Dispatch(() => UserAudioLevel = level);
            _dataService.DebugMessageLogged += message => Debug.WriteLine(message);
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
            // Save local actions before navigating away
            if (SaveLocally)
            {
                await SaveDataItemsToFile();
            }

            // Stop all captures and previews when navigating away
            StopAllCaptures();
            UpdatePreviewSources(false);

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
            Task.Run(() => _screenService.StartWebcamCapture(_pcVisualCts.Token, ActionConfigCard?.ActionName, UserTouchInputText), _pcVisualCts.Token);
        }

        private void StopPCVisual() => _pcVisualCts?.Cancel();

        private void StartUserVisual()
        {
            _userVisualCts = new CancellationTokenSource();
            Task.Run(() => _screenService.StartScreenCapture(_userVisualCts.Token, ActionConfigCard?.ActionName, UserTouchInputText), _userVisualCts.Token);
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

            // Only save if we're saving records
            if (SaveRecord)
            {
                // Process all buffered actions at once now that we're done recording
                await SaveAllBufferedActions();

                // Only save to file if that option is enabled
                if (SaveLocally)
                    await SaveDataItemsToFile();
            }
        }

        // New method to save all buffered actions at once
        private async Task SaveAllBufferedActions()
        {
            string actionName = ActionConfigCard?.ActionName;
            if (string.IsNullOrEmpty(actionName) || _currentRecordingBuffer.Count == 0)
                return;

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

            var existingGroup = Data.FirstOrDefault(ag => ag.Data.ActionGroupObject.ActionName == actionName);

            if (existingGroup != null)
            {
                // Update the existing action group with all buffered items
                existingGroup.Data.ActionGroupObject.ActionArray.AddRange(_currentRecordingBuffer);

                if (!existingGroup.Data.ActionGroupObject.ActionModifiers.Any(am => am.ModifierName == actionModifier.ModifierName))
                    existingGroup.Data.ActionGroupObject.ActionModifiers.Add(actionModifier);

                // Make sure it's marked as local
                existingGroup.Data.ActionGroupObject.IsLocal = true;

                Debug.WriteLine($"Updated Action Group with {_currentRecordingBuffer.Count} items");
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
                    createdAt = DateTime.Now  // Set the timestamp on the DataItem
                };

                Data.Add(newItem);
                Debug.WriteLine($"Saved New Action Group: {actionName} with {_currentRecordingBuffer.Count} items");

                // Save the new action to dataitems.json - only when explicitly stopping
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

                // Default to disabled on page load
                // Only auto-enable if user hasn't explicitly turned it off AND we have active captures
                if (isActive && !CapturePreviewCard.IsUserToggledOff && IsRecordingActive)
                {
                    CapturePreviewCard.IsPreviewEnabled = true;
                }
                else if (!isActive)
                {
                    // When stopping all captures, respect user's toggle preference
                    if (!CapturePreviewCard.IsUserToggledOff)
                    {
                        CapturePreviewCard.IsPreviewEnabled = false;
                    }
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

        private void OnInputCaptured(string inputJson)
        {
            try
            {
                // Process immediately instead of debouncing
                var actionItem = JsonConvert.DeserializeObject<ActionItem>(inputJson);

                // Check if it's a mouse movement event
                bool isMouseMovement = actionItem.EventType == 512 || // Mouse move
                                      actionItem.EventType == 0x0200; // WM_MOUSEMOVE

                if (isMouseMovement)
                {
                    // Process mouse movements directly without debounce
                    ProcessMouseMovement(actionItem);
                }
                else
                {
                    // Only debounce non-mouse movement inputs
                    _debounceTimer?.Dispose();
                    _debounceTimer = new Timer(DebouncedInputCaptured, inputJson, DebounceInterval, Timeout.Infinite);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing input: {ex.Message}");
            }
        }

        // New helper for determining if an action is a mouse click event (down or up)
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

        private async void DebouncedInputCaptured(object state)
        {
            // Only process non-mouse movement inputs (keyboard, buttons, etc.)
            string inputJson = (string)state;
            Dispatcher.Dispatch(async () =>
            {
                UserTouchInputText = inputJson;

                try
                {
                    // Deserialize action item
                    var actionItem = JsonConvert.DeserializeObject<ActionItem>(inputJson);

                    // Process non-mouse events normally
                    if (actionItem.EventType != 512 && actionItem.EventType != 0x0200)
                    {
                        // Update touch level based on active key count
                        int activeKeyCount = _inputService.GetActiveKeyCount();
                        UserTouchLevel = Math.Min(activeKeyCount / 5.0f, 1.0f); // Scale: 5 keys = 100%

                        // Update visualization for keyboard events
                        if (CapturePreviewCard != null)
                        {
                            // Determine if key is being pressed or released
                            var isPressed = actionItem.EventType == 0x0100 || // Key down (WM_KEYDOWN)
                                           actionItem.EventType == 0x0201 || // Left mouse down
                                           actionItem.EventType == 0x0204;   // Right mouse down

                            // Update the key display with keycode and press state
                            CapturePreviewCard.UpdateInputActivity((ushort)actionItem.KeyCode, isPressed);
                        }

                        // Add to recording buffer if recording and not a duplicate
                        if (_isRecording)
                        {
                            bool isDuplicate = IsDuplicateAction(actionItem, _lastRecordedAction);

                            // For mouse clicks and key events, make sure we don't miss any
                            if (!isDuplicate || IsMouseClickEvent(actionItem.EventType))
                            {
                                _currentRecordingBuffer.Add(actionItem);
                                _lastRecordedAction = actionItem;

                                // Log click events for debugging
                                if (IsMouseClickEvent(actionItem.EventType))
                                {
                                    Debug.WriteLine($"Recorded mouse click: {actionItem.EventType} at ({actionItem.Coordinates?.X ?? 0}, {actionItem.Coordinates?.Y ?? 0})");
                                }
                            }
                        }
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
                // Save new data while preserving existing local data
                await _dataService.SaveLocalRichDataAsync(Data);
                Debug.WriteLine("Saved action group to local storage");
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
            if (source == null) return;

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
            if (source == null) return;

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

        private async Task SaveNewActionToFile(DataItem newItem)
        {
            try
            {
                // Load existing items first
                var existingItems = await _dataService.LoadDataItemsFromFile();

                // Add the new item if it doesn't exist yet
                if (!existingItems.Any(item =>
                    item.Data?.ActionGroupObject?.ActionName == newItem.Data?.ActionGroupObject?.ActionName))
                {
                    existingItems.Add(newItem);
                }

                // Save to both regular file and local data store
                await _dataService.SaveDataItemsToFile(existingItems);
                await _dataService.SaveLocalRichDataAsync(new List<DataItem> { newItem });
                Debug.WriteLine("New action saved to both dataitems.json and local data store");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving new action to file: {ex.Message}");
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
