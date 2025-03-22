using System.Diagnostics;
using System.Windows.Input;

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

        //App.Current.UserAppTheme = AppTheme.Dark;

        if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            Shell.Current.CurrentItem = PhoneTabs;

        //Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
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
