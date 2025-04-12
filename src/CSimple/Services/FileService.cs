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
            EnsureFileExists(_localDataItemsFilePath); // Ensure local data items file exists
        }

        public async Task SaveDataItemsAsync(List<DataItem> dataItems)
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to save data to {_dataItemsFilePath}");
            await SaveToFileAsync(_dataItemsFilePath, dataItems);
        }

        public async Task<List<DataItem>> LoadDataItemsAsync() =>
            await LoadFromFileAsync<List<DataItem>, List<DataItem>>(_dataItemsFilePath, c => c?.Where(item => !item.deleted).ToList() ?? new List<DataItem>());

        public async Task SaveLocalDataItemsAsync(List<DataItem> newData)
        {
            try
            {
                // Load existing local data
                var existingData = await LoadLocalDataItemsAsync() ?? new List<DataItem>();

                // Filter out any deleted items from the existing data
                existingData = existingData.Where(item => !item.deleted).ToList();

                // Merge new data with existing data, avoiding duplicates with better detection
                foreach (var item in newData)
                {
                    // Skip deleted items
                    if (item.deleted)
                        continue;

                    // Improved duplicate detection logic
                    bool isDuplicate = false;

                    foreach (var existingItem in existingData)
                    {
                        // Check ID if available
                        if (!string.IsNullOrEmpty(item._id) && !string.IsNullOrEmpty(existingItem._id))
                        {
                            if (item._id == existingItem._id)
                            {
                                isDuplicate = true;
                                break;
                            }
                        }
                        // Otherwise check action name
                        else if (item.Data?.ActionGroupObject?.ActionName == existingItem.Data?.ActionGroupObject?.ActionName)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (!isDuplicate)
                    {
                        // Make sure we mark it as local
                        if (item?.Data?.ActionGroupObject != null)
                        {
                            item.Data.ActionGroupObject.IsLocal = true;
                        }
                        existingData.Add(item);
                    }
                }

                // Save the merged data back to the file
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(existingData, options);
                await File.WriteAllTextAsync(_localDataItemsFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Successfully saved local data to {_localDataItemsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving local data: {ex.Message}");
            }
        }

        public Task<List<DataItem>> LoadLocalDataItemsAsync() =>
            LoadFromFileAsync<List<DataItem>, List<DataItem>>(_localDataItemsFilePath, c => c?.Where(item => !item.deleted).ToList() ?? new List<DataItem>());

        private async Task SaveToFileAsync(string filePath, List<DataItem> newData)
        {
            try
            {
                var existingData = await LoadFromFileAsync<List<DataItem>, List<DataItem>>(filePath, c => c)
                               ?? new List<DataItem>();

                // Filter out deleted items from the existing data
                existingData = existingData.Where(item => !item.deleted).ToList();

                // Create a new list to hold the updated items
                var updatedData = new List<DataItem>();

                // Process each item from existing data
                foreach (var item in existingData)
                {
                    // Skip if this item matches one in the new data (will be replaced)
                    if (newData.Any(x => (!string.IsNullOrEmpty(x._id) && !string.IsNullOrEmpty(item._id) && x._id == item._id) ||
                        (x.Data?.ActionGroupObject?.ActionName == item.Data?.ActionGroupObject?.ActionName)))
                    {
                        continue;
                    }
                    updatedData.Add(item);
                }

                // Add all non-deleted items from new data
                foreach (var item in newData.Where(i => !i.deleted))
                {
                    updatedData.Add(item);
                }

                System.Diagnostics.Debug.WriteLine($"Attempting to save data to {filePath}");
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(updatedData, options);
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