using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Xaml; // Add this namespace for XamlCompilation
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using CSimple;
using CSimple.Models;

namespace CSimple.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ActionDetailPage : ContentPage
    {
        public ActionDetailViewModel ViewModel { get; private set; }

        public ActionDetailPage(ActionGroup actionGroup)
        {
            try
            {
                InitializeComponent();
                ViewModel = new ActionDetailViewModel(actionGroup, Navigation); // Pass Navigation here
                BindingContext = ViewModel;

                // Ensure the hamburger menu is accessible
                Shell.SetNavBarIsVisible(this, true);
                Shell.SetFlyoutBehavior(this, FlyoutBehavior.Flyout);

                Debug.WriteLine("ActionDetailPage initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing ActionDetailPage: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Fall back to a very simple initialization if needed
                BindingContext = new { ActionName = "Error Loading Action", ActionType = "Error" };
            }
        }
    }

    public class StepViewModel
    {
        public string Index { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
        public string KeyName { get; set; } // New property for key name
        public int KeyCode { get; set; } // New property for key code
        public DateTime Timestamp { get; set; } // New property for timestamp
    }

    public class ActionDetailViewModel : INotifyPropertyChanged
    {
        private readonly INavigation _navigation;
        private readonly ActionGroup _actionGroup;

        // Basic properties
        public string ActionName { get; set; }
        public string ActionType { get; set; }
        public string CreatedAt { get; set; }
        public string Duration { get; set; }
        public string Description { get; set; }
        public int UsageCount { get; set; }
        public double SuccessRate { get; set; }
        public bool IsPartOfTraining { get; set; }
        public int StepCount => ActionSteps?.Count ?? 0;
        public string ActionArrayFormatted { get; set; }

        // Collections
        public ObservableCollection<StepViewModel> ActionSteps { get; } = new ObservableCollection<StepViewModel>();
        public ObservableCollection<ModelAssignment> AssignedModels { get; } = new ObservableCollection<ModelAssignment>();
        public ObservableCollection<FileViewModel> AttachedFiles { get; } = new ObservableCollection<FileViewModel>();

        // Commands
        public ICommand BackCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand AssignToModelCommand { get; }
        public ICommand PlayAudioCommand { get; }

        public ActionDetailViewModel(ActionGroup actionGroup, INavigation navigation)
        {
            try
            {
                _actionGroup = actionGroup;
                _navigation = navigation;

                // Set basic properties with null checking
                ActionName = actionGroup?.ActionName ?? "Unnamed Action";
                ActionType = actionGroup?.ActionType ?? "Custom Action";
                CreatedAt = actionGroup?.CreatedAt?.ToString("g") ?? DateTime.Now.ToString("g");
                Duration = "0.5 seconds"; // Default
                Description = actionGroup?.Description ?? "No description available";
                UsageCount = actionGroup?.UsageCount ?? 0;
                SuccessRate = actionGroup?.SuccessRate ?? 0.85;
                IsPartOfTraining = actionGroup?.IsPartOfTraining ?? false;
                ActionArrayFormatted = FormatActionArray();

                // Initialize steps
                InitializeSteps();

                // Initialize models (demo data)
                InitializeModels();

                // Initialize attached files
                InitializeAttachedFiles();

                // Setup commands
                BackCommand = new Command(async () =>
                {
                    try
                    {
                        await Shell.Current.GoToAsync(".."); // Navigate back to the previous page
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in BackCommand: {ex.Message}");
                    }
                });

                DeleteCommand = new Command(DeleteAction);
                ExecuteCommand = new Command(ExecuteAction);
                AssignToModelCommand = new Command(AssignToModel);
                PlayAudioCommand = new Command<string>(PlayAudio);

                Debug.WriteLine("ActionDetailViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ActionDetailViewModel constructor: {ex.Message}");

                // Set default values to avoid null references
                ActionName = "Error";
                ActionType = "Error";
                CreatedAt = DateTime.Now.ToString("g");
                ActionArrayFormatted = "Could not load action data";
                Description = "Error loading action details";

                // Create a fallback command
                BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
                DeleteCommand = new Command(() => { });
                ExecuteCommand = new Command(() => { });
                AssignToModelCommand = new Command(() => { });
            }
        }

        private void InitializeSteps()
        {
            try
            {
                if (_actionGroup?.ActionArray != null)
                {
                    DateTime startTime = DateTime.MaxValue;
                    DateTime endTime = DateTime.MinValue;

                    for (int i = 0; i < _actionGroup.ActionArray.Count; i++)
                    {
                        var step = _actionGroup.ActionArray[i];
                        string description = step.ToString();
                        string keyName = "";
                        int keyCode = 0;
                        string keyAction = ""; // "Key Up" or "Key Down"
                        DateTime timestamp = DateTime.MinValue;

                        // Extract key name and code if it's a key press/release event
                        if (step.EventType == 256 || step.EventType == 257)
                        {
                            keyCode = (int)step.KeyCode;
                            keyName = GetKeyName((ushort)keyCode); // Use GetKeyName method
                            keyAction = step.EventType == 256 ? "Down" : "Up"; // Set "Up" or "Down"
                            description = $"Key {keyName} {keyAction} (Code: {keyCode})";
                        }

                        // Get timestamp
                        if (step.Timestamp != null && DateTime.TryParse(step.Timestamp.ToString(), out timestamp))
                        {
                            // Update start and end times
                            startTime = timestamp < startTime ? timestamp : startTime;
                            endTime = timestamp > endTime ? timestamp : endTime;
                        }

                        ActionSteps.Add(new StepViewModel
                        {
                            Index = (i + 1).ToString(),
                            Description = description,
                            Duration = $"{(new Random().NextDouble() * 0.3).ToString("0.00")}s",
                            KeyName = keyName,
                            KeyCode = keyCode,
                            Timestamp = timestamp
                        });
                    }

                    // Calculate and set the duration
                    if (startTime != DateTime.MaxValue && endTime != DateTime.MinValue)
                    {
                        TimeSpan duration = endTime - startTime;
                        Duration = $"{duration.TotalSeconds:0.00} seconds";
                        OnPropertyChanged(nameof(Duration));
                    }
                }

                // Add demo steps if we don't have any
                if (ActionSteps.Count == 0)
                {
                    ActionSteps.Add(new StepViewModel { Index = "1", Description = "Mouse Move to X:500, Y:300", Duration = "0.12s", Timestamp = DateTime.Now });
                    ActionSteps.Add(new StepViewModel { Index = "2", Description = "Left Click", Duration = "0.05s", Timestamp = DateTime.Now.AddSeconds(0.12) });
                    ActionSteps.Add(new StepViewModel { Index = "3", Description = "Key A Down (65)", Duration = "0.08s", KeyName = "A", KeyCode = 65, Timestamp = DateTime.Now.AddSeconds(0.17) });
                    ActionSteps.Add(new StepViewModel { Index = "4", Description = "Key A Up (65)", Duration = "0.04s", KeyName = "A", KeyCode = 65, Timestamp = DateTime.Now.AddSeconds(0.25) });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing steps: {ex.Message}");
                ActionSteps.Add(new StepViewModel { Index = "!", Description = "Error loading steps", Duration = "N/A" });
            }
        }

        private string GetKeyName(ushort keyCode)
        {
            // Common key mappings
            Dictionary<ushort, string> keyNames = new Dictionary<ushort, string>
            {
                { 8, "Backspace" }, { 9, "Tab" }, { 13, "Enter" }, { 16, "Shift" },
                { 17, "Ctrl" }, { 18, "Alt" }, { 19, "Pause" }, { 20, "Caps Lock" },
                { 27, "Esc" }, { 32, "Space" }, { 33, "Page Up" }, { 34, "Page Down" },
                { 35, "End" }, { 36, "Home" }, { 37, "Left Arrow" }, { 38, "Up Arrow" },
                { 39, "Right Arrow" }, { 40, "Down Arrow" }, { 45, "Insert" }, { 46, "Delete" },
                { 91, "Windows" }, { 93, "Menu" }, { 144, "Num Lock" }, { 186, ";" },
                { 187, "=" }, { 188, "," }, { 189, "-" }, { 190, "." }, { 191, "/" },
                { 192, "`" }, { 219, "[" }, { 220, "\\" }, { 221, "]" }, { 222, "'" },
                { 513, "Left Mouse" }, { 516, "Right Mouse" }, { 519, "Middle Mouse" },
                { 0x0200, "Mouse Move" }
            };

            // Add F1-F12
            for (ushort i = 112; i <= 123; i++)
            {
                keyNames[i] = $"F{i - 111}";
            }

            // Add numbers
            for (ushort i = 48; i <= 57; i++)
            {
                keyNames[i] = $"{i - 48}";
            }

            // Add letters
            for (ushort i = 65; i <= 90; i++)
            {
                keyNames[i] = $"{(char)i}";
            }

            // Return the key name if it exists, otherwise return the key code
            return keyNames.ContainsKey(keyCode) ? keyNames[keyCode] : $"Key {keyCode}";
        }

        private void InitializeModels()
        {
            // Add some demo model assignments
            if (IsPartOfTraining)
            {
                AssignedModels.Add(new ModelAssignment
                {
                    ModelId = "model1",
                    ModelName = "General Assistant",
                    ModelType = "Multimodal",
                    AssignedDate = DateTime.Now.AddDays(-5)
                });

                AssignedModels.Add(new ModelAssignment
                {
                    ModelId = "model2",
                    ModelName = "Workflow Automator",
                    ModelType = "Task Specific",
                    AssignedDate = DateTime.Now.AddDays(-2)
                });
            }
        }

        private void InitializeAttachedFiles()
        {
            try
            {
                if (_actionGroup?.Files != null && _actionGroup.Files.Any())
                {
                    foreach (var file in _actionGroup.Files)
                    {
                        string fileType = GetFileType(file.Filename);
                        string fileTypeIcon = GetFileTypeIcon(fileType);

                        AttachedFiles.Add(new FileViewModel
                        {
                            Filename = file.Filename,
                            FileType = fileType,
                            FileTypeIcon = fileTypeIcon,
                            Data = file.Data // File path or content
                        });
                    }
                }
                else
                {
                    Debug.WriteLine("No files attached to this action group.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing attached files: {ex.Message}");
            }
        }

        private string GetFileType(string filename)
        {
            if (filename.EndsWith(".mp3") || filename.EndsWith(".wav"))
                return "Audio";
            if (filename.EndsWith(".png") || filename.EndsWith(".jpg"))
                return "Image";
            if (filename.EndsWith(".txt"))
                return "Text";
            return "Unknown";
        }

        private string GetFileTypeIcon(string fileType)
        {
            return fileType switch
            {
                "Audio" => "audio_icon.png",
                "Image" => "image_icon.png",
                "Text" => "text_icon.png",
                _ => "unknown_icon.png"
            };
        }

        private string FormatActionArray()
        {
            try
            {
                if (_actionGroup?.ActionArray == null || !_actionGroup.ActionArray.Any())
                    return "No actions defined";

                return string.Join("\n", _actionGroup.ActionArray.Select((a, i) => $"{i + 1}. {a}"));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error formatting action array: {ex.Message}");
                return "Error loading action steps";
            }
        }

        private async void ExecuteAction()
        {
            try
            {
                // Show an alert while we simulate execution
                await Application.Current.MainPage.DisplayAlert(
                    "Executing Action",
                    $"Executing {ActionName}...",
                    "OK");

                // In a real app, you'd execute the action here
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing action: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Could not execute action: {ex.Message}",
                    "OK");
            }
        }

        private async void AssignToModel()
        {
            try
            {
                string modelName = await Application.Current.MainPage.DisplayPromptAsync(
                    "Assign to Model",
                    "Enter model name:",
                    "Assign",
                    "Cancel",
                    "New Model",
                    maxLength: 50);

                if (!string.IsNullOrEmpty(modelName))
                {
                    var newModel = new ModelAssignment
                    {
                        ModelId = Guid.NewGuid().ToString(),
                        ModelName = modelName,
                        ModelType = "Custom Model",
                        AssignedDate = DateTime.Now
                    };

                    AssignedModels.Add(newModel);

                    // Toggle the IsPartOfTraining flag to true
                    IsPartOfTraining = true;
                    OnPropertyChanged(nameof(IsPartOfTraining));

                    // Confirm to the user
                    await Application.Current.MainPage.DisplayAlert(
                        "Model Assigned",
                        $"This action has been assigned to model '{modelName}'",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error assigning to model: {ex.Message}");
            }
        }

        private async void DeleteAction()
        {
            try
            {
                bool confirmed = await Application.Current.MainPage.DisplayAlert(
                    "Confirm Delete",
                    $"Are you sure you want to delete the action '{ActionName}'?",
                    "Yes", "No");

                if (confirmed)
                {
                    // Here you would call a service to delete the action
                    await _navigation.PopModalAsync();

                    // Optionally show a confirmation on the main page
                    await Task.Delay(300); // Allow time for page transition
                    await Application.Current.MainPage.DisplayAlert(
                        "Success",
                        "Action was deleted successfully",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in delete action: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    "Could not delete action",
                    "OK");
            }
        }

        private async void PlayAudio(string audioData)
        {
            try
            {
                // Simulate playing audio (replace with actual audio playback logic)
                await Application.Current.MainPage.DisplayAlert("Play Audio", "Playing audio...", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error playing audio: {ex.Message}");
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class FileViewModel
    {
        public string Filename { get; set; }
        public string FileType { get; set; }
        public string FileTypeIcon { get; set; }
        public string Data { get; set; }
    }

    // Model classes for media files
    public class MediaFile
    {
        public string Filename { get; set; }
        public string Data { get; set; }
    }

    public class AudioFile : MediaFile
    {
        public int Duration { get; set; } // Duration in seconds
    }

    public class TextFile
    {
        public string Filename { get; set; }
        public string Content { get; set; }
    }

    public class OtherFile : MediaFile
    {
        public int FileSize { get; set; } // Size in KB
        public string FileTypeIcon { get; set; }
    }

    public class ActionStep
    {
        public string StepNumber { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
    }

    public class SimilarAction
    {
        public string Name { get; set; }
        public double Similarity { get; set; }
    }
}
