using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CSimple.Services.AppModeService;

namespace CSimple.Services
{
    public class ActionService
    {
        private readonly DataService _dataService;
        private readonly FileService _fileService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
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

        // Add these constants near the top of the class
        private const uint MOUSEEVENTF_MOVE_NOCOALESCING = 0x2000; // More precise movement
        private const int REALIGN_EVERY_N_MOVEMENTS = 10; // Only realign every 10 raw movements
        private bool _useRawMovement = true; // Whether to prioritize raw movement data

        // Add mouse button state tracking for drag operations
        private bool _leftButtonDown = false;
        private bool _rightButtonDown = false;
        private bool _middleButtonDown = false;
        private bool _isDragging = false;

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

        // Options for mouse movement
        public bool UseInterpolation { get; set; } = true;
        public int MovementSteps { get; set; } = 25; // Default steps for movements
        public int MovementDelayMs { get; set; } = 1; // Default minimum delay
        public float GameSensitivityMultiplier { get; set; } = 1.0f; // 1.0 for original speed
        public bool UltraSmoothMode { get; set; } = true; // Use ultra-smooth movement

        // Add position tracking to prevent backward jumps
        private POINT _lastMousePosition;
        private DateTime _lastMoveTime = DateTime.MinValue;
        private readonly object _movementLock = new object();

        public ActionService(DataService dataService, FileService fileService, CSimple.Services.AppModeService.AppModeService appModeService = null)
        {
            _dataService = dataService;
            _fileService = fileService;
            _appModeService = appModeService;
        }

        public async Task<ObservableCollection<DataItem>> LoadDataItemsFromBackend()
        {
            var result = new ObservableCollection<DataItem>();

            // Check if online mode is active
            if (_appModeService?.CurrentMode != AppMode.Online)
            {
                Debug.WriteLine("App is not in online mode. Skipping backend action loading.");
                return result;
            }

            try
            {
                Debug.WriteLine("ONLINE MODE: Starting Action Groups Load Task");
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

                // Delete from backend only if in online mode
                if (_appModeService?.CurrentMode == AppMode.Online)
                {
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
                else
                {
                    Debug.WriteLine("App is not in online mode. Item deleted from local storage only.");
                    return true; // Return success even though we only deleted locally
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
                    // Reset mouse button states at the start of execution
                    _leftButtonDown = false;
                    _rightButtonDown = false;
                    _middleButtonDown = false;
                    _isDragging = false;

                    cancel_simulation = false;
                    DateTime? previousActionTime = null;
                    List<Task> actionTasks = new List<Task>();

                    // Get current cursor position
                    GetCursorPos(out POINT startPoint);
                    int currentX = startPoint.X;
                    int currentY = startPoint.Y;

                    // First, analyze the timestamps to calculate total duration
                    TimeSpan totalDuration = TimeSpan.Zero;
                    if (actionGroup.ActionArray.Count >= 2)
                    {
                        DateTime startTime = DateTime.MinValue;
                        DateTime endTime = DateTime.MinValue;

                        if (DateTime.TryParse(actionGroup.ActionArray.First().Timestamp?.ToString(), out startTime) &&
                            DateTime.TryParse(actionGroup.ActionArray.Last().Timestamp?.ToString(), out endTime))
                        {
                            totalDuration = endTime - startTime;
                            Debug.WriteLine($"Total action duration: {totalDuration.TotalMilliseconds}ms");
                        }
                    }

                    // Schedule all actions first
                    foreach (var action in actionGroup.ActionArray)
                    {
                        if (cancel_simulation)
                        {
                            Debug.WriteLine($"Successfully cancelled action");
                            break;
                        }
                        // Debug.WriteLine($"Scheduling Action: {action.Timestamp}");

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

                            // Skip actions with negative delays
                            if (delay.TotalMilliseconds < 0)
                            {
                                Debug.WriteLine($"Warning: Skipping action due to negative delay: {delay.TotalMilliseconds} ms");
                                continue;
                            }

                            // Debug.WriteLine($"Scheduling delay for {delay.TotalMilliseconds} ms before next action.");
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

                                        // Check if we're in a drag operation
                                        bool isDragging = _leftButtonDown || _rightButtonDown || _middleButtonDown;

                                        if (isDragging && !_isDragging)
                                        {
                                            _isDragging = true;
                                            Debug.WriteLine($"Starting drag operation with mouse button down");
                                        }

                                        Debug.WriteLine($"Simulating Mouse Move at {action.Timestamp} to X: {targetX}, Y: {targetY} {(isDragging ? "(DRAGGING)" : "")}");

                                        // Movement logic remains the same
                                        if (UseInterpolation)
                                        {
                                            // Calculate duration based on the next action's timestamp if available
                                            TimeSpan movementDuration = TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps); // Default

                                            // Try to find the next mouse move action to get exact duration
                                            int currentIndex = actionGroup.ActionArray.IndexOf(action);
                                            if (currentIndex >= 0 && currentIndex < actionGroup.ActionArray.Count - 1)
                                            {
                                                // Find next action with a valid timestamp
                                                for (int i = currentIndex + 1; i < actionGroup.ActionArray.Count; i++)
                                                {
                                                    var nextAction = actionGroup.ActionArray[i];
                                                    if (DateTime.TryParse(action.Timestamp.ToString(), out DateTime thisTime) &&
                                                        DateTime.TryParse(nextAction.Timestamp.ToString(), out DateTime nextTime))
                                                    {
                                                        movementDuration = nextTime - thisTime;
                                                        // Reduce slightly to account for processing time
                                                        movementDuration = TimeSpan.FromMilliseconds(movementDuration.TotalMilliseconds * 0.95);
                                                        break;
                                                    }
                                                }
                                            }

                                            // Use interpolation with accurate timing
                                            int actualSteps = MovementSteps;
                                            int actualDelayMs = (int)(movementDuration.TotalMilliseconds / actualSteps);

                                            // Make sure delay isn't too small
                                            if (actualDelayMs < 1)
                                            {
                                                actualDelayMs = 1;
                                                actualSteps = (int)movementDuration.TotalMilliseconds;
                                            }

                                            Debug.WriteLine($"Mouse movement with {actualSteps} steps and {actualDelayMs}ms delay (total: {movementDuration.TotalMilliseconds}ms)");

                                            // Execute time-accurate smooth movement
                                            await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, actualSteps, actualDelayMs, movementDuration);
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
                                    _leftButtonDown = true;
                                    _isDragging = false;
                                    SendLowLevelMouseClick(MouseButton.Left, false, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case 0x0202: // Left Mouse Button Up
                                    Debug.WriteLine($"Simulating Left Mouse Up at {action.Timestamp}");
                                    _leftButtonDown = false;
                                    _isDragging = _rightButtonDown || _middleButtonDown; // Still dragging if other buttons pressed
                                    SendLowLevelMouseClick(MouseButton.Left, true, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case (int)WM_RBUTTONDOWN: // Right Mouse Button Down
                                    Debug.WriteLine($"Simulating Right Mouse Down at {action.Timestamp}");
                                    _rightButtonDown = true;
                                    _isDragging = false;
                                    SendLowLevelMouseClick(MouseButton.Right, false, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case 0x0205: // Right Mouse Button Up
                                    Debug.WriteLine($"Simulating Right Mouse Up at {action.Timestamp}");
                                    _rightButtonDown = false;
                                    _isDragging = _leftButtonDown || _middleButtonDown; // Still dragging if other buttons pressed
                                    SendLowLevelMouseClick(MouseButton.Right, true, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                // Add middle mouse button support
                                case 0x0207: // Middle Mouse Button Down
                                    Debug.WriteLine($"Simulating Middle Mouse Down at {action.Timestamp}");
                                    _middleButtonDown = true;
                                    _isDragging = false;
                                    SendLowLevelMouseClick(MouseButton.Middle, false, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
                                    break;

                                case 0x0208: // Middle Mouse Button Up
                                    Debug.WriteLine($"Simulating Middle Mouse Up at {action.Timestamp}");
                                    _middleButtonDown = false;
                                    _isDragging = _leftButtonDown || _rightButtonDown; // Still dragging if other buttons pressed
                                    SendLowLevelMouseClick(MouseButton.Middle, true, action.Coordinates?.X ?? currentX, action.Coordinates?.Y ?? currentY);
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

                    // Ensure all mouse buttons are released at the end of execution
                    if (_leftButtonDown)
                    {
                        Debug.WriteLine("Force releasing left mouse button at end of sequence");
                        SendLowLevelMouseClick(MouseButton.Left, true, currentX, currentY);
                        _leftButtonDown = false;
                    }

                    if (_rightButtonDown)
                    {
                        Debug.WriteLine("Force releasing right mouse button at end of sequence");
                        SendLowLevelMouseClick(MouseButton.Right, true, currentX, currentY);
                        _rightButtonDown = false;
                    }

                    if (_middleButtonDown)
                    {
                        Debug.WriteLine("Force releasing middle mouse button at end of sequence");
                        SendLowLevelMouseClick(MouseButton.Middle, true, currentX, currentY);
                        _middleButtonDown = false;
                    }

                    _isDragging = false;
                    Debug.WriteLine($"Completed Simulation for: {actionGroup.ActionName}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during simulation: {ex.Message}");

                    // Safety cleanup - ensure mouse buttons are released even on error
                    try
                    {
                        GetCursorPos(out POINT currentPoint);
                        if (_leftButtonDown) SendLowLevelMouseClick(MouseButton.Left, true, currentPoint.X, currentPoint.Y);
                        if (_rightButtonDown) SendLowLevelMouseClick(MouseButton.Right, true, currentPoint.X, currentPoint.Y);
                        if (_middleButtonDown) SendLowLevelMouseClick(MouseButton.Middle, true, currentPoint.X, currentPoint.Y);
                        _leftButtonDown = _rightButtonDown = _middleButtonDown = _isDragging = false;
                    }
                    catch { /* Ignore cleanup errors */ }

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

        // Fixed version of TimedSmoothMouseMove to prevent jumping and reduce jitter
        private async Task TimedSmoothMouseMove(int startX, int startY, int endX, int endY, int steps, int delayMs, TimeSpan targetDuration)
        {
            // Synchronize movements to prevent conflicts and backward jumps
            lock (_movementLock)
            {
                // Check if this movement might be out of sequence
                if (_lastMoveTime != DateTime.MinValue)
                {
                    // Make sure we're not moving to an outdated position
                    if (DateTime.Now - _lastMoveTime > TimeSpan.FromSeconds(1))
                    {
                        // Get current mouse position to use as the real starting point
                        GetCursorPos(out POINT currentPos);
                        startX = currentPos.X;
                        startY = currentPos.Y;
                    }
                }
                _lastMoveTime = DateTime.Now;
            }

            // Start timing for accurate duration tracking
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Apply sensitivity multiplier if needed
            if (GameSensitivityMultiplier != 1.0f && UseInterpolation)
            {
                int deltaX = endX - startX;
                int deltaY = endY - startY;

                deltaX = (int)(deltaX * GameSensitivityMultiplier);
                deltaY = (int)(deltaY * GameSensitivityMultiplier);

                endX = startX + deltaX;
                endY = startY + deltaY;
            }

            // Calculate distance for optimized steps
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            // Optimize steps based on distance
            int adaptiveSteps;
            if (distance < 10)
            {
                // For tiny movements, use fewer steps to prevent jitter
                adaptiveSteps = 3;
            }
            else if (distance < 100)
            {
                // For short movements, use moderate steps
                adaptiveSteps = Math.Max(5, (int)(distance / 10));
            }
            else
            {
                // For longer movements, use more steps for smoothness
                adaptiveSteps = Math.Min(80, (int)(distance / 5));
            }

            // Ensure steps aren't too low or too high
            adaptiveSteps = Math.Clamp(adaptiveSteps, 3, 100);

            // Calculate actual delay to meet target duration
            double targetMs = targetDuration.TotalMilliseconds;

            // Ensure targetMs is positive
            if (targetMs <= 0)
            {
                targetMs = distance * 2; // Fallback timing based on distance
                Debug.WriteLine("Warning: Target duration was zero or negative. Using fallback timing.");
            }

            // Calculate average delay needed, accounting for processing time
            int avgDelayMs = Math.Max(1, (int)Math.Round((targetMs - adaptiveSteps) / adaptiveSteps));

            // Ensure avgDelayMs is not negative
            if (avgDelayMs < 1)
            {
                avgDelayMs = 1; // Minimum delay of 1ms
                Debug.WriteLine("Warning: Calculated delay was negative. Using minimum delay of 1ms.");
            }

            // Use fixed constant delay if timing issues detected
            if (targetMs <= 0 || adaptiveSteps <= 0)
            {
                avgDelayMs = 1;
                Debug.WriteLine("Warning: Timing issues detected. Using fixed delay of 1ms.");
            }

            // Use perfect control points for smoothest movement
            int control1X = startX + (endX - startX) / 3;
            int control1Y = startY + (endY - startY) / 3;
            int control2X = startX + 2 * (endX - startX) / 3;
            int control2Y = startY + 2 * (endY - startY) / 3;

            // Track the last position to avoid duplicate movements
            int lastX = startX;
            int lastY = startY;

            // Execute the movement with optimized steps and timing
            for (int i = 1; i <= adaptiveSteps; i++)
            {
                if (cancel_simulation) break;

                // Calculate progress
                float t = (float)i / adaptiveSteps;

                // Use super smooth easing - sine-based curve for perfect smoothness
                float easedT = (float)(Math.Sin(Math.PI * (t - 0.5)) / 2 + 0.5);

                // Calculate position using cubic bezier
                float u = 1.0f - easedT;
                float u2 = u * u;
                float u3 = u2 * u;
                float t2 = easedT * easedT;
                float t3 = t2 * easedT;

                int x = (int)Math.Round(u3 * startX +
                               3 * u2 * easedT * control1X +
                               3 * u * t2 * control2X +
                               t3 * endX);

                int y = (int)Math.Round(u3 * startY +
                               3 * u2 * easedT * control1Y +
                               3 * u * t2 * control2Y +
                               t3 * endY);

                // Skip duplicate positions to avoid unnecessary system calls
                if (x != lastX || y != lastY)
                {
                    // Send the mouse movement with no coalescing for more precision
                    SendLowLevelMouseMove(x, y);
                    lastX = x;
                    lastY = y;
                }

                if (i < adaptiveSteps)
                {
                    // Use a consistent delay between points
                    await Task.Delay(avgDelayMs);
                }
            }

            // Always move to the exact endpoint
            SendLowLevelMouseMove(endX, endY);

            // Update the last mouse position for synchronization
            _lastMousePosition.X = endX;
            _lastMousePosition.Y = endY;

            stopwatch.Stop();
        }

        // Direct low-level mouse move using SendInput API
        private void SendLowLevelMouseMove(int x, int y)
        {
            // Get screen dimensions
            int screenWidth = GetSystemMetrics(SM_CXSCREEN);
            int screenHeight = GetSystemMetrics(SM_CYSCREEN);

            // Calculate absolute coordinates (0-65536)
            int absoluteX = (x * 65536) / screenWidth;
            int absoluteY = (y * 65536) / screenHeight;

            // Create INPUT structure for mouse move
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = absoluteX;
            inputs[0].mi.dy = absoluteY;
            inputs[0].mi.mouseData = 0;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE_NOCOALESCING;
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

        // New method to simulate raw mouse movement using deltas instead of absolute coordinates
        private async Task SimulateRawMouseMovement(int deltaX, int deltaY, long timeSinceLastMoveMs, float velocityX, float velocityY)
        {
            const int maxMicroMove = 5; // Maximum micro-movement per step
            const int minDelayMs = 1;  // Minimum delay between steps
            const int maxDelayMs = 10; // Maximum delay between steps

            // Calculate total distance
            double totalDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            // Determine number of steps based on distance and velocity
            int numSteps = (int)Math.Max(1, Math.Min(totalDistance / maxMicroMove, 20));

            // Calculate step size
            double stepX = (double)deltaX / numSteps;
            double stepY = (double)deltaY / numSteps;

            // Calculate delay between steps based on total time and number of steps
            double stepDelayMs = Math.Clamp(timeSinceLastMoveMs / numSteps, minDelayMs, maxDelayMs);

            // Start stopwatch for accurate timing
            Stopwatch sw = Stopwatch.StartNew();
            double elapsedMs = 0;

            for (int i = 0; i < numSteps; i++)
            {
                // Calculate micro-movement for this step
                int microDeltaX = (int)Math.Round(stepX);
                int microDeltaY = (int)Math.Round(stepY);

                // Send raw mouse input
                SendRawMouseInput(microDeltaX, microDeltaY);

                // Calculate actual delay
                double targetElapsedMs = stepDelayMs * (i + 1);
                double actualDelayMs = targetElapsedMs - elapsedMs;

                // Delay for the calculated time
                if (actualDelayMs > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(actualDelayMs));

                // Update elapsed time
                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }

            sw.Stop();
        }

        // Add this new method to send raw mouse movement using INPUT structure
        private void SendRawMouseInput(int deltaX, int deltaY)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].mi.dx = deltaX;
            inputs[0].mi.dy = deltaY;
            inputs[0].mi.mouseData = 0;
            inputs[0].mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_MOVE_NOCOALESCING; // Use more precise movement
            inputs[0].mi.time = 0;
            inputs[0].mi.dwExtraInfo = IntPtr.Zero;

            // Send the raw input
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

        // Fully revised method to ensure NO backend loading in offline mode
        public async Task<List<DataItem>> LoadAllDataItemsAsync()
        {
            var result = new Dictionary<string, DataItem>();
            var debugInfo = new Dictionary<string, int> { { "backend", 0 }, { "local", 0 }, { "duplicates", 0 } };

            try
            {
                // Get app mode with explicit null check
                bool isOnlineMode = false;
                if (_appModeService != null)
                {
                    isOnlineMode = _appModeService.CurrentMode == AppMode.Online;
                    Debug.WriteLine($"AppModeService found. Current mode: {_appModeService.CurrentMode} (Online: {isOnlineMode})");
                }
                else
                {
                    Debug.WriteLine("WARNING: _appModeService is null, defaulting to offline mode");
                }

                // STEP 1: Load backend items ONLY if explicitly in online mode
                if (isOnlineMode)
                {
                    Debug.WriteLine("ONLINE MODE: Loading backend items...");
                    var token = await SecureStorage.GetAsync("userToken");
                    if (!string.IsNullOrEmpty(token))
                    {
                        try
                        {
                            var backendItems = await LoadDataItemsFromBackend();
                            debugInfo["backend"] = backendItems.Count;

                            foreach (var item in backendItems)
                            {
                                if (item?.Data?.ActionGroupObject != null)
                                {
                                    item.Data.ActionGroupObject.IsLocal = false;
                                    string key = !string.IsNullOrEmpty(item._id)
                                        ? "backend_" + item._id
                                        : "backend_" + item.Data.ActionGroupObject.ActionName;
                                    result[key] = item;
                                }
                            }
                            Debug.WriteLine($"ONLINE MODE: Loaded {backendItems.Count} backend items");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading backend items: {ex.Message}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("User not logged in - skipping backend items");
                    }
                }
                else
                {
                    Debug.WriteLine("NOT IN ONLINE MODE: Skipping ALL backend operations");
                }

                // STEP 2: Always load local items, regardless of mode
                Debug.WriteLine($"{(isOnlineMode ? "ONLINE" : "OFFLINE")} MODE: Loading local items...");
                var localItems = await LoadDataItemsFromFile();
                debugInfo["local"] = localItems.Count;

                // Process local items
                foreach (var item in localItems)
                {
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        item.Data.ActionGroupObject.IsLocal = true;
                        string key = !string.IsNullOrEmpty(item._id)
                            ? "local_" + item._id
                            : "local_" + item.Data.ActionGroupObject.ActionName;

                        // In offline mode, we don't need to check for duplicates with backend items
                        if (isOnlineMode || !result.ContainsKey("backend_" + (
                            !string.IsNullOrEmpty(item._id)
                                ? item._id
                                : item.Data.ActionGroupObject.ActionName)))
                        {
                            result[key] = item;
                        }
                        else
                        {
                            debugInfo["duplicates"]++;
                        }
                    }
                }
                Debug.WriteLine($"Loaded {localItems.Count} local file items");

                // STEP 3: Load device-local items
                Debug.WriteLine("Loading device-local items...");
                var deviceLocalItems = await LoadLocalDataItemsAsync();
                foreach (var item in deviceLocalItems)
                {
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        item.Data.ActionGroupObject.IsLocal = true;
                        string key = "device_" + item.Data.ActionGroupObject.ActionName;

                        bool isDuplicate = result.Values.Any(existingItem =>
                            IsSameActionItem(existingItem, item));

                        if (!isDuplicate)
                        {
                            result[key] = item;
                        }
                        else
                        {
                            debugInfo["duplicates"]++;
                        }
                    }
                }

                // Fix timestamps
                foreach (var item in result.Values)
                {
                    if (item?.Data?.ActionGroupObject != null &&
                        (item.Data.ActionGroupObject.CreatedAt == null ||
                         item.Data.ActionGroupObject.CreatedAt == default(DateTime)))
                    {
                        item.Data.ActionGroupObject.CreatedAt = item.createdAt != default(DateTime) ?
                            item.createdAt : DateTime.Now;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadAllDataItemsAsync: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
            }

            // Log summary of what we found - with clear offline/online indicator
            string modeInfo = (_appModeService?.CurrentMode == AppMode.Online) ? "[ONLINE MODE]" : "[OFFLINE MODE]";
            Debug.WriteLine($"{modeInfo} Data summary: {debugInfo["backend"]} backend, " +
                            $"{debugInfo["local"]} local, {debugInfo["duplicates"]} duplicates, " +
                            $"returning {result.Count} items");

            return result.Values.ToList();
        }

        // Helper method to save item to backend with online check
        public async Task<bool> SaveDataItemToBackendAsync(DataItem dataItem, string token)
        {
            // Only save to backend if in online mode
            if (_appModeService?.CurrentMode != AppMode.Online)
            {
                Debug.WriteLine("App is not in online mode. Item saved locally only.");
                return true;
            }

            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("No token provided for backend save");
                return false;
            }

            try
            {
                Debug.WriteLine("ONLINE MODE: Saving item to backend");
                var response = await _dataService.CreateDataAsync(dataItem, token);
                return response.DataIsSuccess;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data item to backend: {ex.Message}");
                return false;
            }
        }
    }
}
