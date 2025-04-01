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
        private const uint RIM_TYPETOUCH = 0x0003;  // Example value, consider touch events as a special HID type
        private const uint RIM_TYPEPEN = 0x0004;  // Example value, consider pen events as another HID type

        // Use a concurrent queue to avoid locking during high-frequency mouse events
        private readonly ConcurrentQueue<Point> _mouseMovementQueue = new ConcurrentQueue<Point>();
        private readonly int _queueMaxSize = 200; // Increased from 100 to 200
        private const int MAX_QUEUE_PROCESS_BATCH = 20; // Process more items per batch

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
            public uint HeaderSize;
            public uint Type;
            public uint MouseFlags;
            public uint MouseData;
            public int LastX; // These represent relative movements, not absolute positions
            public int LastY;
            public uint ButtonData;
            public uint ExtraInformation;
        }

        private bool _isTracking;
        private Point _lastPosition;
        private readonly Stopwatch _frameTimer = new Stopwatch();
        private int _skippedFrames = 0;
        private const int PROCESS_EVERY_N_FRAMES = 1; // Changed from 2 to 1 for better responsiveness
        private const int MIN_MOVEMENT_THRESHOLD = 0; // Changed from 1 to 0 for higher precision

        public List<Point> MouseMovements { get; } = new List<Point>(1000); // Pre-allocated capacity

        public event Action<Point> MouseMoved;

        public MouseTrackingService()
        {
            _lastPosition = new Point(0, 0);
            // Start a background task to process the queue at a controlled rate
            Task.Run(ProcessQueuedMovementsAsync);
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
            RAWINPUTDEVICE[] rid = new RAWINPUTDEVICE[2];

            // Register for mouse input (UsagePage = 1, Usage = 2)
            rid[0].usUsagePage = 0x01; // Generic Desktop Controls
            rid[0].usUsage = 0x02;     // Mouse
            rid[0].dwFlags = 0;        // No flags for relative movement
            rid[0].hwndTarget = hwnd;  // Handle of the window to receive the input

            // Register for touch input (UsagePage = 1, Usage = 4)
            rid[1].usUsagePage = 0x0D; // Digitizer
            rid[1].usUsage = 0x04;     // Touch screen
            rid[1].dwFlags = 0;
            rid[1].hwndTarget = hwnd;

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

            // Skip processing if we're getting too many events - but only apply throttling for
            // very high frequency situations to keep responsiveness
            if (_mouseMovementQueue.Count > 100 && _skippedFrames < PROCESS_EVERY_N_FRAMES)
            {
                _skippedFrames++;
                return;
            }
            _skippedFrames = 0;

            IntPtr rawData = Marshal.AllocHGlobal((int)size);
            try
            {
                if (GetRawInputData(lParam, RID_INPUT, rawData, ref size, (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER))) == size)
                {
                    RAWINPUT rawInput = Marshal.PtrToStructure<RAWINPUT>(rawData);
                    if (rawInput.HeaderSize == RIM_TYPEMOUSE)
                    {
                        int deltaX = rawInput.LastX;
                        int deltaY = rawInput.LastY;

                        // Capture all movement, even small ones, but still skip the real zero movements
                        if (deltaX != 0 || deltaY != 0)
                        {
                            // Add to queue instead of processing immediately
                            EnqueueMouseMovement(new Point(deltaX, deltaY));
                        }
                    }
                    else if (rawInput.HeaderSize == RIM_TYPETOUCH) // RIM_TYPETOUCH for touch input
                    {
                        // Handle touch input
                        Console.WriteLine("mousetrackingservice Touch input received.");
                        // Process the touch data here
                    }
                    else if (rawInput.HeaderSize == RIM_TYPEPEN) // RIM_TYPEPEN for pen input
                    {
                        // Handle pen input
                        Console.WriteLine("mousetrackingservice Pen input received.");
                        // Process the pen data here
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(rawData);
            }
        }

        // Add mouse movement to the queue
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
