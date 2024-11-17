using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CSimple.Models;


namespace CSimple.Services
{
    public class FileService
    {
        private readonly string _actionGroupsFilePath;
        private readonly string _recordedActionsFilePath;

        public FileService()
        {
            // Use the Documents folder or similar location for user-specific files
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            System.Diagnostics.Debug.WriteLine($"Directory: {directory}");

            Directory.CreateDirectory(directory);  // Ensure directory exists
            _actionGroupsFilePath = Path.Combine(directory, "actionGroups.json");
            _recordedActionsFilePath = Path.Combine(directory, "recordedActions.json");

            // Ensure the files exist
            EnsureFileExists(_actionGroupsFilePath);
            EnsureFileExists(_recordedActionsFilePath);
        }


        // This method is used to save the action groups and actions to a JSON file
        public async Task SaveActionGroupsAsync(ObservableCollection<ActionGroup> actionGroups)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to save action groups and actions to {_actionGroupsFilePath}");
                System.Diagnostics.Debug.WriteLine("Length of FileService.ActionGroups:"+JsonSerializer.Serialize(actionGroups).Length.ToString());

                var options = new JsonSerializerOptions { WriteIndented = true };

                // Output the initial actionGroups variable
                System.Diagnostics.Debug.WriteLine("Initial Action Groups:");
                foreach (var actionGroup in actionGroups)
                {
                    if (actionGroup.ActionArrayFormatted == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"ActionGroup with missing ActionArrayFormatted: {JsonSerializer.Serialize(actionGroup)}");
                        actionGroup.ActionArrayFormatted = $"Creator:{(actionGroup.Creator == null ? "userId" : actionGroup.Creator)}|Action:{JsonSerializer.Serialize(actionGroup.ActionArray).Substring(0, 50)}";
                    }
                    System.Diagnostics.Debug.WriteLine(actionGroup.ActionArrayFormatted.ToString());
                }

                string actionGroupsJson;
                string actionArrayJson;

                System.Diagnostics.Debug.WriteLine("Preparing action groups to JSON");
                try
                {
                    // Serialize the action groups to JSON
                    actionGroupsJson = JsonSerializer.Serialize(actionGroups, options);
                    System.Diagnostics.Debug.WriteLine("Serialized action groups to JSON");
                    System.Diagnostics.Debug.WriteLine($"3. (FileService.Save) Action Groups JSON: {actionGroupsJson.Substring(0, Math.Min(50, actionGroupsJson.Length))}");
                }
                catch (JsonException jsonEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Error serializing action groups: {jsonEx.Message}");
                    return;
                }
                // // Extract the action arrays from each action group and combine them into a single array
                // var actionArray = actionGroups.SelectMany(ag => ag.ActionArray).ToArray();
                // System.Diagnostics.Debug.WriteLine($"Extracted {actionArray.Length} actions from action groups");

                // try
                // {
                //     actionArrayJson = JsonSerializer.Serialize(actionArray, options);
                //     System.Diagnostics.Debug.WriteLine("Serialized action array to JSON");
                //     System.Diagnostics.Debug.WriteLine($"Action Array JSON: {actionArrayJson}");
                // }
                // catch (JsonException jsonEx)
                // {
                //     System.Diagnostics.Debug.WriteLine($"Error serializing action array: {jsonEx.Message}");
                //     return;
                // }
                // Combine the serialized action groups and action array into a single JSON object
                // var combinedJson = $"{{\"ActionGroups\": {actionGroupsJson}, \"ActionArray\": {actionArrayJson}}}";
                var combinedJson = $"{{\"ActionGroups\": {actionGroupsJson}}}";
                // System.Diagnostics.Debug.WriteLine("Combined JSON for action groups and action array");
                // System.Diagnostics.Debug.WriteLine($"Combined JSON: {combinedJson}");

                // Write the combined JSON to the specified file path
                await File.WriteAllTextAsync(_actionGroupsFilePath, combinedJson);
                System.Diagnostics.Debug.WriteLine($"Successfully saved action groups and actions to {_actionGroupsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving action groups and actions: {ex.Message}");
            }
        }

        // This method is used to load the action groups and actions from a JSON file
        public async Task<ObservableCollection<ActionGroup>> LoadActionGroupsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to load action groups and actions from {_actionGroupsFilePath}");

                // Read the JSON content from the specified file path
                var jsonContent = await File.ReadAllTextAsync(_actionGroupsFilePath);
                System.Diagnostics.Debug.WriteLine($"3. (FileService.Load) Loaded JSON content: {jsonContent.Substring(0, Math.Min(50, jsonContent.Length))}");

                // Deserialize the JSON content to the helper class
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var container = JsonSerializer.Deserialize<ActionGroupsContainer>(jsonContent, options);
                System.Diagnostics.Debug.WriteLine("Deserialized action groups from JSON");

                return container?.ActionGroups;
            }
            catch (JsonException jsonEx)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading action groups: {jsonEx.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading action groups and actions: {ex.Message}");
                return null;
            }
        }

        // New methods for RecordedActions
        public async Task SaveRecordedActionsAsync(List<string> recordedActions)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to save recorded actions to {_recordedActionsFilePath}");

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(recordedActions, options);

                await File.WriteAllTextAsync(_recordedActionsFilePath, json);
                System.Diagnostics.Debug.WriteLine($"Successfully saved recorded actions to {_recordedActionsFilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving recorded actions: {ex.Message}");
            }
        }

        public async Task<List<string>> LoadRecordedActionsAsync()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to load recorded actions from {_recordedActionsFilePath}");

                if (!File.Exists(_recordedActionsFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("File does not exist. Returning empty list.");
                    return new List<string>();
                }

                var json = await File.ReadAllTextAsync(_recordedActionsFilePath);
                var recordedActions = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                System.Diagnostics.Debug.WriteLine($"Successfully loaded recorded actions from {_recordedActionsFilePath}");
                return recordedActions;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading recorded actions: {ex.Message}");
                return new List<string>();
            }
        }

        private void EnsureFileExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                try
                {
                    // Create the file with an empty array
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
// Helper class to match the JSON structure
public class ActionGroupsContainer{
    public ObservableCollection<ActionGroup> ActionGroups { get; set; }
    public Action[] ActionArray { get; set; }
}