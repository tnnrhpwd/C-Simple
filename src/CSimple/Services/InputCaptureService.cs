using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
        private bool _middleMouseDown = false;

        // Track raw mouse movement for more accurate replay
        private int _accumulatedDeltaX = 0;
        private int _accumulatedDeltaY = 0;
        private Stopwatch _mouseMoveTimer = new Stopwatch();
        private readonly object _inputQueueLock = new object();

        // Touch input tracking
        private Dictionary<int, TouchPoint> _activeTouches = new Dictionary<int, TouchPoint>();
        private DateTime _lastTouchEventTime = DateTime.MinValue;

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

        public InputCaptureService()
        {
            // Initialize the queue
            ResetInputQueue();
            _mouseMoveTimer.Start();
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
                _middleMouseDown = false;
                _accumulatedDeltaX = 0;
                _accumulatedDeltaY = 0;
                _mouseMoveTimer.Restart();

                // Reset touch tracking
                _activeTouches.Clear();
                _lastTouchEventTime = DateTime.MinValue;

                _keyboardProc = HookCallback;
                _mouseProc = HookCallback;
                _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
                GetCursorPos(out _lastMousePos);
                _startMousePos = _lastMousePos; // Save start position for relative tracking
                _isActive = true;
                LogDebug("Input capture started");

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
                LogDebug("Registered window for touch input");
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
                LogDebug("Failed to get touch input info");
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
                if (_leftMouseDown || _rightMouseDown || _middleMouseDown)
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
                LogDebug("Input capture stopped");

                // Complete the input queue to signal the consumer to stop
                try
                {
                    _inputQueue?.CompleteAdding();
                }
                catch (ObjectDisposedException)
                {
                    LogDebug("Input queue was already disposed.");
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
                    IsMiddleButtonDown = _middleMouseDown
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
                    IsMiddleButtonDown = _middleMouseDown
                };
                AddToInputQueue(actionItem);
            }

            if (_middleMouseDown)
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

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var actionItem = new ActionItem
                {
                    Timestamp = DateTime.UtcNow,
                    IsLeftButtonDown = _leftMouseDown,
                    IsRightButtonDown = _rightMouseDown,
                    IsMiddleButtonDown = _middleMouseDown
                };

                // Handle mouse events
                if (wParam == (IntPtr)WM_MOUSEMOVE ||
                    wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_LBUTTONUP ||
                    wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONUP ||
                    wParam == (IntPtr)WM_MBUTTONDOWN || wParam == (IntPtr)WM_MBUTTONUP ||
                    wParam == (IntPtr)WM_MOUSEWHEEL)
                {
                    MSLLHOOKSTRUCT mouseHookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));

                    // Use raw deltas for mouse movement
                    int deltaX = _accumulatedDeltaX;
                    int deltaY = _accumulatedDeltaY;

                    if (wParam == (IntPtr)WM_MOUSEMOVE)
                    {
                        deltaX = mouseHookStruct.pt.X - _lastMousePos.X;
                        deltaY = mouseHookStruct.pt.Y - _lastMousePos.Y;
                        _accumulatedDeltaX = deltaX;
                        _accumulatedDeltaY = deltaY;
                    }

                    actionItem.Coordinates = new Coordinates
                    {
                        X = mouseHookStruct.pt.X,
                        Y = mouseHookStruct.pt.Y,
                        RelativeX = deltaX,
                        RelativeY = deltaY
                    };
                    actionItem.DeltaX = deltaX;
                    actionItem.DeltaY = deltaY;
                    actionItem.EventType = (ushort)wParam;

                    // Update button states
                    if (wParam == (IntPtr)WM_LBUTTONDOWN) _leftMouseDown = true;
                    else if (wParam == (IntPtr)WM_LBUTTONUP) _leftMouseDown = false;
                    else if (wParam == (IntPtr)WM_RBUTTONDOWN) _rightMouseDown = true;
                    else if (wParam == (IntPtr)WM_RBUTTONUP) _rightMouseDown = false;
                    else if (wParam == (IntPtr)WM_MBUTTONDOWN) _middleMouseDown = true;
                    else if (wParam == (IntPtr)WM_MBUTTONUP) _middleMouseDown = false;

                    actionItem.IsLeftButtonDown = _leftMouseDown;
                    actionItem.IsRightButtonDown = _rightMouseDown;
                    actionItem.IsMiddleButtonDown = _middleMouseDown;

                    AddToInputQueue(actionItem);
                }
                // Handle keyboard events
                else if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = vkCode;
                    actionItem.EventType = (ushort)wParam;

                    // Track key state
                    if (wParam == (IntPtr)WM_KEYDOWN)
                    {
                        if (!_activeKeyPresses.ContainsKey((ushort)vkCode))
                        {
                            _activeKeyPresses[(ushort)vkCode] = actionItem;
                            _keyPressDownTimestamps[(ushort)vkCode] = DateTime.UtcNow;
                        }
                    }
                    else if (wParam == (IntPtr)WM_KEYUP)
                    {
                        _activeKeyPresses.Remove((ushort)vkCode);
                        _keyPressDownTimestamps.Remove((ushort)vkCode);
                    }

                    // Add to input queue
                    AddToInputQueue(actionItem);

                    // Log for debugging
                    LogDebug($"Keyboard event: {(wParam == (IntPtr)WM_KEYDOWN ? "DOWN" : "UP")} Key: {vkCode}");
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private void AddToInputQueue(ActionItem actionItem)
        {
            if (_inputQueue != null && !_inputQueue.IsAddingCompleted)
            {
                try
                {
                    _inputQueue.Add(actionItem);
                }
                catch (InvalidOperationException)
                {
                    // The collection has been marked as complete
                    LogDebug("Queue is marked complete and cannot accept new items.");
                }
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
                            IsMiddleButtonDown = _middleMouseDown,
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
                    LogDebug($"Error tracking mouse movements: {ex.Message}");
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
                Coordinates = item.Coordinates != null ? new CSimple.Models.Coordinates
                {
                    X = item.Coordinates.X,
                    Y = item.Coordinates.Y,
                    AbsoluteX = item.Coordinates.AbsoluteX,
                    AbsoluteY = item.Coordinates.AbsoluteY,
                    RelativeX = item.Coordinates.RelativeX,
                    RelativeY = item.Coordinates.RelativeY
                } : null
            }));
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
                        LogDebug($"Error processing input item: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                LogDebug("Input queue processing cancelled.");
            }
            catch (ObjectDisposedException)
            {
                // Expected when the queue is disposed during shutdown
                LogDebug("Input queue processing stopped due to object disposed exception.");
            }
            catch (Exception ex)
            {
                LogDebug($"Error processing input queue: {ex.Message}");
            }
            finally
            {
                LogDebug("Input queue processing completed.");
            }
        }

        private void LogDebug(string message)
        {
            DebugMessageLogged?.Invoke(message);
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
    }
}
