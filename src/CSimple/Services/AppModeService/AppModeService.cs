using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Maui.Storage;

namespace CSimple.Services.AppModeService;

public enum AppMode
{
    Online,
    Offline
}

public class AppModeService : INotifyPropertyChanged
{
    private const string APP_MODE_KEY = "AppMode";
    private AppMode _currentMode = AppMode.Offline;

    public AppModeService()
    {
        // Load saved mode on initialization
        _ = Task.Run(LoadSavedModeAsync);
    }

    public AppMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnPropertyChanged(nameof(CurrentMode));

                // Save the new mode to persistent storage
                _ = Task.Run(() => SaveModeAsync(value));
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task SaveModeAsync(AppMode mode)
    {
        try
        {
            await SecureStorage.SetAsync(APP_MODE_KEY, mode.ToString());
            Debug.WriteLine($"AppModeService: Saved app mode: {mode}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppModeService: Error saving app mode: {ex.Message}");
        }
    }

    private async Task LoadSavedModeAsync()
    {
        try
        {
            var savedMode = await SecureStorage.GetAsync(APP_MODE_KEY);
            if (savedMode != null && Enum.TryParse<AppMode>(savedMode, out AppMode mode))
            {
                // Update the backing field directly to avoid triggering save again
                _currentMode = mode;

                // Notify property changed on main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    OnPropertyChanged(nameof(CurrentMode));
                });

                Debug.WriteLine($"AppModeService: Loaded saved app mode: {mode}");
            }
            else
            {
                Debug.WriteLine("AppModeService: No saved app mode found, defaulting to Offline");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AppModeService: Error loading saved app mode: {ex.Message}");
        }
    }
}
