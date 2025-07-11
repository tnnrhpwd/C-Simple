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
using System.Text;
using System.IO;
using CSimple;
using CSimple.Helpers;
using CSimple.Models;
using CSimple.ViewModels;
using Microsoft.Extensions.DependencyInjection;

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

        private async void NavigateToAiModels_Clicked(object sender, EventArgs e)
        {
            var viewModel = this.Handler.MauiContext.Services.GetService<NetPageViewModel>();
            await Navigation.PushAsync(new NetPage(viewModel));
        }
    }

    public class StepViewModel
    {
        public string Index { get; set; }
        public string Description { get; set; }
        public string Duration { get; set; }
        public string KeyName { get; set; }
        public int KeyCode { get; set; }
        public string Timestamp { get; set; }
        public bool IsMouseMove { get; set; }
        public bool IsMouseButton { get; set; }
        public string MouseButtonAction { get; set; } // "Down" or "Up"
        public string MouseButtonType { get; set; } // "Left", "Right", "Middle"
        public ActionItem RawData { get; set; }

        // Group properties
        public bool IsGrouped { get; set; }
        public int GroupCount { get; set; }
        public string GroupType { get; set; }
        public TimeSpan GroupDuration { get; set; }
        public List<ActionItem> GroupedItems { get; set; } = new List<ActionItem>();
    }

    public class ActionDetailViewModel : INotifyPropertyChanged
    {
        private readonly INavigation _navigation; private readonly ActionGroup _actionGroup;
        private readonly DataService _dataService; // Add DataService
        private readonly FileService _fileService; // Add FileService for local operations
        private readonly ActionService _actionService; // Add ActionService reference
        private readonly NetPageViewModel _netPageViewModel; // Add NetPageViewModel reference for AI models

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
        public ObservableCollection<FileViewModel> AttachedFiles { get; } = new ObservableCollection<FileViewModel>();        // Commands
        public ICommand BackCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand AssignToModelCommand { get; }
        public ICommand PlayAudioCommand { get; }
        public ICommand SaveChangesCommand { get; }
        // AI Model Commands
        public ICommand SelectModelCommand { get; }
        public ICommand ExecuteAiModelCommand { get; }
        public ICommand UndoAiChangesCommand { get; }
        public ICommand NavigateToAiModelsCommand { get; }

        // Add IsLoading property
        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        // Summary properties
        public int ClickCount { get; set; }
        public int PressCount { get; set; }
        public int MoveCount { get; set; }

        // Add this property to hold the combined text of action steps
        private string _actionStepsText;
        public string ActionStepsText
        {
            get => _actionStepsText;
            set
            {
                if (_actionStepsText != value)
                {
                    _actionStepsText = value;
                    OnPropertyChanged(nameof(ActionStepsText));
                }
            }
        }

        // AI Model Properties
        private string _aiPromptText;
        public string AiPromptText
        {
            get => _aiPromptText;
            set
            {
                if (_aiPromptText != value)
                {
                    _aiPromptText = value;
                    OnPropertyChanged(nameof(AiPromptText));
                    OnPropertyChanged(nameof(CanExecuteAiModel));
                }
            }
        }

        private NeuralNetworkModel _selectedAiModel;
        public NeuralNetworkModel SelectedAiModel
        {
            get => _selectedAiModel;
            set
            {
                if (_selectedAiModel != value)
                {
                    _selectedAiModel = value;
                    OnPropertyChanged(nameof(SelectedAiModel));
                    OnPropertyChanged(nameof(HasSelectedAiModel));
                    OnPropertyChanged(nameof(CanExecuteAiModel));
                    OnPropertyChanged(nameof(SelectedModelDownloadStatus));
                }
            }
        }

        public bool HasSelectedAiModel => SelectedAiModel != null;
        public bool CanExecuteAiModel => HasSelectedAiModel && !string.IsNullOrWhiteSpace(AiPromptText) && !IsLoading;

        // Property to show download status of selected model
        public string SelectedModelDownloadStatus
        {
            get
            {
                if (SelectedAiModel == null) return "";
                bool isDownloaded = IsModelDownloaded(SelectedAiModel.HuggingFaceModelId);
                return isDownloaded ? "📱 Downloaded" : "☁️ Cloud";
            }
        }

        private string _originalActionStepsText;
        private bool _hasAiChangesToUndo = false;
        public bool HasAiChangesToUndo
        {
            get => _hasAiChangesToUndo;
            set
            {
                if (_hasAiChangesToUndo != value)
                {
                    _hasAiChangesToUndo = value;
                    OnPropertyChanged(nameof(HasAiChangesToUndo));
                }
            }
        }

        private ObservableCollection<NeuralNetworkModel> _availableTextModels = new ObservableCollection<NeuralNetworkModel>();
        public ObservableCollection<NeuralNetworkModel> AvailableTextModels
        {
            get => _availableTextModels;
            set
            {
                if (_availableTextModels != value)
                {
                    _availableTextModels = value;
                    OnPropertyChanged(nameof(AvailableTextModels));
                }
            }
        }

        public ActionDetailViewModel(ActionGroup actionGroup, INavigation navigation)
        {
            try
            {
                _actionGroup = actionGroup;
                _navigation = navigation;
                _dataService = new DataService(); // Initialize DataService
                _fileService = new FileService(); // Initialize FileService
                _actionService = new ActionService(_dataService, _fileService); // Initialize ActionService

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
                CalculateSummary();

                // Initialize models (demo data)
                InitializeModels();                // Initialize attached files
                InitializeAttachedFiles();                // Initialize AI models (fire and forget since constructor can't be async)
                _ = Task.Run(async () => await InitializeAvailableTextModels());

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
                SaveChangesCommand = new Command(SaveChanges); // Initialize the SaveChangesCommand

                // Initialize AI model commands
                SelectModelCommand = new Command(SelectAiModel);
                ExecuteAiModelCommand = new Command(async () => await ExecuteAiModel());
                UndoAiChangesCommand = new Command(UndoAiChanges);
                NavigateToAiModelsCommand = new Command(async () => await NavigateToAiModels());
                NavigateToAiModelsCommand = new Command(async () => await NavigateToAiModels());

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
                Description = "Error loading action details";                // Create a fallback command
                BackCommand = new Command(async () => await Shell.Current.GoToAsync(".."));
                DeleteCommand = new Command(() => { });
                ExecuteCommand = new Command(() => { });
                AssignToModelCommand = new Command(() => { });
                SelectModelCommand = new Command(() => { });
                ExecuteAiModelCommand = new Command(() => { });
                UndoAiChangesCommand = new Command(() => { });
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

                    // Create temporary list for processing
                    var processedSteps = new List<StepViewModel>();
                    var actionStepsTextBuilder = new System.Text.StringBuilder(); // StringBuilder to build the combined text

                    for (int i = 0; i < _actionGroup.ActionArray.Count; i++)
                    {
                        var step = _actionGroup.ActionArray[i];
                        string description = step.ToString();
                        string keyName = "";
                        int keyCode = 0;
                        bool isMouseButton = false;
                        string mouseButtonType = "";
                        string mouseButtonAction = "";
                        string timestamp = "";

                        // Extract key name and code if it's a key press/release event
                        if (step.EventType == 256 || step.EventType == 257)
                        {
                            keyCode = (int)step.KeyCode;
                            keyName = GetKeyName((ushort)keyCode);
                            string keyAction = step.EventType == 256 ? "Down" : "Up";
                            description = $"Key {keyName} {keyAction} (Code: {keyCode})";
                        }
                        else if (step.EventType == 512 || step.EventType == 0x0200) // Mouse move event
                        {
                            description = $"Mouse Move to X:{step.Coordinates?.X ?? 0}, Y:{step.Coordinates?.Y ?? 0}";
                        }
                        // Handle mouse button events
                        else if (IsMouseButtonEvent(step.EventType))
                        {
                            isMouseButton = true;
                            mouseButtonType = GetMouseButtonType(step.EventType);
                            mouseButtonAction = IsMouseButtonDown(step.EventType) ? "Down" : "Up";
                            description = $"{mouseButtonType} Click";

                            // If coordinates are available, include them
                            if (step.Coordinates != null)
                            {
                                description += $" at X:{step.Coordinates.X}, Y:{step.Coordinates.Y}";
                            }
                        }

                        // Get timestamp
                        if (step.Timestamp != null)
                        {
                            timestamp = step.Timestamp.ToString();
                            if (DateTime.TryParse(timestamp, out DateTime parsedTimestamp))
                            {
                                // Update start and end times
                                startTime = parsedTimestamp < startTime ? parsedTimestamp : startTime;
                                endTime = parsedTimestamp > endTime ? parsedTimestamp : endTime;
                            }
                        }
                        else
                        {
                            timestamp = "N/A";
                        }

                        StepViewModel stepViewModel = new StepViewModel
                        {
                            Index = (i + 1).ToString(),
                            Description = description,
                            Duration = $"{(new Random().NextDouble() * 0.3).ToString("0.00")}s",
                            KeyName = keyName,
                            KeyCode = keyCode,
                            Timestamp = step.Timestamp?.ToString(),
                            IsMouseMove = step.EventType == 512 || step.EventType == 0x0200,
                            IsMouseButton = isMouseButton,
                            MouseButtonType = mouseButtonType,
                            MouseButtonAction = mouseButtonAction,
                            RawData = step
                        };

                        processedSteps.Add(stepViewModel);
                        // Append all relevant step details to the text builder
                        actionStepsTextBuilder.AppendLine($"Description: {description} | Key: {keyName} | Code: {keyCode} | MouseButton: {mouseButtonType} {mouseButtonAction} | Timestamp: {timestamp}");
                    }

                    // Group similar consecutive actions
                    ActionSteps.Clear();
                    ActionStepGrouping.GroupSimilarActions(processedSteps, ActionSteps);

                    // Calculate and set the duration
                    if (startTime != DateTime.MaxValue && endTime != DateTime.MinValue)
                    {
                        TimeSpan duration = endTime - startTime;
                        Duration = $"{duration.TotalSeconds:0.00} seconds";
                        OnPropertyChanged(nameof(Duration));
                    }

                    ActionStepsText = actionStepsTextBuilder.ToString(); // Set the combined text
                }

                // Add demo steps if we don't have any
                if (ActionSteps.Count == 0)
                {
                    AddDemoSteps();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing steps: {ex.Message}");
                ActionSteps.Add(new StepViewModel { Index = "!", Description = "Error loading steps", Duration = "N/A" });
            }
        }

        private void AddDemoSteps()
        {
            // Add simple demo steps
            ActionSteps.Add(new StepViewModel
            {
                Index = "1",
                Description = "Mouse Move to X:500, Y:300",
                Duration = "0.12s",
                Timestamp = DateTime.Now.ToString(),
                IsMouseMove = true
            });

            // Add a group of mouse movements
            ActionSteps.Add(new StepViewModel
            {
                Index = "2",
                Description = "Mouse Movement Path (15 steps)",
                IsGrouped = true,
                GroupCount = 15,
                GroupType = "Mouse Movements",
                Duration = "0.85s",
                GroupDuration = TimeSpan.FromSeconds(0.85),
                IsMouseMove = true,
                Timestamp = DateTime.Now.AddSeconds(0.2).ToString()
            });

            ActionSteps.Add(new StepViewModel
            {
                Index = "3",
                Description = "Left Click",
                Duration = "0.05s",
                Timestamp = DateTime.Now.AddSeconds(1.05).ToString(),
                IsMouseButton = true,
                MouseButtonType = "Left",
                MouseButtonAction = "Down"
            });

            ActionSteps.Add(new StepViewModel
            {
                Index = "4",
                Description = "Left Click",
                Duration = "0.05s",
                Timestamp = DateTime.Now.AddSeconds(1.1).ToString(),
                IsMouseButton = true,
                MouseButtonType = "Left",
                MouseButtonAction = "Up"
            });

            // Add a group of key repetitions
            ActionSteps.Add(new StepViewModel
            {
                Index = "5",
                Description = "Repeated Key A (12 times)",
                IsGrouped = true,
                GroupCount = 12,
                GroupType = "Key Repetition",
                KeyName = "A",
                KeyCode = 65,
                Duration = "0.6s",
                Timestamp = DateTime.Now.AddSeconds(1.2).ToString()
            });
        }

        // Helper method to check if an event is a mouse button event
        private bool IsMouseButtonEvent(int eventType)
        {
            return eventType == 0x0201 || // WM_LBUTTONDOWN
                   eventType == 0x0202 || // WM_LBUTTONUP
                   eventType == 0x0204 || // WM_RBUTTONDOWN
                   eventType == 0x0205 || // WM_RBUTTONUP
                   eventType == 0x0207 || // WM_MBUTTONDOWN
                   eventType == 0x0208;   // WM_MBUTTONUP
        }

        // Helper method to determine if the event is a button down event
        private bool IsMouseButtonDown(int eventType)
        {
            return eventType == 0x0201 || // WM_LBUTTONDOWN
                   eventType == 0x0204 || // WM_RBUTTONDOWN
                   eventType == 0x0207;   // WM_MBUTTONDOWN
        }

        // Helper method to get the button type name
        private string GetMouseButtonType(int eventType)
        {
            switch (eventType)
            {
                case 0x0201: // WM_LBUTTONDOWN
                case 0x0202: // WM_LBUTTONUP
                    return "Left";
                case 0x0204: // WM_RBUTTONDOWN
                case 0x0205: // WM_RBUTTONUP
                    return "Right";
                case 0x0207: // WM_MBUTTONDOWN
                case 0x0208: // WM_MBUTTONUP
                    return "Middle";
                default:
                    return "Unknown";
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
                // Update UI to show executing state
                IsLoading = true;

                // Configure any execution settings before running the action
                ConfigureExecutionSettings();

                // Call the same ToggleSimulateActionGroupAsync method that ActionPage uses
                await _actionService.ToggleSimulateActionGroupAsync(_actionGroup);

                Debug.WriteLine($"Action execution completed for {ActionName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing action: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Could not execute action: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Helper method to configure execution settings
        private void ConfigureExecutionSettings()
        {
            // Configure the ActionService with user preferences for superior drag operations
            _actionService.UseInterpolation = true; // Better for dragging

            // Higher number of steps for smoother drag operations
            _actionService.MovementSteps = 30;

            // Minimal delay between movement steps for responsive dragging
            _actionService.MovementDelayMs = 1;

            // Use a sensitivity multiplier that preserves exact drag positions
            _actionService.GameSensitivityMultiplier = 1.0f;

            // Enable ultra-smooth mode for precise drag operations
            _actionService.UltraSmoothMode = true;

            // Update action group description to indicate click/drag capability
            if (_actionGroup?.Description != null &&
                !_actionGroup.Description.Contains("click and drag"))
            {
                _actionGroup.Description += " (Supports click and drag operations)";
                Description = _actionGroup.Description;
                OnPropertyChanged(nameof(Description));
            }

            Debug.WriteLine("Configured action service for enhanced drag operations");
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
                    // Fix: Properly handle Id regardless of type
                    string actionId = null;
                    if (_actionGroup?.Id != null)
                    {
                        // Handle different Id types safely - convert to string
                        actionId = _actionGroup.Id.ToString();
                    }

                    string actionName = _actionGroup?.ActionName;

                    Debug.WriteLine($"Attempting to delete action with ID: {actionId ?? "null"} and name: {actionName}");

                    // Validate ID format before proceeding
                    if (string.IsNullOrEmpty(actionId))
                    {
                        Debug.WriteLine("Error: Action ID is null or empty");
                        await Application.Current.MainPage.DisplayAlert(
                            "Error",
                            "Cannot delete this action: Invalid action identifier",
                            "OK");
                        return;
                    }

                    // Check if this is a local action
                    bool isLocalOnly = _actionGroup?.IsLocal == true;
                    bool deleteSuccess = false;

                    try
                    {
                        IsLoading = true; // Add loading indicator

                        // Handle local storage deletion - check both dataItems.json and localDataItems.json
                        if (isLocalOnly)
                        {
                            Debug.WriteLine("This is a local action - handling local deletion from all sources");
                            bool localItemsDeleted = false;
                            bool standardItemsDeleted = false;

                            // Step 1: Delete from localDataItems.json
                            var localData = await _fileService.LoadLocalDataItemsAsync() ?? new List<DataItem>();

                            // Try to find the action by ID or name in localDataItems
                            var localItemsToRemove = localData.Where(x =>
                                (x.Data?.ActionGroupObject?.Id != null && x.Data?.ActionGroupObject?.Id.ToString() == actionId) ||
                                (x.Data?.ActionGroupObject?.ActionName == ActionName)).ToList();

                            if (localItemsToRemove.Any())
                            {
                                foreach (var item in localItemsToRemove)
                                {
                                    // Mark as deleted instead of just removing
                                    item.deleted = true;
                                    Debug.WriteLine($"Marked item as deleted in localDataItems.json: {item._id}");
                                }
                                await _fileService.SaveLocalDataItemsAsync(localData);
                                localItemsDeleted = true;
                                Debug.WriteLine($"Saved {localItemsToRemove.Count} deleted items to localDataItems.json");
                            }

                            // Step 2: Also check and delete from standard dataItems.json
                            var standardData = await _fileService.LoadDataItemsAsync() ?? new List<DataItem>();

                            var standardItemsToRemove = standardData.Where(x =>
                                (x.Data?.ActionGroupObject?.Id != null && x.Data?.ActionGroupObject?.Id.ToString() == actionId) ||
                                (x.Data?.ActionGroupObject?.ActionName == ActionName)).ToList();

                            if (standardItemsToRemove.Any())
                            {
                                foreach (var item in standardItemsToRemove)
                                {
                                    // Mark as deleted instead of just removing
                                    item.deleted = true;
                                    Debug.WriteLine($"Marked item as deleted in dataItems.json: {item._id}");
                                }
                                await _fileService.SaveDataItemsAsync(standardData);
                                standardItemsDeleted = true;
                                Debug.WriteLine($"Saved {standardItemsToRemove.Count} deleted items to dataItems.json");
                            }

                            // Step 3: Verify deletion by checking if the items are now marked as deleted
                            bool verifiedLocalDelete = (await _fileService.LoadLocalDataItemsAsync())
                                .Where(x => x.Data?.ActionGroupObject?.Id != null && x.Data?.ActionGroupObject?.Id.ToString() == actionId)
                                .All(x => x.deleted);

                            bool verifiedStandardDelete = (await _fileService.LoadDataItemsAsync())
                                .Where(x => x.Data?.ActionGroupObject?.Id != null && x.Data?.ActionGroupObject?.Id.ToString() == actionId)
                                .All(x => x.deleted);

                            // Success if deleted and verified from either file or both
                            deleteSuccess = (localItemsDeleted && verifiedLocalDelete) ||
                                           (standardItemsDeleted && verifiedStandardDelete);

                            // Additional debug info
                            Debug.WriteLine($"Delete results - Local: {localItemsDeleted} (verified: {verifiedLocalDelete}), " +
                                           $"Standard: {standardItemsDeleted} (verified: {verifiedStandardDelete})");
                        }
                        else
                        {
                            // Handle remote deletion (existing code)
                            // ...existing code for remote deletion...
                        }

                        if (deleteSuccess)
                        {
                            Debug.WriteLine("Delete successful, navigating back to actions list");

                            // Navigate back to the actions page
                            try
                            {
                                await Shell.Current.GoToAsync("///action");
                            }
                            catch (Exception navEx)
                            {
                                Debug.WriteLine($"Primary navigation failed: {navEx.Message}");
                                try { await Shell.Current.GoToAsync("/action"); }
                                catch { await Shell.Current.GoToAsync(".."); }
                            }
                        }
                        else
                        {
                            Debug.WriteLine("Delete verification failed");
                            await Application.Current.MainPage.DisplayAlert(
                                "Warning",
                                "The action may not have been deleted. Please check and try again if needed.",
                                "OK");
                        }
                    }
                    catch (Exception deleteEx)
                    {
                        Debug.WriteLine($"Error during action deletion: {deleteEx.Message}");
                        Debug.WriteLine($"Stack trace: {deleteEx.StackTrace}");

                        await Application.Current.MainPage.DisplayAlert(
                            "Error",
                            "An unexpected error occurred. Please try again later.",
                            "OK");
                    }
                    finally
                    {
                        IsLoading = false; // Remove loading indicator
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception in DeleteAction: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    "Could not process your request due to an application error.",
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

        private void CalculateSummary()
        {
            ClickCount = 0;
            PressCount = 0;
            MoveCount = 0;

            if (_actionGroup?.ActionArray != null)
            {
                foreach (var step in _actionGroup.ActionArray)
                {
                    if (IsMouseButtonEvent(step.EventType))
                        ClickCount++;
                    else if (step.EventType == 256 || step.EventType == 257)
                        PressCount++;
                    else if (step.EventType == 512 || step.EventType == 0x0200)
                        MoveCount++;
                }
            }

            OnPropertyChanged(nameof(ClickCount));
            OnPropertyChanged(nameof(PressCount));
            OnPropertyChanged(nameof(MoveCount));
        }

        // Add this method to handle saving changes to the action steps
        private async void SaveChanges()
        {
            try
            {
                IsLoading = true;

                // Parse the ActionStepsText into individual ActionItems
                List<ActionItem> parsedActionItems = ActionItemParser.ParseActionStepsText(ActionStepsText, _actionGroup);

                // Update the existing ActionArray with the parsed ActionItems
                UpdateActionArray(parsedActionItems);

                // Save the updated action group to the file system
                await SaveActionGroupToFile();

                // Re-initialize the steps to reflect the changes
                InitializeSteps();

                // Notify the user that the changes have been saved
                await Application.Current.MainPage.DisplayAlert("Success", "Action steps have been saved and persisted to file.", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving changes: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert("Error", "Failed to save action steps.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveActionGroupToFile()
        {
            try
            {
                // Determine if this is a local action
                bool isLocalOnly = _actionGroup?.IsLocal == true;

                // Update in localDataItems.json
                var localData = await _fileService.LoadLocalDataItemsAsync() ?? new List<DataItem>();
                bool updatedLocal = false;

                // Find and update the corresponding DataItem in localDataItems
                var itemToUpdateLocal = localData.FirstOrDefault(x =>
                    x.Data?.ActionGroupObject?.Id != null &&
                    x.Data?.ActionGroupObject?.Id.ToString() == _actionGroup.Id.ToString());

                if (itemToUpdateLocal != null)
                {
                    itemToUpdateLocal.Data.ActionGroupObject = _actionGroup;
                    await _fileService.SaveLocalDataItemsAsync(localData);
                    Debug.WriteLine("Updated action group in localDataItems.json");
                    updatedLocal = true;
                }

                // Update in standard dataItems.json
                var standardData = await _fileService.LoadDataItemsAsync() ?? new List<DataItem>();
                bool updatedStandard = false;

                // Find and update the corresponding DataItem in standard dataItems
                var itemToUpdateStandard = standardData.FirstOrDefault(x =>
                    x.Data?.ActionGroupObject?.Id != null &&
                    x.Data?.ActionGroupObject?.Id.ToString() == _actionGroup.Id.ToString());

                if (itemToUpdateStandard != null)
                {
                    itemToUpdateStandard.Data.ActionGroupObject = _actionGroup;
                    await _fileService.SaveDataItemsAsync(standardData);
                    Debug.WriteLine("Updated action group in dataItems.json");
                    updatedStandard = true;
                }

                if (!updatedLocal && isLocalOnly)
                {
                    Debug.WriteLine("Action group not found in localDataItems.json, but is marked as local. This is unexpected.");
                }

                if (!updatedStandard && !isLocalOnly)
                {
                    Debug.WriteLine("Action group not found in dataItems.json, and is not marked as local. This is unexpected.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving action group to file: {ex.Message}");
            }
        }

        private void UpdateActionArray(List<ActionItem> parsedActionItems)
        {
            try
            {
                if (_actionGroup?.ActionArray == null)
                {
                    Debug.WriteLine("ActionGroup or ActionArray is null");
                    return;
                }

                Debug.WriteLine($"Updating ActionArray with {parsedActionItems.Count} parsed items");
                Debug.WriteLine($"Original ActionArray had {_actionGroup.ActionArray.Count} items");

                // Validate that we're not accidentally losing a lot of data
                if (_actionGroup.ActionArray.Count > 50 && parsedActionItems.Count < 10)
                {
                    Debug.WriteLine("WARNING: Significant data loss detected. Aborting update to prevent data loss.");
                    Application.Current.MainPage.DisplayAlert("Warning",
                        "The changes you made could not be parsed properly and would result in data loss. Please check the format and try again.",
                        "OK");
                    return;
                }

                // Create a backup of the original array
                var originalArray = new List<ActionItem>(_actionGroup.ActionArray);

                try
                {
                    // Update with the parsed items
                    _actionGroup.ActionArray = new List<ActionItem>(parsedActionItems);

                    Debug.WriteLine($"ActionArray updated successfully with {_actionGroup.ActionArray.Count} items");

                    // Recalculate summary after updating
                    CalculateSummary();

                    // Update the step count property
                    OnPropertyChanged(nameof(StepCount));
                }
                catch (Exception updateEx)
                {
                    Debug.WriteLine($"Error during array update, restoring original: {updateEx.Message}");
                    // Restore original array on error
                    _actionGroup.ActionArray = originalArray;
                    throw;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating ActionArray: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }        // AI Model Methods
        private async Task InitializeAvailableTextModels()
        {
            try
            {
                // Load available text processing models from the huggingFaceModels.json file
                AvailableTextModels.Clear();

                // Load models from file service
                var allModels = await _fileService.LoadHuggingFaceModelsAsync();

                // Filter for text models only (InputType == 1 is Text)
                var textModels = allModels.Where(m => m.InputType == ModelInputType.Text).ToList();

                Debug.WriteLine($"Found {textModels.Count} text models out of {allModels.Count} total models");

                // Check download status for each model and prioritize downloaded ones
                var downloadedModels = new List<NeuralNetworkModel>();
                var notDownloadedModels = new List<NeuralNetworkModel>();

                foreach (var model in textModels)
                {
                    // Check if model is downloaded using NetPageViewModel's logic
                    bool isDownloaded = IsModelDownloaded(model.HuggingFaceModelId);

                    if (isDownloaded)
                    {
                        downloadedModels.Add(model);
                        Debug.WriteLine($"Added downloaded text model: {model.Name} (ID: {model.HuggingFaceModelId})");
                    }
                    else
                    {
                        notDownloadedModels.Add(model);
                        Debug.WriteLine($"Added text model: {model.Name} (ID: {model.HuggingFaceModelId}) - Not Downloaded");
                    }
                }

                // Add downloaded models first, then non-downloaded ones
                foreach (var model in downloadedModels)
                {
                    AvailableTextModels.Add(model);
                }
                foreach (var model in notDownloadedModels)
                {
                    AvailableTextModels.Add(model);
                }

                // Prefer to select a downloaded model, otherwise select the first available model
                if (downloadedModels.Any())
                {
                    SelectedAiModel = downloadedModels.First();
                    Debug.WriteLine($"Auto-selected first downloaded text model: {SelectedAiModel.Name}");
                }
                else if (AvailableTextModels.Any())
                {
                    SelectedAiModel = AvailableTextModels.First();
                    Debug.WriteLine($"No downloaded models found, auto-selected first available text model: {SelectedAiModel.Name}");
                }
                else
                {
                    Debug.WriteLine("No text models available for selection");
                }

                Debug.WriteLine($"Initialized {AvailableTextModels.Count} text models ({downloadedModels.Count} downloaded, {notDownloadedModels.Count} not downloaded)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing text models: {ex.Message}");

                // Fall back to demo models if loading from file fails
                AvailableTextModels.Add(new NeuralNetworkModel
                {
                    Id = "fallback-text-model",
                    Name = "Fallback Text Processor",
                    Description = "Basic text processing model (fallback)",
                    Type = ModelType.General,
                    InputType = ModelInputType.Text,
                    IsActive = true
                });

                if (AvailableTextModels.Any())
                {
                    SelectedAiModel = AvailableTextModels.First();
                }
            }
        }

        private async void SelectAiModel()
        {
            try
            {
                if (!AvailableTextModels.Any())
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "No Models Available",
                        "No text processing models are currently available. Please check your model configuration.",
                        "OK");
                    return;
                }

                // Create a list of model names with download status for selection
                var modelOptions = AvailableTextModels.Select(m =>
                {
                    bool isDownloaded = IsModelDownloaded(m.HuggingFaceModelId);
                    string downloadStatus = isDownloaded ? "📱 Downloaded" : "☁️ Cloud Only";
                    return $"{m.Name} - {downloadStatus}";
                }).ToArray();

                var selectedOption = await Application.Current.MainPage.DisplayActionSheet(
                    "Select AI Model",
                    "Cancel",
                    null,
                    modelOptions);

                if (!string.IsNullOrEmpty(selectedOption) && selectedOption != "Cancel")
                {
                    // Find the selected model by matching the option
                    var selectedModelIndex = Array.IndexOf(modelOptions, selectedOption);
                    if (selectedModelIndex >= 0 && selectedModelIndex < AvailableTextModels.Count)
                    {
                        SelectedAiModel = AvailableTextModels[selectedModelIndex];
                        bool isDownloaded = IsModelDownloaded(SelectedAiModel.HuggingFaceModelId);
                        Debug.WriteLine($"Selected AI model: {SelectedAiModel.Name} (Downloaded: {isDownloaded})");

                        // If the model is not downloaded, show a helpful message
                        if (!isDownloaded)
                        {
                            await Application.Current.MainPage.DisplayAlert(
                                "Cloud Model Selected",
                                $"You've selected '{SelectedAiModel.Name}' which is not downloaded locally. For better performance, consider downloading this model first from the AI Models page.",
                                "OK");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting AI model: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to select AI model: {ex.Message}",
                    "OK");
            }
        }
        private async Task ExecuteAiModel()
        {
            // Check if a model is selected
            if (SelectedAiModel == null)
            {
                await Application.Current.MainPage.DisplayAlert(
                    "No Model Selected",
                    "Please select an AI model by pressing the model selection button (🤖) before proceeding.",
                    "OK");
                return;
            }

            // Check if prompt text is provided
            if (string.IsNullOrWhiteSpace(AiPromptText))
            {
                await Application.Current.MainPage.DisplayAlert(
                    "No Prompt Provided",
                    "Please enter a prompt to tell the AI how to modify your action steps.",
                    "OK");
                return;
            }

            try
            {
                IsLoading = true;

                // Store original text for undo functionality
                _originalActionStepsText = ActionStepsText;

                // Simulate AI processing with different responses based on model
                await Task.Delay(2000); // Simulate processing time

                string processedText = await ProcessTextWithAiModel(ActionStepsText, AiPromptText, SelectedAiModel);

                if (!string.IsNullOrEmpty(processedText))
                {
                    ActionStepsText = processedText;
                    HasAiChangesToUndo = true;

                    await Application.Current.MainPage.DisplayAlert(
                        "AI Processing Complete",
                        $"Action steps have been modified using {SelectedAiModel.Name}.",
                        "OK");
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert(
                        "Processing Failed",
                        "The AI model was unable to process the text. Please try again.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing AI model: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Error",
                    $"Failed to execute AI model: {ex.Message}",
                    "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }
        private async Task<string> ProcessTextWithAiModel(string originalText, string prompt, NeuralNetworkModel model)
        {
            try
            {
                // This is a simulation of AI processing
                // In a real implementation, this would call the actual AI model service

                return await Task.Run(() =>
                {
                    var processedText = new StringBuilder();
                    var lines = originalText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                    // Process based on the actual loaded HuggingFace model
                    if (model.HuggingFaceModelId?.Contains("deepseek", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // DeepSeek R1 - Advanced reasoning model
                        if (prompt.ToLower().Contains("simplify") || prompt.ToLower().Contains("shorter"))
                        {
                            foreach (var line in lines.Take(Math.Min(lines.Length, 15)))
                            {
                                if (line.Contains("Description:"))
                                {
                                    var simplified = SimplifyDescription(line);
                                    processedText.AppendLine(simplified);
                                }
                                else
                                {
                                    processedText.AppendLine(line);
                                }
                            }
                        }
                        else if (prompt.ToLower().Contains("detail") || prompt.ToLower().Contains("explain"))
                        {
                            foreach (var line in lines.Take(Math.Min(lines.Length, 15)))
                            {
                                processedText.AppendLine(line);
                                if (line.Contains("Mouse Move"))
                                {
                                    processedText.AppendLine("  → Precise cursor positioning for UI interaction");
                                }
                                else if (line.Contains("Click"))
                                {
                                    processedText.AppendLine("  → User input action on interface element");
                                }
                                else if (line.Contains("Key"))
                                {
                                    processedText.AppendLine("  → Keyboard input for text or command entry");
                                }
                            }
                        }
                        else
                        {
                            // Generic improvement
                            foreach (var line in lines.Take(Math.Min(lines.Length, 15)))
                            {
                                if (line.Contains("Description:"))
                                {
                                    var improved = line.Replace("Mouse Move to", "Navigate to")
                                                     .Replace("Left Click at", "Select at")
                                                     .Replace("Right Click at", "Context menu at")
                                                     .Replace("Key ", "Input ");
                                    processedText.AppendLine(improved);
                                }
                                else
                                {
                                    processedText.AppendLine(line);
                                }
                            }
                        }
                    }
                    else if (model.HuggingFaceModelId?.Contains("qwen", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        // Qwen2.5 VL - Vision-Language model (good for action descriptions)
                        foreach (var line in lines.Take(Math.Min(lines.Length, 15)))
                        {
                            if (line.Contains("Description:"))
                            {
                                var enhanced = line.Replace("Mouse Move", "Cursor navigation")
                                                 .Replace("Left Click", "Primary selection")
                                                 .Replace("Right Click", "Secondary menu")
                                                 .Replace("Key ", "Keystroke ");
                                processedText.AppendLine(enhanced);
                            }
                            else
                            {
                                processedText.AppendLine(line);
                            }
                        }
                    }
                    else
                    {
                        // Generic text model processing or fallback models
                        if (prompt.ToLower().Contains("format") || prompt.ToLower().Contains("clean"))
                        {
                            foreach (var line in lines.Take(Math.Min(lines.Length, 15)))
                            {
                                var cleanedLine = line.Trim();
                                if (!string.IsNullOrEmpty(cleanedLine))
                                {
                                    processedText.AppendLine(cleanedLine);
                                }
                            }
                        }
                        else
                        {
                            // Default processing
                            processedText.AppendLine($"Processed by {model.Name}:");
                            if (!string.IsNullOrEmpty(model.HuggingFaceModelId))
                            {
                                processedText.AppendLine($"Model ID: {model.HuggingFaceModelId}");
                            }
                            processedText.AppendLine("");
                            foreach (var line in lines.Take(Math.Min(lines.Length, 10)))
                            {
                                processedText.AppendLine(line);
                            }
                        }
                    }

                    return processedText.ToString();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing text with AI model: {ex.Message}");
                return string.Empty;
            }
        }

        private string SimplifyDescription(string description)
        {
            // Simple description simplification
            return description.Replace("to X:", "→")
                             .Replace("to Y:", "→")
                             .Replace("at X:", "@")
                             .Replace("at Y:", "@")
                             .Replace("Mouse Move", "Move")
                             .Replace("Left Click", "Click")
                             .Replace("Description:", "")
                             .Trim();
        }

        private void UndoAiChanges()
        {
            try
            {
                if (!string.IsNullOrEmpty(_originalActionStepsText))
                {
                    ActionStepsText = _originalActionStepsText;
                    HasAiChangesToUndo = false;
                    _originalActionStepsText = null;

                    Debug.WriteLine("AI changes undone successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error undoing AI changes: {ex.Message}");
            }
        }

        // Helper method to check if a model is downloaded (similar to NetPageViewModel)
        private bool IsModelDownloaded(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return false;

            try
            {
                string cacheDirectory = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";

                if (!Directory.Exists(cacheDirectory))
                    return false;

                // Check for model directory by trying both naming conventions
                var possibleDirNames = new[]
                {
                    modelId.Replace("/", "_"),           // org/model -> org_model
                    $"models--{modelId.Replace("/", "--")}"  // org/model -> models--org--model
                };

                foreach (var dirName in possibleDirNames)
                {
                    var modelPath = Path.Combine(cacheDirectory, dirName);

                    if (Directory.Exists(modelPath))
                    {
                        // Calculate total directory size
                        long totalSize = GetDirectorySize(modelPath);

                        // Consider downloaded if > 5KB (5120 bytes)
                        bool isDownloaded = totalSize > 5120;

#if DEBUG
                        if (totalSize > 0)
                        {
                            Debug.WriteLine($"Model '{modelId}' directory size: {totalSize:N0} bytes ({totalSize / 1024.0:F1} KB) - Downloaded: {isDownloaded}");
                        }
#endif
                        return isDownloaded;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if model '{modelId}' is downloaded: {ex.Message}");
                return false;
            }
        }

        private long GetDirectorySize(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return 0;

                long totalSize = 0;

                // Get size of all files in directory and subdirectories
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting size of file '{file}': {ex.Message}");
                    }
                }

                return totalSize;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting directory size: {ex.Message}");
                return 0;
            }
        }

        private async Task NavigateToAiModels()
        {
            try
            {
                // Navigate to the AI Models (Net) page using Shell navigation
                await Shell.Current.GoToAsync("net");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error navigating to AI Models page: {ex.Message}");
                await Application.Current.MainPage.DisplayAlert(
                    "Navigation Error",
                    "Failed to open the AI Models page. Please try again.",
                    "OK");
            }
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
