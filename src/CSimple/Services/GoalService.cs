using CSimple.Models; // Add this using
using CSimple.Services.AppModeService;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class GoalService
    {
        private readonly DataService _dataService;
        private readonly FileService _fileService; // Inject FileService
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private const string GoalsFilename = "goals.json"; // Define filename for goals

        public GoalService(DataService dataService, FileService fileService, CSimple.Services.AppModeService.AppModeService appModeService)
        {
            _dataService = dataService;
            _fileService = fileService; // Store injected FileService
            _appModeService = appModeService;
        }

        public async Task<bool> IsUserLoggedInAsync()
        {
            return await _dataService.IsLoggedInAsync();
        }

        // --- Local Goal Management (using FileService) ---

        // Save goals to a local file
        public async Task SaveGoalsToFile(IEnumerable<Goal> goals)
        {
            try
            {
                await _fileService.SaveDataAsync(GoalsFilename, goals);
                Debug.WriteLine($"Goals saved successfully to {GoalsFilename}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving goals to file: {ex.Message}");
            }
        }

        // Load goals from a local file
        public async Task<List<Goal>> LoadGoalsFromFile()
        {
            try
            {
                var goals = await _fileService.LoadDataAsync<List<Goal>>(GoalsFilename);
                if (goals != null)
                {
                    Debug.WriteLine($"Goals loaded successfully from {GoalsFilename}");
                    return goals;
                }
                Debug.WriteLine($"No goals file found or file is empty: {GoalsFilename}");
                return new List<Goal>(); // Return empty list if file doesn't exist or is empty
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading goals from file: {ex.Message}");
                return new List<Goal>(); // Return empty list on error
            }
        }

        // Get local goals and update the ObservableCollection
        public async Task GetLocalGoalsAsync(ObservableCollection<Goal> goalsCollection)
        {
            var loadedGoals = await LoadGoalsFromFile();
            goalsCollection.Clear();
            foreach (var goal in loadedGoals.OrderByDescending(g => g.CreatedAt)) // Example sorting
            {
                goalsCollection.Add(goal);
            }
            Debug.WriteLine($"Updated ObservableCollection with {goalsCollection.Count} local goals.");
        }


        // --- Backend Goal Management (Placeholder - Adapt as needed) ---

        // Load goals from backend and merge/update local collection
        public async Task LoadGoalsFromBackend(ObservableCollection<Goal> goalsCollection, ObservableCollection<DataItem> allDataItems)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("Offline mode: Loading goals from local file only.");
                await GetLocalGoalsAsync(goalsCollection);
                return;
            }

            Debug.WriteLine("Online mode: Attempting to load goals from backend.");
            try
            {
                // Placeholder: Replace with actual backend API call
                // var backendGoals = await _dataService.FetchGoalsAsync(); // Assuming such a method exists

                // Simulate fetching backend goals (replace with real data)
                await Task.Delay(500); // Simulate network delay
                var backendGoals = new List<Goal> { /* ... fetch from API ... */ };

                // Merge backend goals with local goals (simple example: replace local with backend)
                goalsCollection.Clear();
                foreach (var goal in backendGoals.OrderByDescending(g => g.CreatedAt))
                {
                    goalsCollection.Add(goal);
                }

                // Optionally save the fetched backend goals locally
                await SaveGoalsToFile(goalsCollection);

                Debug.WriteLine($"Loaded {goalsCollection.Count} goals from backend.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading goals from backend: {ex.Message}. Falling back to local goals.");
                // Fallback to local goals if backend fails
                await GetLocalGoalsAsync(goalsCollection);
            }
        }

        // Save a single goal to the backend (Placeholder)
        public async Task SaveGoalToBackend(Goal goal)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("Offline mode: Skipping backend save for goal.");
                return; // Don't attempt backend save in offline mode
            }

            try
            {
                Debug.WriteLine($"Attempting to save goal '{goal.Title}' to backend.");
                // Placeholder: Replace with actual backend API call
                // await _dataService.PostGoalAsync(goal);
                await Task.Delay(200); // Simulate network delay
                Debug.WriteLine($"Goal '{goal.Title}' saved to backend (simulated).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving goal to backend: {ex.Message}");
                // Handle error (e.g., queue for later sync)
            }
        }

        // Delete a goal from the backend (Placeholder)
        public async Task DeleteGoalFromBackend(string goalId)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("Offline mode: Skipping backend delete for goal.");
                return;
            }

            try
            {
                Debug.WriteLine($"Attempting to delete goal ID '{goalId}' from backend.");
                // Placeholder: Replace with actual backend API call
                // await _dataService.DeleteGoalAsync(goalId);
                await Task.Delay(200); // Simulate network delay
                Debug.WriteLine($"Goal ID '{goalId}' deleted from backend (simulated).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting goal from backend: {ex.Message}");
            }
        }

        // Update a goal on the backend (Placeholder)
        public async Task UpdateGoalOnBackend(Goal goal)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("Offline mode: Skipping backend update for goal.");
                return;
            }

            try
            {
                Debug.WriteLine($"Attempting to update goal '{goal.Title}' on backend.");
                // Placeholder: Replace with actual backend API call
                // await _dataService.PutGoalAsync(goal);
                await Task.Delay(200); // Simulate network delay
                Debug.WriteLine($"Goal '{goal.Title}' updated on backend (simulated).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating goal on backend: {ex.Message}");
            }
        }

        // --- Deprecated Methods (to be removed or updated) ---

        // Deprecated: Save string goals to file - Use SaveGoalsToFile(IEnumerable<Goal>) instead
        [Obsolete("Use SaveGoalsToFile(IEnumerable<Goal>) instead.")]
        public async Task SaveGoalsToFile(ObservableCollection<string> goals)
        {
            // This method is now obsolete. Convert string goals to Goal objects if needed,
            // or update calling code to use the new method.
            Debug.WriteLine("WARNING: Called obsolete SaveGoalsToFile(ObservableCollection<string>).");
            await Task.CompletedTask; // No-op
        }

        // Deprecated: Load string goals from file - Use LoadGoalsFromFile() instead
        [Obsolete("Use LoadGoalsFromFile() which returns List<Goal> instead.")]
        public async Task LoadGoalsFromFile(ObservableCollection<string> goals)
        {
            // This method is now obsolete.
            Debug.WriteLine("WARNING: Called obsolete LoadGoalsFromFile(ObservableCollection<string>).");
            goals.Clear(); // Clear the collection as we can't load strings anymore
            await Task.CompletedTask; // No-op
        }

        // Deprecated: Save single string goal to backend - Use SaveGoalToBackend(Goal) instead
        [Obsolete("Use SaveGoalToBackend(Goal) instead.")]
        public async Task SaveGoalToBackend(string goalText)
        {
            Debug.WriteLine("WARNING: Called obsolete SaveGoalToBackend(string).");
            // Create a dummy Goal object if necessary for compatibility or update calling code
            var dummyGoal = new Goal { Title = goalText, Description = goalText };
            await SaveGoalToBackend(dummyGoal);
        }
    }
}
