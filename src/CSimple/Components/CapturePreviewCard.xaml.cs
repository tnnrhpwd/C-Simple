using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Add this for Color
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading; // Add this for CancellationTokenSource
using System.Threading.Tasks;

namespace CSimple.Components
{
    public partial class CapturePreviewCard : ContentView, INotifyPropertyChanged
    {
        private ImageSource _screenCaptureSource;
        private ImageSource _webcamCaptureSource;
        private bool _isPreviewActive;
        private bool _isLoadingScreenPreview;
        private bool _isLoadingWebcamPreview;
        // Change default value to false
        private bool _isPreviewEnabled = false;
        private bool _isUserToggledOff = true;
        private bool _isToggleChangedByUser = false;

        // Use 'new' keyword to explicitly hide the inherited member
        public new event PropertyChangedEventHandler PropertyChanged;

        // Add event for preview toggled
        public event EventHandler<bool> PreviewEnabledChanged;

        private Dictionary<ushort, Border> _keyMapping;
        private readonly Color _activeKeyColor = Colors.LightGreen;
        private readonly Color _inactiveKeyColor = Colors.Transparent;

        public CapturePreviewCard()
        {
            InitializeComponent();
            this.BindingContext = this;

            // Initialize preview toggle state to off
            PreviewToggle.IsToggled = _isPreviewEnabled;

            // Initialize dictionary for key mapping
            InitializeKeyMapping();

            // Set styles for all keyboard keys
            SetKeyboardStyles();
        }

        public bool IsPreviewEnabled
        {
            get => _isPreviewEnabled;
            set
            {
                if (_isPreviewEnabled != value)
                {
                    _isPreviewEnabled = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsPreviewDisabled));
                    OnPropertyChanged(nameof(IsScreenWaiting));
                    OnPropertyChanged(nameof(IsWebcamWaiting));

                    // Update the toggle UI to match (without triggering the toggle event)
                    _isToggleChangedByUser = false;
                    PreviewToggle.IsToggled = value;
                    _isToggleChangedByUser = true;

                    // Notify parent of change
                    PreviewEnabledChanged?.Invoke(this, value);
                }
            }
        }

        // Property to track if the user has explicitly toggled off previews
        public bool IsUserToggledOff
        {
            get => _isUserToggledOff;
            set
            {
                if (_isUserToggledOff != value)
                {
                    _isUserToggledOff = value;
                    OnPropertyChanged();
                }
            }
        }

        // Property to track if the toggle was changed by user (vs programmatically)
        public bool IsToggleChangedByUser
        {
            get => _isToggleChangedByUser;
            private set => _isToggleChangedByUser = value;
        }

        public bool IsPreviewDisabled => !IsPreviewEnabled;

        public bool IsScreenWaiting => IsScreenCaptureInactive && IsPreviewEnabled;

        public bool IsWebcamWaiting => IsWebcamCaptureInactive && IsPreviewEnabled;

        public ImageSource ScreenCaptureSource
        {
            get => _screenCaptureSource;
            set
            {
                if (_screenCaptureSource != value)
                {
                    _screenCaptureSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsScreenCaptureInactive));
                    OnPropertyChanged(nameof(IsScreenWaiting));
                    IsLoadingScreenPreview = _screenCaptureSource == null && _isPreviewActive && IsPreviewEnabled;
                }
            }
        }

        public ImageSource WebcamCaptureSource
        {
            get => _webcamCaptureSource;
            set
            {
                if (_webcamCaptureSource != value)
                {
                    _webcamCaptureSource = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsWebcamCaptureInactive));
                    OnPropertyChanged(nameof(IsWebcamWaiting));
                    IsLoadingWebcamPreview = _webcamCaptureSource == null && _isPreviewActive && IsPreviewEnabled;
                }
            }
        }

        public bool IsLoadingScreenPreview
        {
            get => _isLoadingScreenPreview;
            set
            {
                if (_isLoadingScreenPreview != value)
                {
                    _isLoadingScreenPreview = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsLoadingWebcamPreview
        {
            get => _isLoadingWebcamPreview;
            set
            {
                if (_isLoadingWebcamPreview != value)
                {
                    _isLoadingWebcamPreview = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsScreenCaptureInactive => ScreenCaptureSource == null;
        public bool IsWebcamCaptureInactive => WebcamCaptureSource == null;

        public string ButtonLabelText
        {
            get => ButtonLabel.Text;
            set => ButtonLabel.Text = value;
        }

        public void SetPreviewActive(bool isActive)
        {
            _isPreviewActive = isActive;

            // Clear preview sources if not active
            if (!isActive)
            {
                ScreenCaptureSource = null;
                WebcamCaptureSource = null;
            }

            // Update loading states
            IsLoadingScreenPreview = isActive && ScreenCaptureSource == null && IsPreviewEnabled;
            IsLoadingWebcamPreview = isActive && WebcamCaptureSource == null && IsPreviewEnabled;

            // Update status texts
            ScreenCaptureStatus.Text = isActive ? "Waiting for screen feed..." : "Screen capture inactive";
            WebcamCaptureStatus.Text = isActive ? "Waiting for webcam feed..." : "Webcam inactive";
        }

        public void UpdateScreenCapture(ImageSource source)
        {
            if (_isPreviewActive && IsPreviewEnabled)
            {
                // Use Dispatcher instead of Device.BeginInvokeOnMainThread
                Dispatcher.Dispatch(() =>
                {
                    ScreenCaptureSource = null;
                    ScreenCaptureSource = source;

                    // Update visibility states
                    if (ScreenCaptureStatus != null)
                    {
                        ScreenCaptureStatus.Text = "Screen feed active";
                        IsLoadingScreenPreview = false;
                    }
                });
            }
        }

        public void UpdateWebcamCapture(ImageSource source)
        {
            if (_isPreviewActive && IsPreviewEnabled)
            {
                // Use Dispatcher instead of Device.BeginInvokeOnMainThread
                Dispatcher.Dispatch(() =>
                {
                    WebcamCaptureSource = null;
                    WebcamCaptureSource = source;

                    // Update visibility states
                    if (WebcamCaptureStatus != null)
                    {
                        WebcamCaptureStatus.Text = "Webcam feed active";
                        IsLoadingWebcamPreview = false;
                    }
                });
            }
        }

        private void OnPreviewToggled(object sender, ToggledEventArgs e)
        {
            // Mark this as a user-initiated change
            _isToggleChangedByUser = true;
            IsPreviewEnabled = e.Value;
        }

        private void InitializeKeyMapping()
        {
            _keyMapping = new Dictionary<ushort, Border>();

            try
            {
                // Map virtual key codes to keyboard UI elements
                // Common virtual key codes (Windows)
                _keyMapping[27] = FindBorderByName("KeyEsc");

                // Function keys
                for (ushort i = 112; i <= 123; i++)
                {
                    var keyName = $"KeyF{i - 111}";
                    _keyMapping[i] = FindBorderByName(keyName);
                }

                // Numbers and letters - populate safely
                PopulateKeyboardDictionary();

                // Mouse
                _keyMapping[513] = FindBorderByName("LeftMouseButton");  // Left mouse button (0x0201)
                _keyMapping[516] = FindBorderByName("RightMouseButton"); // Right mouse button (0x0204)
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing keyboard: {ex.Message}");
            }
        }

        // Helper method to safely find Border elements by name
        private Border FindBorderByName(string name)
        {
            try
            {
                return this.FindByName<Border>(name);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Populate keyboard keys safely
        private void PopulateKeyboardDictionary()
        {
            // Simple mapping with error handling
            var keyMappings = new Dictionary<ushort, string>
            {
                // Numbers row
                { 192, "KeyTilde" }, { 49, "Key1" }, { 50, "Key2" }, { 51, "Key3" },
                { 52, "Key4" }, { 53, "Key5" }, { 54, "Key6" }, { 55, "Key7" },
                { 56, "Key8" }, { 57, "Key9" }, { 48, "Key0" }, { 189, "KeyMinus" },
                { 187, "KeyPlus" },
                
                // QWERTY row
                { 81, "KeyQ" }, { 87, "KeyW" }, { 69, "KeyE" }, { 82, "KeyR" },
                { 84, "KeyT" }, { 89, "KeyY" }, { 85, "KeyU" }, { 73, "KeyI" },
                { 79, "KeyO" }, { 80, "KeyP" },
                
                // ASDF row
                { 65, "KeyA" }, { 83, "KeyS" }, { 68, "KeyD" }, { 70, "KeyF" },
                { 71, "KeyG" }, { 72, "KeyH" }, { 74, "KeyJ" }, { 75, "KeyK" },
                { 76, "KeyL" },
                
                // ZXCV row
                { 90, "KeyZ" }, { 88, "KeyX" }, { 67, "KeyC" }, { 86, "KeyV" },
                { 66, "KeyB" }, { 78, "KeyN" }, { 77, "KeyM" }, { 32, "KeySpace" }
            };

            foreach (var pair in keyMappings)
            {
                var key = FindBorderByName(pair.Value);
                if (key != null)
                {
                    _keyMapping[pair.Key] = key;
                }
            }
        }

        private void SetKeyboardStyles()
        {
            // Apply style class to all keyboard keys if they're not already styled
            foreach (var keyMapping in _keyMapping)
            {
                var key = keyMapping.Value;
                if (key != null)
                {
                    // Check if StyleClass is null and initialize it if needed
                    if (key.StyleClass == null)
                    {
                        key.StyleClass = new List<string>();
                    }

                    if (!key.StyleClass.Contains("KeyboardKey"))
                    {
                        key.StyleClass.Add("KeyboardKey");
                    }

                    // Make sure labels inside keys have correct style
                    if (key.Content is Label label)
                    {
                        label.HorizontalOptions = LayoutOptions.Center;
                        label.VerticalOptions = LayoutOptions.Center;
                        label.FontSize = 10;
                    }
                }
            }
        }

        public void UpdateInputActivity(ushort keyCode, bool isPressed)
        {
            try
            {
                if (_keyMapping != null && _keyMapping.TryGetValue(keyCode, out var keyElement) && keyElement != null)
                {
                    if (isPressed)
                    {
                        VisualStateManager.GoToState(keyElement, "Active");
                    }
                    else
                    {
                        VisualStateManager.GoToState(keyElement, "Normal");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating input activity: {ex.Message}");
            }
        }

        public void UpdateMouseMovement(Microsoft.Maui.Graphics.Point delta)
        {
            // Highlight arrows based on movement direction
            double threshold = 5.0; // Minimum movement to trigger highlight

            if (Math.Abs(delta.X) > threshold || Math.Abs(delta.Y) > threshold)
            {
                // Show arrows in the direction of movement
                if (delta.X > threshold)
                    MouseRightArrow.Fill = _activeKeyColor;
                else if (delta.X < -threshold)
                    MouseLeftArrow.Fill = _activeKeyColor;

                if (delta.Y > threshold)
                    MouseDownArrow.Fill = _activeKeyColor;
                else if (delta.Y < -threshold)
                    MouseUpArrow.Fill = _activeKeyColor;

                // Schedule a reset after a short delay
                ResetMouseArrowsAfterDelay();
            }
        }

        private CancellationTokenSource _arrowResetTokenSource;

        private void ResetMouseArrowsAfterDelay()
        {
            // Cancel any pending reset
            _arrowResetTokenSource?.Cancel();
            _arrowResetTokenSource = new CancellationTokenSource();

            // Schedule a new reset after 300ms
            try
            {
                Task.Delay(300, _arrowResetTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (!t.IsCanceled)
                        {
                            Dispatcher.Dispatch(() =>
                            {
                                MouseRightArrow.Fill = _inactiveKeyColor;
                                MouseLeftArrow.Fill = _inactiveKeyColor;
                                MouseUpArrow.Fill = _inactiveKeyColor;
                                MouseDownArrow.Fill = _inactiveKeyColor;
                            });
                        }
                    }, TaskScheduler.Current);
            }
            catch (TaskCanceledException)
            {
                // Expected when cancellation occurs
            }
        }

        // Use 'new' keyword to explicitly hide the inherited method
        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
