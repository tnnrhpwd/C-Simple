using Microsoft.Maui.Storage;
using System.Diagnostics;

namespace CSimple.Services
{
    public class SettingsService
    {
        private readonly DataService _dataService;

        public List<string> TimeZones { get; private set; }
        public List<string> AvailableModels { get; private set; } = new List<string> { "General Assistant", "Sales Report", "Data Analysis", "Customer Support" };
        public Dictionary<string, bool> Permissions { get; private set; }

        // New properties for membership features
        public enum MembershipTier
        {
            Free,
            Flex,
            Premium
        }

        public class UsageStatistics
        {
            public int ProcessingMinutes { get; set; }
            public int ApiCalls { get; set; }
            public int ApiCallsLimit { get; set; }
            public double StorageMB { get; set; }
            public double StorageLimitMB { get; set; }
            public DateTime BillingCycleEnd { get; set; }
        }

        private MembershipTier _currentTier = MembershipTier.Free;
        private UsageStatistics _usageStats;

        public SettingsService(DataService dataService)
        {
            _dataService = dataService;
            TimeZones = TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => tz.DisplayName)
                .ToList();

            InitializePermissions();
            InitializeUsageStatistics();
        }

        private void InitializePermissions()
        {
            Permissions = new Dictionary<string, bool>
            {
                { "ScreenCapture", false },
                { "AudioCapture", false },
                { "KeyboardSimulation", false },
                { "MouseSimulation", false },
                { "VoiceOutput", false },
                { "SystemCommands", false }
            };

            // Load saved permissions if they exist
            LoadPermissionsAsync().ConfigureAwait(false);
        }

        private void InitializeUsageStatistics()
        {
            _usageStats = new UsageStatistics
            {
                ProcessingMinutes = 0,
                ApiCalls = 0,
                ApiCallsLimit = 100,
                StorageMB = 0,
                StorageLimitMB = 500,
                BillingCycleEnd = DateTime.Now.AddDays(30)
            };

            // Load saved usage statistics if they exist
            LoadUsageStatisticsAsync().ConfigureAwait(false);
        }

        public async Task<(string Nickname, string Email)> LoadUserData()
        {
            try
            {
                var userNickname = await SecureStorage.GetAsync("userNickname");
                var userEmail = await SecureStorage.GetAsync("userEmail");
                return (userNickname, userEmail);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user data: {ex.Message}");
                return (null, null);
            }
        }

        public async Task UpdateUserTimeZone(string selectedZone)
        {
            try
            {
                var userId = await SecureStorage.GetAsync("userID");
                if (!string.IsNullOrEmpty(userId))
                {
                    var updateData = new { TimeZone = selectedZone };
                    await _dataService.UpdateDataAsync(userId, updateData, await SecureStorage.GetAsync("userToken"));
                    Debug.WriteLine($"TimeZone updated to: {selectedZone}");
                }
                else
                {
                    Debug.WriteLine("Error: User ID not found in secure storage.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating TimeZone: {ex.Message}");
            }
        }

        public async Task<bool> IsUserLoggedInAsync()
        {
            return await _dataService.IsLoggedInAsync();
        }

        public void SetAppTheme(AppTheme theme)
        {
            if (App.Current.UserAppTheme == theme)
                return;

            App.Current.UserAppTheme = theme;
        }

        public void Logout()
        {
            try
            {
                _dataService.Logout();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Logout error: {ex.Message}");
                throw;
            }
        }

        public async Task<Dictionary<string, bool>> LoadPermissionsAsync()
        {
            try
            {
                foreach (var permission in Permissions.Keys.ToList())
                {
                    var value = await SecureStorage.GetAsync($"perm_{permission}");
                    if (value != null)
                    {
                        Permissions[permission] = bool.Parse(value);
                    }
                }
                return Permissions;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading permissions: {ex.Message}");
                return Permissions;
            }
        }

        public async Task SavePermissionAsync(string permission, bool value)
        {
            try
            {
                if (Permissions.ContainsKey(permission))
                {
                    Permissions[permission] = value;
                    await SecureStorage.SetAsync($"perm_{permission}", value.ToString());
                    Debug.WriteLine($"Permission {permission} set to {value}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving permission: {ex.Message}");
            }
        }

        public async Task<string> GetActiveModelAsync()
        {
            try
            {
                return await SecureStorage.GetAsync("activeModel") ?? "General Assistant";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving active model: {ex.Message}");
                return "General Assistant";
            }
        }

        public async Task SetActiveModelAsync(string modelName)
        {
            try
            {
                await SecureStorage.SetAsync("activeModel", modelName);
                Debug.WriteLine($"Active model set to: {modelName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting active model: {ex.Message}");
            }
        }

        public async Task<bool> ImportModelAsync(string modelPath)
        {
            try
            {
                // Add actual implementation with await
                Debug.WriteLine($"Importing model from: {modelPath}");

                // Simulate async operation
                await Task.Delay(100);

                // Here you would implement actual model import logic
                // For example: await _modelService.ImportModelAsync(modelPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error importing model: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ExportModelAsync(string modelName, string destinationPath)
        {
            try
            {
                // Add actual implementation with await
                Debug.WriteLine($"Exporting model {modelName} to: {destinationPath}");

                // Simulate async operation
                await Task.Delay(100);

                // Here you would implement actual model export logic
                // For example: await _modelService.ExportModelAsync(modelName, destinationPath);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error exporting model: {ex.Message}");
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> GetFeatureStatesAsync()
        {
            try
            {
                var features = new Dictionary<string, bool>
                {
                    { "ObserveMode", false },
                    { "OrientMode", false },
                    { "PlanMode", false },
                    { "ActionMode", false }
                };

                foreach (var feature in features.Keys.ToList())
                {
                    var value = await SecureStorage.GetAsync($"feature_{feature}");
                    if (value != null)
                    {
                        features[feature] = bool.Parse(value);
                    }
                }
                return features;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading feature states: {ex.Message}");
                return new Dictionary<string, bool>();
            }
        }

        public async Task SaveFeatureStateAsync(string feature, bool enabled)
        {
            try
            {
                await SecureStorage.SetAsync($"feature_{feature}", enabled.ToString());
                Debug.WriteLine($"Feature {feature} set to {enabled}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving feature state: {ex.Message}");
            }
        }

        public async Task<MembershipTier> GetMembershipTierAsync()
        {
            try
            {
                var tierString = await SecureStorage.GetAsync("membershipTier");
                if (Enum.TryParse<MembershipTier>(tierString, out var tier))
                {
                    _currentTier = tier;
                }
                return _currentTier;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving membership tier: {ex.Message}");
                return MembershipTier.Free;
            }
        }

        public async Task SetMembershipTierAsync(MembershipTier tier)
        {
            try
            {
                _currentTier = tier;
                await SecureStorage.SetAsync("membershipTier", tier.ToString());

                // Update limits based on tier
                await UpdateTierLimits(tier);

                Debug.WriteLine($"Membership tier set to: {tier}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting membership tier: {ex.Message}");
            }
        }

        private async Task UpdateTierLimits(MembershipTier tier)
        {
            switch (tier)
            {
                case MembershipTier.Free:
                    _usageStats.ApiCallsLimit = 100;
                    _usageStats.StorageLimitMB = 500;
                    break;
                case MembershipTier.Flex:
                    _usageStats.ApiCallsLimit = 1000;
                    _usageStats.StorageLimitMB = 2000;
                    break;
                case MembershipTier.Premium:
                    _usageStats.ApiCallsLimit = 10000;
                    _usageStats.StorageLimitMB = 10000;
                    break;
            }

            await SaveUsageStatisticsAsync();
        }

        public async Task<UsageStatistics> GetUsageStatisticsAsync()
        {
            await LoadUsageStatisticsAsync();
            return _usageStats;
        }

        public async Task LoadUsageStatisticsAsync()
        {
            try
            {
                var processingMinutes = await SecureStorage.GetAsync("usage_processingMinutes");
                if (processingMinutes != null)
                    _usageStats.ProcessingMinutes = int.Parse(processingMinutes);

                var apiCalls = await SecureStorage.GetAsync("usage_apiCalls");
                if (apiCalls != null)
                    _usageStats.ApiCalls = int.Parse(apiCalls);

                var apiCallsLimit = await SecureStorage.GetAsync("usage_apiCallsLimit");
                if (apiCallsLimit != null)
                    _usageStats.ApiCallsLimit = int.Parse(apiCallsLimit);

                var storageMB = await SecureStorage.GetAsync("usage_storageMB");
                if (storageMB != null)
                    _usageStats.StorageMB = double.Parse(storageMB);

                var storageLimitMB = await SecureStorage.GetAsync("usage_storageLimitMB");
                if (storageLimitMB != null)
                    _usageStats.StorageLimitMB = double.Parse(storageLimitMB);

                var billingCycleEnd = await SecureStorage.GetAsync("usage_billingCycleEnd");
                if (billingCycleEnd != null)
                    _usageStats.BillingCycleEnd = DateTime.Parse(billingCycleEnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading usage statistics: {ex.Message}");
            }
        }

        public async Task SaveUsageStatisticsAsync()
        {
            try
            {
                await SecureStorage.SetAsync("usage_processingMinutes", _usageStats.ProcessingMinutes.ToString());
                await SecureStorage.SetAsync("usage_apiCalls", _usageStats.ApiCalls.ToString());
                await SecureStorage.SetAsync("usage_apiCallsLimit", _usageStats.ApiCallsLimit.ToString());
                await SecureStorage.SetAsync("usage_storageMB", _usageStats.StorageMB.ToString());
                await SecureStorage.SetAsync("usage_storageLimitMB", _usageStats.StorageLimitMB.ToString());
                await SecureStorage.SetAsync("usage_billingCycleEnd", _usageStats.BillingCycleEnd.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving usage statistics: {ex.Message}");
            }
        }

        public async Task IncrementApiCallCountAsync()
        {
            _usageStats.ApiCalls++;
            await SaveUsageStatisticsAsync();
        }

        public async Task AddProcessingTimeAsync(int minutes)
        {
            _usageStats.ProcessingMinutes += minutes;
            await SaveUsageStatisticsAsync();
        }

        public async Task AddStorageUsageAsync(double megabytes)
        {
            _usageStats.StorageMB += megabytes;
            await SaveUsageStatisticsAsync();
        }

        public string GetMembershipFeatures(MembershipTier tier)
        {
            switch (tier)
            {
                case MembershipTier.Free:
                    return "• Limited access to basic features\n• 100 API calls per month\n• 500 MB storage";
                case MembershipTier.Flex:
                    return "• Full access to all features\n• 1,000 API calls per month\n• 2 GB storage\n• Priority support";
                case MembershipTier.Premium:
                    return "• Full access to all features\n• 10,000 API calls per month\n• 10 GB storage\n• Premium support\n• Custom model training\n• Collaborative workspaces";
                default:
                    return string.Empty;
            }
        }

        public async Task ResetBillingCycleAsync()
        {
            _usageStats.ApiCalls = 0;
            _usageStats.ProcessingMinutes = 0;
            _usageStats.BillingCycleEnd = DateTime.Now.AddDays(30);
            await SaveUsageStatisticsAsync();
        }
    }
}
