using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CSimple.Pages; // Add this to access NeuralNetworkModel
using CSimple.Models; // Add this using directive

namespace CSimple.Services
{
    public class FileService
    {
        private readonly IAppPathService _appPathService;
        private readonly string _dataItemsFilePath;
        private readonly string _recordedActionsFilePath;
        private readonly string _goalsFilePath;
        private readonly string _plansFilePath;
        private readonly string _localDataItemsFilePath;
        private readonly string _huggingFaceModelsFilePath; // Added for HuggingFace models
        private readonly string _pipelineDirectoryPath; // Added for pipelines
        private readonly JsonSerializerOptions _jsonOptions;

        public FileService(IAppPathService appPathService)
        {
            _appPathService = appPathService;

            // Initialize paths using the path service
            var resourcesPath = _appPathService.GetResourcesPath();

            System.Diagnostics.Debug.WriteLine($"Directory: {resourcesPath}");

            _dataItemsFilePath = Path.Combine(resourcesPath, "dataItems.json");
            _recordedActionsFilePath = Path.Combine(resourcesPath, "recordedActions.json");
            _goalsFilePath = Path.Combine(resourcesPath, "goals.json");
            _plansFilePath = Path.Combine(resourcesPath, "plans.json");
            _localDataItemsFilePath = Path.Combine(resourcesPath, "localDataItems.json");
            _huggingFaceModelsFilePath = Path.Combine(resourcesPath, "huggingFaceModels.json"); // Path for HF models
            _pipelineDirectoryPath = _appPathService.GetPipelinesPath(); // Pipeline directory

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };

            // Initialize directories and files
            InitializeAsync().ConfigureAwait(false);
        }

        private async Task InitializeAsync()
        {
            try
            {
                await _appPathService.InitializeDirectoriesAsync();

                EnsureFileExists(_dataItemsFilePath);
                EnsureFileExists(_recordedActionsFilePath);
                EnsureFileExists(_goalsFilePath);
                EnsureFileExists(_plansFilePath);
                EnsureFileExists(_localDataItemsFilePath); // Ensure local data items file exists
                EnsureFileExists(_huggingFaceModelsFilePath); // Ensure HuggingFace models file exists
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing FileService: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current directory path for resources
        /// </summary>
        public string GetResourcesDirectory() => _appPathService.GetResourcesPath();

        /// <summary>
        /// Gets the WebcamImages directory path
        /// </summary>
        public string GetWebcamImagesDirectory() => _appPathService.GetWebcamImagesPath();

        /// <summary>
        /// Gets the PCAudio directory path
        /// </summary>
        public string GetPCAudioDirectory() => _appPathService.GetPCAudioPath();

        /// <summary>
        /// Gets the HFModels directory path
        /// </summary>
        public string GetHFModelsDirectory() => _appPathService.GetHFModelsPath();

        /// <summary>
        /// Gets the Pipelines directory path
        /// </summary>
        public string GetPipelinesDirectory() => _appPathService.GetPipelinesPath();

        /// <summary>
        /// Gets the MemoryFiles directory path
        /// </summary>
        public string GetMemoryFilesDirectory() => _appPathService.GetMemoryFilesPath();

        // --- Generic Save/Load Methods ---

        /// <summary>
        /// Saves generic data to a specified JSON file.
        /// </summary>
        /// <typeparam name="T">The type of data to save.</typeparam>
        /// <param name="filename">The name of the file (e.g., "mydata.json").</param>
        /// <param name="data">The data object to serialize and save.</param>
        public async Task SaveDataAsync<T>(string filename, T data)
        {
            var filePath = Path.Combine(_appPathService.GetResourcesPath(), filename);
            try
            {
                EnsureFileDirectoryExists(filePath); // Ensure directory exists
                var json = JsonSerializer.Serialize(data, _jsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                System.Diagnostics.Debug.WriteLine($"Data saved successfully to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving data to {filePath}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        /// <summary>
        /// Loads generic data from a specified JSON file.
        /// </summary>
        /// <typeparam name="T">The type of data to load.</typeparam>
        /// <param name="filename">The name of the file (e.g., "mydata.json").</param>
        /// <returns>The deserialized data object, or default(T) if the file doesn't exist or an error occurs.</returns>
        public async Task<T> LoadDataAsync<T>(string filename)
        {
            var filePath = Path.Combine(_appPathService.GetResourcesPath(), filename);
            try
            {
                if (!File.Exists(filePath))
                {
                    System.Diagnostics.Debug.WriteLine($"File not found: {filePath}. Returning default value.");
                    return default;
                }

                var json = await File.ReadAllTextAsync(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    System.Diagnostics.Debug.WriteLine($"File {filePath} is empty. Returning default value.");
                    return default;
                }

                var data = JsonSerializer.Deserialize<T>(json, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"Data loaded successfully from {filePath}");
                return data;
            }
            catch (JsonException jsonEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error deserializing JSON from {filePath}: {jsonEx.Message}. File content might be invalid.");
                // Consider backup/recovery logic here
                return default;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data from {filePath}: {ex.Message}");
                return default;
            }
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

        // Specific method to save HuggingFace model references
        public async Task SaveHuggingFaceModelsAsync(List<NeuralNetworkModel> models)
        {
            try
            {
                int modelCount = models?.Count ?? 0;
                System.Diagnostics.Debug.WriteLine($"FileService.SaveHuggingFaceModelsAsync: Received {modelCount} models to save to {_huggingFaceModelsFilePath}");

                if (models == null)
                {
                    System.Diagnostics.Debug.WriteLine("FileService.SaveHuggingFaceModelsAsync: Received null list, saving empty array.");
                    Console.WriteLine("FileService.SaveHuggingFaceModelsAsync: Received null list, saving empty array.");
                    models = new List<NeuralNetworkModel>(); // Ensure we save an empty array, not null
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(models, options);

                System.Diagnostics.Debug.WriteLine($"FileService.SaveHuggingFaceModelsAsync: Serialized JSON (first 200 chars): {json.Substring(0, Math.Min(200, json.Length))}");
                Console.WriteLine($"🔥 FileService: Serializing {modelCount} models to JSON");

                // Log InputType values in the models being saved
                foreach (var model in models)
                {
                    System.Diagnostics.Debug.WriteLine($"📋 FileService: Model '{model.Name}' - InputType: {model.InputType}");
                    Console.WriteLine($"📋 FileService: Model '{model.Name}' - InputType: {model.InputType}");
                }

                Console.WriteLine($"💾 FileService: Writing to file: {_huggingFaceModelsFilePath}");
                await File.WriteAllTextAsync(_huggingFaceModelsFilePath, json);
                Console.WriteLine($"✅ FileService: Successfully wrote {modelCount} models to file");
                System.Diagnostics.Debug.WriteLine($"FileService.SaveHuggingFaceModelsAsync: Successfully wrote {modelCount} models to {_huggingFaceModelsFilePath}");

                // Verify the file was actually written by reading it back
                if (File.Exists(_huggingFaceModelsFilePath))
                {
                    var fileSize = new FileInfo(_huggingFaceModelsFilePath).Length;
                    Console.WriteLine($"✅ FileService: Verified file exists with size: {fileSize} bytes");
                    System.Diagnostics.Debug.WriteLine($"✅ FileService: Verified file exists with size: {fileSize} bytes");
                }
                else
                {
                    Console.WriteLine($"❌ FileService: File was NOT created at {_huggingFaceModelsFilePath}");
                    System.Diagnostics.Debug.WriteLine($"❌ FileService: File was NOT created at {_huggingFaceModelsFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CRITICAL ERROR in FileService.SaveHuggingFaceModelsAsync: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                System.Diagnostics.Debug.WriteLine($"FileService.SaveHuggingFaceModelsAsync: Error saving HuggingFace models: {ex.Message}");
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        // Specific method to load HuggingFace model references
        public async Task<List<NeuralNetworkModel>> LoadHuggingFaceModelsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to load HuggingFace models from {_huggingFaceModelsFilePath}");

                if (!File.Exists(_huggingFaceModelsFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("HuggingFace models file does not exist. Returning empty list.");
                    return new List<NeuralNetworkModel>();
                }

                var json = await File.ReadAllTextAsync(_huggingFaceModelsFilePath);
                if (string.IsNullOrWhiteSpace(json) || json == "[]")
                {
                    System.Diagnostics.Debug.WriteLine("HuggingFace models file is empty. Returning empty list.");
                    return new List<NeuralNetworkModel>();
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var models = JsonSerializer.Deserialize<List<NeuralNetworkModel>>(json, options);
                System.Diagnostics.Debug.WriteLine($"Successfully loaded {models?.Count ?? 0} HuggingFace models from {_huggingFaceModelsFilePath}");
                return models ?? new List<NeuralNetworkModel>();
            }
            catch (JsonException jsonEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error deserializing HuggingFace models JSON: {jsonEx.Message}. File content might be invalid.");
                // Optionally, backup the corrupted file and create a new empty one
                // File.Move(_huggingFaceModelsFilePath, _huggingFaceModelsFilePath + ".corrupted", true);
                // File.WriteAllText(_huggingFaceModelsFilePath, "[]");
                return new List<NeuralNetworkModel>(); // Return empty list on error
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading HuggingFace models: {ex.Message}");
                return new List<NeuralNetworkModel>(); // Return empty list on error
            }
        }

        // --- Pipeline Methods ---

        private string GetPipelineFilePath(string pipelineName)
        {
            // Sanitize pipeline name to create a valid filename
            var sanitizedName = string.Join("_", pipelineName.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_pipelineDirectoryPath, $"{sanitizedName}.pipeline.json");
        }

        public async Task SavePipelineAsync(PipelineData pipelineData)
        {
            if (string.IsNullOrWhiteSpace(pipelineData?.Name))
            {
                System.Diagnostics.Debug.WriteLine("Error saving pipeline: Name is missing.");
                return;
            }

            var filePath = GetPipelineFilePath(pipelineData.Name);
            try
            {
                pipelineData.LastModified = DateTime.UtcNow; // Update timestamp on save
                var jsonData = JsonSerializer.Serialize(pipelineData, _jsonOptions);
                await File.WriteAllTextAsync(filePath, jsonData);
                System.Diagnostics.Debug.WriteLine($"Pipeline '{pipelineData.Name}' saved to {filePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving pipeline '{pipelineData.Name}' to {filePath}: {ex.Message}");
            }
        }

        public async Task<PipelineData> LoadPipelineAsync(string pipelineName)
        {
            var filePath = GetPipelineFilePath(pipelineName);
            if (!File.Exists(filePath))
            {
                System.Diagnostics.Debug.WriteLine($"Pipeline file not found: {filePath}");
                return null;
            }

            try
            {
                var jsonData = await File.ReadAllTextAsync(filePath);
                var pipelineData = JsonSerializer.Deserialize<PipelineData>(jsonData, _jsonOptions);
                System.Diagnostics.Debug.WriteLine($"Pipeline '{pipelineName}' loaded from {filePath}");
                return pipelineData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading pipeline '{pipelineName}' from {filePath}: {ex.Message}");
                return null;
            }
        }

        public async Task<List<PipelineData>> ListPipelinesAsync()
        {
            var pipelines = new List<PipelineData>();
            try
            {
                var files = Directory.GetFiles(_pipelineDirectoryPath, "*.pipeline.json");
                foreach (var file in files)
                {
                    try
                    {
                        // Load the full data to get name and timestamp
                        var jsonData = await File.ReadAllTextAsync(file);
                        var pipelineData = JsonSerializer.Deserialize<PipelineData>(jsonData, _jsonOptions);
                        if (pipelineData != null)
                        {
                            pipelines.Add(pipelineData);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error reading pipeline file {file}: {ex.Message}");
                        // Optionally skip corrupted files
                    }
                }
                // Sort by last modified date, newest first
                return pipelines.OrderByDescending(p => p.LastModified).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error listing pipelines in {_pipelineDirectoryPath}: {ex.Message}");
                return pipelines; // Return empty or partially filled list on error
            }
        }

        public Task DeletePipelineAsync(string pipelineName)
        {
            var filePath = GetPipelineFilePath(pipelineName);
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    System.Diagnostics.Debug.WriteLine($"Pipeline '{pipelineName}' deleted from {filePath}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Pipeline file not found for deletion: {filePath}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting pipeline '{pipelineName}' from {filePath}: {ex.Message}");
            }
            return Task.CompletedTask; // Make async if needed
        }

        public async Task<bool> RenamePipelineAsync(string oldName, string newName)
        {
            var oldFilePath = GetPipelineFilePath(oldName);
            var newFilePath = GetPipelineFilePath(newName);

            if (!File.Exists(oldFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"Error renaming: Old pipeline file not found: {oldFilePath}");
                return false;
            }
            if (File.Exists(newFilePath))
            {
                System.Diagnostics.Debug.WriteLine($"Error renaming: New pipeline name '{newName}' already exists.");
                return false; // Prevent overwriting
            }

            try
            {
                // Load the data, update the name inside, save to new file, delete old file
                var jsonData = await File.ReadAllTextAsync(oldFilePath);
                var pipelineData = JsonSerializer.Deserialize<PipelineData>(jsonData, _jsonOptions);

                if (pipelineData != null)
                {
                    pipelineData.Name = newName; // Update the name within the data
                    await SavePipelineAsync(pipelineData); // Save under the new name
                    File.Delete(oldFilePath); // Delete the old file
                    System.Diagnostics.Debug.WriteLine($"Pipeline '{oldName}' renamed to '{newName}'.");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Error renaming: Failed to deserialize old pipeline data from {oldFilePath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error renaming pipeline '{oldName}' to '{newName}': {ex.Message}");
                // Clean up potentially partially created new file if rename failed mid-way?
                if (File.Exists(newFilePath) && !File.Exists(oldFilePath))
                {
                    // If old is gone but new exists, maybe try to move back? Risky.
                    // Or just leave the new file. For simplicity, we'll just log the error.
                }
                return false;
            }
        }

        private async Task SaveToFileAsync(string filePath, List<DataItem> newData)
        {
            try
            {
                // Get existing data
                var existingData = await LoadFromFileAsync<List<DataItem>, List<DataItem>>(filePath, c => c)
                               ?? new List<DataItem>();

                System.Diagnostics.Debug.WriteLine($"SaveToFileAsync: Loaded {existingData.Count} existing items from {filePath}");

                // Create a dictionary to efficiently track items to save by a unique key
                var dataToSaveDict = new Dictionary<string, DataItem>();

                // Add existing non-deleted items first
                foreach (var item in existingData.Where(i => !i.deleted))
                {
                    string key = !string.IsNullOrEmpty(item._id) ? item._id : item.Data?.ActionGroupObject?.ActionName;
                    if (!string.IsNullOrEmpty(key))
                    {
                        dataToSaveDict[key] = item;
                    }
                }

                // Keep track of items for logging
                int replacedCount = 0;
                int newCount = 0;
                int deletedCount = newData.Count(i => i.deleted);

                // Process new data: update existing or add new non-deleted items
                foreach (var item in newData)
                {
                    string key = !string.IsNullOrEmpty(item._id) ? item._id : item.Data?.ActionGroupObject?.ActionName;
                    if (string.IsNullOrEmpty(key)) continue; // Skip items without a key

                    if (item.deleted)
                    {
                        // If marked for deletion, remove from dictionary
                        dataToSaveDict.Remove(key);
                    }
                    else
                    {
                        // If not deleted, add or update in dictionary
                        if (dataToSaveDict.ContainsKey(key))
                        {
                            replacedCount++;
                        }
                        else
                        {
                            newCount++;
                        }
                        dataToSaveDict[key] = item;
                    }
                }

                var dataToSave = dataToSaveDict.Values.OrderByDescending(i => i.createdAt).ToList();

                System.Diagnostics.Debug.WriteLine($"SaveToFileAsync: Saving {dataToSave.Count} items to {filePath} " +
                    $"(replaced {replacedCount}, new {newCount}, removed {deletedCount} deleted items)");

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
                if (string.IsNullOrWhiteSpace(json) || json == "[]")
                {
                    System.Diagnostics.Debug.WriteLine($"File {filePath} is empty or contains only '[]'. Returning default value.");
                    return default;
                }
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var container = JsonSerializer.Deserialize<TContainer>(json, options);
                System.Diagnostics.Debug.WriteLine($"Successfully loaded data from {filePath}");
                return selector(container);
            }
            catch (JsonException jsonEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error deserializing JSON from {filePath}: {jsonEx.Message}. File content might be invalid.");
                // Optionally, backup the corrupted file and create a new empty one
                // File.Move(filePath, filePath + ".corrupted", true);
                // File.WriteAllText(filePath, "[]");
                return default; // Return default on error
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading data from {filePath}: {ex.Message}");
                return default;
            }
        }

        private void EnsureFileExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                try
                {
                    // Ensure the directory exists before writing the file
                    var fileDir = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                        System.Diagnostics.Debug.WriteLine($"Directory created at {fileDir}");
                    }

                    File.WriteAllText(filePath, "[]"); // Initialize with an empty JSON array
                    System.Diagnostics.Debug.WriteLine($"File created at {filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating file {filePath}: {ex.Message}");
                }
            }
            else
            {
                // Optional: Check if the file is empty and initialize if needed
                try
                {
                    if (new FileInfo(filePath).Length == 0)
                    {
                        File.WriteAllText(filePath, "[]");
                        System.Diagnostics.Debug.WriteLine($"Initialized empty file at {filePath}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"File already exists at {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking/initializing existing file {filePath}: {ex.Message}");
                }
            }
        }

        // Helper to ensure directory exists before saving a file
        private void EnsureFileDirectoryExists(string filePath)
        {
            var fileDir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(fileDir))
            {
                try
                {
                    Directory.CreateDirectory(fileDir);
                    System.Diagnostics.Debug.WriteLine($"Directory created at {fileDir}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating directory {fileDir}: {ex.Message}");
                }
            }
        }
    }
}