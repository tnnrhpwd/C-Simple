using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using System.ComponentModel;

namespace CSimple.Pages
{
    public partial class ActionPage : ContentPage
    {
        public ObservableCollection<DataItem> Data { get; set; } = new ObservableCollection<DataItem>();
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

            // Set the BindingContext to the current instance of ActionPage
            BindingContext = this;

            // Initialize fields
            SortPicker = this.FindByName<Picker>("SortPicker");
            InputActionPopup = this.FindByName<ContentView>("InputActionPopup");

            _fileService = new FileService();
            _dataService = new DataService();

            // Initialize commands
            ToggleSimulateActionGroupCommand = new Command<ActionGroup>(async (actionGroup) => await ToggleSimulateActionGroupAsync(actionGroup));
            SaveToFileCommand = new Command(async () => await SaveDataItemsToFile());
            LoadFromFileCommand = new Command(async () => await LoadDataItemsFromFile());
            NavigateToObservePageCommand = new Command(async () => await NavigateToObservePage());
            DeleteActionGroupCommand = new Command<DataItem>(async (dataItem) => await DeleteDataItemAsync(dataItem));

            RowTappedCommand = new Command<ActionGroup>(OnRowTapped);

            // Load existing action groups from file asynchronously
            _ = LoadDataItemsFromFile(); // Ignore the returned task since we only need to ensure it's running
            DebugOutput("Action Page Initialized");
        }

        private async Task DeleteDataItemAsync(DataItem dataItem)
        {
            if (dataItem == null)
                return;

            try
            {
                bool confirmDelete = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the action group '{dataItem.ToString}'?", "Yes", "No");
                if (confirmDelete)
                {
                    // Save the updated collection to file
                    await SaveDataItemsToFile();

                    // Delete from backend
                    var token = await SecureStorage.GetAsync("userToken");
                    if (!string.IsNullOrEmpty(token))
                    {
                        var response = await _dataService.DeleteDataAsync(dataItem._id, token);
                        if (response.DataIsSuccess)
                        {
                            DebugOutput($"Action Group {dataItem.ToString} deleted from backend.");
                        }
                        else
                        {
                            DebugOutput($"Failed to delete Action Group {dataItem.ToString} from backend.");
                        }
                    }

                    DebugOutput($"Action Group {dataItem.ToString} deleted.");
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
            SortPicker.SelectedIndex = 1; // default to CreatedAt Descending
            await LoadDataItemsFromBackend();
            OnSortOrderChanged(SortPicker, EventArgs.Empty); // Ensure data is sorted after loading
        }
        private async Task NavigateToObservePage()
        {
            DebugOutput("Navigating to ObservePage");
            await Shell.Current.GoToAsync("///observe");
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

        private async Task ToggleSimulateActionGroupAsync(ActionGroup actionGroup)
        {
            DebugOutput($"Toggling Simulation for: {actionGroup.ActionName}");
            if (actionGroup != null)
            {
                actionGroup.IsSimulating = !actionGroup.IsSimulating;
                // ((DataModel)BindingContext).OnPropertyChanged(nameof(DataModel.Data.Data.ActionGroups));

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
                            if (!DateTime.TryParse(action.Timestamp.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out currentActionTime))
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
                                            int x = action.Coordinates?.X ?? 0;
                                            int y = action.Coordinates?.Y ?? 0;

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
                        // ((DataModel)BindingContext).OnPropertyChanged(nameof(DataModel.ActionGroups));
                    }
                }
                else
                {
                    cancel_simulation = true;
                }

                // Reload ActionGroups after toggling simulation
                await LoadDataItemsFromBackend();
            }
        }

        private async Task SaveDataItemsToFile()
        {
            try
            {
                DebugOutput("3. Actionpage.SaveDataItemsToFile Length of DataModel:" + JsonSerializer.Serialize(Data).Length.ToString());
                DebugOutput("Type of Data:" + Data.GetType().ToString());
                
                // Convert ObservableCollection to List
                List<DataItem> dataList = new List<DataItem>(Data);
                
                await _fileService.SaveDataItemsAsync(dataList);
                DebugOutput("Action Groups and Actions Saved to File");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error saving data items: {ex.Message}");
            }
        }

        private async Task LoadDataItemsFromFile()
        {
            try
            {
                var loadedDataItems = await _fileService.LoadDataItemsAsync();
                if (loadedDataItems != null && loadedDataItems.Count > 0)
                {
                    Data.Clear();
                    foreach (var dataItem in loadedDataItems)
                    {
                        ParseDataItemText(dataItem);
                        Data.Add(dataItem);
                    }
                    DebugOutput($"Data Items Loaded from SecureStorage. Data length: {Data.Count}");
                }
                else
                {
                    DebugOutput("No data items found in SecureStorage.");
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading data items from SecureStorage: {ex.Message}");
            }
        }

        private async Task LoadDataItemsFromBackend()
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
                var dataItems = await _dataService.GetDataAsync(data, token);
                DebugOutput($"Received data items from backend");

                DebugOutput("Length of dataItems.Data:" + JsonSerializer.Serialize(dataItems.Data).Length.ToString());
                // DebugOutput($"2. (ActionPage.LoadActionGroupsFromBackend) Raw response data: {JsonSerializer.Serialize(dataItems.Data)}");

                var formattedDataItems = dataItems.Data ?? new List<DataItem>();
                DebugOutput($"Received {formattedDataItems.Count} DataItems from backend");

                Data.Clear();

                foreach (var dataItem in formattedDataItems)
                {
                    // DebugOutput($"2. (ActionPage.LoadActionGroupsFromBackend.BindingContext) dataItem: {JsonSerializer.Serialize(dataItem)}");
                    if (dataItem != null)
                    {
                        ParseDataItemText(dataItem);
                        DebugOutput($"2. (ActionPage.LoadActionGroupsFromBackend.dataitem) Parsing complete. starting add to Data");
                        Data.Add(dataItem);
                        DebugOutput($"2. (ActionPage.LoadActionGroupsFromBackend.dataitem) Data.Add complete");
                    }
                }
                DebugOutput("Data Items Loaded from Backend");

                // Save loaded action groups to file
                await SaveDataItemsToFile();
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading action groups from backend: {ex.Message}");
            }
        }

        private static void ParseDataItemText(DataItem dataItem)
        {
            if (string.IsNullOrEmpty(dataItem?.Data?.text)) return;

            var parts = dataItem.Data.text.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var creatorPart = parts.FirstOrDefault(p => p.StartsWith("Creator:"));
            var actionPart = parts.FirstOrDefault(p => p.StartsWith("Action:"));
            var publicPart = parts.FirstOrDefault(p => p.StartsWith("IsPublic:"));

            dataItem.Creator = (creatorPart != null) ? creatorPart.Substring("Creator:".Length).Trim() : "";
            if (dataItem.Data.ActionGroupObject != null)
            {
                var actionGroup = dataItem.Data.ActionGroupObject;
                actionGroup.ActionName = (actionPart != null && actionPart.Contains("\"ActionName\":\""))
                    ? ExtractStringBetween(actionPart, "\"ActionName\":\"", "\",")
                    : (actionPart != null
                        ? (actionPart.Length > 50
                            ? actionPart.Substring(0, 50) + "..."
                            : actionPart.Substring("Action:".Length).Trim())
                        : "");
            }
            if (publicPart != null)
            {
                try
                {
                    dataItem.IsPublic = publicPart.Substring("IsPublic:".Length).Trim().ToLower() == "true";
                }
                catch (ArgumentOutOfRangeException)
                {
                    dataItem.IsPublic = false;
                }
            }
            else
            {
                dataItem.IsPublic = false;
            }
            Debug.Write($"Parsed dataItem.Creator: {dataItem.Creator}");
            Debug.Write($"Parsed dataItem.ActionName: {dataItem.Data.ActionGroupObject.ActionName}"); 
            Debug.Write($"Parsed dataItem.IsPublic: {dataItem.IsPublic}");
        }

        private static string ExtractStringBetween(string source, string start, string end)
        {
            int startIndex = source.IndexOf(start);
            if (startIndex < 0) return "";
            startIndex += start.Length;
            int endIndex = source.IndexOf(end, startIndex);
            if (endIndex < 0) return "";
            return source.Substring(startIndex, endIndex - startIndex);
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

        private void SendVolumeCommand(byte volumeCommand)
        {
            keybd_event(volumeCommand, 0, 0, UIntPtr.Zero);
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero); // Key up
            DebugOutput($"Executed Volume Command: {(volumeCommand == VK_VOLUME_MUTE ? "Mute" : volumeCommand == VK_VOLUME_DOWN ? "Volume Down" : "Volume Up")}");
        }

        private void OnSortOrderChanged(object sender, EventArgs e)
        {
            if (SortPicker.SelectedIndex < 0 || Data == null || Data.Count == 0)
                return;

            var sortedList = Data.ToList();

            switch (SortPicker.SelectedIndex)
            {
                case 0:
                    sortedList = sortedList.OrderBy(d => d.createdAt).ToList();
                    break;
                case 1:
                    sortedList = sortedList.OrderByDescending(d => d.createdAt).ToList();
                    break;
                case 2:
                    sortedList = sortedList.OrderBy(d => d.Creator).ToList();
                    break;
                case 3:
                    sortedList = sortedList.OrderByDescending(d => d.Creator).ToList();
                    break;
                case 4:
                    sortedList = sortedList.OrderBy(d => d.Data?.ActionGroupObject?.ActionName).ToList();
                    break;
                case 5:
                    sortedList = sortedList.OrderByDescending(d => d.Data?.ActionGroupObject?.ActionName).ToList();
                    break;
                default:
                    return;
            }

            Data.Clear();
            foreach (var item in sortedList)
            {
                Data.Add(item);
            }

            OnPropertyChanged(nameof(Data));
        }

        public new event PropertyChangedEventHandler PropertyChanged;

        public new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}