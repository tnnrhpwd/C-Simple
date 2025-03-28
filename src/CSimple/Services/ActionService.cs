using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace CSimple.Services
{
    public class ActionService
    {
        private readonly DataService _dataService;
        private readonly FileService _fileService;
        private bool cancel_simulation = false;

        // P/Invoke for various system functions
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

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
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public ActionService(DataService dataService, FileService fileService)
        {
            _dataService = dataService;
            _fileService = fileService;
        }

        public async Task<ObservableCollection<DataItem>> LoadDataItemsFromBackend()
        {
            var result = new ObservableCollection<DataItem>();
            try
            {
                Debug.WriteLine("Starting Action Groups Load Task");
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return result;
                }

                var data = "Action";
                var dataItems = await _dataService.GetDataAsync(data, token);
                Debug.WriteLine($"Received data items from backend");

                Debug.WriteLine("Length of dataItems.Data:" + JsonSerializer.Serialize(dataItems.Data).Length.ToString());

                var formattedDataItems = dataItems.Data ?? new List<DataItem>();
                Debug.WriteLine($"Received {formattedDataItems.Count} DataItems from backend");

                foreach (var dataItem in formattedDataItems)
                {
                    if (dataItem != null)
                    {
                        ParseDataItemText(dataItem);
                        result.Add(dataItem);
                    }
                }
                Debug.WriteLine("Data Items Loaded from Backend");

                // Add more detailed debugging information
                if (result.Count > 0)
                {
                    var firstItem = result[0];
                    Debug.WriteLine($"First item: {firstItem.ToString()}");
                    Debug.WriteLine($"First item has ActionGroup? {firstItem.Data?.ActionGroupObject != null}");
                }

                // Save loaded action groups to file
                await SaveDataItemsToFile(new List<DataItem>(result));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading action groups from backend: {ex.Message}");
            }
            return result;
        }

        public async Task SaveDataItemsToFile(List<DataItem> data)
        {
            try
            {
                Debug.WriteLine("3. Actionpage.SaveDataItemsToFile Length of DataModel:" + JsonSerializer.Serialize(data).Length.ToString());
                Debug.WriteLine("Type of Data:" + data.GetType().ToString());

                await _fileService.SaveDataItemsAsync(data);
                Debug.WriteLine("Action Groups and Actions Saved to File");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data items: {ex.Message}");
            }
        }

        public async Task<List<DataItem>> LoadDataItemsFromFile()
        {
            var result = new List<DataItem>();
            try
            {
                var loadedDataItems = await _fileService.LoadDataItemsAsync();
                if (loadedDataItems != null && loadedDataItems.Count > 0)
                {
                    foreach (var dataItem in loadedDataItems)
                    {
                        ParseDataItemText(dataItem);
                        result.Add(dataItem);
                    }
                    Debug.WriteLine($"Data Items Loaded from SecureStorage. Data length: {result.Count}");
                }
                else
                {
                    Debug.WriteLine("No data items found in SecureStorage.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading data items from SecureStorage: {ex.Message}");
            }
            return result;
        }

        public async Task<bool> DeleteDataItemAsync(DataItem dataItem)
        {
            if (dataItem == null)
                return false;

            try
            {
                // Save the updated collection to file
                await SaveDataItemsToFile(await LoadDataItemsFromFile());

                // Delete from backend
                var token = await SecureStorage.GetAsync("userToken");
                if (!string.IsNullOrEmpty(token))
                {
                    var response = await _dataService.DeleteDataAsync(dataItem._id, token);
                    if (response.DataIsSuccess)
                    {
                        Debug.WriteLine($"Action Group {dataItem.ToString()} deleted from backend.");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to delete Action Group {dataItem.ToString()} from backend.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting action group: {ex.Message}");
            }
            return false;
        }

        public async Task<bool> ToggleSimulateActionGroupAsync(ActionGroup actionGroup)
        {
            Debug.WriteLine($"Toggling Simulation for: {actionGroup.ActionName}");
            if (actionGroup == null)
                return false;

            actionGroup.IsSimulating = !actionGroup.IsSimulating;

            if (actionGroup.IsSimulating)
            {
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
                            Debug.WriteLine($"Successfully cancelled action");
                            break;
                        }
                        Debug.WriteLine($"Scheduling Action: {action.Timestamp}");

                        DateTime currentActionTime;
                        if (!DateTime.TryParse(action.Timestamp.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out currentActionTime))
                        {
                            Debug.WriteLine($"Failed to parse Timestamp: {action.Timestamp}");
                            continue;
                        }

                        // Delay based on the difference between current and previous action times
                        if (previousActionTime.HasValue)
                        {
                            TimeSpan delay = currentActionTime - previousActionTime.Value;
                            Debug.WriteLine($"Scheduling delay for {delay.TotalMilliseconds} ms before next action.");
                            await Task.Delay(delay);
                        }

                        previousActionTime = currentActionTime;

                        // Schedule the action
                        Task actionTask = Task.Run(async () =>
                        {
                            if (cancel_simulation)
                            {
                                Debug.WriteLine($"Successfully cancelled action");
                                return;
                            }

                            switch (action.EventType)
                            {
                                case 512: // Mouse Move
                                    if (action.Coordinates != null)
                                    {
                                        int x = action.Coordinates?.X ?? 0;
                                        int y = action.Coordinates?.Y ?? 0;

                                        Debug.WriteLine($"Simulating Mouse Move at {action.Timestamp} to X: {x}, Y: {y}");
                                        InputSimulator.MoveMouse(x, y);
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"Mouse move action at {action.Timestamp} missing coordinates.");
                                    }
                                    break;

                                case 256: // Key Press
                                    Debug.WriteLine($"Simulating KeyPress at {action.Timestamp} with KeyCode: {action.KeyCode}");

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
                                        Debug.WriteLine($"Invalid KeyPress key code: {action.KeyCode}");
                                    }
                                    break;

                                case (int)WM_LBUTTONDOWN: // Left Mouse Button Down
                                case (int)WM_RBUTTONDOWN: // Right Mouse Button Down
                                    Debug.WriteLine($"Simulating Mouse Click at {action.Timestamp} with EventType: {action.EventType}");
                                    if (action.EventType == (int)WM_LBUTTONDOWN)
                                    {
                                        InputSimulator.SimulateMouseClick(MouseButton.Left, action.Coordinates.X, action.Coordinates.Y);
                                    }
                                    else if (action.EventType == (int)WM_RBUTTONDOWN)
                                    {
                                        InputSimulator.SimulateMouseClick(MouseButton.Right, action.Coordinates.X, action.Coordinates.Y);
                                    }
                                    break;

                                case 257: // Key Release
                                    Debug.WriteLine($"Simulating KeyRelease at {action.Timestamp} with KeyCode: {action.KeyCode}");

                                    int releaseKeyCodeInt = (int)action.KeyCode;
                                    if (Enum.IsDefined(typeof(VirtualKey), releaseKeyCodeInt))
                                    {
                                        VirtualKey virtualKey = (VirtualKey)releaseKeyCodeInt;

                                        InputSimulator.SimulateKeyUp(virtualKey);
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"Invalid KeyRelease key code: {action.KeyCode}");
                                    }
                                    break;

                                default:
                                    Debug.WriteLine($"Unhandled action type: {action.EventType} at {action.Timestamp}");
                                    break;
                            }
                        });

                        actionTasks.Add(actionTask);
                    }

                    // Execute all scheduled actions
                    await Task.WhenAll(actionTasks);

                    Debug.WriteLine($"Completed Simulation for: {actionGroup.ActionName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during simulation: {ex.Message}");
                    return false;
                }
                finally
                {
                    actionGroup.IsSimulating = false;
                }
            }
            else
            {
                cancel_simulation = true;
            }

            return true;
        }

        public void CancelSimulation()
        {
            cancel_simulation = true;
        }

        public static void ParseDataItemText(DataItem dataItem)
        {
            if (string.IsNullOrEmpty(dataItem?.Data?.Text)) return;

            var parts = dataItem.Data.Text.Split('|', StringSplitOptions.RemoveEmptyEntries);
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

        public static List<DataItem> SortDataItems(List<DataItem> items, int sortIndex)
        {
            switch (sortIndex)
            {
                case 0:
                    return items.OrderBy(d => d.createdAt).ToList();
                case 1:
                    return items.OrderByDescending(d => d.createdAt).ToList();
                case 2:
                    return items.OrderBy(d => d.Creator).ToList();
                case 3:
                    return items.OrderByDescending(d => d.Creator).ToList();
                case 4:
                    return items.OrderBy(d => d.Data?.ActionGroupObject?.ActionName).ToList();
                case 5:
                    return items.OrderByDescending(d => d.Data?.ActionGroupObject?.ActionName).ToList();
                default:
                    return items;
            }
        }

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
                    Debug.WriteLine($"Unknown Command: {command}");
                    break;
            }
        }

        private void SendVolumeCommand(byte volumeCommand)
        {
            keybd_event(volumeCommand, 0, 0, UIntPtr.Zero);
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero); // Key up
            Debug.WriteLine($"Executed Volume Command: {(volumeCommand == VK_VOLUME_MUTE ? "Mute" : volumeCommand == VK_VOLUME_DOWN ? "Volume Down" : "Volume Up")}");
        }

        // Update method to accept any ID type
        public async Task<List<ActionFile>> GetActionFilesAsync(string actionId)
        {
            try
            {
                // Handle null or empty actionId
                if (string.IsNullOrEmpty(actionId))
                {
                    actionId = "unknown";
                }

                Debug.WriteLine($"Getting files for action ID: {actionId}");

                // This would typically call a backend service or database
                // For now, we'll return a mock list of files
                return await Task.FromResult(new List<ActionFile>
                {
                    new ActionFile { Filename = $"screenshot-{actionId}.png", Data = "base64data" },
                    new ActionFile { Filename = $"recording-{actionId}.wav", Data = "base64data" },
                    new ActionFile { Filename = $"notes-{actionId}.txt", Data = "Sample notes for action: " + actionId }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting action files: {ex.Message}");
                return new List<ActionFile>();
            }
        }
    }
}
