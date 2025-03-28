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
        try
        {
            // Create converters before initialization
            var boolToColorConverter = new BoolToColorConverter();
            var intToColorConverter = new IntToColorConverter();
            var intToBoolConverter = new IntToBoolConverter();
            var inverseBoolConverter = new InverseBoolConverter();

            Debug.WriteLine("App constructor: Converters created");

            InitializeComponent();

            Debug.WriteLine("App constructor: InitializeComponent completed");

            // Register converters directly after initialization to ensure they're available
            try
            {
                Resources["BoolToColorConverter"] = boolToColorConverter;
                Resources["IntToColorConverter"] = intToColorConverter;
                Resources["IntToBoolConverter"] = intToBoolConverter;
                Resources["InverseBoolConverter"] = inverseBoolConverter;

                Debug.WriteLine("App constructor: Converters registered successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering converters: {ex.Message}");
            }

            // Register routes
            Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(HomePage), typeof(HomePage));
            Routing.RegisterRoute(nameof(NetPage), typeof(NetPage));

            //App.Current.UserAppTheme = AppTheme.Dark;

            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
                Shell.Current.CurrentItem = PhoneTabs;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Critical error in App constructor: {ex.Message}");
        }

        ToggleFlyoutCommand = new Command(() =>
        {
            Shell.Current.FlyoutIsPresented = !Shell.Current.FlyoutIsPresented;
        });
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
