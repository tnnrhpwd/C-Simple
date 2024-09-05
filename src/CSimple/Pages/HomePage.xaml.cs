using System.Diagnostics;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using CSimple.Services;
using CSimple.ViewModels;
using Application = Microsoft.Maui.Controls.Application;
using WindowsConfiguration = Microsoft.Maui.Controls.PlatformConfiguration.Windows;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;

namespace CSimple.Pages;

public partial class HomePage : ContentPage
{
    static bool isSetup = false;
        public ICommand NavigateCommand { get; set; }
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        NavigateCommand = new Command(NavigateLogin());
        BindingContext = vm;

        if (!isSetup)
        {
            isSetup = true;

            SetupAppActions();
            SetupTrayIcon();
        }
    }
    async void NavigateLogin()
    {
        try
        {
            await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"err: {ex.Message}");
        }
    }
    private void SetupAppActions()
    {
        try
        {
            #if WINDOWS
            //AppActions.IconDirectory = Application.Current.On<WindowsConfiguration>().GetImageDirectory();
            #endif
            AppActions.SetAsync(
                new AppAction("current_info", "Check Current Weather", icon: "current_info"),
                new AppAction("add_location", "Add a Location", icon: "add_location")
            );
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine("App Actions not supported", ex);
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
                    ?.ShowNotification("Hello Build! 😻 From .NET MAUI", "How's your weather?  It's sunny where we are 🌞");
        }
    }
}
