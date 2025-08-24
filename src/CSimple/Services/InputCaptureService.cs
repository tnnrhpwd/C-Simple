using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Windows.Forms; // Add this for Keys enum

namespace CSimple.Services
{
    public class InputCaptureService : IDisposable
    {
        #region Events
        public event Action<string> InputCaptured;
        public event Action<string> DebugMessageLogged;
        public event Action<string> TouchInputCaptured; // New event for touch input
        #endregion

        #region Properties
        private Dictionary<ushort, ActionItem> _activeKeyPresses = new Dictionary<ushort, ActionItem>();
        private Dictionary<ushort, DateTime> _keyPressDownTimestamps = new Dictionary<ushort, DateTime>();
        private DateTime _lastMouseEventTime = DateTime.MinValue;
        private bool _isActive = false;
        private bool _previewModeActive = false;
        private CancellationTokenSource _previewCts;
        private CancellationTokenSource _queueProcessingCts;

        // Concurrent queue for processing input events
        private BlockingCollection<ActionItem> _inputQueue;

        // Enhanced mouse movement handling for gaming
        private const int MOUSE_MOVEMENT_THROTTLE_MS = 1; // Reduced to 1ms (was 4ms)
        private DateTime _lastMouseMoveSent = DateTime.MinValue;
        private POINT _lastProcessedMousePos;
        private POINT _startMousePos; // Track start position for relative movements
        private bool _leftMouseDown = false;
        private bool _rightMouseDown = false;
        private bool _middleButtonDown = false;

        // Track raw mouse movement for more accurate replay
        private int _accumulatedDeltaX = 0;
        private int _accumulatedDeltaY = 0;
        private Stopwatch _mouseMoveTimer = new Stopwatch();
        private readonly object _inputQueueLock = new object();

        // Touch input tracking
        private Dictionary<int, TouchPoint> _activeTouches = new Dictionary<int, TouchPoint>();
        private DateTime _lastTouchEventTime = DateTime.MinValue;

        // Enhanced state tracking - prevent duplicates while ensuring all press/release events are captured
        private Dictionary<ushort, DateTime> _lastKeyDownEvents = new Dictionary<ushort, DateTime>();
        private Dictionary<ushort, DateTime> _lastKeyUpEvents = new Dictionary<ushort, DateTime>();
        private Dictionary<ushort, DateTime> _lastMouseButtonDownEvents = new Dictionary<ushort, DateTime>();
        private Dictionary<ushort, DateTime> _lastMouseButtonUpEvents = new Dictionary<ushort, DateTime>();

        // Minimum time between same key events to be considered unique (in milliseconds)
        private const int KEY_EVENT_DEBOUNCE_MS = 50;
        private const int MOUSE_BUTTON_DEBOUNCE_MS = 50;

        // State tracking for keys and buttons
        private HashSet<ushort> _currentlyPressedKeys = new HashSet<ushort>();
        private HashSet<ushort> _currentlyPressedButtons = new HashSet<ushort>();

        #endregion

        #region Constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207; // Middle button down
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONUP = 0x0208; // Middle button up
        private const int WM_MOUSEWHEEL = 0x020A; // Mouse wheel
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104; // Added for system keys (ALT, etc.)
        private const int WM_SYSKEYUP = 0x0105; // Added for system keys release

        // Touch input constants
        private const int WM_TOUCH = 0x0240;
        private const int WM_POINTERDOWN = 0x0246;
        private const int WM_POINTERUP = 0x0247;
        private const int WM_POINTERUPDATE = 0x0245;
        #endregion

        #region Windows API
#if WINDOWS
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc _keyboardProc;
        private LowLevelKeyboardProc _mouseProc;
        private IntPtr _keyboardHookID = IntPtr.Zero;
        private IntPtr _mouseHookID = IntPtr.Zero;
        private POINT _lastMousePos;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        // Required to get raw mouse data
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // Touch input structures
        [StructLayout(LayoutKind.Sequential)]
        private struct TOUCHINPUT
        {
            public int x;
            public int y;
            public IntPtr hSource;
            public int dwID;
            public int dwFlags;
            public int dwMask;
            public int dwTime;
            public IntPtr dwExtraInfo;
            public int cxContact;
            public int cyContact;
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterTouchWindow(IntPtr hwnd, uint ulFlags);

        [DllImport("user32.dll")]
        private static extern bool GetTouchInputInfo(IntPtr hTouchInput, uint cInputs, [Out] TOUCHINPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern bool CloseTouchInputHandle(IntPtr hTouchInput);
#endif
        #endregion

        // Enhanced ActionItem to store richer mouse data and touch data
        public class ActionItem
        {
            public ushort EventType { get; set; }
            public int KeyCode { get; set; }
            public DateTime Timestamp { get; set; }
            public Coordinates Coordinates { get; set; }
            public int DeltaX { get; set; } // For raw mouse movement
            public int DeltaY { get; set; } // For raw mouse movement
            public uint MouseData { get; set; } // For wheel and other mouse data
            public uint Flags { get; set; } // Special flags from raw input
            public bool IsLeftButtonDown { get; set; }
            public bool IsRightButtonDown { get; set; }
            public bool IsMiddleButtonDown { get; set; }
            public TimeSpan TimeSinceLastMove { get; set; } // For timing-accurate replay
            public float VelocityX { get; set; } // Mouse movement velocity
            public float VelocityY { get; set; } // Mouse movement velocity
            public int Duration { get; set; } // Add missing Duration property

            // Touch-specific properties
            public bool IsTouch { get; set; }
            public int TouchId { get; set; }
            public TouchAction TouchAction { get; set; }
            public int TouchWidth { get; set; }
            public int TouchHeight { get; set; }
            public float Pressure { get; set; } // Touch pressure if available
        }

        public enum TouchAction
        {
            None,
            Down,
            Move,
            Up,
            Cancel
        }

        // Class to track touch points
        private class TouchPoint
        {
            public int Id { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime LastUpdateTime { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
        }

        public class Coordinates
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int AbsoluteX { get; set; } // Absolute screen position
            public int AbsoluteY { get; set; } // Absolute screen position
            public int RelativeX { get; set; } // Position relative to starting point
            public int RelativeY { get; set; } // Position relative to starting point
        }

        // Add missing class definitions
        public class InputEvent { }

        public class MouseInputManager { }

        public class KeyboardInputManager { }

        // Add ActionService dependency
        private readonly ActionService _actionService;

        // Static flag to indicate cancellation via CTRL+SHIFT+ESC
        public static bool SimulationCancelledByTaskManager { get; internal set; } = false;

        public InputCaptureService(ActionService actionService)
        {
            // Initialize the queue
            ResetInputQueue();
            _mouseMoveTimer.Start();
            _actionService = actionService;
        }

        // Create a method to reset the input queue
        private void ResetInputQueue()
        {
            _inputQueue?.Dispose();
            _inputQueue = new BlockingCollection<ActionItem>();
            _queueProcessingCts?.Cancel();
            _queueProcessingCts = new CancellationTokenSource();

            // Start a consumer task to process input events
            Task.Run(() => ProcessInputQueue(_queueProcessingCts.Token));
        }

        public void StartCapturing()
        {
#if WINDOWS
            if (!_isActive)
            {
                // Reset the input queue when starting a new capture session
                ResetInputQueue();

                // Reset mouse state tracking
                _leftMouseDown = false;
                _rightMouseDown = false;
                _middleButtonDown = false;
                _accumulatedDeltaX = 0;
                _accumulatedDeltaY = 0;
                _mouseMoveTimer.Restart();

                // Reset touch tracking
                _activeTouches.Clear();
                _lastTouchEventTime = DateTime.MinValue;

                // Create distinct delegates to avoid GC issues
                _keyboardProc = KeyboardHookCallback;
                _mouseProc = MouseHookCallback;

                // Set hooks with explicit error handling
                _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                if (_keyboardHookID == IntPtr.Zero)
                {
                    Debug.Print($"Failed to set keyboard hook. Error: {Marshal.GetLastWin32Error()}");
                }

                _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
                GetCursorPos(out _lastMousePos);
                _startMousePos = _lastMousePos; // Save start position for relative tracking
                _isActive = true;
                Debug.Print("Input capture started - Keyboard hook status: " + (_keyboardHookID != IntPtr.Zero ? "Active" : "Failed"));

                // Start a high-frequency mouse movement tracker
                Task.Run(() => TrackMouseMovements());

                // Register for touch input on the window
                RegisterForTouchInput();
            }
#endif
        }

#if WINDOWS
        private void RegisterForTouchInput()
        {
            // This method would register the current window for touch input
            // Get the current window handle - this is simplified and would need to be implemented
            var hwnd = GetActiveWindow();
            if (hwnd != IntPtr.Zero)
            {
                // Register window to receive touch input
                RegisterTouchWindow(hwnd, 0);
                Debug.Print("Registered window for touch input");
            }
        }

        // Get active window handle - placeholder implementation
        private IntPtr GetActiveWindow()
        {
            // In actual implementation, this would use P/Invoke to get the active window handle
            return IntPtr.Zero;
        }

        // Process touch input - to be called from the window procedure
        public void ProcessTouchInput(IntPtr lParam, int numInputs)
        {
            TOUCHINPUT[] inputs = new TOUCHINPUT[numInputs];

            if (!GetTouchInputInfo(lParam, (uint)numInputs, inputs, Marshal.SizeOf(typeof(TOUCHINPUT))))
            {
                Debug.Print("Failed to get touch input info");
                return;
            }

            try
            {
                for (int i = 0; i < numInputs; i++)
                {
                    var input = inputs[i];

                    // Determine touch action
                    TouchAction action = TouchAction.None;
                    if ((input.dwFlags & 0x0001) != 0) // TOUCHEVENTF_DOWN
                        action = TouchAction.Down;
                    else if ((input.dwFlags & 0x0002) != 0) // TOUCHEVENTF_UP
                        action = TouchAction.Up;
                    else if ((input.dwFlags & 0x0004) != 0) // TOUCHEVENTF_MOVE
                        action = TouchAction.Move;

                    // Create action item for the touch event
                    var actionItem = new ActionItem
                    {
                        EventType = (ushort)(WM_TOUCH + (int)action), // Use custom event type
                        Timestamp = DateTime.UtcNow,
                        IsTouch = true,
                        TouchId = input.dwID,
                        TouchAction = action,
                        TouchWidth = input.cxContact,
                        TouchHeight = input.cyContact,
                        Coordinates = new Coordinates
                        {
                            X = input.x,
                            Y = input.y,
                            AbsoluteX = input.x,
                            AbsoluteY = input.y
                        },
                        TimeSinceLastMove = DateTime.UtcNow - _lastTouchEventTime
                    };

                    // Update touch tracking
                    if (action == TouchAction.Down)
                    {
                        _activeTouches[input.dwID] = new TouchPoint
                        {
                            Id = input.dwID,
                            X = input.x,
                            Y = input.y,
                            StartTime = DateTime.UtcNow,
                            LastUpdateTime = DateTime.UtcNow,
                            Width = input.cxContact,
                            Height = input.cyContact
                        };
                    }
                    else if (action == TouchAction.Move && _activeTouches.ContainsKey(input.dwID))
                    {
                        var touchPoint = _activeTouches[input.dwID];
                        actionItem.DeltaX = input.x - touchPoint.X;
                        actionItem.DeltaY = input.y - touchPoint.Y;

                        // Update the stored touch point
                        touchPoint.X = input.x;
                        touchPoint.Y = input.y;
                        touchPoint.LastUpdateTime = DateTime.UtcNow;
                        touchPoint.Width = input.cxContact;
                        touchPoint.Height = input.cyContact;
                    }
                    else if (action == TouchAction.Up && _activeTouches.ContainsKey(input.dwID))
                    {
                        _activeTouches.Remove(input.dwID);
                    }

                    _lastTouchEventTime = DateTime.UtcNow;

                    // Add to queue for processing
                    AddToInputQueue(actionItem);
                }
            }
            finally
            {
                CloseTouchInputHandle(lParam);
            }
        }
#endif

        public void StopCapturing()
        {
#if WINDOWS
            if (_isActive)
            {
                _isActive = false; // Set _isActive to false first

                // Store final mouse state before stopping
                if (_leftMouseDown || _rightMouseDown || _middleButtonDown)
                {
                    // Create final mouse up events if mouse buttons were down at stop
                    RecordFinalMouseState();
                }

                if (_keyboardHookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHookID);
                    _keyboardHookID = IntPtr.Zero;
                }
                if (_mouseHookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookID);
                    _mouseHookID = IntPtr.Zero;
                }

                _activeKeyPresses.Clear();
                _keyPressDownTimestamps.Clear();
                Debug.Print("Input capture stopped");

                // Complete the input queue to signal the consumer to stop
                try
                {
                    _inputQueue?.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                    Debug.Print("Input queue was already disposed.");
                }
            }
#endif
        }

        private void RecordFinalMouseState()
        {
            GetCursorPos(out POINT currentPos);

            // Create proper mouse up events for any buttons still pressed
            if (_leftMouseDown)
            {
                var actionItem = new ActionItem
                {
                    EventType = WM_LBUTTONUP,
                    Coordinates = CreateCoordinates(currentPos),
                    Timestamp = DateTime.UtcNow,
                    IsLeftButtonDown = false,
                    IsRightButtonDown = _rightMouseDown,
                    IsMiddleButtonDown = _middleButtonDown
                };
                AddToInputQueue(actionItem);
            }

            if (_rightMouseDown)
            {
                var actionItem = new ActionItem
                {
                    EventType = WM_RBUTTONUP,
                    Coordinates = CreateCoordinates(currentPos),
                    Timestamp = DateTime.UtcNow,
                    IsLeftButtonDown = false,
                    IsRightButtonDown = false,
                    IsMiddleButtonDown = _middleButtonDown
                };
                AddToInputQueue(actionItem);
            }

            if (_middleButtonDown)
            {
                var actionItem = new ActionItem
                {
                    EventType = WM_MBUTTONUP,
                    Coordinates = CreateCoordinates(currentPos),
                    Timestamp = DateTime.UtcNow,
                    IsLeftButtonDown = false,
                    IsRightButtonDown = false,
                    IsMiddleButtonDown = false
                };
                AddToInputQueue(actionItem);
            }
        }

        private Coordinates CreateCoordinates(POINT currentPos)
        {
            return new Coordinates
            {
                X = currentPos.X,
                Y = currentPos.Y,
                AbsoluteX = currentPos.X,
                AbsoluteY = currentPos.Y,
                RelativeX = currentPos.X - _startMousePos.X,
                RelativeY = currentPos.Y - _startMousePos.Y
            };
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc, int hookType)
        {
            using (var curProcess = Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(hookType, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    // Immediately grab event details
                    int wParamInt = wParam.ToInt32();
                    int vkCode = Marshal.ReadInt32(lParam);
                    ushort keyCode = (ushort)vkCode;
                    bool isKeyDown = wParamInt == WM_KEYDOWN || wParamInt == WM_SYSKEYDOWN;
                    bool isKeyUp = wParamInt == WM_KEYUP || wParamInt == WM_SYSKEYUP;
                    DateTime now = DateTime.UtcNow;

                    // Check for CTRL+SHIFT+ESC key combination
                    bool isCtrlPressed = (Control.ModifierKeys & Keys.Control) != 0;
                    bool isShiftPressed = (Control.ModifierKeys & Keys.Shift) != 0;
                    bool isEscPressed = keyCode == 27; // VK_ESCAPE = 0x1B = 27

                    if (isCtrlPressed && isShiftPressed && isEscPressed && isKeyDown)
                    {
                        // Cancel the simulation
                        _actionService?.CancelSimulation();
                        Debug.Print("CTRL+SHIFT+ESC detected - Simulation cancelled.");

                        // Set the static flag
                        SimulationCancelledByTaskManager = true;
                    }

                    // Skip recording if this is a duplicate key event (key is already in desired state)
                    if ((isKeyDown && _currentlyPressedKeys.Contains(keyCode)) ||
                        (isKeyUp && !_currentlyPressedKeys.Contains(keyCode)))
                    {
                        // This is a duplicate event (OS repeating key down/up) - ignore it
                        return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
                    }

                    // Update key state
                    if (isKeyDown)
                    {
                        _currentlyPressedKeys.Add(keyCode);
                        _keyPressDownTimestamps[keyCode] = now;
                        Debug.Print($"KEY DOWN: {vkCode:X2} ({(Keys)vkCode}) - added to pressed keys");
                    }
                    else if (isKeyUp)
                    {
                        _currentlyPressedKeys.Remove(keyCode);
                        Debug.Print($"KEY UP: {vkCode:X2} ({(Keys)vkCode}) - removed from pressed keys");
                    }

                    // Create the action item regardless of event type
                    var actionItem = new ActionItem
                    {
                        Timestamp = now,
                        KeyCode = vkCode,
                        EventType = (ushort)wParamInt,
                        IsLeftButtonDown = _leftMouseDown,
                        IsRightButtonDown = _rightMouseDown,
                        IsMiddleButtonDown = _middleButtonDown
                    };

                    // Add duration for key up events
                    if (isKeyUp && _keyPressDownTimestamps.TryGetValue(keyCode, out DateTime downTime))
                    {
                        actionItem.Duration = (int)(now - downTime).TotalMilliseconds;
                        _keyPressDownTimestamps.Remove(keyCode);
                    }

                    // Add rich data for accurate replay
                    GetCursorPos(out POINT curPos);
                    actionItem.Coordinates = new Coordinates
                    {
                        X = curPos.X,
                        Y = curPos.Y,
                        AbsoluteX = curPos.X,
                        AbsoluteY = curPos.Y
                    };

                    // Add to input queue
                    AddToInputQueue(actionItem);

                    // Log the event for debugging/monitoring
                    string eventName = isKeyDown ? (wParamInt == WM_KEYDOWN ? "WM_KEYDOWN" : "WM_SYSKEYDOWN") :
                                      isKeyUp ? (wParamInt == WM_KEYUP ? "WM_KEYUP" : "WM_SYSKEYUP") : "UNKNOWN";
                    Debug.Print($"KEYBOARD: {eventName} Key: {vkCode} (0x{vkCode:X2}) ({(Keys)vkCode}) at ({curPos.X},{curPos.Y})");
                }
                catch (Exception ex)
                {
                    Debug.Print($"Error in keyboard hook: {ex.Message}");
                }
            }

            // Always pass the event to the next hook
            return CallNextHookEx(_keyboardHookID, nCode, wParam, lParam);
        }

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                try
                {
                    int wParamInt = wParam.ToInt32();
                    bool isButtonEvent = IsMouseButtonEvent(wParamInt);
                    bool isMouseMove = wParamInt == WM_MOUSEMOVE;
                    DateTime now = DateTime.UtcNow;

                    // Process mouse data
                    MSLLHOOKSTRUCT mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    // Create action item with the current mouse state
                    var actionItem = new ActionItem
                    {
                        Timestamp = now,
                        EventType = (ushort)wParamInt,
                        IsLeftButtonDown = _leftMouseDown,
                        IsRightButtonDown = _rightMouseDown,
                        IsMiddleButtonDown = _middleButtonDown,
                        Coordinates = new Coordinates
                        {
                            X = mouseHookStruct.pt.X,
                            Y = mouseHookStruct.pt.Y,
                            AbsoluteX = mouseHookStruct.pt.X,
                            AbsoluteY = mouseHookStruct.pt.Y,
                            RelativeX = mouseHookStruct.pt.X - _startMousePos.X,
                            RelativeY = mouseHookStruct.pt.Y - _startMousePos.Y
                        },
                        MouseData = mouseHookStruct.mouseData,
                        Flags = mouseHookStruct.flags
                    };

                    // Check for button events
                    bool actionNeeded = true;
                    ushort buttonCode = 0;
                    DateTime downTime = DateTime.MinValue;

                    switch (wParamInt)
                    {
                        case WM_LBUTTONDOWN:
                            // Skip if already down (duplicate)
                            if (_leftMouseDown)
                            {
                                actionNeeded = false;
                                Debug.Print($"Skipping duplicate Left button DOWN at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            else
                            {
                                _leftMouseDown = true;
                                _currentlyPressedButtons.Add((ushort)WM_LBUTTONDOWN);
                                _keyPressDownTimestamps[(ushort)WM_LBUTTONDOWN] = now;
                                Debug.Print($"Left button DOWN at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            break;

                        case WM_LBUTTONUP:
                            // Skip if already up (duplicate)
                            if (!_leftMouseDown)
                            {
                                actionNeeded = false;
                                Debug.Print($"Skipping duplicate Left button UP at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            else
                            {
                                _leftMouseDown = false;
                                _currentlyPressedButtons.Remove((ushort)WM_LBUTTONDOWN);
                                if (_keyPressDownTimestamps.TryGetValue((ushort)WM_LBUTTONDOWN, out downTime))
                                {
                                    actionItem.Duration = (int)(now - downTime).TotalMilliseconds;
                                    _keyPressDownTimestamps.Remove((ushort)WM_LBUTTONDOWN);
                                }
                                Debug.Print($"Left button UP at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            break;

                        case WM_RBUTTONDOWN:
                            if (_rightMouseDown)
                            {
                                actionNeeded = false;
                                Debug.Print($"Skipping duplicate Right button DOWN at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            else
                            {
                                _rightMouseDown = true;
                                _currentlyPressedButtons.Add((ushort)WM_RBUTTONDOWN);
                                _keyPressDownTimestamps[(ushort)WM_RBUTTONDOWN] = now;
                                Debug.Print($"Right button DOWN at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            break;

                        case WM_RBUTTONUP:
                            if (!_rightMouseDown)
                            {
                                actionNeeded = false;
                                Debug.Print($"Skipping duplicate Right button UP at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            else
                            {
                                _rightMouseDown = false;
                                _currentlyPressedButtons.Remove((ushort)WM_RBUTTONDOWN);
                                if (_keyPressDownTimestamps.TryGetValue((ushort)WM_RBUTTONDOWN, out downTime))
                                {
                                    actionItem.Duration = (int)(now - downTime).TotalMilliseconds;
                                    _keyPressDownTimestamps.Remove((ushort)WM_RBUTTONDOWN);
                                }
                                Debug.Print($"Right button UP at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            break;

                        case WM_MBUTTONDOWN:
                            if (_middleButtonDown)
                            {
                                actionNeeded = false;
                                Debug.Print($"Skipping duplicate Middle button DOWN at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            else
                            {
                                _middleButtonDown = true;
                                _currentlyPressedButtons.Add((ushort)WM_MBUTTONDOWN);
                                _keyPressDownTimestamps[(ushort)WM_MBUTTONDOWN] = now;
                                Debug.Print($"Middle button DOWN at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            break;

                        case WM_MBUTTONUP:
                            if (!_middleButtonDown)
                            {
                                actionNeeded = false;
                                Debug.Print($"Skipping duplicate Middle button UP at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            else
                            {
                                _middleButtonDown = false;
                                _currentlyPressedButtons.Remove((ushort)WM_MBUTTONDOWN);
                                if (_keyPressDownTimestamps.TryGetValue((ushort)WM_MBUTTONDOWN, out downTime))
                                {
                                    actionItem.Duration = (int)(now - downTime).TotalMilliseconds;
                                    _keyPressDownTimestamps.Remove((ushort)WM_MBUTTONDOWN);
                                }
                                Debug.Print($"Middle button UP at ({mouseHookStruct.pt.X}, {mouseHookStruct.pt.Y})");
                            }
                            break;

                        case WM_MOUSEMOVE:
                            // Calculate delta for more precise movement recording
                            actionItem.DeltaX = mouseHookStruct.pt.X - _lastMousePos.X;
                            actionItem.DeltaY = mouseHookStruct.pt.Y - _lastMousePos.Y;

                            // Skip tiny movements - they're often noise
                            if (Math.Abs(actionItem.DeltaX) < 2 && Math.Abs(actionItem.DeltaY) < 2)
                            {
                                actionNeeded = false;
                            }

                            // Always update the last position regardless of whether we queue the event
                            _lastMousePos = mouseHookStruct.pt;
                            _lastMouseEventTime = now;
                            break;
                    }

                    // Only add to queue if action is needed (not a duplicate)
                    if (actionNeeded)
                    {
                        AddToInputQueue(actionItem);
                        // Debug.Print($"Adding to queue: Mouse EventType={wParamInt}, ActionNeeded={actionNeeded}");
                    }
                    else
                    {
                        // Debug.Print($"Skipping queue add: Mouse EventType={wParamInt}, ActionNeeded={actionNeeded}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print($"Error in mouse hook: {ex.Message}");
                }
            }

            return CallNextHookEx(_mouseHookID, nCode, wParam, lParam);
        }

        // Helper methods to identify mouse button events
        private bool IsMouseButtonEvent(int eventType)
        {
            return eventType == WM_LBUTTONDOWN || eventType == WM_LBUTTONUP ||
                   eventType == WM_RBUTTONDOWN || eventType == WM_RBUTTONUP ||
                   eventType == WM_MBUTTONDOWN || eventType == WM_MBUTTONUP;
        }

        private bool IsMouseButtonDownEvent(int eventType)
        {
            return eventType == WM_LBUTTONDOWN || eventType == WM_RBUTTONDOWN || eventType == WM_MBUTTONDOWN;
        }

        public void Dispose()
        {
            StopCapturing();
            _queueProcessingCts?.Cancel();
            _inputQueue?.Dispose();
            _inputQueue = null;
            _queueProcessingCts?.Dispose();
            _queueProcessingCts = null;
            _previewCts?.Dispose();
            _previewCts = null;
        }

        private void AddToInputQueue(ActionItem actionItem)
        {
            if (_inputQueue != null && !_inputQueue.IsAddingCompleted)
            {
                try
                {
                    bool isKeyboardEvent = actionItem.EventType == WM_KEYDOWN ||
                                           actionItem.EventType == WM_KEYUP ||
                                           actionItem.EventType == WM_SYSKEYDOWN ||
                                           actionItem.EventType == WM_SYSKEYUP;

                    if (isKeyboardEvent)
                    {
                        bool isKeyDown = actionItem.EventType == WM_KEYDOWN || actionItem.EventType == WM_SYSKEYDOWN;
                        Debug.Print($"Queue: Adding key {(isKeyDown ? "DOWN" : "UP")} event for key {actionItem.KeyCode}");
                    }

                    _inputQueue.Add(actionItem);
                }
                catch (InvalidOperationException)
                {
                    // The collection has been marked as complete
                    Debug.Print("Queue is marked complete and cannot accept new items.");
                }
            }
            else
            {
                Debug.Print("Queue is null or completed - could not add item.");
            }
        }

        private async Task TrackMouseMovements()
        {
            Stopwatch frameTimer = new Stopwatch();
            frameTimer.Start();
            POINT lastReportedPos = new POINT();
            GetCursorPos(out lastReportedPos);

            while (_isActive)
            {
                try
                {
                    GetCursorPos(out POINT currentMousePos);

                    // Detect movement with higher precision for gaming
                    bool positionChanged = currentMousePos.X != lastReportedPos.X ||
                                           currentMousePos.Y != lastReportedPos.Y;

                    // For tracking every movement, we don't use the time threshold
                    // This ensures we capture all movements regardless of timing
                    if (positionChanged) // Removed time threshold check
                    {
                        // Calculate delta movement
                        int deltaX = currentMousePos.X - lastReportedPos.X;
                        int deltaY = currentMousePos.Y - lastReportedPos.Y;

                        TimeSpan timeDelta = _mouseMoveTimer.Elapsed;
                        _mouseMoveTimer.Restart();

                        // Calculate velocity for more accurate replay
                        float velocityX = timeDelta.TotalSeconds > 0 ? (float)(deltaX / timeDelta.TotalSeconds) : 0;
                        float velocityY = timeDelta.TotalSeconds > 0 ? (float)(deltaY / timeDelta.TotalSeconds) : 0;

                        var actionItem = new ActionItem
                        {
                            EventType = WM_MOUSEMOVE,
                            Coordinates = new Coordinates
                            {
                                X = currentMousePos.X,
                                Y = currentMousePos.Y,
                                AbsoluteX = currentMousePos.X,
                                AbsoluteY = currentMousePos.Y,
                                RelativeX = currentMousePos.X - _startMousePos.X,
                                RelativeY = currentMousePos.Y - _startMousePos.Y
                            },
                            DeltaX = deltaX,
                            DeltaY = deltaY,
                            Timestamp = DateTime.UtcNow,
                            IsLeftButtonDown = _leftMouseDown,
                            IsRightButtonDown = _rightMouseDown,
                            IsMiddleButtonDown = _middleButtonDown,
                            TimeSinceLastMove = DateTime.UtcNow - _lastMouseMoveSent,
                            VelocityX = velocityX,
                            VelocityY = velocityY
                        };

                        lastReportedPos = currentMousePos;
                        _lastMouseMoveSent = DateTime.UtcNow;
                        _lastProcessedMousePos = currentMousePos;

                        AddToInputQueue(actionItem);
                    }

                    // Use fixed minimal delay to maximize sampling rate
                    // Minimal delay of 1ms to prevent CPU overuse but still capture at ~1000Hz
                    await Task.Delay(1);
                }
                catch (Exception ex)
                {
                    Debug.Print($"Error tracking mouse movements: {ex.Message}");
                    await Task.Delay(5); // Short delay on error
                }
            }

            frameTimer.Stop();
        }

        #region Model Conversion Methods

        /// <summary>
        /// Converts an internal ActionItem to a CSimple.ActionItem for storage
        /// </summary>
        public CSimple.ActionItem ConvertToModelActionItem(ActionItem item)
        {
            if (item == null) return null;

            var modelItem = new CSimple.ActionItem
            {
                Timestamp = item.Timestamp,
                EventType = item.EventType,
                KeyCode = item.KeyCode,

                // Copy enhanced mouse data
                DeltaX = item.DeltaX,
                DeltaY = item.DeltaY,
                MouseData = item.MouseData,
                Flags = item.Flags,
                IsLeftButtonDown = item.IsLeftButtonDown,
                IsRightButtonDown = item.IsRightButtonDown,
                IsMiddleButtonDown = item.IsMiddleButtonDown,
                TimeSinceLastMoveMs = (long)item.TimeSinceLastMove.TotalMilliseconds,
                VelocityX = item.VelocityX,
                VelocityY = item.VelocityY,

                // Copy touch-specific data
                IsTouch = item.IsTouch,
                TouchId = item.TouchId,
                TouchAction = (int)item.TouchAction,
                TouchWidth = item.TouchWidth,
                TouchHeight = item.TouchHeight,
                Pressure = item.Pressure
            };

            // Handle coordinates conversion
            if (item.Coordinates != null)
            {
                modelItem.Coordinates = new CSimple.Coordinates
                {
                    X = item.Coordinates.X,
                    Y = item.Coordinates.Y,
                    AbsoluteX = item.Coordinates.AbsoluteX,
                    AbsoluteY = item.Coordinates.AbsoluteY,
                    RelativeX = item.Coordinates.RelativeX,
                    RelativeY = item.Coordinates.RelativeY
                };
            }

            return modelItem;
        }

        /// <summary>
        /// Bulk converts a collection of ActionItems for storage in ActionGroup
        /// </summary>
        public List<CSimple.ActionItem> ConvertToModelActionItems(IEnumerable<ActionItem> items)
        {
            var result = new List<CSimple.ActionItem>();

            if (items == null)
                return result;

            foreach (var item in items)
            {
                var converted = ConvertToModelActionItem(item);
                if (converted != null)
                {
                    result.Add(converted);
                }
            }

            return result;
        }

        /// <summary>
        /// Processes captured input data and adds it to an ActionGroup
        /// </summary>
        public void ProcessInputDataToActionGroup(CSimple.Models.ActionGroup actionGroup, ActionItem[] capturedInputs)
        {
            if (actionGroup == null || capturedInputs == null || capturedInputs.Length == 0)
                return;

            // Log the number of captured inputs for diagnostics
            Debug.Print($"Processing {capturedInputs.Length} captured inputs to action group");

            // Count mouse button events for debugging
            int mouseButtonEvents = capturedInputs.Count(i =>
                i.EventType == WM_LBUTTONDOWN || i.EventType == WM_LBUTTONUP ||
                i.EventType == WM_RBUTTONDOWN || i.EventType == WM_RBUTTONUP ||
                i.EventType == WM_MBUTTONDOWN || i.EventType == WM_MBUTTONUP);

            Debug.Print($"Found {mouseButtonEvents} mouse button events to process");

            // Convert the captured inputs to model items
            var modelItems = ConvertToModelActionItems(capturedInputs);

            // Add to the action group
            if (actionGroup.ActionArray == null)
                actionGroup.ActionArray = new List<CSimple.Models.ActionItem>();

            actionGroup.ActionArray.AddRange(modelItems.Select(item => new CSimple.Models.ActionItem
            {
                Timestamp = item.Timestamp,
                EventType = item.EventType,
                KeyCode = item.KeyCode,
                DeltaX = item.DeltaX,
                DeltaY = item.DeltaY,
                MouseData = item.MouseData,
                Flags = item.Flags,
                IsLeftButtonDown = item.IsLeftButtonDown,
                IsRightButtonDown = item.IsRightButtonDown,
                IsMiddleButtonDown = item.IsMiddleButtonDown,
                TimeSinceLastMoveMs = item.TimeSinceLastMoveMs,
                VelocityX = item.VelocityX,
                VelocityY = item.VelocityY,
                // Ensure coordinates are properly copied
                Coordinates = item.Coordinates != null ? new CSimple.Models.Coordinates
                {
                    X = item.Coordinates.X,
                    Y = item.Coordinates.Y,
                    AbsoluteX = item.Coordinates.AbsoluteX,
                    AbsoluteY = item.Coordinates.AbsoluteY,
                    RelativeX = item.Coordinates.RelativeX,
                    RelativeY = item.Coordinates.RelativeY
                } : null,
                // Ensure touch data is properly copied
                IsTouch = item.IsTouch,
                TouchId = item.TouchId,
                TouchAction = item.TouchAction,
                TouchWidth = item.TouchWidth,
                TouchHeight = item.TouchHeight,
                Pressure = item.Pressure
            }));

            // Log confirmation that events were added
            Debug.Print($"Added {modelItems.Count} items to action group");
        }

        #endregion

        private void ProcessInputQueue(CancellationToken cancellationToken)
        {
            try
            {
                // Increase batch size for mouse movements
                const int batchSize = 30; // Increased from 15 to 30
                List<ActionItem> batch = new List<ActionItem>(batchSize);
                Stopwatch batchTimer = new Stopwatch();

                // Continue processing until cancellation is requested or queue is completed
                while (!cancellationToken.IsCancellationRequested && (_inputQueue != null && !_inputQueue.IsCompleted))
                {
                    try
                    {
                        batch.Clear();
                        int count = 0;
                        batchTimer.Restart();

                        // Try to take multiple items at once with shorter timeout
                        while (count < batchSize && _inputQueue.TryTake(out ActionItem item, 10))
                        {
                            if (item != null)
                            {
                                batch.Add(item);
                                count++;

                                // Debug log for key events to track what's being processed
                                if (item.EventType == WM_KEYDOWN || item.EventType == WM_SYSKEYDOWN)
                                {
                                    Debug.Print($"Recorded keyboard event: DOWN KeyCode: {item.KeyCode}");
                                }
                                else if (item.EventType == WM_KEYUP || item.EventType == WM_SYSKEYUP)
                                {
                                    Debug.Print($"Recorded keyboard event: UP KeyCode: {item.KeyCode}");
                                }
                                // Add explicit logging for mouse button events
                                else if (item.EventType == WM_LBUTTONDOWN)
                                {
                                    Debug.Print($"Recorded left mouse DOWN event at ({item.Coordinates?.X}, {item.Coordinates?.Y})");
                                }
                                else if (item.EventType == WM_LBUTTONUP)
                                {
                                    Debug.Print($"Recorded left mouse UP event at ({item.Coordinates?.X}, {item.Coordinates?.Y})");
                                }
                                else if (item.EventType == WM_RBUTTONDOWN || item.EventType == WM_RBUTTONUP ||
                                         item.EventType == WM_MBUTTONDOWN || item.EventType == WM_MBUTTONUP)
                                {
                                    Debug.Print($"Recorded mouse button event: {item.EventType} at ({item.Coordinates?.X}, {item.Coordinates?.Y})");
                                }
                            }
                        }

                        // Process the batch efficiently
                        if (count > 0)
                        {
                            // Use direct invocation for efficiency - avoid foreach
                            for (int i = 0; i < batch.Count; i++)
                            {
                                string inputJson = JsonConvert.SerializeObject(batch[i]);
                                InputCaptured?.Invoke(inputJson);
                            }

                            // Add adaptive throttling for very high-frequency events to prevent CPU overload
                            if (batchTimer.ElapsedMilliseconds < 1 && batch.Count == batchSize)
                            {
                                // If we're processing batches extremely quickly, add a tiny delay
                                Thread.Sleep(1);
                            }
                        }
                        else
                        {
                            // Shorter sleep if no items - don't block too long
                            Thread.Sleep(1);
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // This exception is expected when CompleteAdding is called
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.Print($"Error processing input item: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Debug.Print("Input queue processing cancelled.");
            }
            catch (ObjectDisposedException)
            {
                // Expected when the queue is disposed during shutdown
                Debug.Print("Input queue processing stopped due to object disposed exception.");
            }
            catch (Exception ex)
            {
                Debug.Print($"Error processing input queue: {ex.Message}");
            }
            finally
            {
                Debug.Print("Input queue processing completed.");
            }
        }

        // Add a dedicated keyboard event handler method for clarity 
        public void ProcessKeyboardEvent(ActionItem keyboardEvent)
        {
            // Ensure we properly record both key down and key up events
            bool isKeyDown = keyboardEvent.EventType == WM_KEYDOWN || keyboardEvent.EventType == WM_SYSKEYDOWN;
            string eventType = isKeyDown ? "DOWN" : "UP";

            Debug.Print($"Processing keyboard event: {eventType} KeyCode: {keyboardEvent.KeyCode}");

            // Convert to JSON and invoke the event
            string inputJson = JsonConvert.SerializeObject(keyboardEvent);
            InputCaptured?.Invoke(inputJson);
        }

        public void StartPreviewMode()
        {
            _previewModeActive = true;
            _previewCts = new CancellationTokenSource();

            // Start monitoring input for preview
            Task.Run(() => MonitorInputForPreview(_previewCts.Token));

            DebugMessageLogged?.Invoke("Input capture preview mode started");
        }

        public void StopPreviewMode()
        {
            _previewModeActive = false;
            Debug.WriteLine("[InputCaptureService] StopPreviewMode - Calling _previewCts?.Cancel()");
            _previewCts?.Cancel();
            _previewCts = null;

            DebugMessageLogged?.Invoke("Input capture preview mode stopped");
        }

        private async Task MonitorInputForPreview(CancellationToken token)
        {
            try
            {
                // This is a placeholder for input monitoring in preview mode
                // In a real implementation, you would monitor actual inputs without recording

                while (!token.IsCancellationRequested && _previewModeActive)
                {
                    // Simulated delay between input checks
                    await Task.Delay(100, token);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                DebugMessageLogged?.Invoke($"Error in input preview monitoring: {ex.Message}");
            }
        }

        public string GetActiveInputsDisplay()
        {
            var activeInputsDisplay = new StringBuilder();
            activeInputsDisplay.AppendLine("Active Key/Mouse Presses:");

            foreach (var kvp in _activeKeyPresses)
            {
                activeInputsDisplay.AppendLine($"KeyCode/MouseCode: {kvp.Key}");
            }

            return activeInputsDisplay.ToString();
        }

        public int GetActiveKeyCount()
        {
            return _activeKeyPresses.Count;
        }

        // Add diagnostic method to help troubleshoot key capture issues
        public void DiagnoseKeyboardCapture()
        {
            Debug.Print($"Keyboard hook status: {(_keyboardHookID != IntPtr.Zero ? "Active" : "Inactive")}");
            Debug.Print($"Mouse hook status: {(_mouseHookID != IntPtr.Zero ? "Active" : "Inactive")}");
            Debug.Print($"Active key count: {_activeKeyPresses.Count}");
            Debug.Print($"Is capturing active: {_isActive}");

            // Force refresh keyboard hook
            if (_isActive && _keyboardHookID != IntPtr.Zero)
            {
                Debug.Print("Refreshing keyboard hook...");
                try
                {
                    UnhookWindowsHookEx(_keyboardHookID);
                    _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                    Debug.Print($"Keyboard hook refreshed: {(_keyboardHookID != IntPtr.Zero ? "Success" : "Failed")}");
                }
                catch (Exception ex)
                {
                    Debug.Print($"Error refreshing keyboard hook: {ex.Message}");
                }
            }
        }

        // Add method to test if keyboard input is being captured correctly
        public void TestKeyboardCapture()
        {
            Debug.Print("Running keyboard capture test...");

            // Force keyboard hook to be properly set
            if (_keyboardHookID == IntPtr.Zero && _isActive)
            {
                _keyboardProc = KeyboardHookCallback;
                _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                Debug.Print($"Reinstalled keyboard hook: {(_keyboardHookID != IntPtr.Zero ? "Success" : "Failed")}");
            }

            // Verify Windows messages are being processed correctly
            Debug.Print("Key event constants check:");
            Debug.Print($"WM_KEYDOWN: {WM_KEYDOWN} (0x{WM_KEYDOWN:X4})");
            Debug.Print($"WM_KEYUP: {WM_KEYUP} (0x{WM_KEYUP:X4})");
            Debug.Print($"WM_SYSKEYDOWN: {WM_SYSKEYDOWN} (0x{WM_SYSKEYDOWN:X4})");
            Debug.Print($"WM_SYSKEYUP: {WM_SYSKEYUP} (0x{WM_SYSKEYUP:X4})");

            // Sample the current state
            var activeKeys = string.Join(", ", _currentlyPressedKeys);
            Debug.Print($"Currently tracked keys: {activeKeys}");
        }

        // Add a diagnostic method to test if recording is working properly
        public void TestInputRecording()
        {
            Debug.Print("DIAGNOSTIC TEST: Checking input recording system");

            // Check hook status
            if (_keyboardHookID == IntPtr.Zero)
                Debug.Print("WARNING: Keyboard hook is not active!");
            else
                Debug.Print("Keyboard hook is active");

            if (_mouseHookID == IntPtr.Zero)
                Debug.Print("WARNING: Mouse hook is not active!");
            else
                Debug.Print("Mouse hook is active");

            // Log current state tracking
            Debug.Print($"Current active key count: {_activeKeyPresses.Count}");
            Debug.Print($"Current mouse button states: Left={_leftMouseDown}, Right={_rightMouseDown}, Middle={_middleButtonDown}");
            Debug.Print($"Input queue size: {(_inputQueue?.Count ?? 0)}");

            // Test event by directly adding a test action
            var testAction = new ActionItem
            {
                EventType = 0xFFFF, // Test event code
                KeyCode = 0,
                Timestamp = DateTime.UtcNow,
                IsLeftButtonDown = _leftMouseDown,
                IsRightButtonDown = _rightMouseDown,
                IsMiddleButtonDown = _middleButtonDown
            };

            GetCursorPos(out POINT curPos);
            testAction.Coordinates = new Coordinates
            {
                X = curPos.X,
                Y = curPos.Y,
                AbsoluteX = curPos.X,
                AbsoluteY = curPos.Y
            };

            AddToInputQueue(testAction);
            Debug.Print("Added test action to input queue");
        }
    }
}
