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

        public async Task DeleteDataItemsAsync(List<string> itemIds, List<string> actionNames)
        {
            try
            {
                // Load existing data
                var existingData = await LoadFromFileAsync<List<DataItem>, List<DataItem>>(_dataItemsFilePath, c => c)
                                ?? new List<DataItem>();

                // Remove items that match any of the provided IDs or action names
                var remainingData = existingData.Where(item =>
                    !(itemIds.Contains(item._id) ||
                     (item.Data?.ActionGroupObject?.ActionName != null &&
                      actionNames.Contains(item.Data.ActionGroupObject.ActionName))))
                    .ToList();

                // Save the filtered list back to the file
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(remainingData, options);
                await File.WriteAllTextAsync(_dataItemsFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Successfully removed {existingData.Count - remainingData.Count} items from {_dataItemsFilePath}");

                // Also delete from local data items
                await DeleteLocalDataItemsAsync(itemIds, actionNames);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting data items: {ex.Message}");
            }
        }

        public async Task DeleteLocalDataItemsAsync(List<string> itemIds, List<string> actionNames)
        {
            try
            {
                // Load existing local data
                var existingData = await LoadFromFileAsync<List<DataItem>, List<DataItem>>(_localDataItemsFilePath, c => c)
                                ?? new List<DataItem>();

                // Remove items that match any of the provided IDs or action names
                var remainingData = existingData.Where(item =>
                    !(itemIds.Contains(item._id) ||
                     (item.Data?.ActionGroupObject?.ActionName != null &&
                      actionNames.Contains(item.Data.ActionGroupObject.ActionName))))
                    .ToList();

                // Save the filtered list back to the file
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(remainingData, options);
                await File.WriteAllTextAsync(_localDataItemsFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Successfully removed {existingData.Count - remainingData.Count} local items from {_localDataItemsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting local data items: {ex.Message}");
            }
        }

        public async Task SaveLocalDataItemsAsync(List<DataItem> newData)
        {
            try
            {
                // Load existing local data
                var existingData = await LoadLocalDataItemsAsync() ?? new List<DataItem>();
                System.Diagnostics.Debug.WriteLine($"SaveLocalDataItemsAsync: Loaded {existingData.Count} existing items");

                // Filter out any deleted items from the existing data
                existingData = existingData.Where(item => !item.deleted).ToList();
                System.Diagnostics.Debug.WriteLine($"SaveLocalDataItemsAsync: {existingData.Count} items after filtering deleted");

                // Keep track of items added
                int addedCount = 0;
                int updatedCount = 0;

                // Merge new data with existing data, avoiding duplicates with better detection
                foreach (var item in newData.Where(i => !i.deleted))
                {
                    // Skip deleted items
                    if (item.deleted)
                        continue;

                    // Improved duplicate detection logic
                    bool isDuplicate = false;
                    int duplicateIndex = -1;

                    for (int i = 0; i < existingData.Count; i++)
                    {
                        var existingItem = existingData[i];
                        // Check ID if available
                        if (!string.IsNullOrEmpty(item._id) && !string.IsNullOrEmpty(existingItem._id))
                        {
                            if (item._id == existingItem._id)
                            {
                                isDuplicate = true;
                                duplicateIndex = i;
                                break;
                            }
                        }
                        // Otherwise check action name
                        else if (item.Data?.ActionGroupObject?.ActionName == existingItem.Data?.ActionGroupObject?.ActionName)
                        {
                            isDuplicate = true;
                            duplicateIndex = i;
                            break;
                        }
                    }

                    if (isDuplicate)
                    {
                        // Replace existing item with new version
                        if (duplicateIndex >= 0)
                        {
                            existingData[duplicateIndex] = item;
                            updatedCount++;
                        }
                    }
                    else
                    {
                        // Make sure we mark it as local
                        if (item?.Data?.ActionGroupObject != null)
                        {
                            item.Data.ActionGroupObject.IsLocal = true;
                        }
                        existingData.Add(item);
                        addedCount++;
                    }
                }

                // Sort items by creation date to keep newest at the top
                existingData = existingData.OrderByDescending(i => i.createdAt).ToList();

                // Save the merged data back to the file
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(existingData, options);
                await File.WriteAllTextAsync(_localDataItemsFilePath, json);

                System.Diagnostics.Debug.WriteLine($"Successfully saved local data: {addedCount} items added, {updatedCount} items updated, total {existingData.Count} items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving local data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        public Task<List<DataItem>> LoadLocalDataItemsAsync() =>
            LoadFromFileAsync<List<DataItem>, List<DataItem>>(_localDataItemsFilePath, c => c?.Where(item => !item.deleted).ToList() ?? new List<DataItem>());

        private async Task SaveToFileAsync(string filePath, List<DataItem> newData)
        {
            try
            {
                // Get existing data
                var existingData = await LoadFromFileAsync<List<DataItem>, List<DataItem>>(filePath, c => c)
                               ?? new List<DataItem>();

                System.Diagnostics.Debug.WriteLine($"SaveToFileAsync: Loaded {existingData.Count} existing items from {filePath}");

                // Create a list of items to keep - don't use deleted flag anymore
                var dataToSave = new List<DataItem>();

                // Keep track of items for logging
                int keptCount = 0;
                int replacedCount = 0;
                int newCount = 0;
                int deletedCount = newData.Count(i => i.deleted);

                // First, add all existing items that aren't being replaced or deleted
                foreach (var item in existingData)
                {
                    // Skip if this item is marked as deleted
                    if (item.deleted)
                        continue;

                    // Skip if this item is being replaced by new data
                    bool isBeingReplaced = newData.Any(x =>
                        (!string.IsNullOrEmpty(x._id) && !string.IsNullOrEmpty(item._id) && x._id == item._id) ||
                        (x.Data?.ActionGroupObject?.ActionName == item.Data?.ActionGroupObject?.ActionName));

                    if (!isBeingReplaced)
                    {
                        dataToSave.Add(item);
                        keptCount++;
                    }
                    else
                    {
                        replacedCount++;
                    }
                }

                // Then add all non-deleted items from new data
                foreach (var item in newData)
                {
                    if (!item.deleted)
                    {
                        dataToSave.Add(item);
                        newCount++;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"SaveToFileAsync: Saving {dataToSave.Count} items to {filePath} " +
                    $"(kept {keptCount}, replaced {replacedCount}, new {newCount}, removed {deletedCount} deleted items)");

                // Sort items by creation date to keep newest at the top
                dataToSave = dataToSave.OrderByDescending(i => i.createdAt).ToList();

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(dataToSave, options);
                await File.WriteAllTextAsync(filePath, json);
                System.Diagnostics.Debug.WriteLine($"Successfully saved data to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
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