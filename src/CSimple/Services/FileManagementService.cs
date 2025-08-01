using CSimple.ViewModels;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Services
{
    /// <summary>
    /// Service responsible for managing file operations for nodes
    /// </summary>
    public interface IFileManagementService
    {
        Task ExecuteSelectSaveFileAsync(
            NodeViewModel selectedNode,
            Func<string, string, string, Task> showAlert,
            Func<Task> saveCurrentPipelineAsync);

        Task ExecuteCreateNewMemoryFileAsync(
            NodeViewModel selectedNode,
            string memoryFileName,
            Func<string, string, string, Task> showAlert,
            Func<Task> saveCurrentPipelineAsync,
            Action<string> setMemoryFileName);
    }

    public class FileManagementService : IFileManagementService
    {
        public async Task ExecuteSelectSaveFileAsync(
            NodeViewModel selectedNode,
            Func<string, string, string, Task> showAlert,
            Func<Task> saveCurrentPipelineAsync)
        {
            try
            {
                if (selectedNode == null || !selectedNode.IsFileNode)
                {
                    await showAlert?.Invoke("Error", "Please select a file node first.", "OK");
                    return;
                }

                Debug.WriteLine($"🗂️ [FileManagementService.ExecuteSelectSaveFileAsync] Opening file picker for node: {selectedNode.Name}");

                // Use the MAUI FilePicker to select a file for saving
                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select save file for text output",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".txt", ".md", ".log" } },
                        { DevicePlatform.Android, new[] { "text/*" } },
                        { DevicePlatform.iOS, new[] { "public.text" } },
                        { DevicePlatform.macOS, new[] { "txt", "md", "log" } }
                    })
                });

                if (fileResult != null)
                {
                    // Update the selected node's save file path
                    selectedNode.SaveFilePath = fileResult.FullPath;

                    Debug.WriteLine($"✅ [FileManagementService.ExecuteSelectSaveFileAsync] File selected: {fileResult.FullPath}");

                    // Persist the pipeline to save the file selection
                    await saveCurrentPipelineAsync();

                    Debug.WriteLine($"💾 [FileManagementService.ExecuteSelectSaveFileAsync] Pipeline saved with updated file path");
                }
                else
                {
                    Debug.WriteLine($"❌ [FileManagementService.ExecuteSelectSaveFileAsync] File selection cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [FileManagementService.ExecuteSelectSaveFileAsync] Error selecting file: {ex.Message}");
                await showAlert?.Invoke("Error", $"Failed to select file: {ex.Message}", "OK");
            }
        }

        public async Task ExecuteCreateNewMemoryFileAsync(
            NodeViewModel selectedNode,
            string memoryFileName,
            Func<string, string, string, Task> showAlert,
            Func<Task> saveCurrentPipelineAsync,
            Action<string> setMemoryFileName)
        {
            try
            {
                if (selectedNode == null || !selectedNode.IsFileNode)
                {
                    await showAlert?.Invoke("Error", "No file node selected.", "OK");
                    return;
                }

                Debug.WriteLine($"🗂️ [FileManagementService.ExecuteCreateNewMemoryFileAsync] Creating new memory file for node: {selectedNode.Name}");

                // Get the user-specific memory files directory path
                string userName = Environment.UserName;
                string memoryFilesDir = Path.Combine("C:", "Users", userName, "Documents", "CSimple", "Resources", "MemoryFiles");

                // Ensure the directory exists
                if (!Directory.Exists(memoryFilesDir))
                {
                    Directory.CreateDirectory(memoryFilesDir);
                    Debug.WriteLine($"📁 [FileManagementService.ExecuteCreateNewMemoryFileAsync] Created memory files directory: {memoryFilesDir}");
                }

                // Get filename from input field
                string fileName = string.IsNullOrWhiteSpace(memoryFileName)
                    ? $"Memory_{selectedNode.Name}_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : memoryFileName.Trim();

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    await showAlert?.Invoke("Error", "Please enter a name for the memory file.", "OK");
                    return;
                }

                // Validate filename - remove invalid characters
                char[] invalidChars = Path.GetInvalidFileNameChars();
                foreach (char c in invalidChars)
                {
                    fileName = fileName.Replace(c, '_');
                }

                // Ensure .txt extension
                if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".txt";
                }

                string fullFilePath = Path.Combine(memoryFilesDir, fileName);

                // Check if file already exists
                if (File.Exists(fullFilePath))
                {
                    bool overwrite = await Application.Current.MainPage.DisplayAlert(
                        "File Exists",
                        $"A file named '{fileName}' already exists. Do you want to overwrite it?",
                        "Yes", "No");

                    if (!overwrite)
                    {
                        Debug.WriteLine($"❌ [FileManagementService.ExecuteCreateNewMemoryFileAsync] File creation cancelled - user chose not to overwrite");
                        return;
                    }
                }

                // Create the file with initial content
                string initialContent = $"# Memory File for {selectedNode.Name}\n" +
                                      $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                      $"Node Type: {selectedNode.Type}\n" +
                                      $"Data Type: {selectedNode.DataType}\n\n" +
                                      $"## Memory Contents\n" +
                                      $"This file will store outputs from the '{selectedNode.Name}' node.\n\n";

                await File.WriteAllTextAsync(fullFilePath, initialContent);

                // Update the selected node's save file path
                selectedNode.SaveFilePath = fullFilePath;

                Debug.WriteLine($"✅ [FileManagementService.ExecuteCreateNewMemoryFileAsync] Memory file created: {fullFilePath}");

                // Persist the pipeline to save the file selection
                await saveCurrentPipelineAsync();

                Debug.WriteLine($"💾 [FileManagementService.ExecuteCreateNewMemoryFileAsync] Pipeline saved with new memory file path");

                // Clear the memory file name input for next use
                setMemoryFileName("");

                // Show success message
                await showAlert?.Invoke("Success", $"Memory file '{fileName}' created successfully!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [FileManagementService.ExecuteCreateNewMemoryFileAsync] Error creating memory file: {ex.Message}");
                await showAlert?.Invoke("Error", $"Failed to create memory file: {ex.Message}", "OK");
            }
        }
    }
}
