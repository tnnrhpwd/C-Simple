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
            // For game movements, adjust the end points based on sensitivity
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

            // Calculate total distance for adaptive step sizing
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            // Dynamically adjust step count for very short or very long distances
            if (distance < 10)
                steps = Math.Max(5, steps / 3); // Fewer steps for tiny movements
            else if (distance > 500)
                steps = steps * 2; // More steps for large movements

            Random random = new Random();

            // Control points for natural arc (slight bezier curve effect)
            // For human movement, we often don't move in perfect straight lines
            int controlX = (startX + endX) / 2 + random.Next(-10, 10);
            int controlY = (startY + endY) / 2 + random.Next(-10, 10);

            // Previous point for calculating realistic speed
            int prevX = startX;
            int prevY = startY;

            // Human movements have variable speed - start slower, middle faster, end slower (ease in-out)
            for (int i = 1; i <= steps; i++)
            {
                if (cancel_simulation) break;

                // Calculate progress with custom easing
                float t = (float)i / steps;

                // Human-like movement: slow start, faster middle, slow end (ease in-out)
                float easedT = t < 0.5f
                    ? 2.0f * t * t
                    : 1.0f - (float)Math.Pow(-2.0f * t + 2.0f, 2) / 2.0f;

                // Apply slight bezier curve for more natural movement path
                float u = 1.0f - easedT;
                float tt = easedT * easedT;
                float uu = u * u;

                // Quadratic bezier curve with single control point
                int x = (int)(uu * startX + 2 * u * easedT * controlX + tt * endX);
                int y = (int)(uu * startY + 2 * u * easedT * controlY + tt * endY);

                // Add subtle human jitter (more prominent in middle, less at start/end)
                float jitterFactor = 4.0f * easedT * (1.0f - easedT); // Most jitter in middle
                if (distance > 20) // Only add jitter for larger movements
                {
                    x += (int)(random.Next(-2, 3) * jitterFactor);
                    y += (int)(random.Next(-2, 3) * jitterFactor);
                }

                // Calculate actual speed based on distance moved
                double segmentDistance = Math.Sqrt(Math.Pow(x - prevX, 2) + Math.Pow(y - prevY, 2));

                // Adaptive delay - slower for precise movements, faster for big sweeps
                int adaptiveDelay = delayMs;
                if (segmentDistance < 2 && distance > 100)
                    adaptiveDelay = delayMs / 2; // Move faster when small segments in large movement
                else if (segmentDistance > 10)
                    adaptiveDelay = delayMs * 2; // Slow down for large jumps

                // Send raw mouse move
                SendLowLevelMouseMove(x, y);

                // Store current position as previous for next iteration
                prevX = x;
                prevY = y;

                // Variable delay between moves for natural speed
                if (adaptiveDelay > 0)
                    await Task.Delay(adaptiveDelay);
            }

            // Always ensure we reach exact final position
            if (!cancel_simulation)
                SendLowLevelMouseMove(endX, endY);
        }

        // New method for time-accurate mouse movement
        private async Task TimedSmoothMouseMove(int startX, int startY, int endX, int endY, int steps, int delayMs, TimeSpan targetDuration)
        {
            // Start timing so we can adjust to hit the target duration
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // For game movements, adjust the end points based on sensitivity
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

            // Calculate total distance for adaptive step sizing
            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            // Adaptively increase steps for more smoothness
            int adaptiveSteps = distance < 50 ? steps : (int)(steps * Math.Sqrt(distance / 100.0));
            adaptiveSteps = Math.Max(adaptiveSteps, 60); // Minimum 60 steps for ultra-smooth movement
            adaptiveSteps = Math.Min(adaptiveSteps, 200); // Cap at 200 steps to avoid excessive processing
            steps = adaptiveSteps;

            Random random = new Random();

            // Multiple control points for more natural arc using cubic bezier
            // This creates more natural curved paths that humans naturally make
            int control1X = startX + (endX - startX) / 3 + random.Next(-5, 6);
            int control1Y = startY + (endY - startY) / 3 + random.Next(-5, 6);
            int control2X = startX + 2 * (endX - startX) / 3 + random.Next(-5, 6);
            int control2Y = startY + 2 * (endY - startY) / 3 + random.Next(-5, 6);

            // Previous point for calculating realistic speed
            int prevX = startX;
            int prevY = startY;

            // Calculate average delay needed to meet total duration
            double totalDelayNeeded = targetDuration.TotalMilliseconds - (5 * steps); // Subtract estimated processing time
            int avgDelayMs = (int)Math.Max(1, totalDelayNeeded / steps);

            // Time correction variable to ensure we stay on schedule
            double cumulativeDelayError = 0;

            // Human movements have variable speed - start slower, middle faster, end slower
            for (int i = 1; i <= steps; i++)
            {
                if (cancel_simulation) break;

                // Calculate progress with custom easing
                float t = (float)i / steps;

                // Apply sophisticated easing function for ultra-realistic movement
                float easedT = ApplyAdvancedEasing(t);

                // Apply cubic bezier curve for more sophisticated natural movement path
                // Cubic bezier (P₀(1-t)³ + 3P₁t(1-t)² + 3P₂t²(1-t) + P₃t³)
                float u = 1.0f - easedT;
                float u2 = u * u;
                float u3 = u2 * u;
                float t2 = easedT * easedT;
                float t3 = t2 * easedT;

                // Calculate position with cubic bezier curve
                int x = (int)(u3 * startX +
                              3 * u2 * easedT * control1X +
                              3 * u * t2 * control2X +
                              t3 * endX);

                int y = (int)(u3 * startY +
                              3 * u2 * easedT * control1Y +
                              3 * u * t2 * control2Y +
                              t3 * endY);

                // Add realistic human micro-tremors (hand isn't perfectly steady)
                // More evident in the middle, minimal at start/end
                float trembleFactor = 4.0f * easedT * (1.0f - easedT);

                // Make trembles proportional to distance but subtle 
                float trembleIntensity = Math.Min((float)(distance / 500), 1.0f);

                if (distance > 10) // Only add micro-tremors for non-tiny movements
                {
                    // Randomize tremor direction and intensity
                    if (random.NextDouble() < 0.7) // 70% chance of tremor
                    {
                        float microTremor = trembleIntensity * trembleFactor * (float)random.NextDouble();
                        int trembleX = random.Next(-1, 2);
                        int trembleY = random.Next(-1, 2);

                        // Apply very subtle movement
                        x += (int)(trembleX * microTremor);
                        y += (int)(trembleY * microTremor);
                    }
                }

                // Calculate current elapsed time
                double elapsedMs = stopwatch.ElapsedMilliseconds;
                double targetElapsedMs = (targetDuration.TotalMilliseconds * i) / steps;

                // Calculate ideal time vs. actual time
                double timeDiscrepancy = targetElapsedMs - elapsedMs;

                // Send raw mouse move
                SendLowLevelMouseMove(x, y);

                // Store current position as previous for next iteration
                prevX = x;
                prevY = y;

                // Dynamically adjust delay to match target duration with high precision
                if (i < steps) // Skip delay on last iteration
                {
                    // Adjust delay based on:
                    // 1. How far behind/ahead we are from target time
                    // 2. Accumulated error over time
                    double idealDelay = targetElapsedMs - elapsedMs;
                    cumulativeDelayError += (idealDelay - Math.Floor(idealDelay));

                    // Apply correction when accumulated error exceeds 1ms
                    int correctionMs = (int)cumulativeDelayError;
                    if (Math.Abs(correctionMs) >= 1)
                    {
                        idealDelay += correctionMs;
                        cumulativeDelayError -= correctionMs;
                    }

                    // Ensure we never wait less than 1ms
                    int adaptiveDelay = Math.Max(1, (int)idealDelay);
                    await Task.Delay(adaptiveDelay);
                }
            }

            // Always ensure we reach exact final position
            if (!cancel_simulation)
                SendLowLevelMouseMove(endX, endY);

            // If we completed early, wait for any remaining time to ensure accurate timing
            if (stopwatch.ElapsedMilliseconds < targetDuration.TotalMilliseconds)
            {
                int finalDelay = (int)(targetDuration.TotalMilliseconds - stopwatch.ElapsedMilliseconds);
                if (finalDelay > 0)
                    await Task.Delay(finalDelay);
            }

            stopwatch.Stop();
            Debug.WriteLine($"Mouse movement completed in {stopwatch.ElapsedMilliseconds}ms (target: {targetDuration.TotalMilliseconds}ms)");
        }

        // Advanced easing function for ultra-realistic movement patterns
        private float ApplyAdvancedEasing(float t)
        {
            // Enhanced multi-phase easing that better represents human motion
            if (t < 0.2f) // Initial acceleration phase
            {
                // Slow start with gradually increasing acceleration
                return 0.5f * (float)Math.Pow(t / 0.2f, 2.3) * 0.2f;
            }
            else if (t < 0.8f) // Middle phase (more consistent speed)
            {
                // Map t from [0.2, 0.8] to [0.1, 0.9]
                float normalized = 0.1f + (t - 0.2f) * 0.8f / 0.6f;

                // Apply slight ease-in-out in the middle section
                return 0.2f + (normalized - 0.1f) * 0.6f;
            }
            else // Final deceleration phase
            {
                // Gradual deceleration with slight settling at the end
                float phase = (t - 0.8f) / 0.2f;
                return 0.8f + (float)(0.2f * (1 - Math.Pow(1 - phase, 2.5)));
            }
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
