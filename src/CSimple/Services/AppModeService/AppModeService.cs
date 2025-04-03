using System.ComponentModel;

namespace CSimple.Services.AppModeService;

public enum AppMode
{
    Online,
    Offline
}

public class AppModeService : INotifyPropertyChanged
{
    private AppMode _currentMode = AppMode.Offline;

    public AppMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnPropertyChanged(nameof(CurrentMode));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
