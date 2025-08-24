using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CSimple.Services.AppModeService;
using CSimple.Models;
using CSimple.Input; // Added using for LowLevelInputSimulator
using CSimple.Utils;  // Added using for ActionServiceUtils

namespace CSimple.Services
{
    public class ActionService
    {
        private readonly DataService _dataService;
        private readonly FileService _fileService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private bool cancel_simulation = false;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_VOLUME_MUTE = 0xAD; // Keep volume keys if ExecuteWindowsCommand stays
        private const byte VK_VOLUME_DOWN = 0xAE;
        private const byte VK_VOLUME_UP = 0xAF;

        private bool _leftButtonDown = false;
        private bool _rightButtonDown = false;
        private bool _middleButtonDown = false;
        private bool _isDragging = false;

        public bool UseInterpolation { get; set; } = true;
        public int MovementSteps { get; set; } = 25;
        public int MovementDelayMs { get; set; } = 1;
        public float GameSensitivityMultiplier { get; set; } = 1.0f;
        public bool UltraSmoothMode { get; set; } = true; // Consider if this is still relevant
        public bool UseRawInput { get; set; } = true;

        private LowLevelInputSimulator.POINT _lastMousePosition; // Use namespaced struct
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

            if (_appModeService?.CurrentMode != AppMode.Online)
            {
                return result;
            }

            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return result;
                }

                var data = "Action";
                var dataItems = await _dataService.GetDataAsync(data, token);

                var formattedDataItems = dataItems.Data ?? new List<DataItem>();

                foreach (var dataItem in formattedDataItems)
                {
                    if (dataItem != null)
                    {
                        ActionServiceUtils.ParseDataItemText(dataItem); // Use utility class
                        result.Add(dataItem);
                    }
                }

                if (result.Count > 0)
                {
                    var firstItem = result[0];
                }

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
                foreach (var item in data)
                {
                    if (item.createdAt == default(DateTime))
                    {
                        item.createdAt = DateTime.Now;
                    }

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
                    var uniqueItems = new Dictionary<string, DataItem>();

                    foreach (var dataItem in loadedDataItems)
                    {
                        ActionServiceUtils.ParseDataItemText(dataItem); // Use utility class

                        if (dataItem?.Data?.ActionGroupObject != null)
                        {
                            if (dataItem.Data.ActionGroupObject.CreatedAt == null ||
                                dataItem.Data.ActionGroupObject.CreatedAt == default(DateTime))
                            {
                                dataItem.Data.ActionGroupObject.CreatedAt = dataItem.createdAt;
                            }

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
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading regular data items: {ex.Message}");
            }
            return result;
        }

        private bool IsSameActionItem(DataItem item1, DataItem item2)
        {
            if (!string.IsNullOrEmpty(item1._id) && !string.IsNullOrEmpty(item2._id))
            {
                return item1._id == item2._id;
            }

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
                List<string> idList = new List<string>();
                List<string> nameList = new List<string>();

                if (!string.IsNullOrEmpty(dataItem._id))
                    idList.Add(dataItem._id);

                if (dataItem?.Data?.ActionGroupObject?.ActionName != null)
                    nameList.Add(dataItem.Data.ActionGroupObject.ActionName);

                await _fileService.DeleteDataItemsAsync(idList, nameList);

                if (_appModeService?.CurrentMode == AppMode.Online)
                {
                    var token = await SecureStorage.GetAsync("userToken");
                    if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(dataItem._id))
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
                    return true;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting action group: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DataItem>> LoadLocalDataItemsAsync()
        {
            try
            {
                var localItems = await _fileService.LoadLocalDataItemsAsync() ?? new List<DataItem>();

                var uniqueLocalItems = new Dictionary<string, DataItem>();

                foreach (var item in localItems)
                {
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        item.Data.ActionGroupObject.IsLocal = true;

                        if (item.Data.ActionGroupObject.CreatedAt == null ||
                            item.Data.ActionGroupObject.CreatedAt == default(DateTime))
                        {
                            item.Data.ActionGroupObject.CreatedAt = item.createdAt;
                        }

                        string key = item.Data.ActionGroupObject.ActionName;

                        if (!string.IsNullOrEmpty(key) && !uniqueLocalItems.ContainsKey(key))
                        {
                            uniqueLocalItems[key] = item;
                        }
                    }
                }

                return uniqueLocalItems.Values.ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading local items: {ex.Message}");
                return new List<DataItem>();
            }
        }

        public async Task<bool> ToggleSimulateActionGroupAsync(ActionGroup actionGroup)
        {
            if (actionGroup == null)
                return false;

            actionGroup.IsSimulating = !actionGroup.IsSimulating;

            if (actionGroup.IsSimulating)
            {
                try
                {
                    InputCaptureService.SimulationCancelledByTaskManager = false;

                    _leftButtonDown = false;
                    _rightButtonDown = false;
                    _middleButtonDown = false;
                    _isDragging = false;

                    cancel_simulation = false;
                    DateTime? previousActionTime = null;
                    bool prevLeftButtonDown = false; // Renamed to avoid conflict with class member
                    bool prevRightButtonDown = false;
                    bool prevMiddleButtonDown = false;

                    LowLevelInputSimulator.GetCursorPos(out LowLevelInputSimulator.POINT startPoint); // Use utility class
                    int currentX = startPoint.X;
                    int currentY = startPoint.Y;

                    Dictionary<int, bool> pressedKeys = new Dictionary<int, bool>();

                    Random random = new Random();
                    const int MIN_KEY_DOWN_DURATION = 50;
                    const int MAX_KEY_DOWN_DURATION = 150;

                    foreach (var action in actionGroup.ActionArray)
                    {
                        if (cancel_simulation || InputCaptureService.SimulationCancelledByTaskManager)
                            break;

                        DateTime currentActionTime;
                        if (!DateTime.TryParse(action.Timestamp.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out currentActionTime))
                        {
                            // Debug.WriteLine($"Failed to parse Timestamp: {action.Timestamp}");
                            continue;
                        }

                        if (previousActionTime.HasValue)
                        {
                            TimeSpan delay = currentActionTime - previousActionTime.Value;
                            if (delay.TotalMilliseconds > 0 && delay.TotalMilliseconds < 2000)
                                await Task.Delay(delay);
                        }
                        previousActionTime = currentActionTime;

                        try
                        {
                            if (action.EventType == 0x0100) // KeyDown
                            {
                                pressedKeys[action.KeyCode] = true;
                                LowLevelInputSimulator.SendKeyboardInput((ushort)action.KeyCode, false); // Use utility class

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    LowLevelInputSimulator.SendKeyboardInput((ushort)action.KeyCode, true); // Use utility class
                                    pressedKeys.Remove(action.KeyCode);
                                }
                            }
                            else if (action.EventType == 0x0101) // KeyUp
                            {
                                LowLevelInputSimulator.SendKeyboardInput((ushort)action.KeyCode, true); // Use utility class
                                pressedKeys.Remove(action.KeyCode);
                            }
                            else if (action.EventType == 0x0201) // LeftButtonDown
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseInterpolation)
                                    {
                                        await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                            TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                    }
                                    else
                                    {
                                        LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                // Use Input.MouseButton explicitly
                                LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Left, false, targetX, targetY);
                                _leftButtonDown = true; // Track state within ActionService

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    // Use Input.MouseButton explicitly
                                    LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Left, true, targetX, targetY);
                                    _leftButtonDown = false;
                                }
                            }
                            else if (action.EventType == 0x0202) // LeftButtonUp
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseInterpolation)
                                    {
                                        await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                            TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                    }
                                    else
                                    {
                                        LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                // Use Input.MouseButton explicitly
                                LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Left, true, targetX, targetY);
                                _leftButtonDown = false;
                            }
                            else if (action.EventType == 0x0204) // RightButtonDown
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseInterpolation)
                                    {
                                        await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                            TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                    }
                                    else
                                    {
                                        LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                // Use Input.MouseButton explicitly
                                LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Right, false, targetX, targetY);
                                prevRightButtonDown = true; // Use local variable

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    // Use Input.MouseButton explicitly
                                    LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Right, true, targetX, targetY);
                                    prevRightButtonDown = false;
                                }
                            }
                            else if (action.EventType == 0x0205) // RightButtonUp
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseInterpolation)
                                    {
                                        await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                            TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                    }
                                    else
                                    {
                                        LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                // Use Input.MouseButton explicitly
                                LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Right, true, targetX, targetY);
                                prevRightButtonDown = false;
                            }
                            else if (action.EventType == 0x0207) // MiddleButtonDown
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseInterpolation)
                                    {
                                        await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                            TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                    }
                                    else
                                    {
                                        LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                // Use Input.MouseButton explicitly
                                LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Middle, false, targetX, targetY);
                                _middleButtonDown = true; // Track state within ActionService

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    // Use Input.MouseButton explicitly
                                    LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Middle, true, targetX, targetY);
                                    _middleButtonDown = false;
                                }
                            }
                            else if (action.EventType == 0x0208) // MiddleButtonUp
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseInterpolation)
                                    {
                                        await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                            TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                    }
                                    else
                                    {
                                        LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                // Use Input.MouseButton explicitly
                                LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Middle, true, targetX, targetY);
                                _middleButtonDown = false;
                            }
                            else if (action.EventType == 512 || action.EventType == 0x0200) // MouseMove
                            {
                                int targetX = action.Coordinates?.X ?? currentX;
                                int targetY = action.Coordinates?.Y ?? currentY;

                                if (action.DeltaX != 0 || action.DeltaY != 0)
                                {
                                    targetX = currentX + action.DeltaX;
                                    targetY = currentY + action.DeltaY;
                                }

                                if (targetX != currentX || targetY != currentY)
                                {
                                    if (UseRawInput && action.DeltaX != 0 || action.DeltaY != 0)
                                    {
                                        await SimulateRawMouseMovement(action.DeltaX, action.DeltaY, (long)action.TimeSinceLastMoveMs, action.VelocityX, action.VelocityY);
                                        currentX += action.DeltaX;
                                        currentY += action.DeltaY;
                                    }
                                    else
                                    {
                                        if (UseInterpolation)
                                        {
                                            await TimedSmoothMouseMove(currentX, currentY, targetX, targetY, MovementSteps, MovementDelayMs,
                                                TimeSpan.FromMilliseconds(MovementDelayMs * MovementSteps));
                                        }
                                        else
                                        {
                                            LowLevelInputSimulator.SendLowLevelMouseMove(targetX, targetY); // Use utility class
                                        }
                                        currentX = targetX;
                                        currentY = targetY;
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error executing action: {ex.Message}");
                        }
                    }

                    // Cleanup pressed keys
                    foreach (var keyCode in pressedKeys.Keys.ToList())
                    {
                        LowLevelInputSimulator.SendKeyboardInput((ushort)keyCode, true); // Use utility class
                    }

                    // Cleanup mouse buttons
                    if (_leftButtonDown)
                    {
                        // Use Input.MouseButton explicitly
                        LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Left, true, currentX, currentY);
                        _leftButtonDown = false;
                    }
                    if (prevRightButtonDown) // Use local variable
                    {
                        // Use Input.MouseButton explicitly
                        LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Right, true, currentX, currentY);
                        prevRightButtonDown = false;
                    }
                    if (_middleButtonDown) // Check class member state
                    {
                        // Use Input.MouseButton explicitly
                        LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Middle, true, currentX, currentY);
                        _middleButtonDown = false;
                    }
                    // Note: prevMiddleButtonDown was not used consistently, switched to checking _middleButtonDown state

                    _isDragging = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during simulation: {ex.Message}");
                    Debug.WriteLine(ex.StackTrace);

                    try
                    {
                        LowLevelInputSimulator.GetCursorPos(out LowLevelInputSimulator.POINT currentPoint); // Use utility class
                        // Use Input.MouseButton explicitly
                        if (_leftButtonDown) LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Left, true, currentPoint.X, currentPoint.Y);
                        // Use Input.MouseButton explicitly
                        if (_rightButtonDown) LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Right, true, currentPoint.X, currentPoint.Y);
                        // Use Input.MouseButton explicitly
                        if (_middleButtonDown) LowLevelInputSimulator.SendLowLevelMouseClick(Input.MouseButton.Middle, true, currentPoint.X, currentPoint.Y);
                        _leftButtonDown = _rightButtonDown = _middleButtonDown = _isDragging = false;
                    }
                    catch { }

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


        private async Task TimedSmoothMouseMove(int startX, int startY, int endX, int endY, int steps, int delayMs, TimeSpan targetDuration)
        {
            lock (_movementLock)
            {
                if (_lastMoveTime != DateTime.MinValue)
                {
                    if (DateTime.Now - _lastMoveTime > TimeSpan.FromSeconds(1))
                    {
                        LowLevelInputSimulator.GetCursorPos(out LowLevelInputSimulator.POINT currentPos); // Use utility class
                        startX = currentPos.X;
                        startY = currentPos.Y;
                    }
                }
                _lastMoveTime = DateTime.Now;
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            if (GameSensitivityMultiplier != 1.0f && UseInterpolation)
            {
                int deltaX = endX - startX;
                int deltaY = endY - startY;

                deltaX = (int)(deltaX * GameSensitivityMultiplier);
                deltaY = (int)(deltaY * GameSensitivityMultiplier);

                endX = startX + deltaX;
                endY = startY + deltaY;
            }

            double distance = Math.Sqrt(Math.Pow(endX - startX, 2) + Math.Pow(endY - startY, 2));

            int adaptiveSteps;
            if (distance < 10)
            {
                adaptiveSteps = 3;
            }
            else if (distance < 100)
            {
                adaptiveSteps = Math.Max(5, (int)(distance / 10));
            }
            else
            {
                adaptiveSteps = Math.Min(80, (int)(distance / 5));
            }

            adaptiveSteps = Math.Clamp(adaptiveSteps, 3, 100);

            double targetMs = targetDuration.TotalMilliseconds;

            if (targetMs <= 0)
            {
                targetMs = distance * 2;
            }

            int avgDelayMs = Math.Max(1, (int)Math.Round((targetMs - adaptiveSteps) / adaptiveSteps));

            if (avgDelayMs < 1)
            {
                avgDelayMs = 1;
            }

            if (targetMs <= 0 || adaptiveSteps <= 0)
            {
                avgDelayMs = 1;
            }

            int control1X = startX + (endX - startX) / 3;
            int control1Y = startY + (endY - startY) / 3;
            int control2X = startX + 2 * (endX - startX) / 3;
            int control2Y = startY + 2 * (endY - startY) / 3;

            int lastX = startX;
            int lastY = startY;

            for (int i = 1; i <= adaptiveSteps; i++)
            {
                if (cancel_simulation) break;

                float t = (float)i / adaptiveSteps;

                float easedT = (float)(Math.Sin(Math.PI * (t - 0.5)) / 2 + 0.5);

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

                if (x != lastX || y != lastY)
                {
                    LowLevelInputSimulator.SendLowLevelMouseMove(x, y); // Use utility class
                    lastX = x;
                    lastY = y;
                }

                if (i < adaptiveSteps)
                {
                    await Task.Delay(avgDelayMs);
                }
            }

            LowLevelInputSimulator.SendLowLevelMouseMove(endX, endY); // Use utility class

            _lastMousePosition.X = endX;
            _lastMousePosition.Y = endY;

            stopwatch.Stop();
        }

        private async Task SimulateRawMouseMovement(int deltaX, int deltaY, long timeSinceLastMoveMs, float velocityX, float velocityY)
        {
            const int maxMicroMove = 3;
            const int minDelayMs = 1;
            const int maxDelayMs = 8;

            double totalDistance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

            int numSteps = (int)Math.Max(5, Math.Min(totalDistance / maxMicroMove, 30));

            double stepX = (double)deltaX / numSteps;
            double stepY = (double)deltaY / numSteps;

            double stepDelayMs = Math.Clamp(timeSinceLastMoveMs / numSteps, minDelayMs, maxDelayMs);

            Stopwatch sw = Stopwatch.StartNew();
            double elapsedMs = 0;

            for (int i = 0; i < numSteps; i++)
            {
                int microDeltaX = (int)Math.Round(stepX);
                int microDeltaY = (int)Math.Round(stepY);

                LowLevelInputSimulator.SendRawMouseInput(microDeltaX, microDeltaY); // Use utility class

                double targetElapsedMs = stepDelayMs * (i + 1);
                double actualDelayMs = targetElapsedMs - elapsedMs;

                if (actualDelayMs > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(actualDelayMs));

                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }

            sw.Stop();
        }

        public void CancelSimulation()
        {
            cancel_simulation = true;
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
            // This uses keybd_event directly, keep it or refactor ExecuteWindowsCommand
            // to use LowLevelInputSimulator.SendKeyboardInput if preferred.
            keybd_event(volumeCommand, 0, 0, UIntPtr.Zero); // KEYEVENTF_KEYDOWN
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero); // KEYEVENTF_KEYUP
        }

        public async Task<List<ActionFile>> GetActionFilesAsync(string actionId)
        {
            try
            {
                if (string.IsNullOrEmpty(actionId))
                {
                    actionId = "unknown";
                }

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

        public async Task<List<DataItem>> LoadAllDataItemsAsync()
        {
            var result = new Dictionary<string, DataItem>();
            var debugInfo = new Dictionary<string, int> { { "backend", 0 }, { "local", 0 }, { "duplicates", 0 } };

            try
            {
                bool isOnlineMode = false;
                if (_appModeService != null)
                {
                    isOnlineMode = _appModeService.CurrentMode == AppMode.Online;
                }
                else
                {
                    Debug.WriteLine("WARNING: _appModeService is null, defaulting to offline mode");
                }

                if (isOnlineMode)
                {
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
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading backend items: {ex.Message}");
                        }
                    }
                }

                var localItems = await LoadDataItemsFromFile();
                debugInfo["local"] = localItems.Count;

                foreach (var item in localItems)
                {
                    if (item?.Data?.ActionGroupObject != null)
                    {
                        item.Data.ActionGroupObject.IsLocal = true;
                        string key = !string.IsNullOrEmpty(item._id)
                            ? "local_" + item._id
                            : "local_" + item.Data.ActionGroupObject.ActionName;

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

            string modeInfo = (_appModeService?.CurrentMode == AppMode.Online) ? "[ONLINE MODE]" : "[OFFLINE MODE]";
            Debug.WriteLine($"{modeInfo} Data summary: {debugInfo["backend"]} backend, {debugInfo["local"]} local, {debugInfo["duplicates"]} duplicates -> {result.Count} items");

            return result.Values.ToList();
        }

        public async Task<bool> SaveDataItemToBackendAsync(DataItem dataItem, string token)
        {
            if (_appModeService?.CurrentMode != AppMode.Online)
            {
                return true;
            }

            if (string.IsNullOrEmpty(token))
            {
                Debug.WriteLine("No token provided for backend save");
                return false;
            }

            try
            {
                var response = await _dataService.CreateDataAsync(dataItem, token);
                return response.DataIsSuccess;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving data item to backend: {ex.Message}");
                return false;
            }
        }

        public async Task RefreshDataAsync(ObservableCollection<DataItem> dataItems, ObservableCollection<ActionGroup> actionGroups, ObservableCollection<DataItem> localItems)
        {
            dataItems.Clear();
            actionGroups.Clear();
            localItems.Clear();

            var allDataItems = await LoadAllDataItemsAsync();

            foreach (var item in allDataItems)
            {
                dataItems.Add(item);
            }

            var allLocalItems = await LoadLocalDataItemsAsync();
            foreach (var item in allLocalItems)
            {
                localItems.Add(item);
            }
        }

        public string DetermineCategory(ActionGroup actionGroup)
        {
            string name = actionGroup.ActionName.ToLowerInvariant();
            string steps = actionGroup.ActionArray?.FirstOrDefault()?.ToString()?.ToLowerInvariant() ?? "";

            if (name.Contains("excel") || name.Contains("spreadsheet") || steps.Contains("excel"))
                return "Data Analysis";

            if (name.Contains("word") || name.Contains("document") || steps.Contains("word") || steps.Contains(".doc"))
                return "Document Editing";

            if (name.Contains("browser") || name.Contains("chrome") || name.Contains("firefox") || name.Contains("edge") || steps.Contains("browser"))
                return "Browser";

            if (name.Contains("file") || name.Contains("folder") || name.Contains("copy") || name.Contains("move"))
                return "File Management";

            if (name.Contains("email") || name.Contains("outlook") || name.Contains("teams") || name.Contains("slack"))
                return "Communication";

            if (name.Contains("code") || name.Contains("visual studio") || name.Contains("vs code") || steps.Contains("code"))
                return "Development";

            if (name.Contains("system") || name.Contains("settings") || name.Contains("control"))
                return "System";

            return "Productivity";
        }

        public string DetermineActionTypeFromSteps(ActionGroup actionGroup)
        {
            if (actionGroup.ActionArray == null || !actionGroup.ActionArray.Any())
                return "Unknown";

            int keyboardActions = 0;
            int mouseActions = 0;
            int applicationActions = 0;

            foreach (var action in actionGroup.ActionArray)
            {
                string actionStr = action.ToString().ToLowerInvariant();

                if (actionStr.Contains("key") || actionStr.Contains("type") || action.EventType == 256 || action.EventType == 257)
                    keyboardActions++;
                else if (actionStr.Contains("mouse") || actionStr.Contains("click") || action.EventType == 512 || action.EventType == 0x0201)
                    mouseActions++;
                else if (actionStr.Contains("launch") || actionStr.Contains("start") || actionStr.Contains("open"))
                    applicationActions++;
            }

            if (keyboardActions > mouseActions && keyboardActions > applicationActions)
                return "Keyboard Action";
            if (mouseActions > keyboardActions && mouseActions > applicationActions)
                return "Mouse Action";
            if (applicationActions > keyboardActions && applicationActions > mouseActions)
                return "Application Launch";

            if (keyboardActions > 0 && mouseActions > 0)
                return "Mixed Input";

            return "Custom Action";
        }
    }
}
