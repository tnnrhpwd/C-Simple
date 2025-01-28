using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace CSimple.Services
{
    public class FileService
    {
        private readonly string _directory;
        private readonly string _dataItemsFilePath;
        private readonly string _recordedActionsFilePath;
        private readonly string _goalsFilePath;
        private readonly string _plansFilePath;
        private readonly string _localDataItemsFilePath;

        public FileService()
        {
            _directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            System.Diagnostics.Debug.WriteLine($"Directory: {_directory}");

            Directory.CreateDirectory(_directory);  // Ensure directory exists
            _dataItemsFilePath = Path.Combine(_directory, "dataItems.json");
            _recordedActionsFilePath = Path.Combine(_directory, "recordedActions.json");
            _goalsFilePath = Path.Combine(_directory, "goals.json");
            _plansFilePath = Path.Combine(_directory, "plans.json");
            _localDataItemsFilePath = Path.Combine(_directory, "localDataItems.json");

            EnsureFileExists(_dataItemsFilePath);
            EnsureFileExists(_recordedActionsFilePath);
            EnsureFileExists(_goalsFilePath);
            EnsureFileExists(_plansFilePath);
        }

        public async Task SaveDataItemsAsync(List<DataItem> dataItems)
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to save data to {_dataItemsFilePath}");
            await SaveToFileAsync(_dataItemsFilePath, dataItems);
        }

        public Task<List<DataItem>> LoadDataItemsAsync() =>
            LoadFromFileAsync<List<DataItem>, List<DataItem>>(_dataItemsFilePath, c => new List<DataItem>(c));

        public async Task SaveLocalDataItemsAsync(List<DataItem> dataItems)
        {
            await SaveToFileAsync(_localDataItemsFilePath, dataItems);
        }

        public Task<List<DataItem>> LoadLocalDataItemsAsync() =>
            LoadFromFileAsync<List<DataItem>, List<DataItem>>(_localDataItemsFilePath, c => new List<DataItem>(c));

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
}