using System.ComponentModel;
using System.Windows.Input;

namespace CSimple.ViewModels;

public class ObservePageViewModel : INotifyPropertyChanged
{
    private string _pcVisualButtonText = "Read";
    private string _pcAudibleButtonText = "Read";
    private string _userVisualButtonText = "Read";
    private string _userAudibleButtonText = "Read";
    private string _userTouchButtonText = "Read";
    private ImageSource _capturedImageSource;

    public string PCVisualButtonText
    {
        get => _pcVisualButtonText;
        set
        {
            _pcVisualButtonText = value;
            OnPropertyChanged(nameof(PCVisualButtonText));
        }
    }

    public string PCAudibleButtonText
    {
        get => _pcAudibleButtonText;
        set
        {
            _pcAudibleButtonText = value;
            OnPropertyChanged(nameof(PCAudibleButtonText));
        }
    }

    public string UserVisualButtonText
    {
        get => _userVisualButtonText;
        set
        {
            _userVisualButtonText = value;
            OnPropertyChanged(nameof(UserVisualButtonText));
        }
    }

    public string UserAudibleButtonText
    {
        get => _userAudibleButtonText;
        set
        {
            _userAudibleButtonText = value;
            OnPropertyChanged(nameof(UserAudibleButtonText));
        }
    }

    public string UserTouchButtonText
    {
        get => _userTouchButtonText;
        set
        {
            _userTouchButtonText = value;
            OnPropertyChanged(nameof(UserTouchButtonText));
        }
    }

    public ImageSource CapturedImageSource
    {
        get => _capturedImageSource;
        set
        {
            _capturedImageSource = value;
            OnPropertyChanged(nameof(CapturedImageSource));
        }
    }

    public ICommand TogglePCVisualCommand { get; }
    public ICommand TogglePCAudibleCommand { get; }
    public ICommand ToggleUserVisualCommand { get; }
    public ICommand ToggleUserAudibleCommand { get; }
    public ICommand ToggleUserTouchCommand { get; }

    public ObservePageViewModel()
    {
        TogglePCVisualCommand = new Command(TogglePCVisual);
        TogglePCAudibleCommand = new Command(TogglePCAudible);
        ToggleUserVisualCommand = new Command(ToggleUserVisual);
        ToggleUserAudibleCommand = new Command(ToggleUserAudible);
        ToggleUserTouchCommand = new Command(ToggleUserTouch);
    }

    private void TogglePCVisual()
    {
        if (PCVisualButtonText == "Read")
        {
            PCVisualButtonText = "Stop";
            // Start reading logic
        }
        else
        {
            PCVisualButtonText = "Read";
            // Stop reading logic
        }
    }

    private void TogglePCAudible()
    {
        if (PCAudibleButtonText == "Read")
        {
            PCAudibleButtonText = "Stop";
            // Start reading logic
        }
        else
        {
            PCAudibleButtonText = "Read";
            // Stop reading logic
        }
    }

    private void ToggleUserVisual()
    {
        if (UserVisualButtonText == "Read")
        {
            UserVisualButtonText = "Stop";
            // Start reading logic
        }
        else
        {
            UserVisualButtonText = "Read";
            // Stop reading logic
        }
    }

    private void ToggleUserAudible()
    {
        if (UserAudibleButtonText == "Read")
        {
            UserAudibleButtonText = "Stop";
            // Start reading logic
        }
        else
        {
            UserAudibleButtonText = "Read";
            // Stop reading logic
        }
    }

    private void ToggleUserTouch()
    {
        if (UserTouchButtonText == "Read")
        {
            UserTouchButtonText = "Stop";
            // Start reading logic
        }
        else
        {
            UserTouchButtonText = "Read";
            // Stop reading logic
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
