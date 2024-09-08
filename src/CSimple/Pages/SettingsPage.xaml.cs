using CSimple.ViewModels;
using System.Diagnostics;
namespace CSimple.Pages;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System.Threading.Tasks;
using CSimple.Services;
using Microsoft.Maui.Controls.PlatformConfiguration.WindowsSpecific;
using Application = Microsoft.Maui.Controls.Application;
using WindowsConfiguration = Microsoft.Maui.Controls.PlatformConfiguration.Windows;
using System.Windows.Input;
using System;

public partial class SettingsPage : ContentPage
{
    private readonly DataService _dataService;
    public ICommand LogoutCommand { get; }

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
            }else{
                Debug.WriteLine($"Error: Nickname and Email returned empty.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving user data: {ex.Message}");
        }
    }
    private async Task UpdateButtonText()
    {
        bool isLoggedIn = await IsUserLoggedIn();
        SignOutButton.Text = isLoggedIn ? "Sign Out" : "Sign In";
    }
    // Handle sign-out
    async void OnSignOut(object sender, EventArgs eventArgs)
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
        await DisplayAlert("You Chose", action, "Okay");
    }

    // Handle theme change
    void RadioButton_CheckedChanged(System.Object sender, CheckedChangedEventArgs e)
    {
        AppTheme val = (AppTheme)((RadioButton)sender).Value;
        if (App.Current.UserAppTheme == val)
            return;

        App.Current.UserAppTheme = val;
    }
}
