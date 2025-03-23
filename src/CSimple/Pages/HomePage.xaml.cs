using CSimple.ViewModels;
using System.Diagnostics;
using System.Windows.Input;

namespace CSimple.Pages;

public partial class HomePage : ContentPage
{
    private readonly DataService _dataService; // AuthService instance for login/logout
    static bool isSetup = false;
    public ICommand NavigateCommand { get; set; }
    public ICommand LogoutCommand { get; }

    // Constructor with AuthService injection
    public HomePage(HomeViewModel vm, DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
        LogoutCommand = new Command(ExecuteLogout);
        BindingContext = vm;
        if (!isSetup)
        {
            isSetup = true;
            SetupAppActions();
            SetupTrayIcon();
        }
    }
    private async Task Initialize()
    {
        await Task.Run(() =>
        {
            // if (!await IsLoggedInAsync())
            // if (true)
            // {
            //     Debug.WriteLine("Navigating...");
            //     NavigateLogin();
            // }
        });
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Initialize();
    }
    async void NavigateLogin()
    {
        try
        {
            await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }
    private void ExecuteLogout()
    {
        try
        {
            _dataService.Logout();
            Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }
    private async Task<bool> IsUserLoggedIn()
    {
        return await _dataService.IsLoggedInAsync();
    }
    private void SetupAppActions()
    {
        try
        {
#if WINDOWS
#endif
            AppActions.SetAsync(
                new AppAction("current_info", "Check Current Weather", icon: "current_info"),
                new AppAction("add_location", "Add a Location", icon: "add_location")
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"App Actions not supported: {ex.Message}");
        }
    }
    private void SetupTrayIcon()
    {
        var trayService = ServiceProvider.GetService<ITrayService>();

        if (trayService != null)
        {
            trayService.Initialize();
            trayService.ClickHandler = () =>
                ServiceProvider.GetService<INotificationService>()
                    ?.ShowNotification("Hello from .NET MAUI", "How's your weather? 🌞");
        }
    }
        private async void OnGetStartedClicked(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("Get Started button clicked, attempting navigation to action page");
            await Shell.Current.GoToAsync("///action");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error in Get Started button click: {ex.Message}");
            // Fallback approach if the first one fails
            try
            {
                await Shell.Current.GoToAsync($"///{nameof(ActionPage)}");
            }
            catch (Exception innerEx)
            {
                Debug.WriteLine($"Fallback navigation also failed: {innerEx.Message}");
            }
        }
    }
}
