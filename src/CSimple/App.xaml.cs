using System.Diagnostics;
using System.Windows.Input;
using CSimple.Pages;
using System;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml;
using CSimple.Converters;
using CSimple.Views;

[assembly: XamlCompilation(XamlCompilationOptions.Compile)]

namespace CSimple;

public partial class App : Application
{
    public ICommand ToggleFlyoutCommand { get; }

    public App()
    {
        InitializeComponent();

        ToggleFlyoutCommand = new Command(() =>
        {
            Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
        });

        // Register routes
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
        Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
        Routing.RegisterRoute(nameof(NetPage), typeof(NetPage));

        //App.Current.UserAppTheme = AppTheme.Dark;

        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            Shell.Current.CurrentItem = PhoneTabs;

        // Register the InverseBoolConverter if it's not automatically registered
        if (!Resources.TryGetValue("InverseBoolConverter", out _))
        {
            Resources.Add("InverseBoolConverter", new InverseBoolConverter());
        }

        // Add converter resources here after the app is initialized
        Resources.Add("BoolToColorConverter", new BoolToColorConverter());
        Resources.Add("IntToColorConverter", new IntToColorConverter());
        Resources.Add("IntToBoolConverter", new IntToBoolConverter());

        // MainPage = new AppShell();
    }

    async void TapGestureRecognizer_Tapped(System.Object sender, System.EventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync($"///settings");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"err: {ex.Message}");
        }
    }
}
