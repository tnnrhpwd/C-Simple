using Microsoft.Maui.Graphics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Diagnostics;

#if WINDOWS
#endif

namespace CSimple.Services
{
    public class MouseTrackingService
    {
        // Constants for raw input
        private const uint RIM_TYPEMOUSE = 0;
        private const uint RID_INPUT = 0x10000003;
        private const uint RIM_TYPEHID = 0x0002;  // For HID devices including touch and trackpad
        private const uint RIM_TYPETOUCH = 0x0003;  // Specific for touch events
        private const uint RIM_TYPEPEN = 0x0004;  // Specific for pen input

        // Use a concurrent queue to avoid locking during high-frequency mouse events
        private readonly ConcurrentQueue<PointWithButtonState> _mouseMovementQueue = new ConcurrentQueue<PointWithButtonState>();
        private readonly ConcurrentQueue<TouchEvent> _touchEventQueue = new ConcurrentQueue<TouchEvent>();
        private readonly int _queueMaxSize = 200;
        private const int MAX_QUEUE_PROCESS_BATCH = 20;

        // Define touch event structure
        public class TouchEvent
        {
            public int X { get; set; }
            public int Y { get; set; }
            public TouchEventType Type { get; set; }
            public int ContactId { get; set; }
            public DateTime Timestamp { get; set; }
        }

        public enum TouchEventType
        {
            Down,
            Move,
            Up,
            Cancel
        }

        private bool _isTracking;
        private Point _lastPosition;
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private int _skippedFrames = 0;
        private const int PROCESS_EVERY_N_FRAMES = 1; // Changed from 2 to 1 for better responsiveness
        private const int MIN_MOVEMENT_THRESHOLD = 0; // Changed from 1 to 0 for higher precision

        public List<Point> MouseMovements { get; } = new List<Point>(1000); // Pre-allocated capacity

        public event Action<Point> MouseMoved;
        public event Action<TouchEvent> TouchInputReceived;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterRawInputDevices([MarshalAs(UnmanagedType.LPArray, SizeConst = 1)] RAWINPUTDEVICE[] pRawInputDevices, uint uiNumDevices, uint cbSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetRawInputData(IntPtr hRawInput, uint uiCommand, IntPtr pData, ref uint pcbSize, uint cbSizeHeader);

        // Raw input structures
        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTDEVICE
        {
            public ushort usUsagePage;
            public ushort usUsage;
            public uint dwFlags;
            public IntPtr hwndTarget;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUTHEADER
        {
            public uint dwType;
            public uint dwSize;
            public IntPtr hDevice;
            public IntPtr wParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWMOUSE
        {
            public ushort usFlags;
            public uint ulButtons;
            public ushort usButtonFlags;  // Added missing field
            public ushort usButtonData;   // Added missing field
            public uint ulRawButtons;
            public int lLastX;
            public int lLastY;
            public uint ulExtraInformation;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RAWINPUT
        {
            public RAWINPUTHEADER header;
            public RAWMOUSE mouse;
        }

        // Constants for mouse button flags
        private const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
        private const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
        private const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
        private const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
        private const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;

        public MouseTrackingService()
        {
            _lastPosition = new Point(0, 0);
            // Start a background task to process the queue at a controlled rate
            Task.Run(ProcessQueuedMovementsAsync);
            Task.Run(ProcessTouchEventsAsync);
        }

        public void StartTracking(IntPtr hwnd)
        {
            _isTracking = true;
            RegisterForRawInput(hwnd);

            // Start tracking loop
            _ = TrackMouseMovementAsync();
        }

        public void StopTracking()
        {
            _isTracking = false;
        }

        // Register the application to receive raw mouse input
        private void RegisterForRawInput(IntPtr hwnd)
        {
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[3]; // Increased from 2 to 3

            // Register for mouse input (UsagePage = 1, Usage = 2)
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = 0;        // No flags for relative movement
            rid[0].hwndTarget = hwnd;  // Handle of the window to receive the input

            // Register for touch input (UsagePage = 0x0D, Usage = 0x04)
            rid[1].usUsagePage = 0x0D; // Digitizer
            rid[1].usUsage = 0x04;     // Touch screen
            rid[1].dwFlags = 0;
            rid[1].hwndTarget = hwnd;

            // Register for trackpad input (UsagePage = 0x0D, Usage = 0x05)
            rid[2].usUsagePage = 0x0D; // Digitizer
            rid[2].usUsage = 0x05;     // Touch pad
            rid[2].dwFlags = 0;
            rid[2].hwndTarget = hwnd;

            if (!RegisterRawInputDevices(rid, (uint)rid.Length, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            {
                throw new Exception("Failed to register raw input devices.");
            }
        }

        // Capture the raw input and process mouse movement
        public void ProcessRawInput(IntPtr lParam)
        {
            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER)));

            IntPtr rawData = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, rawData, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == size)
                {
                    RAWINPUT rawInput = Marshal.PtrToStructure<RAWINPUT>(rawData);
                    if (rawInput.header.dwType == RIM_TYPEMOUSE)
                    {
                        // Get the mouse button flags
                        ushort buttonFlags = rawInput.mouse.usButtonFlags;

                        // Track button states - this is key to fixing the issue
                        bool leftButtonDown = false;
                        bool rightButtonDown = false;
                        bool middleButtonDown = false;

                        // Check for left button down/up
                        if ((buttonFlags & 0x0001) != 0) // RI_MOUSE_LEFT_BUTTON_DOWN
                        {
                            leftButtonDown = true;
                            EnqueueMouseEvent(new Point(0, 0), 0x0201, true, false, false); // WM_LBUTTONDOWN
                            Debug.WriteLine("Left mouse button DOWN detected");
                        }
                        else if ((buttonFlags & 0x0002) != 0) // RI_MOUSE_LEFT_BUTTON_UP
                        {
                            EnqueueMouseEvent(new Point(0, 0), 0x0202, false, false, false); // WM_LBUTTONUP
                            Debug.WriteLine("Left mouse button UP detected");
                        }

                        // Check for right button down/up
                        if ((buttonFlags & 0x0004) != 0) // RI_MOUSE_RIGHT_BUTTON_DOWN
                        {
                            rightButtonDown = true;
                            EnqueueMouseEvent(new Point(0, 0), 0x0204, false, true, false); // WM_RBUTTONDOWN
                            Debug.WriteLine("Right mouse button DOWN detected");
                        }
                        else if ((buttonFlags & 0x0008) != 0) // RI_MOUSE_RIGHT_BUTTON_UP
                        {
                            EnqueueMouseEvent(new Point(0, 0), 0x0205, false, false, false); // WM_RBUTTONUP
                            Debug.WriteLine("Right mouse button UP detected");
                        }

                        // Check for middle button down/up
                        if ((buttonFlags & 0x0010) != 0) // RI_MOUSE_MIDDLE_BUTTON_DOWN
                        {
                            middleButtonDown = true;
                            EnqueueMouseEvent(new Point(0, 0), 0x0207, false, false, true); // WM_MBUTTONDOWN
                            Debug.WriteLine("Middle mouse button DOWN detected");
                        }
                        else if ((buttonFlags & 0x0020) != 0) // RI_MOUSE_MIDDLE_BUTTON_UP
                        {
                            EnqueueMouseEvent(new Point(0, 0), 0x0208, false, false, false); // WM_MBUTTONUP
                            Debug.WriteLine("Middle mouse button UP detected");
                        }

                        // Always process the mouse movement regardless of button state
                        int deltaX = rawInput.mouse.lLastX;
                        int deltaY = rawInput.mouse.lLastY;

                        if (deltaX != 0 || deltaY != 0)
                        {
                            // Check current button state from flags
                            bool isLeftDown = (rawInput.mouse.usButtonFlags & 0x0001) != 0 ||
                                             ((rawInput.mouse.ulButtons & 0x0001) != 0 && (rawInput.mouse.usButtonFlags & 0x0002) == 0);
                            bool isRightDown = (rawInput.mouse.usButtonFlags & 0x0004) != 0 ||
                                              ((rawInput.mouse.ulButtons & 0x0002) != 0 && (rawInput.mouse.usButtonFlags & 0x0008) == 0);
                            bool isMiddleDown = (rawInput.mouse.usButtonFlags & 0x0010) != 0 ||
                                               ((rawInput.mouse.ulButtons & 0x0004) != 0 && (rawInput.mouse.usButtonFlags & 0x0020) == 0);

                            EnqueueMouseMovement(new Point(deltaX, deltaY), isLeftDown, isRightDown, isMiddleDown);
                        }
                    }
                    else if (rawInput.header.dwType == RIM_TYPEHID)
                    {
                        // This could be touch or trackpad
                        ProcessHidInput(rawData, rawInput, size);
                    }
                    else if (rawInput.header.dwType == RIM_TYPETOUCH)
                    {
                        ProcessTouchInput(rawData, rawInput);
                    }
                    else if (rawInput.header.dwType == RIM_TYPEPEN)
                    {
                        ProcessPenInput(rawData, rawInput);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rawData);
            }
        }

        // Add a new method to enqueue mouse button events
        private void EnqueueMouseEvent(Point delta, int eventType, bool isLeftDown, bool isRightDown, bool isMiddleDown)
        {
            // Create TouchEvent to mimic mouse button input
            var touchEvent = new TouchEvent
            {
                X = 0, // Will be set by the system
                Y = 0, // Will be set by the system
                Type = GetTouchEventTypeFromMouseEvent(eventType),
                ContactId = 0, // Main contact for mouse
                Timestamp = DateTime.UtcNow
            };

            EnqueueTouchEvent(touchEvent);

            // Also queue as mouse movement to ensure button states are tracked
            EnqueueMouseMovement(delta, isLeftDown, isRightDown, isMiddleDown);
        }

        // Helper to convert mouse event to touch event type
        private TouchEventType GetTouchEventTypeFromMouseEvent(int mouseEventType)
        {
            switch (mouseEventType)
            {
                case 0x0201: // WM_LBUTTONDOWN
                case 0x0204: // WM_RBUTTONDOWN
                case 0x0207: // WM_MBUTTONDOWN
                    return TouchEventType.Down;

                case 0x0202: // WM_LBUTTONUP
                case 0x0205: // WM_RBUTTONUP
                case 0x0208: // WM_MBUTTONUP
                    return TouchEventType.Up;

                default:
                    return TouchEventType.Move;
            }
        }

        // Modify the EnqueueMouseMovement method to include button states
        private void EnqueueMouseMovement(Point delta, bool isLeftDown = false, bool isRightDown = false, bool isMiddleDown = false)
        {
            // Limit queue size by trimming if needed
            if (_mouseMovementQueue.Count >= _queueMaxSize)
            {
                // Try to dequeue an item to make room
                _mouseMovementQueue.TryDequeue(out _);
            }

            // Create a custom Point with button state
            var pointWithState = new PointWithButtonState
            {
                Delta = delta,
                IsLeftButtonDown = isLeftDown,
                IsRightButtonDown = isRightDown,
                IsMiddleButtonDown = isMiddleDown,
                Timestamp = DateTime.UtcNow
            };

            _mouseMovementQueue.Enqueue(pointWithState);
        }

        // Define a new class to track button states with points
        private class PointWithButtonState
        {
            public Point Delta { get; set; }
            public bool IsLeftButtonDown { get; set; }
            public bool IsRightButtonDown { get; set; }
            public bool IsMiddleButtonDown { get; set; }
            public DateTime Timestamp { get; set; }
        }

        // Process queued movements at a controlled rate
        private async Task ProcessQueuedMovementsAsync()
        {
            // Use Stopwatch for adaptive timing
            Stopwatch processingTimer = new Stopwatch();

            while (true)
            {
                try
                {
                    processingTimer.Restart();

                    if (_isTracking && _mouseMovementQueue.Count > 0)
                    {
                        // Process more items at once for efficiency
                        int processCount = Math.Min(_mouseMovementQueue.Count, MAX_QUEUE_PROCESS_BATCH);

                        // Pre-fetch items from queue for faster processing
                        PointWithButtonState[] pointsToProcess = new PointWithButtonState[processCount];
                        int actualCount = 0;

                        for (int i = 0; i < processCount; i++)
                        {
                            if (_mouseMovementQueue.TryDequeue(out PointWithButtonState pointWithState))
                            {
                                pointsToProcess[actualCount++] = pointWithState;
                            }
                        }

                        // Process all fetched items
                        for (int i = 0; i < actualCount; i++)
                        {
                            PointWithButtonState pointWithState = pointsToProcess[i];

                            // Add to the movements list
                            MouseMovements.Add(pointWithState.Delta);

                            // Invoke the event with button state information
                            MouseMoved?.Invoke(pointWithState.Delta);

                            // Also include button state information when reporting mouse movements
                            if (pointWithState.IsLeftButtonDown || pointWithState.IsRightButtonDown || pointWithState.IsMiddleButtonDown)
                            {
                                Debug.WriteLine($"Mouse movement with button state - Left: {pointWithState.IsLeftButtonDown}, " +
                                               $"Right: {pointWithState.IsRightButtonDown}, Middle: {pointWithState.IsMiddleButtonDown}");
                            }
                        }
                    }

                    // Adaptive delay based on processing time
                    long processingTime = processingTimer.ElapsedMilliseconds;
                    int targetFrameTime = 16; // Target ~60fps

                    // Calculate remaining time in the frame
                    int delayTime = Math.Max(1, targetFrameTime - (int)processingTime);

                    // More efficient short delay for better responsiveness
                    await Task.Delay(delayTime);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing mouse movements: {ex.Message}");
                    await Task.Delay(50); // Moderate delay on error
                }
            }
        }

        // Process touch events in a separate background task
        private async Task ProcessTouchEventsAsync()
        {
            Stopwatch processingTimer = new Stopwatch();

            while (true)
            {
                try
                {
                    processingTimer.Restart();

                    if (_isTracking && _touchEventQueue.Count > 0)
                    {
                        int processCount = Math.Min(_touchEventQueue.Count, MAX_QUEUE_PROCESS_BATCH);

                        // Pre-fetch items from queue for faster processing
                        TouchEvent[] eventsToProcess = new TouchEvent[processCount];
                        int actualCount = 0;

                        for (int i = 0; i < processCount; i++)
                        {
                            if (_touchEventQueue.TryDequeue(out TouchEvent touchEvent))
                            {
                                eventsToProcess[actualCount++] = touchEvent;
                            }
                        }

                        // Process all fetched items
                        for (int i = 0; i < actualCount; i++)
                        {
                            // Raise event for touch input
                            TouchInputReceived?.Invoke(eventsToProcess[i]);
                        }
                    }

                    // Adaptive delay based on processing time
                    long processingTime = processingTimer.ElapsedMilliseconds;
                    int targetFrameTime = 16; // Target ~60fps
                    int delayTime = Math.Max(1, targetFrameTime - (int)processingTime);
                    await Task.Delay(delayTime);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing touch events: {ex.Message}");
                    await Task.Delay(50);
                }
            }
        }

        private async Task TrackMouseMovementAsync()
        {
            _frameTimer.Start();

            while (_isTracking)
            {
                // Shorter fixed delay for better responsiveness
                await Task.Delay(5);
            }

            _frameTimer.Stop();
        }

        // Add missing method to enqueue touch events
        private void EnqueueTouchEvent(TouchEvent touchEvent)
        {
            // Limit queue size by trimming if needed
            if (_touchEventQueue.Count >= _queueMaxSize)
            {
                // Try to dequeue an item to make room
                _touchEventQueue.TryDequeue(out _);
            }

            _touchEventQueue.Enqueue(touchEvent);
        }

        // Add missing input processing methods
        private void ProcessHidInput(IntPtr rawData, RAWINPUT rawInput, uint size)
        {
            // Simplified implementation for HID inputs
            Debug.WriteLine("HID input received - implementing basic processing");

            // For now, treat as generic mouse movement with no button state
            if (rawInput.mouse.lLastX != 0 || rawInput.mouse.lLastY != 0)
            {
                EnqueueMouseMovement(
                    new Point(rawInput.mouse.lLastX, rawInput.mouse.lLastY),
                    false, false, false);
            }
        }

        private void ProcessTouchInput(IntPtr rawData, RAWINPUT rawInput)
        {
            // Simplified touch input handling
            Debug.WriteLine("Touch input received - implementing basic processing");

            // Create a simple touch event based on available data
            var touchEvent = new TouchEvent
            {
                X = rawInput.mouse.lLastX,
                Y = rawInput.mouse.lLastY,
                Type = TouchEventType.Move,  // Default to move
                ContactId = 0,               // Default contact ID
                Timestamp = DateTime.UtcNow
            };

            EnqueueTouchEvent(touchEvent);
        }

        private void ProcessPenInput(IntPtr rawData, RAWINPUT rawInput)
        {
            // Simplified pen input handling
            Debug.WriteLine("Pen input received - implementing basic processing");

            // For now, treat similar to touch
            var touchEvent = new TouchEvent
            {
                X = rawInput.mouse.lLastX,
                Y = rawInput.mouse.lLastY,
                Type = TouchEventType.Move,
                ContactId = 0,
                Timestamp = DateTime.UtcNow
            };

            EnqueueTouchEvent(touchEvent);
        }
    }

#if WINDOWS
    public static class WindowsMouseHelper
    {
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        public static POINT GetMousePosition()
        {
            GetCursorPos(out POINT lpPoint);
            return lpPoint;
        }
    }
#endif
}
