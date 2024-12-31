using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace CSimple.Services
{
    public class FileService
    {
        private readonly string _directory;
        private readonly string _actionGroupsFilePath;
        private readonly string _recordedActionsFilePath;
        private readonly string _goalsFilePath;
        private readonly string _plansFilePath;

        public FileService()
        {
            _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            System.Diagnostics.Debug.WriteLine($"Directory: {_directory}");

            Directory.CreateDirectory(_directory);  // Ensure directory exists
            _actionGroupsFilePath = Path.Combine(_directory, "actionGroups.json");
            _recordedActionsFilePath = Path.Combine(_directory, "recordedActions.json");
            _goalsFilePath = Path.Combine(_directory, "goals.json");
            _plansFilePath = Path.Combine(_directory, "plans.json");

            EnsureFileExists(_actionGroupsFilePath);
            EnsureFileExists(_recordedActionsFilePath);
            EnsureFileExists(_goalsFilePath);
            EnsureFileExists(_plansFilePath);
        }

        public Task SaveActionGroupsAsync(List<DataItem> dataItems) =>
            SaveToFileAsync(_actionGroupsFilePath, new { dataItems = dataItems });

        public Task<ObservableCollection<ActionGroup>> LoadActionGroupsAsync() =>
            LoadFromFileAsync<ObservableCollection<ActionGroup>, ActionGroupsContainer>(_actionGroupsFilePath, c => c.ActionGroups);

        public Task SaveRecordedActionsAsync(List<string> recordedActions) =>
            SaveToFileAsync(_recordedActionsFilePath, recordedActions);

        public Task<List<string>> LoadRecordedActionsAsync() =>
            LoadFromFileAsync<List<string>, List<string>>(_recordedActionsFilePath, r => r);

        public Task SaveGoalsAsync(ObservableCollection<string> goals) =>
            SaveToFileAsync(_goalsFilePath, goals);

        public Task<ObservableCollection<string>> LoadGoalsAsync() =>
            LoadFromFileAsync<ObservableCollection<string>, ObservableCollection<string>>(_goalsFilePath, g => g);

        public Task SavePlansAsync(ObservableCollection<string> plans) =>
            SaveToFileAsync(_plansFilePath, plans);

        public Task<ObservableCollection<string>> LoadPlansAsync() =>
            LoadFromFileAsync<ObservableCollection<string>, ObservableCollection<string>>(_plansFilePath, p => p);

        private async Task SaveToFileAsync<T>(string filePath, T data)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to save data to {filePath}");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(data, options);
                await File.WriteAllTextAsync(filePath, json);
                System.Diagnostics.Debug.WriteLine($"Successfully saved data to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
            }
        }

        private async Task<T> LoadFromFileAsync<T, TContainer>(string filePath, Func<TContainer, T> selector)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to load data from {filePath}");

                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine("File does not exist. Returning default value.");
                    return default;
                }

                var json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var container = JsonSerializer.Deserialize<TContainer>(json, options);
                System.Diagnostics.Debug.WriteLine($"Successfully loaded data from {filePath}");
                return selector(container);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
                return default;
            }
        }

        private void EnsureFileExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                try
                {
                    File.WriteAllText(filePath, "[]");
                    System.Diagnostics.Debug.WriteLine($"File created at {filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating file: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"File already exists at {filePath}");
            }
        }
    }

    public class ActionGroupsContainer
    {
        public ObservableCollection<ActionGroup> ActionGroups { get; set; }
    }
}