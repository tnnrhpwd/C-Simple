using Microsoft.Maui.Storage;
using System.Diagnostics;

namespace CSimple.Services
{
    public class SettingsService
    {
        private readonly DataService _dataService;

        public List<string> TimeZones { get; private set; }

        public SettingsService(DataService dataService)
        {
            _dataService = dataService;
            TimeZones = TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => tz.DisplayName)
                .ToList();
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
    }
}
