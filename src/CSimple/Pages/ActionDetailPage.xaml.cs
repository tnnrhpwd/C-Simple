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
using Microsoft.Maui.Storage;

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
        public string KeyName { get; set; }
        public int KeyCode { get; set; }
        public DateTime Timestamp { get; set; }
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
        private readonly INavigation _navigation;
        private readonly ActionGroup _actionGroup;
        private readonly DataService _dataService; // Add DataService
        private readonly FileService _fileService; // Add FileService for local operations
        private readonly ActionService _actionService; // Add ActionService reference

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

                    // Create temporary list for processing
                    var processedSteps = new List<StepViewModel>();

                    for (int i = 0; i < _actionGroup.ActionArray.Count; i++)
                    {
                        var step = _actionGroup.ActionArray[i];
                        string description = step.ToString();
                        string keyName = "";
                        int keyCode = 0;
                        bool isMouseButton = false;
                        string mouseButtonType = "";
                        string mouseButtonAction = "";
                        DateTime timestamp = DateTime.MinValue;

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
                        if (step.Timestamp != null && DateTime.TryParse(step.Timestamp.ToString(), out timestamp))
                        {
                            // Update start and end times
                            startTime = timestamp < startTime ? timestamp : startTime;
                            endTime = timestamp > endTime ? timestamp : endTime;
                        }

                        StepViewModel stepViewModel = new StepViewModel
                        {
                            Index = (i + 1).ToString(),
                            Description = description,
                            Duration = $"{(new Random().NextDouble() * 0.3).ToString("0.00")}s",
                            KeyName = keyName,
                            KeyCode = keyCode,
                            Timestamp = timestamp,
                            IsMouseMove = step.EventType == 512 || step.EventType == 0x0200,
                            IsMouseButton = isMouseButton,
                            MouseButtonType = mouseButtonType,
                            MouseButtonAction = mouseButtonAction,
                            RawData = step
                        };

                        processedSteps.Add(stepViewModel);
                    }

                    // Group similar consecutive actions
                    ActionSteps.Clear();
                    GroupSimilarActions(processedSteps);

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
                    AddDemoSteps();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing steps: {ex.Message}");
                ActionSteps.Add(new StepViewModel { Index = "!", Description = "Error loading steps", Duration = "N/A" });
            }
        }

        private void GroupSimilarActions(List<StepViewModel> steps)
        {
            if (steps == null || steps.Count == 0)
            {
                Debug.WriteLine("No steps to group");
                return;
            }

            try
            {
                // Constants for grouping configuration
                const int MIN_GROUP_SIZE = 3; // Minimum number of similar actions to form a group
                const int MAX_MOUSE_MOVES_BEFORE_GROUPING = 4; // Show this many individual moves before grouping

                int currentIndex = 0;
                int displayIndex = 1; // For user-visible indexing (starts at 1)

                while (currentIndex < steps.Count)
                {
                    var currentStep = steps[currentIndex];

                    // Check if we can start a grouping from this step
                    bool canGroup = false;
                    string groupType = "";

                    // 1. Check for consecutive mouse movements
                    if (currentStep.IsMouseMove && currentIndex + MIN_GROUP_SIZE <= steps.Count)
                    {
                        int mouseMoveCount = 1;
                        for (int i = currentIndex + 1; i < steps.Count; i++)
                        {
                            if (steps[i].IsMouseMove)
                                mouseMoveCount++;
                            else
                                break;
                        }

                        if (mouseMoveCount >= MIN_GROUP_SIZE)
                        {
                            canGroup = true;
                            groupType = "MouseMove";
                        }
                    }
                    // Grouping for Mouse Clicks
                    else if (currentStep.IsMouseButton && currentIndex + MIN_GROUP_SIZE <= steps.Count)
                    {
                        int mouseClickCount = 1;
                        for (int i = currentIndex + 1; i < steps.Count; i++)
                        {
                            if (steps[i].IsMouseButton &&
                                steps[i].MouseButtonType == currentStep.MouseButtonType &&
                                steps[i].MouseButtonAction == currentStep.MouseButtonAction)
                                mouseClickCount++;
                            else
                                break;
                        }

                        if (mouseClickCount >= MIN_GROUP_SIZE)
                        {
                            canGroup = true;
                            groupType = "MouseClick";
                        }
                    }

                    // 2. Check for consecutive key presses of the same key
                    else if (!string.IsNullOrEmpty(currentStep.KeyName) && currentIndex + MIN_GROUP_SIZE <= steps.Count)
                    {
                        int sameKeyCount = 1;
                        string keyName = currentStep.KeyName;

                        for (int i = currentIndex + 1; i < steps.Count; i++)
                        {
                            if (!string.IsNullOrEmpty(steps[i].KeyName) && steps[i].KeyName == keyName)
                                sameKeyCount++;
                            else
                                break;
                        }

                        if (sameKeyCount >= MIN_GROUP_SIZE)
                        {
                            canGroup = true;
                            groupType = "KeyPress";
                        }
                    }

                    // 3. Process the grouping or individual step
                    if (canGroup)
                    {
                        if (groupType == "MouseMove")
                        {
                            // Count consecutive mouse moves
                            int mouseMoveCount = 0;
                            for (int i = currentIndex; i < steps.Count; i++)
                            {
                                if (steps[i].IsMouseMove)
                                    mouseMoveCount++;
                                else
                                    break;
                            }

                            // Add individual mouse moves at the beginning for context
                            int individualMovesToShow = Math.Min(MAX_MOUSE_MOVES_BEFORE_GROUPING, mouseMoveCount / 2);
                            for (int j = 0; j < individualMovesToShow && currentIndex + j < steps.Count; j++)
                            {
                                var step = steps[currentIndex + j];
                                step.Index = displayIndex.ToString();
                                ActionSteps.Add(step);
                                displayIndex++;
                            }

                            // Skip if all moves are shown individually
                            if (mouseMoveCount <= individualMovesToShow)
                            {
                                currentIndex += mouseMoveCount;
                                continue;
                            }

                            // Create group for remaining moves
                            var groupedMoves = new List<ActionItem>();
                            DateTime firstTimestamp = DateTime.MaxValue;
                            DateTime lastTimestamp = DateTime.MinValue;

                            for (int j = individualMovesToShow; j < mouseMoveCount && currentIndex + j < steps.Count; j++)
                            {
                                if (steps[currentIndex + j].RawData != null)
                                {
                                    groupedMoves.Add(steps[currentIndex + j].RawData);

                                    if (steps[currentIndex + j].Timestamp < firstTimestamp)
                                        firstTimestamp = steps[currentIndex + j].Timestamp;
                                    if (steps[currentIndex + j].Timestamp > lastTimestamp)
                                        lastTimestamp = steps[currentIndex + j].Timestamp;
                                }
                            }

                            if (groupedMoves.Any())
                            {
                                var firstPoint = groupedMoves.First().Coordinates;
                                var lastPoint = groupedMoves.Last().Coordinates;

                                // Fix: Add null checks for coordinates
                                int firstX = firstPoint?.X ?? 0;
                                int firstY = firstPoint?.Y ?? 0;
                                int lastX = lastPoint?.X ?? 0;
                                int lastY = lastPoint?.Y ?? 0;

                                // Create a grouped step with null-safe coordinate handling
                                ActionSteps.Add(new StepViewModel
                                {
                                    Index = displayIndex.ToString(),
                                    Description = $"Mouse Movement Path ({groupedMoves.Count} steps)",
                                    IsGrouped = true,
                                    GroupCount = groupedMoves.Count,
                                    GroupType = "Mouse Movements",
                                    Duration = (lastTimestamp - firstTimestamp).TotalSeconds.ToString("0.00") + "s",
                                    GroupDuration = lastTimestamp - firstTimestamp,
                                    IsMouseMove = true,
                                    GroupedItems = groupedMoves,
                                    Timestamp = firstTimestamp,
                                    RawData = new ActionItem
                                    {
                                        EventType = 512, // Mouse move
                                        Coordinates = new Coordinates { X = firstX, Y = firstY },
                                        DeltaX = lastX - firstX,
                                        DeltaY = lastY - firstY
                                    }
                                });
                                displayIndex++;
                            }

                            // Add the last few individual moves for context
                            int lastMovesToShow = Math.Min(2, mouseMoveCount - individualMovesToShow);
                            for (int j = 0; j < lastMovesToShow; j++)
                            {
                                int index = currentIndex + mouseMoveCount - lastMovesToShow + j;
                                if (index < steps.Count)
                                {
                                    var step = steps[index];
                                    step.Index = displayIndex.ToString();
                                    ActionSteps.Add(step);
                                    displayIndex++;
                                }
                            }

                            currentIndex += mouseMoveCount;
                        }
                        else if (groupType == "MouseClick")
                        {
                            // Count consecutive mouse clicks
                            int mouseClickCount = 0;
                            for (int i = currentIndex; i < steps.Count; i++)
                            {
                                if (steps[i].IsMouseButton &&
                                    steps[i].MouseButtonType == currentStep.MouseButtonType &&
                                    steps[i].MouseButtonAction == currentStep.MouseButtonAction)
                                    mouseClickCount++;
                                else
                                    break;
                            }

                            // Add first key event individually
                            ActionSteps.Add(steps[currentIndex]);
                            steps[currentIndex].Index = displayIndex.ToString();
                            displayIndex++;

                            // Group the middle key events if there are enough
                            if (mouseClickCount > 3)
                            {
                                var groupedItems = new List<ActionItem>();
                                DateTime firstTimestamp = steps[currentIndex + 1].Timestamp;
                                DateTime lastTimestamp = steps[currentIndex + mouseClickCount - 2 >= currentIndex + 1
                                    ? mouseClickCount - 2 : 1].Timestamp;

                                for (int j = 1; j < mouseClickCount - 1 && currentIndex + j < steps.Count; j++)
                                {
                                    if (steps[currentIndex + j].RawData != null)
                                        groupedItems.Add(steps[currentIndex + j].RawData);
                                }

                                if (groupedItems.Any())
                                {
                                    ActionSteps.Add(new StepViewModel
                                    {
                                        Index = displayIndex.ToString(),
                                        Description = $"Repeated {currentStep.MouseButtonType} Click {currentStep.MouseButtonAction} ({groupedItems.Count} times)",
                                        IsGrouped = true,
                                        GroupCount = groupedItems.Count,
                                        GroupType = "Mouse Click Repetition",
                                        MouseButtonType = currentStep.MouseButtonType,
                                        MouseButtonAction = currentStep.MouseButtonAction,
                                        Duration = (lastTimestamp - firstTimestamp).TotalSeconds.ToString("0.00") + "s",
                                        GroupDuration = lastTimestamp - firstTimestamp,
                                        GroupedItems = groupedItems,
                                        Timestamp = firstTimestamp
                                    });
                                    displayIndex++;
                                }
                            }

                            // Add the last key event if there are at least 2 events
                            if (currentIndex + mouseClickCount - 1 >= currentIndex + 1 && currentIndex + mouseClickCount - 1 < steps.Count)
                            {
                                steps[currentIndex + mouseClickCount - 1].Index = displayIndex.ToString();
                                ActionSteps.Add(steps[currentIndex + mouseClickCount - 1]);
                                displayIndex++;
                            }

                            currentIndex += mouseClickCount;
                        }
                        else if (groupType == "KeyPress")
                        {
                            string keyName = currentStep.KeyName;
                            int sameKeyCount = 0;
                            for (int i = currentIndex; i < steps.Count; i++)
                            {
                                if (!string.IsNullOrEmpty(steps[i].KeyName) && steps[i].KeyName == keyName)
                                    sameKeyCount++;
                                else
                                    break;
                            }

                            // Add first key event individually
                            ActionSteps.Add(steps[currentIndex]);
                            steps[currentIndex].Index = displayIndex.ToString();
                            displayIndex++;

                            // Group the middle key events if there are enough
                            if (sameKeyCount > 3)
                            {
                                var groupedItems = new List<ActionItem>();
                                DateTime firstTimestamp = steps[currentIndex + 1].Timestamp;
                                DateTime lastTimestamp = steps[currentIndex + sameKeyCount - 2 >= currentIndex + 1
                                    ? sameKeyCount - 2 : 1].Timestamp;

                                for (int j = 1; j < sameKeyCount - 1 && currentIndex + j < steps.Count; j++)
                                {
                                    if (steps[currentIndex + j].RawData != null)
                                        groupedItems.Add(steps[currentIndex + j].RawData);
                                }

                                if (groupedItems.Any())
                                {
                                    ActionSteps.Add(new StepViewModel
                                    {
                                        Index = displayIndex.ToString(),
                                        Description = $"Repeated Key {keyName} ({groupedItems.Count} times)",
                                        IsGrouped = true,
                                        GroupCount = groupedItems.Count,
                                        GroupType = "Key Repetition",
                                        KeyName = keyName,
                                        KeyCode = currentStep.KeyCode,
                                        Duration = (lastTimestamp - firstTimestamp).TotalSeconds.ToString("0.00") + "s",
                                        GroupDuration = lastTimestamp - firstTimestamp,
                                        GroupedItems = groupedItems,
                                        Timestamp = firstTimestamp
                                    });
                                    displayIndex++;
                                }
                            }

                            // Add the last key event if there are at least 2 events
                            if (currentIndex + sameKeyCount - 1 >= currentIndex + 1 && currentIndex + sameKeyCount - 1 < steps.Count)
                            {
                                steps[currentIndex + sameKeyCount - 1].Index = displayIndex.ToString();
                                ActionSteps.Add(steps[currentIndex + sameKeyCount - 1]);
                                displayIndex++;
                            }

                            currentIndex += sameKeyCount;
                        }
                    }
                    else
                    {
                        // Add individual step
                        currentStep.Index = displayIndex.ToString();
                        ActionSteps.Add(currentStep);
                        displayIndex++;
                        currentIndex++;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in GroupSimilarActions: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                // If grouping fails, fall back to adding all steps individually
                for (int i = 0; i < steps.Count; i++)
                {
                    steps[i].Index = (i + 1).ToString();
                    ActionSteps.Add(steps[i]);
                }
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
                Timestamp = DateTime.Now,
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
                Timestamp = DateTime.Now.AddSeconds(0.2)
            });

            ActionSteps.Add(new StepViewModel
            {
                Index = "3",
                Description = "Left Click",
                Duration = "0.05s",
                Timestamp = DateTime.Now.AddSeconds(1.05),
                IsMouseButton = true,
                MouseButtonType = "Left",
                MouseButtonAction = "Down"
            });

            ActionSteps.Add(new StepViewModel
            {
                Index = "4",
                Description = "Left Click",
                Duration = "0.05s",
                Timestamp = DateTime.Now.AddSeconds(1.1),
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
                Timestamp = DateTime.Now.AddSeconds(1.2)
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
