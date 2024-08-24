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

    // Existing methods for ActionGroups
    public async Task SaveActionGroupsAsync(ObservableCollection<ActionGroup> actionGroups)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to save action groups to {_actionGroupsFilePath}");

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(actionGroups, options);

            await File.WriteAllTextAsync(_actionGroupsFilePath, json);
            System.Diagnostics.Debug.WriteLine($"Successfully saved action groups to {_actionGroupsFilePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving action groups: {ex.Message}");
        }
    }

    public async Task<ObservableCollection<ActionGroup>> LoadActionGroupsAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Attempting to load action groups from {_actionGroupsFilePath}");

            if (!File.Exists(_actionGroupsFilePath))
            {
                System.Diagnostics.Debug.WriteLine("File does not exist. Returning empty collection.");
                return new ObservableCollection<ActionGroup>();
            }

            var json = await File.ReadAllTextAsync(_actionGroupsFilePath);
            var actionGroups = JsonSerializer.Deserialize<ObservableCollection<ActionGroup>>(json) ?? new ObservableCollection<ActionGroup>();
            System.Diagnostics.Debug.WriteLine($"Successfully loaded action groups from {_actionGroupsFilePath}");
            return actionGroups;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading action groups: {ex.Message}");
            return new ObservableCollection<ActionGroup>();
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
