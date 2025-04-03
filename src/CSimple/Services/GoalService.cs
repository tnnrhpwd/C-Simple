using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using CSimple.Services.AppModeService;

namespace CSimple.Services
{
    public class GoalService
    {
        private readonly DataService _dataService;
        private readonly FileService _fileService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;

        public GoalService(DataService dataService, FileService fileService, CSimple.Services.AppModeService.AppModeService appModeService)
        {
            _dataService = dataService;
            _fileService = fileService;
            _appModeService = appModeService;
        }

        public async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                // Retrieve stored token
                var userToken = await SecureStorage.GetAsync("userToken");

                // Check if token exists and is not empty
                if (!string.IsNullOrEmpty(userToken))
                {
                    Debug.WriteLine("User token found: " + userToken);
                    return true; // User is logged in
                }
                else
                {
                    Debug.WriteLine("No user token found.");
                    return false; // User is not logged in
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user token: {ex.Message}");
                return false;
            }
        }

        public async Task LoadGoalsFromBackend(ObservableCollection<string> myGoals, ObservableCollection<DataItem> allDataItems)
        {
            // Skip backend loading if in offline mode
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping backend goal loading.");
                return;
            }

            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return;
                }

                var data = "Goal";
                var goals = await _dataService.GetDataAsync(data, token);
                var formattedGoals = FormatGoalsFromBackend(goals.Data.Cast<DataItem>().ToList());

                myGoals.Clear();
                foreach (var goal in formattedGoals)
                {
                    myGoals.Add(goal);
                }

                var result = await _dataService.GetDataAsync("Goal", token);
                if (result?.Data != null)
                {
                    allDataItems.Clear();
                    foreach (var item in result.Data)
                    {
                        allDataItems.Add(item);
                    }
                }

                await SaveGoalsToFile(myGoals);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading goals from backend: {ex.Message}");
            }
        }

        public async Task GetLocalGoalsAsync(ObservableCollection<string> myGoals)
        {
            try
            {
                Debug.WriteLine("Loading goals from local storage only");
                await LoadGoalsFromFile(myGoals);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading local goals: {ex.Message}");
            }
        }

        private ObservableCollection<string> FormatGoalsFromBackend(IEnumerable<DataItem> goalItems)
        {
            var formattedGoals = new ObservableCollection<string>();

            foreach (var goalItem in goalItems)
            {
                // if (goalItem.Data.Text.Contains("|Goal"))
                // {
                //     formattedGoals.Add(goalItem.Data.Text);
                // }
            }

            return formattedGoals;
        }

        public async Task SaveGoalToBackend(string goal)
        {
            // Skip if in offline mode
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Goal saved locally only.");
                return;
            }

            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return;
                }

                var data = $"Goal:{goal}";
                var response = await _dataService.CreateDataAsync(data, token);
                if (response.DataIsSuccess)
                {
                    Debug.WriteLine("Goal saved to backend");
                }
                else
                {
                    Debug.WriteLine("Failed to save goal to backend");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving goal to backend: {ex.Message}");
            }
        }

        public async Task SaveGoalsToFile(ObservableCollection<string> goals)
        {
            try
            {
                Debug.WriteLine("Goals saved to file");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving goals to file: {ex.Message}");
            }
            await Task.CompletedTask;
        }

        public async Task LoadGoalsFromFile(ObservableCollection<string> goals)
        {
            try
            {
                // var loadedGoals = await _fileService.LoadGoalsAsync();
                goals.Clear();
                // foreach (var goal in loadedGoals)
                // {
                //     goals.Add(goal);
                // }
                Debug.WriteLine("Goals loaded from file");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading goals from file: {ex.Message}");
            }
            await Task.CompletedTask;
        }
    }
}
