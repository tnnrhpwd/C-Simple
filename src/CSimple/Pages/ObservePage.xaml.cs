using System.Diagnostics;
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
            // But leave the preview display disabled
            await Task.Delay(500);
            UpdatePreviewSources(true);
        }

        protected override void OnDisappearing()
        {
            // Unregister from events
            if (CapturePreviewCard != null)
            {
                CapturePreviewCard.PreviewEnabledChanged -= OnPreviewEnabledChanged;
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

        // Updated StartUserTouch method to reset touch level
        private void StartUserTouch()
        {
            _inputService.StartCapturing();
            _mouseService.StartTracking(IntPtr.Zero);
            UserTouchLevel = 0.0f; // Reset to zero when starting
        }

        private async void StopUserTouch()
        {
            _inputService.StopCapturing();
            _mouseService.StopTracking();

            // Only save if we're saving records
            if (SaveRecord)
            {
                await SaveNewActionGroup();

                // Only save to file if that option is enabled
                if (SaveLocally)
                    await SaveDataItemsToFile();
            }
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

        // Updated OnInputCaptured method to visualize keyboard/mouse
        private void OnInputCaptured(string inputJson)
        {
            Dispatcher.Dispatch(async () =>
            {
                UserTouchInputText = inputJson;
                Debug.WriteLine(inputJson);
                SaveAction();

                try
                {
                    // Deserialize action item
                    var actionItem = JsonConvert.DeserializeObject<ActionItem>(inputJson);

                    // Update touch level based on active key count
                    int activeKeyCount = _inputService.GetActiveKeyCount();
                    UserTouchLevel = Math.Min(activeKeyCount / 5.0f, 1.0f); // Scale: 5 keys = 100%

                    // Always update the visualization, regardless of preview state
                    if (CapturePreviewCard != null)
                    {
                        // Clear existing data
                        Debug.WriteLine($"Updating key: {actionItem.KeyCode}, EventType: {actionItem.EventType}");

                        // Determine if key is being pressed or released
                        var isPressed = actionItem.EventType == 0x0100 || // Key down (WM_KEYDOWN)
                                       actionItem.EventType == 0x0201 || // Left mouse down
                                       actionItem.EventType == 0x0204;   // Right mouse down

                        // Update the key display with keycode and press state
                        CapturePreviewCard.UpdateInputActivity((ushort)actionItem.KeyCode, isPressed);

                        // Animate button to show there's activity
                        var originalColor = Colors.Transparent;
                        await ButtonColorAnimation(Colors.LightGreen);
                        await ButtonColorAnimation(originalColor);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error updating input visualization: {ex.Message}");
                }
            });
        }

        private async Task ButtonColorAnimation(Color color, int duration = 200)
        {
            // This is a placeholder for button color animation
            // Since we can't directly access ButtonLabel anymore
            await Task.Delay(duration);
        }

        private void OnFileCaptured(string filePath)
        {
            Dispatcher.Dispatch(() =>
            {
                if (Data.Any())
                {
                    _dataService.AddFileToDataItem(Data.Last(), filePath);

                    // Update preview image with animation if it's an image file
                    if (filePath.EndsWith(".jpg") || filePath.EndsWith(".png"))
                    {
                        var image = ImageSource.FromFile(filePath);
                        AnimateImageChange(image);
                    }
                }
            });
        }

        private async void AnimateImageChange(ImageSource newImage)
        {
            await this.ScaleTo(0.95, 150, Easing.CubicInOut);
            CapturedImageSource = newImage;
            OnPropertyChanged(nameof(CapturedImageSource));
            await this.ScaleTo(1, 150, Easing.CubicInOut);
        }

        private void SaveAction()
        {
            // If not saving records, don't proceed
            if (!SaveRecord) return;

            string actionName = ActionConfigCard?.ActionName;
            if (string.IsNullOrEmpty(actionName) || string.IsNullOrEmpty(UserTouchInputText)) return;

            var actionItem = JsonConvert.DeserializeObject<ActionItem>(UserTouchInputText);
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

            var existingGroup = Data.FirstOrDefault(ag => ag.Data.ActionGroupObject.ActionName == actionName);

            if (existingGroup != null)
            {
                existingGroup.Data.ActionGroupObject.ActionArray.Add(actionItem);

                if (!existingGroup.Data.ActionGroupObject.ActionModifiers.Any(am => am.ModifierName == actionModifier.ModifierName))
                    existingGroup.Data.ActionGroupObject.ActionModifiers.Add(actionModifier);

                Debug.WriteLine($"Updated Action Group: {UserTouchInputText}");
            }
            else
            {
                var newItem = _dataService.CreateOrUpdateActionItem(
                    actionName, actionItem, ActionConfigCard?.ModifierName, InputModifierPopupControl?.Description, priority);

                Data.Add(newItem);
                Debug.WriteLine($"Saved Action Group: {actionName}");
            }
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
                await _dataService.SaveDataItemsToFile(Data);
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
