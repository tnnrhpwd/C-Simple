using System.Diagnostics;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using CSimple.Services;
using CSimple.ViewModels;
using Application = Microsoft.Maui.Controls.Application;
using WindowsConfiguration = Microsoft.Maui.Controls.PlatformConfiguration.Windows;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System;
using System.Threading.Tasks;

namespace CSimple.Pages;

public partial class HomePage : ContentPage
{
    private readonly AuthService _authService; // AuthService instance for login/logout
    static bool isSetup = false;
    public ICommand NavigateCommand { get; set; }
    public ICommand LogoutCommand { get; }

    // Constructor with AuthService injection
    public HomePage(HomeViewModel vm)
    // public HomePage(HomeViewModel vm, AuthService authService)
    {
        InitializeComponent();
        // _authService = authService;
        // LogoutCommand = new Command(ExecuteLogout);
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
        // if (!await IsUserLoggedIn())
        if (true)
        {
            Debug.WriteLine("Navigating...");
            NavigateLogin();
        }
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
            // await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Navigation error: {ex.Message}");
        }
    }
    // private void ExecuteLogout()
    // {
    //     try
    //     {
    //         _authService.Logout();
    //         Shell.Current.GoToAsync($"///login");
    //     }
    //     catch (Exception ex)
    //     {
    //         Debug.WriteLine($"Logout error: {ex.Message}");
    //     }
    // }
    // private async Task<bool> IsUserLoggedIn()
    // {
    //     return await _authService.IsLoggedInAsync();
    // }
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
}
