using System.Windows.Input;
using System.Diagnostics;

namespace CSimple.ViewModels;

public class HomeViewModel : BaseViewModel
{
    private readonly DataService _dataService;
    public Command QuitCommand { get; set; } = new Command(() =>
    {
        Application.Current.Quit();
    });
    private Command toggleModeCommand;
    public Command ToggleModeCommand
    {
        get
        {
            return toggleModeCommand;
        }
        set
        {
            toggleModeCommand = value;
            OnPropertyChanged();
        }
    }
    public ICommand LoadDataCommand { get; }
    public ICommand PreloadNetPageCommand { get; }

    public HomeViewModel(DataService dataService)
    {
        _dataService = dataService;
        LoadDataCommand = new Command(async () => await LoadDataAsync());
        PreloadNetPageCommand = new Command(async () => await PreloadNetPageAsync());
    }

    private async Task LoadDataAsync()
    {
        await Task.Run(() =>
        {
            // var token = await SecureStorage.GetAsync("userToken");
            // var data = await _dataService.GetDataAsync(new { }, token);
            // Process the data as needed
        });
    }

    private async Task PreloadNetPageAsync()
    {
        try
        {
            Debug.WriteLine("HomeViewModel: Starting NetPage preload...");

            // Get the NetPageViewModel from the app
            var app = Application.Current as App;
            var netPageViewModel = app?.NetPageViewModel;

            if (netPageViewModel != null)
            {
                Debug.WriteLine("HomeViewModel: Loading NetPage data...");
                await netPageViewModel.LoadDataAsync();
                Debug.WriteLine($"HomeViewModel: NetPage preload completed. Loaded {netPageViewModel.AvailableModels?.Count ?? 0} models.");
            }
            else
            {
                Debug.WriteLine("HomeViewModel: NetPageViewModel not found in app instance");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"HomeViewModel: Error preloading NetPage: {ex.Message}");
        }
    }

    public HomeViewModel()
    {
        ToggleModeCommand = new Command(() =>
        {
            App.Current.UserAppTheme = App.Current.UserAppTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        });
        LoadDataCommand = new Command(async () => await LoadDataAsync());
        PreloadNetPageCommand = new Command(async () => await PreloadNetPageAsync());
    }
}
