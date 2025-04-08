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
        private readonly ConcurrentQueue<Point> _mouseMovementQueue = new ConcurrentQueue<Point>();
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
                        // Use raw deltas directly
                        int deltaX = rawInput.mouse.lLastX;
                        int deltaY = rawInput.mouse.lLastY;

                        if (deltaX != 0 || deltaY != 0)
                        {
                            EnqueueMouseMovement(new Point(deltaX, deltaY));
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

        private void ProcessHidInput(IntPtr rawData, RAWINPUT rawInput, uint size)
        {
            // Parse HID data to determine if it's touch or trackpad
            // This is a simplified placeholder implementation

            // Analyze the device info to determine device type
            if (IsTouchDevice(rawInput.header.hDevice))
            {
                ProcessTouchInput(rawData, rawInput);
            }
            else if (IsTrackpadDevice(rawInput.header.hDevice))
            {
                ProcessTrackpadInput(rawData, rawInput);
            }
            else
            {
                Debug.WriteLine("Unknown HID device input received");
            }
        }

        private bool IsTouchDevice(IntPtr hDevice)
        {
            // Implement device identification logic
            // This would use GetRawInputDeviceInfo to query device properties
            return false; // Placeholder
        }

        private bool IsTrackpadDevice(IntPtr hDevice)
        {
            // Implement device identification logic
            return false; // Placeholder
        }

        private void ProcessTouchInput(IntPtr rawData, RAWINPUT rawInput)
        {
            // Parse touch data from the raw input
            // In a real implementation, this would extract coordinates, contact ID, etc.

            var touchEvent = new TouchEvent
            {
                X = 0, // Extract from rawData
                Y = 0, // Extract from rawData
                Type = TouchEventType.Down, // Determine from data
                ContactId = 0, // Extract contact ID
                Timestamp = DateTime.UtcNow
            };

            EnqueueTouchEvent(touchEvent);
            Debug.WriteLine("Touch input processed");
        }

        private void ProcessTrackpadInput(IntPtr rawData, RAWINPUT rawInput)
        {
            // Parse trackpad data from raw input
            // This would be similar to mouse movement but potentially with multi-touch support
            Debug.WriteLine("Trackpad input processed");

            // For trackpad movements, we often treat them like mouse movements
            int deltaX = 0; // Extract from rawData
            int deltaY = 0; // Extract from rawData

            if (deltaX != 0 || deltaY != 0)
            {
                EnqueueMouseMovement(new Point(deltaX, deltaY));
            }
        }

        private void ProcessPenInput(IntPtr rawData, RAWINPUT rawInput)
        {
            // Process pen/stylus input
            Debug.WriteLine("Pen input processed");
        }

        private void EnqueueMouseMovement(Point delta)
        {
            // Limit queue size by trimming if needed
            if (_mouseMovementQueue.Count >= _queueMaxSize)
            {
                // Try to dequeue an item to make room
                _mouseMovementQueue.TryDequeue(out _);
            }

            _mouseMovementQueue.Enqueue(delta);
        }

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
                        Point[] pointsToProcess = new Point[processCount];
                        int actualCount = 0;

                        for (int i = 0; i < processCount; i++)
                        {
                            if (_mouseMovementQueue.TryDequeue(out Point delta))
                            {
                                pointsToProcess[actualCount++] = delta;
                            }
                        }

                        // Process all fetched items
                        for (int i = 0; i < actualCount; i++)
                        {
                            Point delta = pointsToProcess[i];

                            // Add to the movements list
                            MouseMovements.Add(delta);

                            // Invoke the event without blocking
                            MouseMoved?.Invoke(delta);
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
