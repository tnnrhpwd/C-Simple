using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using CSimple.Services.AppModeService;
using CSimple.Models;

namespace CSimple.Services
{
    public class ActionService
    {
        private readonly DataService _dataService;
        private readonly FileService _fileService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private bool cancel_simulation = false;

        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

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
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

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

        private const uint MOUSEEVENTF_MOVE_NOCOALESCING = 0x2000;
        private const int REALIGN_EVERY_N_MOVEMENTS = 10;
        private bool _useRawMovement = true;

        private bool _leftButtonDown = false;
        private bool _rightButtonDown = false;
        private bool _middleButtonDown = false;
        private bool _isDragging = false;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const int INPUT_KEYBOARD = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion u;
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
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public bool UseInterpolation { get; set; } = true;
        public int MovementSteps { get; set; } = 25;
        public int MovementDelayMs { get; set; } = 1;
        public float GameSensitivityMultiplier { get; set; } = 1.0f;
        public bool UltraSmoothMode { get; set; } = true;
        public bool UseRawInput { get; set; } = true;

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
                        ParseDataItemText(dataItem);
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
                        ParseDataItemText(dataItem);

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
                    bool prevLeftButtonDown = false;
                    bool prevRightButtonDown = false;
                    bool prevMiddleButtonDown = false;

                    GetCursorPos(out POINT startPoint);
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
                            Debug.WriteLine($"Failed to parse Timestamp: {action.Timestamp}");
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
                            if (action.EventType == 0x0100)
                            {
                                pressedKeys[action.KeyCode] = true;
                                SendKeyboardInput((ushort)action.KeyCode, false);

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    SendKeyboardInput((ushort)action.KeyCode, true);
                                    pressedKeys.Remove(action.KeyCode);
                                }
                            }
                            else if (action.EventType == 0x0101)
                            {
                                SendKeyboardInput((ushort)action.KeyCode, true);
                                pressedKeys.Remove(action.KeyCode);
                            }
                            else if (action.EventType == 0x0201)
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
                                        SendLowLevelMouseMove(targetX, targetY);
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                SendLowLevelMouseClick(MouseButton.Left, false, targetX, targetY);
                                _leftButtonDown = true;

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    SendLowLevelMouseClick(MouseButton.Left, true, targetX, targetY);
                                    _leftButtonDown = false;
                                }
                            }
                            else if (action.EventType == 0x0202)
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
                                        SendLowLevelMouseMove(targetX, targetY);
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                SendLowLevelMouseClick(MouseButton.Left, true, targetX, targetY);
                                _leftButtonDown = false;
                            }
                            else if (action.EventType == 0x0204)
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
                                        SendLowLevelMouseMove(targetX, targetY);
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                SendLowLevelMouseClick(MouseButton.Right, false, targetX, targetY);
                                prevRightButtonDown = true;

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    SendLowLevelMouseClick(MouseButton.Right, true, targetX, targetY);
                                    prevRightButtonDown = false;
                                }
                            }
                            else if (action.EventType == 0x0205)
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
                                        SendLowLevelMouseMove(targetX, targetY);
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                SendLowLevelMouseClick(MouseButton.Right, true, targetX, targetY);
                                prevRightButtonDown = false;
                            }
                            else if (action.EventType == 0x0207)
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
                                        SendLowLevelMouseMove(targetX, targetY);
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                SendLowLevelMouseClick(MouseButton.Middle, false, targetX, targetY);
                                _middleButtonDown = true;

                                if (action.Duration > 0)
                                {
                                    await Task.Delay(action.Duration);
                                    SendLowLevelMouseClick(MouseButton.Middle, true, targetX, targetY);
                                    _middleButtonDown = false;
                                }
                            }
                            else if (action.EventType == 0x0208)
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
                                        SendLowLevelMouseMove(targetX, targetY);
                                    }
                                    currentX = targetX;
                                    currentY = targetY;
                                }

                                SendLowLevelMouseClick(MouseButton.Middle, true, targetX, targetY);
                                _middleButtonDown = false;
                            }
                            else if (action.EventType == 512 || action.EventType == 0x0200)
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
                                            SendLowLevelMouseMove(targetX, targetY);
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

                    foreach (var keyCode in pressedKeys.Keys.ToList())
                    {
                        SendKeyboardInput((ushort)keyCode, true);
                    }

                    if (_leftButtonDown)
                    {
                        SendLowLevelMouseClick(MouseButton.Left, true, currentX, currentY);
                        _leftButtonDown = false;
                    }
                    if (prevRightButtonDown)
                    {
                        SendLowLevelMouseClick(MouseButton.Right, true, currentX, currentY);
                        prevRightButtonDown = false;
                    }
                    if (prevMiddleButtonDown)
                    {
                        SendLowLevelMouseClick(MouseButton.Middle, true, currentX, currentY);
                        prevMiddleButtonDown = false;
                    }

                    _isDragging = false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during simulation: {ex.Message}");
                    Debug.WriteLine(ex.StackTrace);

                    try
                    {
                        GetCursorPos(out POINT currentPoint);
                        if (_leftButtonDown) SendLowLevelMouseClick(MouseButton.Left, true, currentPoint.X, currentPoint.Y);
                        if (_rightButtonDown) SendLowLevelMouseClick(MouseButton.Right, true, currentPoint.X, currentPoint.Y);
                        if (_middleButtonDown) SendLowLevelMouseClick(MouseButton.Middle, true, currentPoint.X, currentPoint.Y);
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
                        GetCursorPos(out POINT currentPos);
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
                    SendLowLevelMouseMove(x, y);
                    lastX = x;
                    lastY = y;
                }

                if (i < adaptiveSteps)
                {
                    await Task.Delay(avgDelayMs);
                }
            }

            SendLowLevelMouseMove(endX, endY);

            _lastMousePosition.X = endX;
            _lastMousePosition.Y = endY;

            stopwatch.Stop();
        }

        private void SendLowLevelMouseMove(int x, int y)
        {
            try
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;

                int screenWidth = GetSystemMetrics(SM_CXSCREEN);
                int screenHeight = GetSystemMetrics(SM_CYSCREEN);

                inputs[0].u.mi.dx = (x * 65535) / screenWidth;
                inputs[0].u.mi.dy = (y * 65535) / screenHeight;
                inputs[0].u.mi.mouseData = 0;
                inputs[0].u.mi.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
                inputs[0].u.mi.time = 0;
                inputs[0].u.mi.dwExtraInfo = IntPtr.Zero;

                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

                if (result != 1)
                {
                    Debug.WriteLine($"⚠️ SendInput failed for mouse move to ({x},{y}), error code: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendLowLevelMouseMove: {ex.Message}");
            }
        }

        private enum MouseButton
        {
            Left,
            Right,
            Middle
        }

        private void SendLowLevelMouseClick(MouseButton button, bool isUp, int x, int y)
        {
            try
            {
                SendLowLevelMouseMove(x, y);

                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_MOUSE;
                inputs[0].u.mi.dx = 0;
                inputs[0].u.mi.dy = 0;
                inputs[0].u.mi.mouseData = 0;
                inputs[0].u.mi.time = 0;
                inputs[0].u.mi.dwExtraInfo = IntPtr.Zero;

                switch (button)
                {
                    case MouseButton.Left:
                        inputs[0].u.mi.dwFlags = (uint)(isUp ? MOUSEEVENTF_LEFTUP : MOUSEEVENTF_LEFTDOWN);
                        break;
                    case MouseButton.Right:
                        inputs[0].u.mi.dwFlags = (uint)(isUp ? MOUSEEVENTF_RIGHTUP : MOUSEEVENTF_RIGHTDOWN);
                        break;
                    case MouseButton.Middle:
                        inputs[0].u.mi.dwFlags = (uint)(isUp ? MOUSEEVENTF_MIDDLEUP : MOUSEEVENTF_MIDDLEDOWN);
                        break;
                }

                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

                if (result != 1)
                {
                    Debug.WriteLine($"⚠️ SendInput failed for mouse {button} {(isUp ? "UP" : "DOWN")}, error code: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendLowLevelMouseClick: {ex.Message}");
            }
        }

        private void SendKeyboardInput(ushort key, bool isKeyUp)
        {
            try
            {
                INPUT[] inputs = new INPUT[1];
                inputs[0].type = INPUT_KEYBOARD;
                inputs[0].u.ki.wVk = key;
                inputs[0].u.ki.wScan = 0;

                if (IsExtendedKey(key))
                {
                    inputs[0].u.ki.dwFlags = isKeyUp ?
                        KEYEVENTF_KEYUP | KEYEVENTF_EXTENDEDKEY :
                        KEYEVENTF_KEYDOWN | KEYEVENTF_EXTENDEDKEY;
                }
                else
                {
                    inputs[0].u.ki.dwFlags = isKeyUp ? KEYEVENTF_KEYUP : KEYEVENTF_KEYDOWN;
                }

                inputs[0].u.ki.time = 0;
                inputs[0].u.ki.dwExtraInfo = IntPtr.Zero;

                uint result = SendInput(1, inputs, Marshal.SizeOf(typeof(INPUT)));

                if (result != 1)
                {
                    Debug.WriteLine($"⚠️ SendInput failed for key {key}, error code: {Marshal.GetLastWin32Error()}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in SendKeyboardInput: {ex.Message}");
            }
        }

        private bool IsExtendedKey(ushort vkCode)
        {
            return vkCode == 0x5B ||
                   vkCode == 0x5C ||
                   vkCode == 0x5D ||
                   vkCode == 0x23 ||
                   vkCode == 0x24 ||
                   vkCode == 0x25 ||
                   vkCode == 0x26 ||
                   vkCode == 0x27 ||
                   vkCode == 0x28 ||
                   vkCode == 0x2D ||
                   vkCode == 0x2E ||
                   vkCode == 0x21 ||
                   vkCode == 0x22 ||
                   vkCode == 0x90 ||
                   vkCode == 0x91 ||
                   vkCode == 0xA0 ||
                   vkCode == 0xA1 ||
                   vkCode == 0xA2 ||
                   vkCode == 0xA3 ||
                   vkCode == 0xA4 ||
                   vkCode == 0xA5;
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

                SendRawMouseInput(microDeltaX, microDeltaY);

                double targetElapsedMs = stepDelayMs * (i + 1);
                double actualDelayMs = targetElapsedMs - elapsedMs;

                if (actualDelayMs > 0)
                    await Task.Delay(TimeSpan.FromMilliseconds(actualDelayMs));

                elapsedMs = sw.Elapsed.TotalMilliseconds;
            }

            sw.Stop();
        }

        private void SendRawMouseInput(int deltaX, int deltaY)
        {
            INPUT[] inputs = new INPUT[1];
            inputs[0].type = INPUT_MOUSE;
            inputs[0].u.mi.dx = deltaX;
            inputs[0].u.mi.dy = deltaY;
            inputs[0].u.mi.mouseData = 0;
            inputs[0].u.mi.dwFlags = MOUSEEVENTF_MOVE | MOUSEEVENTF_MOVE_NOCOALESCING;
            inputs[0].u.mi.time = 0;
            inputs[0].u.mi.dwExtraInfo = IntPtr.Zero;

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
            keybd_event(volumeCommand, 0, 0x0002, UIntPtr.Zero);
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
