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
            // Set the file path to the Data folder in the project directory
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSimple", "Resources");
            Directory.CreateDirectory(directory);  // Ensure directory exists
            _filePath = Path.Combine(directory, "actionGroups.json");
        }

        public async Task SaveActionGroupsAsync(ObservableCollection<ActionGroup> actionGroups)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(actionGroups, options);

            await File.WriteAllTextAsync(_filePath, json);
        }

        public async Task<ObservableCollection<ActionGroup>> LoadActionGroupsAsync()
        {
            if (!File.Exists(_filePath))
                return new ObservableCollection<ActionGroup>();

            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<ObservableCollection<ActionGroup>>(json);
        }
    }
}
