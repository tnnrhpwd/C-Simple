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
using static CSimple.Services.SettingsService;

public partial class SettingsPage : ContentPage
{
    private readonly DataService _dataService;
    private readonly SettingsService _settingsService;
    public ICommand LogoutCommand { get; }
    private Dictionary<string, Switch> _permissionSwitches;
    private Dictionary<string, Switch> _featureSwitches;
    private MembershipTier _currentTier = MembershipTier.Free;

    public SettingsPage(DataService dataService)
    {
        InitializeComponent();
        _dataService = dataService;
        _settingsService = new SettingsService(dataService);
        LogoutCommand = new Command(ExecuteLogout);
        BindingContext = new SettingsViewModel();

        _permissionSwitches = new Dictionary<string, Switch>
        {
            { "ScreenCapture", ScreenCaptureSwitch },
            { "AudioCapture", AudioCaptureSwitch },
            { "KeyboardSimulation", KeyboardSimulationSwitch },
            { "MouseSimulation", MouseSimulationSwitch },
            { "VoiceOutput", VoiceOutputSwitch },
            { "SystemCommands", SystemCommandsSwitch }
        };

        _featureSwitches = new Dictionary<string, Switch>
        {
            { "ObserveMode", ObserveModeSwitch },
            { "OrientMode", OrientModeSwitch },
            { "PlanMode", PlanModeSwitch },
            { "ActionMode", ActionModeSwitch }
        };
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

        // Initialize AI settings
        await InitializeAISettings();
        await LoadMembershipDataAsync();
    }

    private async Task InitializeAISettings()
    {
        // Initialize model picker
        ModelPicker.ItemsSource = _settingsService.AvailableModels;
        var activeModel = await _settingsService.GetActiveModelAsync();
        ModelPicker.SelectedItem = activeModel;

        // Initialize permissions
        var permissions = await _settingsService.LoadPermissionsAsync();
        foreach (var permission in permissions)
        {
            if (_permissionSwitches.TryGetValue(permission.Key, out Switch sw))
            {
                sw.IsToggled = permission.Value;
            }
        }

        // Initialize feature states
        var features = await _settingsService.GetFeatureStatesAsync();
        foreach (var feature in features)
        {
            if (_featureSwitches.TryGetValue(feature.Key, out Switch sw))
            {
                sw.IsToggled = feature.Value;
            }
        }
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

    private async void ModelPicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        string selectedModel = ModelPicker.SelectedItem?.ToString();
        if (!string.IsNullOrEmpty(selectedModel))
        {
            await _settingsService.SetActiveModelAsync(selectedModel);
        }
    }

    private async void Permission_Toggled(object sender, ToggledEventArgs e)
    {
        var sw = (Switch)sender;
        var permission = _permissionSwitches.FirstOrDefault(x => x.Value == sw).Key;

        if (!string.IsNullOrEmpty(permission))
        {
            await _settingsService.SavePermissionAsync(permission, e.Value);

            // If this is a sensitive permission, show warning
            if (e.Value && (permission == "KeyboardSimulation" || permission == "MouseSimulation" || permission == "SystemCommands"))
            {
                bool confirmed = await DisplayAlert("Security Warning",
                    $"Enabling {permission} allows the AI to control your device. Are you sure?",
                    "Yes", "No");

                if (!confirmed)
                {
                    sw.IsToggled = false;
                    await _settingsService.SavePermissionAsync(permission, false);
                }
            }
        }
    }

    private async void FeatureMode_Toggled(object sender, ToggledEventArgs e)
    {
        var sw = (Switch)sender;
        var feature = _featureSwitches.FirstOrDefault(x => x.Value == sw).Key;

        if (!string.IsNullOrEmpty(feature))
        {
            await _settingsService.SaveFeatureStateAsync(feature, e.Value);
        }
    }

    private async void ImportModel_Clicked(object sender, EventArgs e)
    {
        try
        {
            // In a real app, use FilePicker or similar
            string result = await DisplayPromptAsync("Import Model", "Enter model path or URL:", "Import", "Cancel");

            if (!string.IsNullOrEmpty(result))
            {
                bool success = await _settingsService.ImportModelAsync(result);
                if (success)
                {
                    await DisplayAlert("Success", "Model imported successfully", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to import model", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Import model error: {ex.Message}");
            await DisplayAlert("Error", "An error occurred during import", "OK");
        }
    }

    private async void ExportModel_Clicked(object sender, EventArgs e)
    {
        try
        {
            string model = ModelPicker.SelectedItem?.ToString();
            if (string.IsNullOrEmpty(model))
            {
                await DisplayAlert("Error", "Please select a model first", "OK");
                return;
            }

            // In a real app, use FolderPicker or similar
            string destination = await DisplayPromptAsync("Export Model", "Enter export destination:", "Export", "Cancel");

            if (!string.IsNullOrEmpty(destination))
            {
                bool success = await _settingsService.ExportModelAsync(model, destination);
                if (success)
                {
                    await DisplayAlert("Success", "Model exported successfully", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to export model", "OK");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Export model error: {ex.Message}");
            await DisplayAlert("Error", "An error occurred during export", "OK");
        }
    }

    private async void UpgradePlan_Clicked(object sender, EventArgs e)
    {
        // Show upgrade options based on current tier
        string title = "Upgrade Plan";
        string message = "Choose your new membership tier:";
        string cancel = "Cancel";

        string option1 = _currentTier == MembershipTier.Free ? "Flex ($9.99/month)" : "Premium ($19.99/month)";
        string option2 = _currentTier == MembershipTier.Premium ? "Flex ($9.99/month)" : "Premium ($19.99/month)";

        if (_currentTier == MembershipTier.Premium)
        {
            await DisplayAlert("Upgrade Plan", "You are already on our highest tier plan.", "OK");
            return;
        }

        string action = await DisplayActionSheet(title, cancel, null, option1, option2);

        if (action == option1)
        {
            if (_currentTier == MembershipTier.Free)
            {
                await _settingsService.SetMembershipTierAsync(MembershipTier.Flex);
                await DisplayAlert("Plan Upgraded", "You have successfully upgraded to the Flex plan.", "OK");
            }
            else
            {
                await _settingsService.SetMembershipTierAsync(MembershipTier.Premium);
                await DisplayAlert("Plan Upgraded", "You have successfully upgraded to the Premium plan.", "OK");
            }
        }
        else if (action == option2)
        {
            await _settingsService.SetMembershipTierAsync(MembershipTier.Premium);
            await DisplayAlert("Plan Upgraded", "You have successfully upgraded to the Premium plan.", "OK");
        }

        // Refresh the membership data display
        await LoadMembershipDataAsync();
    }

    private async void ViewBilling_Clicked(object sender, EventArgs e)
    {
        // For demonstration purposes, just show a simple alert with billing information
        var stats = await _settingsService.GetUsageStatisticsAsync();

        string billingInfo = $"Current Plan: {_currentTier}\n" +
                            $"Billing Cycle End: {stats.BillingCycleEnd.ToShortDateString()}\n\n" +
                            $"Recent Charges:\n" +
                            $"- {DateTime.Now.AddMonths(-1).ToString("MMM d, yyyy")}: ${GetPlanPrice(_currentTier)}\n";

        await DisplayAlert("Billing History", billingInfo, "OK");
    }

    private string GetPlanPrice(MembershipTier tier)
    {
        return tier switch
        {
            MembershipTier.Free => "0.00",
            MembershipTier.Flex => "9.99",
            MembershipTier.Premium => "19.99",
            _ => "0.00",
        };
    }

    private async Task LoadMembershipDataAsync()
    {
        try
        {
            // Get membership tier and update display
            _currentTier = await _settingsService.GetMembershipTierAsync();
            MembershipTierLabel.Text = _currentTier.ToString();

            // Get usage statistics
            var stats = await _settingsService.GetUsageStatisticsAsync();
            ProcessingTimeLabel.Text = $"{stats.ProcessingMinutes} minutes";
            ApiCallsLabel.Text = $"{stats.ApiCalls} / {stats.ApiCallsLimit}";
            StorageLabel.Text = $"{stats.StorageMB:F1} MB / {stats.StorageLimitMB:F1} MB";
            BillingCycleLabel.Text = stats.BillingCycleEnd.ToString("MMM d, yyyy");

            // Update membership features description
            MembershipFeaturesLabel.Text = _settingsService.GetMembershipFeatures(_currentTier);

            // Update button visibility based on tier
            UpgradePlanButton.IsVisible = _currentTier != MembershipTier.Premium;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading membership data: {ex.Message}");
            await DisplayAlert("Error", "Failed to load membership data", "OK");
        }
    }
}
