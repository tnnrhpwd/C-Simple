using CSimple.ViewModels;
using System.Diagnostics;
using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace CSimple.Pages;

public partial class HomePage : ContentPage
{
    private readonly DataService _dataService; // AuthService instance for login/logout
    private readonly AppModeService _appModeService;
    static bool isSetup = false;
    public ICommand NavigateCommand { get; set; }
    public ICommand LogoutCommand { get; }

    // New properties to showcase AI capabilities
    public bool IsAIEnabled { get; set; } = true;
    public string ActiveAIStatus { get; set; } = "AI Assistant Active";
    public string AIStatusDetail { get; set; } = "Monitoring inputs and providing assistance";
    public int ActiveModelsCount { get; set; } = 2;
    public int TodayActionsCount { get; set; } = 15;
    public double SuccessRate { get; set; } = 0.92;
    public double SystemHealthPercentage { get; set; } = 0.87;
    public string SystemHealthStatus { get; set; } = "Systems nominal, resources optimized";
    public Color SystemHealthColor => SystemHealthPercentage > 0.7 ? Colors.Green : SystemHealthPercentage > 0.4 ? Colors.Orange : Colors.Red;
    public double AverageAIAccuracy { get; set; } = 0.89;

    public bool IsOnlineMode
    {
        get => _appModeService.CurrentMode == AppMode.Online;
        set => _appModeService.CurrentMode = value ? AppMode.Online : AppMode.Offline;
    }

    public string AppModeLabel => IsOnlineMode ? "Online Mode" : "Offline Mode";

    // Commands for AI features
    public ICommand RefreshDashboardCommand { get; }
    public ICommand CreateNewGoalCommand { get; }
    public ICommand NavigateToObserveCommand { get; }
    public ICommand TrainModelCommand { get; }
    public ICommand DiscoverSharedGoalsCommand { get; }

    // Constructor with AuthService injection
    public HomePage(HomeViewModel vm, DataService dataService, AppModeService appModeService)
    {
        InitializeComponent();
        _dataService = dataService;
        _appModeService = appModeService;
        LogoutCommand = new Command(ExecuteLogout);

        // Bind to AppModeService changes
        _appModeService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AppModeService.CurrentMode))
            {
                OnPropertyChanged(nameof(IsOnlineMode));
                OnPropertyChanged(nameof(AppModeLabel));
            }
        };

        // Initialize new commands
        RefreshDashboardCommand = new Command(RefreshDashboard);
        CreateNewGoalCommand = new Command(async () => await Shell.Current.GoToAsync("///goal"));
        NavigateToObserveCommand = new Command(async () => await Shell.Current.GoToAsync("///observe"));
        TrainModelCommand = new Command(async () => await Shell.Current.GoToAsync("///orient"));
        DiscoverSharedGoalsCommand = new Command(ShowSharedGoalsPopup);

        BindingContext = this;
        if (!isSetup)
        {
            isSetup = true;
            SetupAppActions();
            SetupTrayIcon();
        }
    }

    // New methods for AI features
    private void RefreshDashboard()
    {
        // Simulate refreshing system stats
        SystemHealthPercentage = new Random().NextDouble() * 0.3 + 0.7; // Between 0.7 and 1.0
        ActiveModelsCount = new Random().Next(1, 5);
        TodayActionsCount = new Random().Next(10, 30);
        SuccessRate = new Random().NextDouble() * 0.2 + 0.8; // Between 0.8 and 1.0
        AverageAIAccuracy = new Random().NextDouble() * 0.15 + 0.85; // Between 0.85 and 1.0

        // Update bindings
        OnPropertyChanged(nameof(SystemHealthPercentage));
        OnPropertyChanged(nameof(ActiveModelsCount));
        OnPropertyChanged(nameof(TodayActionsCount));
        OnPropertyChanged(nameof(SuccessRate));
        OnPropertyChanged(nameof(AverageAIAccuracy));
        OnPropertyChanged(nameof(SystemHealthColor));
    }

    private async void ShowSharedGoalsPopup()
    {
        await DisplayAlert("Shared Goals", "Discovering goals shared by other users. This feature allows you to download pre-trained models for specific tasks.", "OK");
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

        // Refresh dashboard stats on appearing
        RefreshDashboard();
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
