using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;

namespace CSimple.Pages
{
    public partial class ActionPage : ContentPage
    {
        public ObservableCollection<ActionGroup> ActionGroups { get; set; }
        public ICommand NavigateToObservePageCommand { get; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand ToggleSimulateActionGroupCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }
        public ICommand RowTappedCommand { get; }
        public ICommand DeleteActionGroupCommand { get; set; }
        private bool _isSimulating = false;
        private readonly DataService _dataService;
        private readonly FileService _fileService;

        public bool IsSimulating
        {
            get => _isSimulating;
            set
            {
                _isSimulating = value;
                OnPropertyChanged(nameof(IsSimulating));
            }
        }
        private void DebugOutput(string message)
        {
            Debug.WriteLine(message);
        }

        public ActionPage()
        {
            InitializeComponent();

            _fileService = new FileService();
            _dataService = new DataService();

            // Initialize commands
            ToggleSimulateActionGroupCommand = new Command<ActionGroup>(async (actionGroup) => await ToggleSimulateActionGroupAsync(actionGroup));
            SaveToFileCommand = new Command(async () => await SaveActionGroupsToFile());
            LoadFromFileCommand = new Command(async () => await LoadActionGroupsFromFile());
            NavigateToObservePageCommand = new Command(async () => await NavigateToObservePage());
            DeleteActionGroupCommand = new Command<ActionGroup>(async (actionGroup) => await DeleteActionGroupAsync(actionGroup));

            RowTappedCommand = new Command<ActionGroup>(OnRowTapped);
            // Initialize ActionGroups collection
            ActionGroups = new ObservableCollection<ActionGroup>();

            // Load existing action groups from file asynchronously
            _ = LoadActionGroupsFromFile(); // Ignore the returned task since we only need to ensure it's running
            DebugOutput("Action Page Initialized");
            BindingContext = this;
        }

        private async Task DeleteActionGroupAsync(ActionGroup actionGroup)
        {
            if (actionGroup == null)
                return;

            try
            {
                bool confirmDelete = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the action group '{actionGroup.ActionName}'?", "Yes", "No");
                if (confirmDelete)
                {
                    // Remove from the collection
                    ActionGroups.Remove(actionGroup);

                    // Save the updated collection to file
                    await SaveActionGroupsToFile();

                    // Delete from backend
                    var token = await SecureStorage.GetAsync("userToken");
                    if (!string.IsNullOrEmpty(token))
                    {
                        var response = await _dataService.DeleteDataAsync(actionGroup.Id.ToString(), token);
                        if (response.DataIsSuccess)
                        {
                            DebugOutput($"Action Group {actionGroup.ActionName} deleted from backend.");
                        }
                        else
                        {
                            DebugOutput($"Failed to delete Action Group {actionGroup.ActionName} from backend.");
                        }
                    }

                    DebugOutput($"Action Group {actionGroup.ActionName} deleted.");
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error deleting action group: {ex.Message}");
            }
        }

        private bool cancel_simulation = false;
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await LoadActionGroupsFromBackend();
        }
        private async Task NavigateToObservePage()
        {
            await Shell.Current.GoToAsync($"///observe");
        }
        private void OnInputActionClicked(object sender, EventArgs e)
        {
            InputActionPopup.IsVisible = true;
        }

        private void OnOkayClick(object sender, EventArgs e)
        {
            InputActionPopup.IsVisible = false;
        }
        private async void OnRowTapped(ActionGroup actionGroup)
        {
            var actionDetailPage = new ActionDetailPage(actionGroup);
            await Navigation.PushModalAsync(actionDetailPage);
        }

        // private async void SaveAction(object parameter)
        // {
        //     string actionName = ActionNameEntry.Text?.Trim();
        //     string actionArrayText = ActionArrayEntry.Text?.Trim();

        //     if (!string.IsNullOrEmpty(actionName) && !string.IsNullOrEmpty(actionArrayText))
        //     {
        //         var actions = actionArrayText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
        //         .Select(a => new ActionArrayItem
        //         {
        //             KeyCode = 54, 
        //             Timestamp = DateTime.UtcNow.ToString("o"), 
        //         })
        //         .ToList();

        //         var newActionGroup = new ActionGroup { ActionName = actionName, ActionArray = actions };
        //         ActionGroups.Add(newActionGroup);
        //         DebugOutput($"Saved Action Group: {actionName}");

        //         // Save to backend
        //         var token = await SecureStorage.GetAsync("userToken");
        //         if (string.IsNullOrEmpty(token))
        //         {
        //             DebugOutput("User is not logged in.");
        //             return;
        //         }

        //         var userId = await SecureStorage.GetAsync("userId");
        //         if (string.IsNullOrEmpty(userId))
        //         {
        //             DebugOutput("User ID not found.");
        //             return;
        //         }

        //         var actionArrayString = string.Join("+", actions.Select(a => $"{a.KeyCode}:{a.Timestamp}"));
        //         var queryParams = new Dictionary<string, string>
        //         {
        //             { "data", $"Creator:{userId}|Action:{actionName}+{actionArrayString}" }
        //         };
        //         var response = await _dataService.CreateDataAsync(queryParams["data"], token);
        //         if (response.DataIsSuccess == true)
        //         {
        //             DebugOutput("Action Group saved to backend");
        //         }
        //         else
        //         {
        //             DebugOutput("Failed to save Action Group to backend");
        //         }

        //         // Trigger save to file after adding new action group
        //         await SaveActionGroupsToFile();
        //     }
        //     else
        //     {
        //         DebugOutput("Please enter both Action Name and Action Array.");
        //     }
        // }
        private async Task ToggleSimulateActionGroupAsync(ActionGroup actionGroup)
        {
            DebugOutput($"Toggling Simulation for: {actionGroup.ActionName}");
            if (actionGroup != null)
            {
                actionGroup.IsSimulating = !actionGroup.IsSimulating;
                OnPropertyChanged(nameof(ActionGroups));

                if (actionGroup.IsSimulating)
                {
                    IsSimulating = true;
                    try
                    {
                        cancel_simulation = false;
                        DateTime? previousActionTime = null;
                        List<Task> actionTasks = new List<Task>();

                        // Schedule all actions first
                        foreach (var action in actionGroup.ActionArray)
                        {
                            if (cancel_simulation)
                            {
                                DebugOutput($"Successfully cancelled action");
                                break;
                            }
                            DebugOutput($"Scheduling Action: {action.Timestamp}");

                            DateTime currentActionTime;
                            if (!DateTime.TryParse(action.Timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out currentActionTime))
                            {
                                DebugOutput($"Failed to parse Timestamp: {action.Timestamp}");
                                continue;
                            }

                            // Delay based on the difference between current and previous action times
                            if (previousActionTime.HasValue)
                            {
                                TimeSpan delay = currentActionTime - previousActionTime.Value;
                                DebugOutput($"Scheduling delay for {delay.TotalMilliseconds} ms before next action.");
                                await Task.Delay(delay);
                            }

                            previousActionTime = currentActionTime;

                            // Schedule the action
                            Task actionTask = Task.Run(async () =>
                            {
                                if (cancel_simulation)
                                {
                                    DebugOutput($"Successfully cancelled action");
                                    return;
                                }

                                switch (action.EventType)
                                {
                                    case 512: // Mouse Move
                                        if (action.Coordinates != null)
                                        {
                                            int x = action.Coordinates.X;
                                            int y = action.Coordinates.Y;

                                            DebugOutput($"Simulating Mouse Move at {action.Timestamp} to X: {x}, Y: {y}");
                                            InputSimulator.MoveMouse(x, y);
                                        }
                                        else
                                        {
                                            DebugOutput($"Mouse move action at {action.Timestamp} missing coordinates.");
                                        }
                                        break;

                                    case 256: // Key Press
                                        DebugOutput($"Simulating KeyPress at {action.Timestamp} with KeyCode: {action.KeyCode}");

                                        int keyCodeInt = (int)action.KeyCode;
                                        if (Enum.IsDefined(typeof(VirtualKey), keyCodeInt))
                                        {
                                            VirtualKey virtualKey = (VirtualKey)keyCodeInt;

                                            InputSimulator.SimulateKeyDown(virtualKey);

                                            if (action.Duration > 0)
                                            {
                                                await Task.Delay(action.Duration);
                                                InputSimulator.SimulateKeyUp(virtualKey);
                                            }
                                        }
                                        else
                                        {
                                            DebugOutput($"Invalid KeyPress key code: {action.KeyCode}");
                                        }
                                        break;

                                    case (int)WM_LBUTTONDOWN: // Left Mouse Button Down
                                    case (int)WM_RBUTTONDOWN: // Right Mouse Button Down
                                        DebugOutput($"Simulating Mouse Click at {action.Timestamp} with EventType: {action.EventType}");
                                        if (action.EventType == (int)WM_LBUTTONDOWN)
                                        {
                                            InputSimulator.SimulateMouseClick(CSimple.Services.MouseButton.Left, action.Coordinates.X, action.Coordinates.Y);
                                        }
                                        else if (action.EventType == (int)WM_RBUTTONDOWN)
                                        {
                                            InputSimulator.SimulateMouseClick(CSimple.Services.MouseButton.Right, action.Coordinates.X, action.Coordinates.Y);
                                        }
                                        break;

                                    case 257: // Key Release
                                        DebugOutput($"Simulating KeyRelease at {action.Timestamp} with KeyCode: {action.KeyCode}");

                                        int releaseKeyCodeInt = (int)action.KeyCode;
                                        if (Enum.IsDefined(typeof(VirtualKey), releaseKeyCodeInt))
                                        {
                                            VirtualKey virtualKey = (VirtualKey)releaseKeyCodeInt;

                                            InputSimulator.SimulateKeyUp(virtualKey);
                                        }
                                        else
                                        {
                                            DebugOutput($"Invalid KeyRelease key code: {action.KeyCode}");
                                        }
                                        break;

                                    default:
                                        DebugOutput($"Unhandled action type: {action.EventType} at {action.Timestamp}");
                                        break;
                                }
                            });

                            actionTasks.Add(actionTask);
                        }

                        // Execute all scheduled actions
                        await Task.WhenAll(actionTasks);

                        DebugOutput($"Completed Simulation for: {actionGroup.ActionName}");
                    }
                    catch (Exception ex)
                    {
                        DebugOutput($"Error during simulation: {ex.Message}");
                    }
                    finally
                    {
                        actionGroup.IsSimulating = false;
                        IsSimulating = false; // Set IsSimulating to false when simulation ends
                        OnPropertyChanged(nameof(ActionGroups));
                    }
                }
                else
                {
                    cancel_simulation = true;
                }

                // Reload ActionGroups after toggling simulation
                await LoadActionGroupsFromBackend();
            }
        }

        private async Task SaveActionGroupsToFile()
        {
            try
            {
                DebugOutput("Length of ActionGroups:" + JsonSerializer.Serialize(ActionGroups).Length.ToString());
                await _fileService.SaveActionGroupsAsync(ActionGroups);
                DebugOutput("Action Groups and Actions Saved to File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error saving action groups and actions: {ex.Message}");
            }
        }
        private ObservableCollection<ActionGroup> FormatActionsFromBackend(IEnumerable<string> actionGroupStrings)
        {
            var formattedActionGroups = new ObservableCollection<ActionGroup>();

            foreach (var actionGroupString in actionGroupStrings)
            {
                try
                {
                    var parts = actionGroupString.Split('|');
                    var creatorPart = parts.FirstOrDefault(p => p.StartsWith("Creator:"));
                    var actionPart = parts.FirstOrDefault(p => p.StartsWith("Action:"));

                    if (creatorPart != null && actionPart != null)
                    {
                        var actionJson = actionPart.Substring("Action:".Length);
                        var actionGroup = JsonSerializer.Deserialize<ActionGroup>(actionJson);

                        if (actionGroup != null)
                        {
                            actionGroup.Creator = creatorPart.Substring("Creator:".Length);
                            actionGroup.ActionArray = actionGroup.ActionArray ?? new List<ActionArrayItem>();
                            actionGroup.ActionArrayFormatted = actionGroupString.Length > 50 ? actionGroupString.Substring(0, 50) + "..." : actionGroupString;

                            // Initialize Id if not already set
                            if (actionGroup.Id == Guid.Empty)
                            {
                                actionGroup.Id = Guid.NewGuid();
                            }

                            formattedActionGroups.Add(actionGroup);
                            DebugOutput($"Adding action: {actionGroup.ActionName}");
                        }
                        else
                        {
                            DebugOutput($"Failed to deserialize action group JSON: {actionJson}");
                        }
                    }
                    else
                    {
                        DebugOutput($"Invalid action group string: {actionGroupString}");
                    }
                }
                catch (JsonException jsonEx)
                {
                    DebugOutput($"JSON error parsing action group: {jsonEx.Message}");
                }
                catch (Exception ex)
                {
                    DebugOutput($"Error parsing action group: {ex.Message}");
                }
            }
            // Log raw response data
            Debug.WriteLine("Length of formattedActionGroups:" + JsonSerializer.Serialize(formattedActionGroups).Length.ToString());
            DebugOutput($"2. (ActionPage.FormatActionsFromBackend) Raw response data: {JsonSerializer.Serialize(formattedActionGroups)}");
            return formattedActionGroups;
        }

        private async Task LoadActionGroupsFromBackend()
        {
            try
            {
                DebugOutput("Starting Action Groups Load Task");
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    DebugOutput("User is not logged in.");
                    return;
                }

                var data = "Action";
                var actionGroups = await _dataService.GetDataAsync(data, token);
                DebugOutput($"Received action groups from backend");

                // Log raw response data
                Debug.WriteLine("Length of actionGroups.Data:" + JsonSerializer.Serialize(actionGroups.Data).Length.ToString());
                DebugOutput($"2. (ActionPage.LoadActionGroupsFromBackend) Raw response data: {JsonSerializer.Serialize(actionGroups.Data)}");

                var formattedActionGroups = FormatActionsFromBackend(actionGroups.Data);
                DebugOutput($"Received {actionGroups.Data.Count} action groups from backend");

                if (!ActionGroups.SequenceEqual(formattedActionGroups))
                {
                    ActionGroups.Clear();
                    foreach (var actionGroup in formattedActionGroups)
                    {
                        ActionGroups.Add(actionGroup);
                    }
                    DebugOutput("Action Groups Loaded from Backend");

                    // Save loaded action groups to file
                    await SaveActionGroupsToFile();
                }
                else
                {
                    DebugOutput("No changes in Action Groups from Backend");
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups from backend: {ex.Message}");
            }
        }

        private async Task LoadActionGroupsFromFile()
        {
            try
            {
                var loadedActionGroups = await _fileService.LoadActionGroupsAsync();
                var formattedActionGroups = new ObservableCollection<ActionGroup>(loadedActionGroups.Cast<ActionGroup>());

                if (!ActionGroups.SequenceEqual(formattedActionGroups))
                {
                    ActionGroups.Clear();
                    foreach (var actionGroup in formattedActionGroups)
                    {
                        ActionGroups.Add(actionGroup);
                    }
                    DebugOutput("Action Groups Loaded from File");
                }
                else
                {
                    DebugOutput("No changes in Action Groups from File");
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups from file: {ex.Message}");
            }
        }

        // P/Invoke for volume commands
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        // P/Invoke for mouse commands
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const byte VK_VOLUME_MUTE = 0xAD;
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;
        private const uint MOUSEEVENTF_MOVE = 0x0001;
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
        private void ExecuteWindowsCommand(string command)
        {
            switch (command.ToLower())
            {
                case "mute":
                    SendVolumeCommand(VK_VOLUME_MUTE);
                    break;
                case "volumeup":
                    SendVolumeCommand(VK_VOLUME_UP);
                    break;
                case "volumedown":
                    SendVolumeCommand(VK_VOLUME_DOWN);
                    break;
                default:
                    DebugOutput($"Unknown Command: {command}");
                    break;
            }
        }

        private const uint KEYEVENTF_KEYUP = 0x0002;

        private void SimulateKeyPress(VirtualKey key)
        {
            // Simulate key down
            keybd_event((byte)key, 0, 0, UIntPtr.Zero);
            // Simulate key up
            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            DebugOutput($"Executed KeyPress: {key}");
        }

        private void SendVolumeCommand(byte volumeCommand)
        {
            keybd_event(volumeCommand, 0, 0, UIntPtr.Zero);
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero); // Key up
            DebugOutput($"Executed Volume Command: {(volumeCommand == VK_VOLUME_MUTE ? "Mute" : volumeCommand == VK_VOLUME_DOWN ? "Volume Down" : "Volume Up")}");
        }

        private void MoveMouse(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_MOVE, (uint)x, (uint)y, 0, UIntPtr.Zero);
            DebugOutput($"Moved Mouse to Position: {x},{y}");
        }
    }
}