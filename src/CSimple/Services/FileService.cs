using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CSimple.Models;

namespace CSimple.Services
{
    public class FileService
    {
        private readonly string _filePath;
        public FileService()
        {
            // Use the Documents folder or similar location for user-specific files
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources");
            System.Diagnostics.Debug.WriteLine($"Directory: {directory}");

            Directory.CreateDirectory(directory);  // Ensure directory exists
            _filePath = Path.Combine(directory, "actionGroups.json");

            // Ensure the file exists
            EnsureFileExists();
        }

        public async Task SaveActionGroupsAsync(ObservableCollection<ActionGroup> actionGroups)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to save action groups to {_filePath}");

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(actionGroups, options);

                await File.WriteAllTextAsync(_filePath, json);
                System.Diagnostics.Debug.WriteLine($"Successfully saved action groups to {_filePath}");
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
                System.Diagnostics.Debug.WriteLine($"Attempting to load action groups from {_filePath}");

                if (!File.Exists(_filePath))
                {
                    System.Diagnostics.Debug.WriteLine("File does not exist. Returning empty collection.");
                    return new ObservableCollection<ActionGroup>();
                }

                var json = await File.ReadAllTextAsync(_filePath);
                var actionGroups = JsonSerializer.Deserialize<ObservableCollection<ActionGroup>>(json) ?? new ObservableCollection<ActionGroup>();
                System.Diagnostics.Debug.WriteLine($"Successfully loaded action groups from {_filePath}");
                return actionGroups;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading action groups: {ex.Message}");
                return new ObservableCollection<ActionGroup>();
            }
        }

        private void EnsureFileExists()
        {
            if (!File.Exists(_filePath))
            {
                try
                {
                    // Create the file with an empty array
                    File.WriteAllText(_filePath, "[]");
                    System.Diagnostics.Debug.WriteLine($"File created at {_filePath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error creating file: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"File already exists at {_filePath}");
            }
        }
    }
}
