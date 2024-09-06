using CSimple.ViewModels;
using System.Diagnostics;
namespace CSimple.Pages;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();

        BindingContext = new SettingsViewModel();
        LoadUserData();
    }
    private async void LoadUserData()
    {
        // string userToken = await SecureStorage.GetAsync("userToken");
        string userNickname = await SecureStorage.GetAsync("userNickname");
        // string userEmail = await SecureStorage.GetAsync("userEmail");

        // Bind the data to UI elements
        UserNicknameLabel.Text = userNickname;
    }
    async void OnSignOut(object sender, EventArgs eventArgs)
    {
        await DisplayAlert("Sign Out", "Are you sure?", "Yes", "No");
        try
        {
            await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"err: {ex.Message}");
        }
    }

    async void OnSupportTapped(object sender, EventArgs eventArgs)
    {
        string action = await DisplayActionSheet("Get Help", "Cancel", null, "Email", "Chat", "Phone");
        await DisplayAlert("You Chose", action, "Okay");
    }

    void RadioButton_CheckedChanged(System.Object sender, CheckedChangedEventArgs e)
    {
        AppTheme val = (AppTheme)((RadioButton)sender).Value;
        if (App.Current.UserAppTheme == val)
            return;

        App.Current.UserAppTheme = val;
    }
}
