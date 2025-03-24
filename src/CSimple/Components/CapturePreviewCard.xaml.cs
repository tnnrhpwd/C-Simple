using Microsoft.Maui.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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

        public CapturePreviewCard()
        {
            InitializeComponent();
            this.BindingContext = this;

            // Initialize preview toggle state to off
            PreviewToggle.IsToggled = _isPreviewEnabled;
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

        // Use 'new' keyword to explicitly hide the inherited method
        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
