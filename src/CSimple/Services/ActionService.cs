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

        // Additional P/Invoke declarations for low-level input
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

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

        // Constants for SendInput
        private const int INPUT_MOUSE = 0;
        private const int MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        // Struct definitions for SendInput
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public MOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // Options for mouse movement - adjusted for slower game movement
        public bool UseInterpolation { get; set; } = true;
        public int MovementSteps { get; set; } = 40; // Increased from 20 to 40 for smoother motion
        public int MovementDelayMs { get; set; } = 5; // Increased from 1 to 5ms for slower movement
        public float GameSensitivityMultiplier { get; set; } = 0.5f; // New property to slow down game camera movements

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
                // Ensure all items have proper dates and local flags before saving
                foreach (var item in data)
                {
                    // Make sure createdAt is set if it's not already
                    if (item.createdAt == default(DateTime))
                    {
                        item.createdAt = DateTime.Now;
                    }

                    // Make sure ActionGroup.CreatedAt is consistent with item.createdAt
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        if (item.Data.ActionGroupObject.CreatedAt == null ||
                            item.Data.ActionGroupObject.CreatedAt == default(DateTime))
                        {
                            item.Data.ActionGroupObject.CreatedAt = item.createdAt;
                        }
                    }
                }

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
                    // Use a dictionary to ensure we only add unique items
                    var uniqueItems = new Dictionary<string, DataItem>();

                    foreach (var dataItem in loadedDataItems)
                    {
                        ParseDataItemText(dataItem);

                        // IMPORTANT: Don't override original creation dates
                        if (dataItem?.Data?.ActionGroupObject != null)
                        {
                            // Ensure non-local status for backend items
                            dataItem.Data.ActionGroupObject.IsLocal = false;

                            // Set CreatedAt explicitly to the original createdAt value from the database
                            // This preserves the actual creation time rather than using current time
                            dataItem.Data.ActionGroupObject.CreatedAt = dataItem.createdAt;

                            // Create unique key using ID or name
                            string key = !string.IsNullOrEmpty(dataItem._id)
                                ? dataItem._id
                                : dataItem.Data.ActionGroupObject.ActionName;

                            if (!string.IsNullOrEmpty(key) && !uniqueItems.ContainsKey(key))
                            {
                                uniqueItems[key] = dataItem;
                            }
                        }
                    }

                    result = uniqueItems.Values.ToList();
                    Debug.WriteLine($"LoadDataItemsFromFile: Loaded {result.Count} unique regular items");
                }
                else
                {
                    Debug.WriteLine("No items found in regular storage");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading regular data items: {ex.Message}");
            }
            return result;
        }

        // Helper method to check if two action items are the same
        private bool IsSameActionItem(DataItem item1, DataItem item2)
        {
            // First check IDs if they exist
            if (!string.IsNullOrEmpty(item1._id) && !string.IsNullOrEmpty(item2._id))
            {
                return item1._id == item2._id;
            }

            // Then check names
            var name1 = item1.Data?.ActionGroupObject?.ActionName;
            var name2 = item2.Data?.ActionGroupObject?.ActionName;

            if (!string.IsNullOrEmpty(name1) && !string.IsNullOrEmpty(name2))
            {
                return name1 == name2;
            }

            return false;
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

                    // Get current cursor position
                    GetCursorPos(out POINT startPoint);
                    int currentX = startPoint.X;
                    int currentY = startPoint.Y;

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
                                        int targetX = action.Coordinates?.X ?? 0;
                                        int targetY = action.Coordinates?.Y ?? 0;

                                        Debug.WriteLine($"Simulating Mouse Move at {action.Timestamp} to X: {targetX}, Y: {targetY}");

                                        if (UseInterpolation)
                                        {
                                            // For game optimization - use more steps and delay for smoother movement
                                            int gameSteps = MovementSteps;
                                            int gameDelay = MovementDelayMs;

                                            // If this appears to be a large movement (likely a game camera move), use more steps
                                            int deltaX = Math.Abs(targetX - currentX);
                                            int deltaY = Math.Abs(targetY - currentY);

                                            if (deltaX > 50 || deltaY > 50)
                                            {
                                                gameSteps = 60; // More steps for large movements
                                                gameDelay = 8;  // Larger delay for smoother game camera
                                            }

                                            // Move mouse smoothly (for games)
                                            await SmoothMouseMove(currentX, currentY, targetX, targetY, gameSteps, gameDelay);
                                        }
                                        else
                                        {
                                            // Direct move (traditional)
                                            SendLowLevelMouseMove(targetX, targetY);
                                        }

                                        // Update current position
                                        currentX = targetX;
                                        currentY = targetY;
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
                                    Debug.WriteLine($"Simulating Left Mouse Down at {action.Timestamp}");
                                    SendLowLevelMouseClick(MouseButton.Left, false, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case 0x0202: // Left Mouse Button Up
                                    Debug.WriteLine($"Simulating Left Mouse Up at {action.Timestamp}");
                                    SendLowLevelMouseClick(MouseButton.Left, true, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case (int)WM_RBUTTONDOWN: // Right Mouse Button Down
                                    Debug.WriteLine($"Simulating Right Mouse Down at {action.Timestamp}");
                                    SendLowLevelMouseClick(MouseButton.Right, false, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case 0x0205: // Right Mouse Button Up
                                    Debug.WriteLine($"Simulating Right Mouse Up at {action.Timestamp}");
                                    SendLowLevelMouseClick(MouseButton.Right, true, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
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

        // Smooth mouse movement for game crosshairs - uses interpolation between points
        private async Task SmoothMouseMove(int startX, int startY, int endX, int endY, int steps, int delayMs)
        {
            // For game movements, we might want to adjust the end points based on sensitivity
            if (GameSensitivityMultiplier != 1.0f && UseInterpolation)
            {
                // Calculate how far we're moving
                int deltaX = endX - startX;
                int deltaY = endY - startY;

                // Apply the sensitivity multiplier for games
                deltaX = (int)(deltaX * GameSensitivityMultiplier);
                deltaY = (int)(deltaY * GameSensitivityMultiplier);

                // Recalculate end position with adjusted delta
                endX = startX + deltaX;
                endY = startY + deltaY;
            }

            for (int i = 1; i <= steps; i++)
            {
                if (cancel_simulation) break;

                // Calculate intermediate position with easing for smoother movement
                float t = (float)i / steps;

                // Apply easing function (ease-out) for smoother deceleration
                t = 1 - (1 - t) * (1 - t);

                int x = (int)(startX + (endX - startX) * t);
                int y = (int)(startY + (endY - startY) * t);

                // Send raw mouse move
                SendLowLevelMouseMove(x, y);

                // Small delay between moves for smoothness
                if (delayMs > 0)
                    await Task.Delay(delayMs);
            }

            // Ensure final position
            if (!cancel_simulation)
                SendLowLevelMouseMove(endX, endY);
        }

        // Direct low-level mouse move using SendInput API for games
        private void SendLowLevelMouseMove(int x, int y)
        {
            // Get screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Calculate absolute coordinates (0-65536)
            int absoluteX = x * 65536 / screenWidth;
            int absoluteY = y * 65536 / screenHeight;

            // Create INPUT structure for mouse move
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = absoluteX;
            inputs[0].mi.dy = absoluteY;
            inputs[0].mi.mouseData = 0;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE;
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = IntPtr.Zero;

            // Send input
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        // Define MouseButton enum if it doesn't exist elsewhere in the code
        private enum MouseButton
        {
            Left,
            Right,
            Middle
        }

        // Low-level mouse click (down or up) using SendInput API
        private void SendLowLevelMouseClick(MouseButton button, bool isUp, int x, int y)
        {
            // First move to position
            SendLowLevelMouseMove(x, y);

            // Create INPUT structure for mouse click
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = 0;
            inputs[0].mi.dy = 0;
            inputs[0].mi.mouseData = 0;
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = IntPtr.Zero;

            // Set appropriate flags based on button and action
            switch (button)
            {
                case MouseButton.Left:
                    inputs[0].mi.dwFlags = isUp ? (uint)MOUSEEVENTF_LEFTUP : (uint)MOUSEEVENTF_LEFTDOWN;
                    break;
                case MouseButton.Right:
                    inputs[0].mi.dwFlags = isUp ? (uint)MOUSEEVENTF_RIGHTUP : (uint)MOUSEEVENTF_RIGHTDOWN;
                    break;
                case MouseButton.Middle:
                    inputs[0].mi.dwFlags = isUp ? (uint)MOUSEEVENTF_MIDDLEUP : (uint)MOUSEEVENTF_MIDDLEDOWN;
                    break;
            }

            // Send input
            SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));
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

        public async Task<List<DataItem>> LoadLocalDataItemsAsync()
        {
            try
            {
                var localItems = await _fileService.LoadLocalDataItemsAsync() ?? new List<DataItem>();

                // Filter out any duplicate items by ID or name
                var uniqueLocalItems = new Dictionary<string, DataItem>();

                foreach (var item in localItems)
                {
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        // Always mark local items as local
                        item.Data.ActionGroupObject.IsLocal = true;

                        // Always use the original timestamp from the database/file
                        if (item.Data.ActionGroupObject.CreatedAt == null ||
                            item.Data.ActionGroupObject.CreatedAt == default(DateTime))
                        {
                            // Use item.createdAt directly without fallback to current time
                            item.Data.ActionGroupObject.CreatedAt = item.createdAt;
                        }

                        // Use actionName as the key for local items
                        string key = item.Data.ActionGroupObject.ActionName;

                        if (!string.IsNullOrEmpty(key) && !uniqueLocalItems.ContainsKey(key))
                        {
                            uniqueLocalItems[key] = item;
                        }
                    }
                }

                Debug.WriteLine($"LoadLocalDataItemsAsync: Loaded {uniqueLocalItems.Count} unique local items");
                return uniqueLocalItems.Values.ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading local items: {ex.Message}");
                return new List<DataItem>();
            }
        }
    }
}
