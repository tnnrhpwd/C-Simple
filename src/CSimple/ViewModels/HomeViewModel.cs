using System.Windows.Input;

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

    public HomeViewModel(DataService dataService)
    {
        _dataService = dataService;
        LoadDataCommand = new Command(async () => await LoadDataAsync());
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
    public HomeViewModel()
    {
        ToggleModeCommand = new Command(() =>
        {
            App.Current.UserAppTheme = App.Current.UserAppTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;
        });
    }
}
