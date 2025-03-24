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

        // Use 'new' keyword to explicitly hide the inherited member
        public new event PropertyChangedEventHandler PropertyChanged;

        public CapturePreviewCard()
        {
            InitializeComponent();
            this.BindingContext = this;
        }

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
                    IsLoadingScreenPreview = _screenCaptureSource == null && _isPreviewActive;
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
                    IsLoadingWebcamPreview = _webcamCaptureSource == null && _isPreviewActive;
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
            IsLoadingScreenPreview = isActive && ScreenCaptureSource == null;
            IsLoadingWebcamPreview = isActive && WebcamCaptureSource == null;

            // Update status texts
            ScreenCaptureStatus.Text = isActive ? "Waiting for screen feed..." : "Screen capture inactive";
            WebcamCaptureStatus.Text = isActive ? "Waiting for webcam feed..." : "Webcam inactive";
        }

        public void UpdateScreenCapture(ImageSource source)
        {
            if (_isPreviewActive)
            {
                ScreenCaptureSource = source;
            }
        }

        public void UpdateWebcamCapture(ImageSource source)
        {
            if (_isPreviewActive)
            {
                WebcamCaptureSource = source;
            }
        }

        // Use 'new' keyword to explicitly hide the inherited method
        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
