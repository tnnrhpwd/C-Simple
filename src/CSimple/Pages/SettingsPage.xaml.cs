using CSimple.ViewModels;
using System.Diagnostics;
namespace CSimple.Pages;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;

public partial class SettingsPage : ContentPage
{
    private readonly DataService _dataService;
    public ICommand LogoutCommand { get; }
    private List<string> _timeZones = TimeZoneInfo.GetSystemTimeZones()
        .Select(tz => tz.DisplayName)
        .ToList();

    public SettingsPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
        LogoutCommand = new Command(ExecuteLogout);
        BindingContext = new SettingsViewModel();
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadUserData();
        await UpdateButtonText();
        TimeZonePicker.ItemsSource = _timeZones;
    }
    // Load user data from SecureStorage
    private async Task LoadUserData()
    {
        try
        {
            // Retrieve userNickname from secure storage
            string userNickname = await SecureStorage.GetAsync("userNickname");
            string userEmail = await SecureStorage.GetAsync("userEmail");

            // Bind the retrieved nickname to the UI label
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
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving user data: {ex.Message}");
        }
    }
    private void UpdateAccountSectionVisibility()
    {
        AccountSection.IsVisible = SignOutButton.Text == "Sign Out";
    }
    private async Task UpdateButtonText()
    {
        bool isLoggedIn = await IsUserLoggedIn();
        SignOutButton.Text = isLoggedIn ? "Sign Out" : "Sign In";
        UpdateAccountSectionVisibility();
    }
    // Handle sign-out
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

    // Execute logout and navigate to login page
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

    // Handle support action
    async void OnSupportTapped(object sender, EventArgs eventArgs)
    {
        string action = await DisplayActionSheet("Get Help", "Cancel", null, "Email", "Chat", "Phone");
        // await DisplayAlert("You Chose", action, "Okay");
    }

    // Handle theme change
    void RadioButton_CheckedChanged(System.Object sender, CheckedChangedEventArgs e)
    {
        AppTheme val = (AppTheme)((RadioButton)sender).Value;
        if (App.Current.UserAppTheme == val)
            return;

        App.Current.UserAppTheme = val;
    }

    private void TimeZonePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        string selectedZone = (string)TimeZonePicker.SelectedItem;
        // Handle or store 'selectedZone'
    }
}
