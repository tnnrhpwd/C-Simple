using CSimple.ViewModels;
using System.Diagnostics;
using CSimple.Services;
using System.Collections.ObjectModel;
namespace CSimple.Pages;

using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage; // For Preferences
using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using CSimple.Services;
using static CSimple.Services.SettingsService;

// Data model for recent usage display
public class UsageItem
{
    public string ApiDisplayName { get; set; }
    public string Date { get; set; }
    public string Usage { get; set; }
    public string Cost { get; set; }
}

// Enhanced usage statistics model
public class EnhancedUsageStats
{
    public decimal CurrentUsage { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal RemainingBalance { get; set; }
    public double UsagePercentage { get; set; }
    public bool IsUnlimited { get; set; }
    public List<UsageItem> RecentUsage { get; set; } = new List<UsageItem>();
}

public partial class SettingsPage : ContentPage
{
    private readonly DataService _dataService;
    private readonly SettingsService _settingsService;
    private readonly IDebugConsoleService _debugConsoleService;
    private readonly IAppPathService _appPathService;
    public ICommand LogoutCommand { get; }
    private Dictionary<string, Switch> _permissionSwitches;
    private Dictionary<string, Switch> _featureSwitches;
    private MembershipTier _currentTier = MembershipTier.Free;
    private EnhancedUsageStats _enhancedUsageStats;
    private ObservableCollection<UsageItem> _recentUsageItems;

    private readonly string[] _gpuCapabilityLevels = new[]
    {
        "CPU Only",
        "NVIDIA GTX 10xx (6GB+)",
        "NVIDIA RTX 20xx/30xx (8GB+)",
        "NVIDIA RTX 40xx (12GB+)",
        "Apple M1/M2",
        "AMD RDNA2 (8GB+)",
        "A100/H100/FP8 (24GB+)",
        "Other/Unknown"
    };

    public SettingsPage(DataService dataService, IDebugConsoleService debugConsoleService, IAppPathService appPathService = null)
    {
        InitializeComponent();
        _dataService = dataService;
        _debugConsoleService = debugConsoleService;
        _appPathService = appPathService;
        _settingsService = new SettingsService(dataService, appPathService);
        LogoutCommand = new Command(ExecuteLogout);
        BindingContext = new SettingsViewModel();

        // Initialize collections
        _recentUsageItems = new ObservableCollection<UsageItem>();
        RecentUsageList.ItemsSource = _recentUsageItems;

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

        // Set GPU capability picker items
        GpuCapabilityPicker.ItemsSource = _gpuCapabilityLevels;
        // Default to CPU Only if not set
        GpuCapabilityPicker.SelectedIndex = Preferences.Get("ModelCompat_GpuCapabilityIndex", 0);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load and display app version
        LoadAppVersion();

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

        // Initialize membership tier if not set (for debugging)
        await EnsureMembershipTierIsSet();

        await LoadMembershipDataAsync();

        LoadModelCompatibilitySettings();

        // Initialize debug console switch state
        bool debugConsoleEnabled = Preferences.Get("DebugConsoleEnabled", false);
        DebugConsoleSwitch.IsToggled = debugConsoleEnabled;

        // Initialize intelligence settings
        LoadIntelligenceSettings();

        // Load and display current application paths
        await LoadApplicationPaths();
    }

    private async Task EnsureMembershipTierIsSet()
    {
        try
        {
            Debug.WriteLine("Ensuring membership tier is properly set...");
            var currentTier = await _settingsService.GetMembershipTierAsync();
            Debug.WriteLine($"Current stored tier: {currentTier}");

            // If somehow the tier is not properly stored, you can force set it here
            // Remove this once you've confirmed the tier is properly persisting
            if (currentTier == MembershipTier.Free)
            {
                Debug.WriteLine("Tier is Free - this might be incorrect if user should be on Flex plan");
                // Uncomment the next line to force set to Flex for testing
                // await _settingsService.SetMembershipTierAsync(MembershipTier.Flex);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error ensuring membership tier: {ex.Message}");
        }
    }

    // Application Path Management Methods
    private Task LoadApplicationPaths()
    {
        try
        {
            var basePath = _settingsService.GetApplicationBasePath();
            CurrentBasePathLabel.Text = basePath;

            // Clear existing path details
            PathDetailsContainer.Children.Clear();

            // Get all paths and display them
            var allPaths = _settingsService.GetAllApplicationPaths();
            foreach (var pathInfo in allPaths)
            {
                if (pathInfo.Key != "BasePath") // Skip base path as it's already shown
                {
                    var pathLabel = new Label
                    {
                        Text = $"• {pathInfo.Key.Replace("Path", "")}: {pathInfo.Value}",
                        TextColor = Application.Current.RequestedTheme == AppTheme.Dark
                            ? Color.FromArgb("#b0b0b0")
                            : Color.FromArgb("#757575"),
                        FontSize = 12,
                        Margin = new Thickness(0, 1)
                    };
                    PathDetailsContainer.Children.Add(pathLabel);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading application paths: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async void OnChangeBasePathClicked(object sender, EventArgs e)
    {
        try
        {
            var action = await DisplayActionSheet(
                "Change Application Folder",
                "Cancel",
                null,
                "Select Custom Folder",
                "Use Documents Folder",
                "Use Desktop Folder");

            string newBasePath = null;

            switch (action)
            {
                case "Use Documents Folder":
                    newBasePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    break;
                case "Use Desktop Folder":
                    newBasePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    break;
                case "Select Custom Folder":
                    // For now, let user type the path. In a full implementation, 
                    // you might want to use a folder picker
                    newBasePath = await DisplayPromptAsync(
                        "Custom Folder Path",
                        "Enter the full path where you want to store CSimple data:",
                        "OK",
                        "Cancel",
                        _settingsService.GetApplicationBasePath());
                    break;
                default:
                    return; // User cancelled
            }

            if (!string.IsNullOrWhiteSpace(newBasePath))
            {
                var success = await _settingsService.SetApplicationBasePath(newBasePath);
                if (success)
                {
                    await DisplayAlert("Success",
                        $"Application folder changed to: {newBasePath}/CSimple\n\nNote: You may need to restart the application for all changes to take effect.",
                        "OK");
                    await LoadApplicationPaths(); // Refresh the display
                }
                else
                {
                    await DisplayAlert("Error",
                        "Failed to change application folder. Please ensure the path is valid and accessible.",
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error changing base path: {ex.Message}");
            await DisplayAlert("Error",
                $"An error occurred while changing the application folder: {ex.Message}",
                "OK");
        }
    }

    private async void OnResetPathsClicked(object sender, EventArgs e)
    {
        try
        {
            var confirm = await DisplayAlert("Reset Paths",
                "This will reset the application folder to the default location (Documents/CSimple). Do you want to continue?",
                "Yes",
                "No");

            if (confirm)
            {
                var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var success = await _settingsService.SetApplicationBasePath(defaultPath);

                if (success)
                {
                    await DisplayAlert("Success",
                        "Application folder has been reset to the default location.\n\nNote: You may need to restart the application for all changes to take effect.",
                        "OK");
                    await LoadApplicationPaths(); // Refresh the display
                }
                else
                {
                    await DisplayAlert("Error",
                        "Failed to reset application folder to default location.",
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error resetting paths: {ex.Message}");
            await DisplayAlert("Error",
                $"An error occurred while resetting paths: {ex.Message}",
                "OK");
        }
    }

    private void LoadAppVersion()
    {
        try
        {
            var version = AppInfo.VersionString;
            var buildString = AppInfo.BuildString;

            // Display version with build number
            AppVersionLabel.Text = $"Version {version} (Build {buildString})";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading app version: {ex.Message}");
            AppVersionLabel.Text = "Version information unavailable";
        }
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

    private async void RefreshMembership_Clicked(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("Manual membership refresh triggered");

            // Show options for debugging
            var action = await DisplayActionSheet(
                "Debug Membership",
                "Cancel",
                null,
                "Set to Free",
                "Set to Flex",
                "Set to Premium",
                "Just Refresh");

            switch (action)
            {
                case "Set to Free":
                    await _settingsService.SetMembershipTierAsync(MembershipTier.Free);
                    break;
                case "Set to Flex":
                    await _settingsService.SetMembershipTierAsync(MembershipTier.Flex);
                    break;
                case "Set to Premium":
                    await _settingsService.SetMembershipTierAsync(MembershipTier.Premium);
                    break;
                case "Just Refresh":
                    // Do nothing, just refresh
                    break;
                default:
                    return;
            }

            // Force refresh the membership display
            await LoadMembershipDataAsync();
            await DisplayAlert("Debug", $"Membership refreshed. Current tier: {_currentTier}", "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in RefreshMembership_Clicked: {ex.Message}");
            await DisplayAlert("Error", $"Failed to refresh membership: {ex.Message}", "OK");
        }
    }

    private async void ResetPassword_Clicked(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("Password reset requested");

            // Get user email for display
            var userEmail = await SecureStorage.GetAsync("userEmail");
            if (string.IsNullOrEmpty(userEmail))
            {
                await DisplayAlert("Error", "Unable to send password reset email. No email address found.", "OK");
                return;
            }

            // Confirm action
            bool confirmed = await DisplayAlert(
                "Reset Password",
                $"Send password reset email to {userEmail}?",
                "Send",
                "Cancel"
            );

            if (!confirmed)
                return;

            // Show loading state
            var button = sender as Button;
            var originalText = button?.Text;
            if (button != null)
            {
                button.Text = "📤 Sending...";
                button.IsEnabled = false;
            }

            // Send password reset email
            bool success = await _dataService.SendPasswordResetAsync();

            // Restore button state
            if (button != null)
            {
                button.Text = originalText;
                button.IsEnabled = true;
            }

            if (success)
            {
                await DisplayAlert(
                    "Success",
                    $"Password reset email sent to {userEmail}. Check your inbox and follow the instructions to reset your password.",
                    "OK"
                );
            }
            else
            {
                await DisplayAlert(
                    "Error",
                    "Failed to send password reset email. Please try again later.",
                    "OK"
                );
            }
        }
        catch (UnauthorizedAccessException)
        {
            await DisplayAlert("Error", "Your session has expired. Please log in again.", "OK");
            // Navigate to login if needed
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ResetPassword_Clicked: {ex.Message}");
            await DisplayAlert("Error", "Failed to send password reset email. Please try again.", "OK");
        }
    }

    private async void ViewBilling_Clicked(object sender, EventArgs e)
    {
        try
        {
            var stats = await _settingsService.GetUsageStatisticsAsync();

            string billingInfo = $"Current Plan: {_currentTier}\n" +
                                $"Billing Cycle End: {stats.BillingCycleEnd.ToShortDateString()}\n\n";

            if (_enhancedUsageStats != null)
            {
                billingInfo += "Current Usage Summary:\n" +
                              $"• API Usage: ${_enhancedUsageStats.CurrentUsage:F4}\n" +
                              $"• Monthly Limit: {(_enhancedUsageStats.IsUnlimited ? "Unlimited" : $"${_enhancedUsageStats.MonthlyLimit:F2}")}\n" +
                              $"• Remaining: {(_enhancedUsageStats.IsUnlimited ? "Unlimited" : $"${_enhancedUsageStats.RemainingBalance:F4}")}\n\n";

                if (_enhancedUsageStats.RecentUsage?.Any() == true)
                {
                    billingInfo += "Recent Charges:\n";
                    foreach (var usage in _enhancedUsageStats.RecentUsage.Take(3))
                    {
                        billingInfo += $"• {usage.Date}: {usage.Cost}\n";
                    }
                }
            }
            else
            {
                billingInfo += $"Recent Charges:\n" +
                              $"• {DateTime.Now.AddMonths(-1).ToString("MMM d, yyyy")}: ${GetPlanPrice(_currentTier)}\n";
            }

            await DisplayAlert("Billing History", billingInfo, "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error displaying billing info: {ex.Message}");
            await DisplayAlert("Error", "Failed to load billing information", "OK");
        }
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
            Debug.WriteLine("Starting LoadMembershipDataAsync...");

            // Get stored user data for basic info
            var user = await _dataService.GetStoredUserAsync();
            if (user != null)
            {
                Debug.WriteLine($"LoadMembershipDataAsync: Found user - Nickname: {user.Nickname}, Email: {user.Email}");
                UserNicknameLabel.Text = user.Nickname ?? "N/A";
                UserEmailLabel.Text = user.Email ?? "N/A";
            }
            else
            {
                Debug.WriteLine("LoadMembershipDataAsync: No stored user found");
                UserNicknameLabel.Text = "N/A";
                UserEmailLabel.Text = "N/A";
            }

            // Get subscription data from backend API
            var subscription = await _dataService.GetUserSubscriptionAsync();
            if (subscription != null && !string.IsNullOrEmpty(subscription.SubscriptionPlan))
            {
                Debug.WriteLine($"LoadMembershipDataAsync: Backend subscription data - Plan: {subscription.SubscriptionPlan}");

                // Convert subscription plan to membership tier
                _currentTier = subscription.SubscriptionPlan.ToLower() switch
                {
                    "flex" => MembershipTier.Flex,
                    "premium" => MembershipTier.Premium,
                    _ => MembershipTier.Free
                };

                // Update displays
                MembershipTierLabel.Text = _currentTier.ToString();

                // Store updated membership locally for consistency
                await _settingsService.SetMembershipTierAsync(_currentTier);
                Debug.WriteLine($"LoadMembershipDataAsync: Updated to {_currentTier} from backend plan: {subscription.SubscriptionPlan}");
            }
            else
            {
                Debug.WriteLine("LoadMembershipDataAsync: Failed to get subscription data from backend, falling back to local storage");

                // Fallback to local settings if API fails
                _currentTier = await _settingsService.GetMembershipTierAsync();
                MembershipTierLabel.Text = _currentTier.ToString();
                Debug.WriteLine($"LoadMembershipDataAsync: Using fallback membership: {_currentTier}");
            }

            // Get usage statistics from backend
            var backendUsage = await _dataService.GetUserUsageAsync();
            var backendStorage = await _dataService.GetUserStorageAsync();
            UsageStatistics localStats = null;

            if (backendUsage != null)
            {
                Debug.WriteLine($"Retrieved backend usage data - Available Credits: ${backendUsage.AvailableCredits:F4}, Total Usage: ${backendUsage.TotalUsage:F4}");

                // Use real backend data
                await LoadEnhancedUsageStatsFromBackend(backendUsage, backendStorage);
            }
            else
            {
                Debug.WriteLine("Failed to get backend usage data, falling back to local stats");

                // Fallback to local usage statistics
                localStats = await _settingsService.GetUsageStatisticsAsync();
                await LoadEnhancedUsageStats(localStats);
            }

            // Update legacy labels (hidden by default) - use fallback if needed
            if (localStats == null)
                localStats = await _settingsService.GetUsageStatisticsAsync();

            ProcessingTimeLabel.Text = $"{localStats.ProcessingMinutes} minutes";
            ApiCallsLabel.Text = $"{localStats.ApiCalls} / {localStats.ApiCallsLimit}";
            StorageLabel.Text = $"{localStats.StorageMB:F1} MB / {localStats.StorageLimitMB:F1} MB";
            BillingCycleLabel.Text = localStats.BillingCycleEnd.ToString("MMM d, yyyy");

            // Update membership features description
            MembershipFeaturesLabel.Text = _settingsService.GetMembershipFeatures(_currentTier);

            // Update button visibility based on tier
            UpgradePlanButton.IsVisible = _currentTier != MembershipTier.Premium;

            // Update enhanced UI
            UpdateEnhancedUsageDisplay();

            // Update storage display if we have backend storage data
            if (backendStorage != null)
            {
                UpdateStorageDisplay(backendStorage);
            }

            Debug.WriteLine($"Completed LoadMembershipDataAsync successfully with tier: {_currentTier}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading membership data: {ex.Message}");
            await DisplayAlert("Error", "Failed to load membership data", "OK");
        }
    }

    private async Task LoadEnhancedUsageStats(UsageStatistics stats)
    {
        try
        {
            Debug.WriteLine($"Loading enhanced usage stats for tier: {_currentTier}");

            // Calculate enhanced usage statistics
            decimal currentUsage = (decimal)(stats.ApiCalls * 0.002); // Estimate $0.002 per API call
            decimal monthlyLimit = _currentTier switch
            {
                MembershipTier.Free => 0m,
                MembershipTier.Flex => 10m,
                MembershipTier.Premium => decimal.MaxValue, // Unlimited
                _ => 0m
            };

            bool isUnlimited = _currentTier == MembershipTier.Premium;
            decimal remainingBalance = isUnlimited ? decimal.MaxValue : Math.Max(0, monthlyLimit - currentUsage);
            double usagePercentage = isUnlimited ? 0 : monthlyLimit > 0 ? (double)(currentUsage / monthlyLimit * 100) : 0;

            _enhancedUsageStats = new EnhancedUsageStats
            {
                CurrentUsage = currentUsage,
                MonthlyLimit = monthlyLimit,
                RemainingBalance = remainingBalance,
                UsagePercentage = usagePercentage,
                IsUnlimited = isUnlimited,
                RecentUsage = await GenerateRecentUsageData(stats)
            };

            Debug.WriteLine($"Enhanced stats calculated - Current: ${currentUsage:F4}, Limit: {(isUnlimited ? "Unlimited" : $"${monthlyLimit:F2}")}, Remaining: {(isUnlimited ? "Unlimited" : $"${remainingBalance:F4}")}, Percentage: {usagePercentage:F1}%");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading enhanced usage stats: {ex.Message}");
            // Fallback to default values
            _enhancedUsageStats = new EnhancedUsageStats();
        }
    }

    private Task<List<UsageItem>> GenerateRecentUsageData(UsageStatistics stats)
    {
        var recentUsage = new List<UsageItem>();

        try
        {
            // Generate sample recent usage data (you can replace this with actual data from your service)
            if (stats.ApiCalls > 0)
            {
                var random = new Random();
                var apiTypes = new[]
                {
                    ("🤖 OpenAI", "openai"),
                    ("📝 Word Generator", "rapidword"),
                    ("📚 Dictionary", "rapiddef")
                };

                for (int i = 0; i < Math.Min(5, stats.ApiCalls); i++)
                {
                    var apiType = apiTypes[random.Next(apiTypes.Length)];
                    var date = DateTime.Now.AddDays(-random.Next(0, 30));
                    var usage = random.Next(1, 10);
                    var cost = usage * 0.002m;

                    recentUsage.Add(new UsageItem
                    {
                        ApiDisplayName = apiType.Item1,
                        Date = date.ToString("MMM d, yyyy"),
                        Usage = $"{usage} calls",
                        Cost = $"${cost:F4}"
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error generating recent usage data: {ex.Message}");
        }

        return Task.FromResult(recentUsage.OrderByDescending(x => DateTime.Parse(x.Date)).ToList());
    }

    private Task LoadEnhancedUsageStatsFromBackend(DataModel.UserUsage backendUsage, DataModel.UserStorage backendStorage)
    {
        try
        {
            Debug.WriteLine($"Loading enhanced usage stats from backend data - Membership: {backendUsage.Membership}");

            // Use real backend data
            decimal currentUsage = backendUsage.TotalUsage;
            decimal monthlyLimit = backendUsage.CustomLimit ?? backendUsage.Limit;
            bool isUnlimited = backendUsage.Membership == "Premium" && monthlyLimit <= 0;
            decimal remainingBalance = backendUsage.AvailableCredits;
            double usagePercentage = backendUsage.PercentUsed;

            // Convert backend usage breakdown to UI format
            var recentUsage = new List<UsageItem>();
            if (backendUsage.UsageBreakdown?.Any() == true)
            {
                foreach (var breakdown in backendUsage.UsageBreakdown.Take(5))
                {
                    string apiDisplayName = breakdown.Api?.ToLower() switch
                    {
                        "openai" => "🤖 OpenAI",
                        "rapidword" => "📝 Word Generator",
                        "rapiddef" => "📚 Dictionary",
                        _ => $"🔧 {breakdown.Api}"
                    };

                    recentUsage.Add(new UsageItem
                    {
                        ApiDisplayName = apiDisplayName,
                        Date = breakdown.FullDate ?? breakdown.Date ?? DateTime.Now.ToString("MMM d, yyyy"),
                        Usage = breakdown.Usage ?? "1 call",
                        Cost = $"${breakdown.Cost:F4}"
                    });
                }
            }

            _enhancedUsageStats = new EnhancedUsageStats
            {
                CurrentUsage = currentUsage,
                MonthlyLimit = monthlyLimit,
                RemainingBalance = remainingBalance,
                UsagePercentage = usagePercentage,
                IsUnlimited = isUnlimited,
                RecentUsage = recentUsage.OrderByDescending(x => x.Date).ToList()
            };

            Debug.WriteLine($"Backend enhanced stats calculated - Current: ${currentUsage:F4}, Limit: {(isUnlimited ? "Unlimited" : $"${monthlyLimit:F2}")}, Remaining: ${remainingBalance:F4}, Percentage: {usagePercentage:F1}%");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading enhanced usage stats from backend: {ex.Message}");
            // Fallback to default values
            _enhancedUsageStats = new EnhancedUsageStats();
        }

        return Task.CompletedTask;
    }

    private async void RefreshUsage_Clicked(object sender, EventArgs e)
    {
        try
        {
            Debug.WriteLine("Manual refresh of usage data requested");

            // Show loading indicator
            var button = sender as Button;
            var originalText = button?.Text;
            if (button != null)
            {
                button.Text = "🔄 Refreshing...";
                button.IsEnabled = false;
            }

            // Refresh usage and storage data
            var backendUsage = await _dataService.GetUserUsageAsync();
            var backendStorage = await _dataService.GetUserStorageAsync();

            if (backendUsage != null)
            {
                await LoadEnhancedUsageStatsFromBackend(backendUsage, backendStorage);
                UpdateEnhancedUsageDisplay();

                if (backendStorage != null)
                {
                    UpdateStorageDisplay(backendStorage);
                }

                await DisplayAlert("Success", "Usage data refreshed successfully!", "OK");
            }
            else
            {
                await DisplayAlert("Error", "Failed to refresh usage data from server.", "OK");
            }

            // Restore button
            if (button != null)
            {
                button.Text = originalText;
                button.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error refreshing usage data: {ex.Message}");
            await DisplayAlert("Error", "Failed to refresh usage data.", "OK");
        }
    }

    private void UpdateStorageDisplay(DataModel.UserStorage storageData)
    {
        try
        {
            Debug.WriteLine($"Updating storage display - Used: {storageData.TotalStorageFormatted}, Limit: {storageData.StorageLimitFormatted}");

            // Update storage overview cards
            StorageTotalUsedLabel.Text = storageData.TotalStorageFormatted ?? "0 B";
            StorageLimitLabel.Text = storageData.StorageLimitFormatted ?? "10 MB";
            StorageItemCountLabel.Text = storageData.ItemCount.ToString();
            StorageFileCountLabel.Text = storageData.FileCount.ToString();

            // Update storage progress bar
            if (storageData.StorageUsagePercent > 0)
            {
                StorageProgressContainer.IsVisible = true;

                // Calculate progress bar width
                double progressWidth = Math.Min(storageData.StorageUsagePercent / 100.0 * 280, 280); // Assuming 280px container width
                StorageProgressBar.WidthRequest = progressWidth;

                // Update status and color based on percentage
                if (storageData.IsOverLimit)
                {
                    StorageStatusLabel.Text = "🚨 Over Limit";
                    StorageProgressBar.BackgroundColor = Color.FromArgb("#FF4757"); // Red
                }
                else if (storageData.StorageUsagePercent >= 90)
                {
                    StorageStatusLabel.Text = "⚠️ Nearly Full";
                    StorageProgressBar.BackgroundColor = Color.FromArgb("#FFA500"); // Orange
                }
                else if (storageData.StorageUsagePercent >= 75)
                {
                    StorageStatusLabel.Text = "🔶 High Usage";
                    StorageProgressBar.BackgroundColor = Color.FromArgb("#FFCC02"); // Yellow
                }
                else
                {
                    StorageStatusLabel.Text = $"✅ {storageData.StorageUsagePercent:F1}% Used";
                    StorageProgressBar.BackgroundColor = Color.FromArgb("#4CAF50"); // Green
                }
            }
            else
            {
                StorageProgressContainer.IsVisible = false;
            }

            // Show storage warnings
            if (storageData.IsOverLimit || storageData.IsNearLimit)
            {
                StorageWarningFrame.IsVisible = true;

                if (storageData.IsOverLimit)
                {
                    StorageWarningIcon.Text = "🚨";
                    StorageWarningTitle.Text = "Storage Limit Exceeded";
                    StorageWarningMessage.Text = "You've exceeded your storage limit. Delete some items or upgrade to continue storing data.";
                }
                else if (storageData.IsNearLimit)
                {
                    StorageWarningIcon.Text = "⚠️";
                    StorageWarningTitle.Text = "Storage Nearly Full";
                    StorageWarningMessage.Text = $"You're using {storageData.StorageUsagePercent:F1}% of your storage limit.";
                }
            }
            else
            {
                StorageWarningFrame.IsVisible = false;
            }

            Debug.WriteLine($"Storage display updated - Progress: {storageData.StorageUsagePercent:F1}%, Warning visible: {StorageWarningFrame.IsVisible}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating storage display: {ex.Message}");
        }
    }

    private void UpdateEnhancedUsageDisplay()
    {
        if (_enhancedUsageStats == null)
        {
            Debug.WriteLine("_enhancedUsageStats is null, cannot update display");
            return;
        }

        try
        {
            Debug.WriteLine($"Updating enhanced usage display for {_currentTier} tier");

            // Update usage overview cards
            CurrentUsageLabel.Text = $"${_enhancedUsageStats.CurrentUsage:F4}";
            MonthlyLimitLabel.Text = _enhancedUsageStats.IsUnlimited ? "∞ Unlimited" : $"${_enhancedUsageStats.MonthlyLimit:F2}";
            RemainingBalanceLabel.Text = _enhancedUsageStats.IsUnlimited ? "∞ Unlimited" : $"${_enhancedUsageStats.RemainingBalance:F4}";
            UsagePercentageLabel.Text = _enhancedUsageStats.IsUnlimited ? "N/A" : $"{_enhancedUsageStats.UsagePercentage:F1}%";

            Debug.WriteLine($"Updated labels - Usage: {CurrentUsageLabel.Text}, Limit: {MonthlyLimitLabel.Text}, Remaining: {RemainingBalanceLabel.Text}, Percentage: {UsagePercentageLabel.Text}");

            // Update progress bar
            if (!_enhancedUsageStats.IsUnlimited && _enhancedUsageStats.UsagePercentage > 0)
            {
                UsageProgressContainer.IsVisible = true;

                // Calculate progress bar width (relative to container)
                double progressWidth = Math.Min(_enhancedUsageStats.UsagePercentage / 100.0 * 280, 280); // Assuming 280px container width
                UsageProgressBar.WidthRequest = progressWidth;

                // Update status and color based on percentage
                if (_enhancedUsageStats.UsagePercentage >= 90)
                {
                    UsageStatusLabel.Text = "⚠️ Nearly Full";
                    UsageProgressBar.BackgroundColor = Color.FromArgb("#FF4757"); // Red
                }
                else if (_enhancedUsageStats.UsagePercentage >= 75)
                {
                    UsageStatusLabel.Text = "🔶 High Usage";
                    UsageProgressBar.BackgroundColor = Color.FromArgb("#FFA500"); // Orange
                }
                else
                {
                    UsageStatusLabel.Text = "✅ Good";
                    UsageProgressBar.BackgroundColor = Color.FromArgb("#4CAF50"); // Green
                }

                Debug.WriteLine($"Progress bar visible with width: {progressWidth}, status: {UsageStatusLabel.Text}");
            }
            else
            {
                UsageProgressContainer.IsVisible = false;
                Debug.WriteLine("Progress bar hidden");
            }

            // Update recent usage list
            if (_enhancedUsageStats.RecentUsage?.Any() == true)
            {
                _recentUsageItems.Clear();
                foreach (var item in _enhancedUsageStats.RecentUsage.Take(5))
                {
                    _recentUsageItems.Add(item);
                }
                RecentUsageContainer.IsVisible = true;
                Debug.WriteLine($"Recent usage visible with {_recentUsageItems.Count} items");
            }
            else
            {
                RecentUsageContainer.IsVisible = false;
                Debug.WriteLine("Recent usage hidden");
            }

            // Show upgrade prompt for free users
            UpgradePromptFrame.IsVisible = _currentTier == MembershipTier.Free;
            Debug.WriteLine($"Upgrade prompt visible: {UpgradePromptFrame.IsVisible}");

            // Show legacy stats container if needed (hidden by default)
            LegacyStatsContainer.IsVisible = false;

            Debug.WriteLine("Enhanced usage display update completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating enhanced usage display: {ex.Message}");
        }
    }

    private async void LoadModelCompatibilitySettings()
    {
        // Load existing settings from Preferences (maintaining compatibility)
        GpuCapabilityPicker.SelectedIndex = Preferences.Get("ModelCompat_GpuCapabilityIndex", 0);
        MaxVramEntry.Text = Preferences.Get("ModelCompat_MaxVram", "4");
        AllowLargeModelsSwitch.IsToggled = Preferences.Get("ModelCompat_AllowLargeModels", false);

        // Load auto-select setting from SettingsService
        await LoadAutoSelectModelSetting();

        // Update recommended model preview
        UpdateRecommendedModelPreview();
    }

    private async Task LoadAutoSelectModelSetting()
    {
        try
        {
            var hardwareCapabilities = await _settingsService.GetHardwareCapabilitiesAsync();
            AutoSelectModelSwitch.IsToggled = hardwareCapabilities.AutoSelectModel;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading auto-select setting: {ex.Message}");
            AutoSelectModelSwitch.IsToggled = true; // Default to enabled
        }
    }

    private async void OnModelCompatSettingChanged(object sender, EventArgs e)
    {
        await SaveModelCompatibilitySettings();
        UpdateRecommendedModelPreview();
    }

    private async Task SaveModelCompatibilitySettings()
    {
        // Save to Preferences (maintaining compatibility)
        Preferences.Set("ModelCompat_GpuCapabilityIndex", GpuCapabilityPicker.SelectedIndex);
        Preferences.Set("ModelCompat_MaxVram", MaxVramEntry.Text ?? "4");
        Preferences.Set("ModelCompat_AllowLargeModels", AllowLargeModelsSwitch.IsToggled);

        // Save to SettingsService as well
        await SaveHardwareCapabilitiesToService();
    }

    private async Task SaveHardwareCapabilitiesToService()
    {
        try
        {
            string gpuCapability = GpuCapabilityPicker.SelectedIndex >= 0 && GpuCapabilityPicker.SelectedIndex < _gpuCapabilityLevels.Length
                ? _gpuCapabilityLevels[GpuCapabilityPicker.SelectedIndex]
                : "CPU Only";

            int maxVram = int.TryParse(MaxVramEntry.Text, out int vram) ? vram : 4;

            await _settingsService.SetHardwareCapabilityAsync(
                gpuCapability,
                maxVram,
                AllowLargeModelsSwitch.IsToggled,
                AutoSelectModelSwitch.IsToggled
            );
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error saving hardware capabilities: {ex.Message}");
        }
    }

    private void UpdateRecommendedModelPreview()
    {
        try
        {
            string recommendedModel = _settingsService.GetRecommendedModelBasedOnHardware();
            RecommendedModelLabel.Text = AutoSelectModelSwitch.IsToggled
                ? $"Recommended: {recommendedModel}"
                : "Auto-selection disabled";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating recommended model preview: {ex.Message}");
            RecommendedModelLabel.Text = "General Assistant";
        }
    }

    private void DebugConsoleSwitch_Toggled(object sender, ToggledEventArgs e)
    {
        try
        {
            if (e.Value)
            {
                _debugConsoleService?.Show();
                CSimple.Utilities.DebugConsole.Info("Debug console enabled from settings");
            }
            else
            {
                _debugConsoleService?.Hide();
                CSimple.Utilities.DebugConsole.Info("Debug console disabled from settings");
            }

            // Save the preference
            Preferences.Set("DebugConsoleEnabled", e.Value);
        }
        catch (Exception ex)
        {
            CSimple.Utilities.DebugConsole.Error($"Error toggling debug console: {ex.Message}");
        }
    }

    private void LoadIntelligenceSettings()
    {
        try
        {
            // Load intelligence interval setting
            int intervalMs = _settingsService.GetIntelligenceIntervalMs();
            IntelligenceIntervalEntry.Text = intervalMs.ToString();

            // Load initial delay setting
            int initialDelayMs = _settingsService.GetIntelligenceInitialDelayMs();
            IntelligenceInitialDelayEntry.Text = initialDelayMs.ToString();

            // Load auto-execution setting
            bool autoExecutionEnabled = _settingsService.GetIntelligenceAutoExecutionEnabled();
            IntelligenceAutoExecutionSwitch.IsToggled = autoExecutionEnabled;

            Debug.WriteLine($"Loaded intelligence settings: interval={intervalMs}ms, initial-delay={initialDelayMs}ms, auto-execution={autoExecutionEnabled}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error loading intelligence settings: {ex.Message}");
        }
    }

    private void IntelligenceInterval_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue))
                return;

            if (int.TryParse(e.NewTextValue, out int intervalMs))
            {
                _settingsService.SetIntelligenceIntervalMs(intervalMs);
                Debug.WriteLine($"Intelligence interval updated to: {intervalMs}ms");
            }
            else
            {
                // Reset to default if invalid input
                IntelligenceIntervalEntry.Text = "1000";
                _settingsService.SetIntelligenceIntervalMs(1000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating intelligence interval: {ex.Message}");
        }
    }

    private void IntelligenceInitialDelay_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(e.NewTextValue))
                return;

            if (int.TryParse(e.NewTextValue, out int delayMs))
            {
                _settingsService.SetIntelligenceInitialDelayMs(delayMs);
                Debug.WriteLine($"Intelligence initial delay updated to: {delayMs}ms");
            }
            else
            {
                // Reset to default if invalid input
                IntelligenceInitialDelayEntry.Text = "5000";
                _settingsService.SetIntelligenceInitialDelayMs(5000);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error updating intelligence initial delay: {ex.Message}");
        }
    }

    private void IntelligenceAutoExecution_Toggled(object sender, ToggledEventArgs e)
    {
        try
        {
            _settingsService.SetIntelligenceAutoExecutionEnabled(e.Value);
            Debug.WriteLine($"Intelligence auto-execution toggled to: {e.Value}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error toggling intelligence auto-execution: {ex.Message}");
        }
    }
}
