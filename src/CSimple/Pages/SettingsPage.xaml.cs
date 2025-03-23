using CSimple.ViewModels;
using System.Diagnostics;
namespace CSimple.Pages;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using CSimple.Services;

public partial class SettingsPage : ContentPage
{
    private readonly DataService _dataService;
    private readonly SettingsService _settingsService;
    public ICommand LogoutCommand { get; }

    public SettingsPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
        _settingsService = new SettingsService(dataService);
        LogoutCommand = new Command(ExecuteLogout);
        BindingContext = new SettingsViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var (userNickname, userEmail) = await _settingsService.LoadUserData();
        if (!string.IsNullOrEmpty(userNickname) || !string.IsNullOrEmpty(userEmail))
        {
            Debug.WriteLine($"userNickname: {userNickname}, userEmail: {userEmail}");
            UserNicknameLabel.Text = userNickname;
            UserEmailLabel.Text = userEmail;
        }
        else
        {
            Debug.WriteLine($"Error: Nickname and Email returned empty.");
            ExecuteLogout();
        }
        await UpdateButtonText();
        TimeZonePicker.ItemsSource = _settingsService.TimeZones;
    }

    private void UpdateAccountSectionVisibility()
    {
        AccountSection.IsVisible = SignOutButton.Text == "Sign Out";
    }

    private async Task UpdateButtonText()
    {
        bool isLoggedIn = await _settingsService.IsUserLoggedInAsync();
        SignOutButton.Text = isLoggedIn ? "Sign Out" : "Sign In";
        UpdateAccountSectionVisibility();
    }

    async void OnSignClick(object sender, EventArgs eventArgs)
    {
        if (SignOutButton.Text == "Sign Out")
        {
            bool confirm = await DisplayAlert("Sign Out", "Are you sure?", "Yes", "No");
            if (confirm)
            {
                try
                {
                    ExecuteLogout();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Sign out error: {ex.Message}");
                }
            }
        }
        else
        {
            await Task.Run(() =>
            {
                Shell.Current.GoToAsync($"///login");
            });
        }
    }

    private void ExecuteLogout()
    {
        try
        {
            _settingsService.Logout();
            Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Logout error: {ex.Message}");
        }
    }

    async void OnSupportTapped(object sender, EventArgs eventArgs)
    {
        string action = await DisplayActionSheet("Get Help", "Cancel", null, "Email", "Chat", "Phone");
    }

    void RadioButton_CheckedChanged(System.Object sender, CheckedChangedEventArgs e)
    {
        AppTheme val = (AppTheme)((RadioButton)sender).Value;
        _settingsService.SetAppTheme(val);
    }

    private async void TimeZonePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        var selectedZone = (string)TimeZonePicker.SelectedItem;
        await _settingsService.UpdateUserTimeZone(selectedZone);
    }
}
