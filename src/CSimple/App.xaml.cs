using System.Diagnostics;
using System.Windows.Input;
using CSimple.Pages; // Assuming HomePage is in the Pages namespace

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
        // Add other routes as needed

        //App.Current.UserAppTheme = AppTheme.Dark;

        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            Shell.Current.CurrentItem = PhoneTabs;
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
