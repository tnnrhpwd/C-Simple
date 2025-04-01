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

        // Better mouse movement handling
        private const int MOUSE_MOVEMENT_THROTTLE_MS = 10; // More efficient throttling
        private DateTime _lastMouseMoveSent = DateTime.MinValue;
        private POINT _lastProcessedMousePos;
        private int _mouseMovementThreshold = 1; // Lower threshold for more accuracy
        private int _mouseQueueProcessingBatchSize = 15; // Process more items per batch
        #endregion

        #region Constants
        private const int WH_KEYBOARD_LL = 13;
        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }
#endif
        #endregion

        public InputCaptureService()
        {
            // Initialize the queue
            ResetInputQueue();
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

                _keyboardProc = HookCallback;
                _mouseProc = HookCallback;
                _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
                GetCursorPos(out _lastMousePos);
                _isActive = true;
                LogDebug("Input capture started");

                // Start a high-frequency mouse movement tracker
                Task.Run(() => TrackMouseMovements());
            }
#endif
        }

        public void StopCapturing()
        {
#if WINDOWS
            if (_isActive)
            {
                _isActive = false; // Set _isActive to false first

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

        // Add a method to get the count of active keys for the progress bar
        public int GetActiveKeyCount()
        {
            return _activeKeyPresses.Count;
        }

#if WINDOWS
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
                    Timestamp = DateTime.UtcNow
                };

                if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    GetCursorPos(out POINT currentMousePos);
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = WM_MOUSEMOVE;
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    GetCursorPos(out POINT currentMousePos);
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_RBUTTONUP)
                {
                    GetCursorPos(out POINT currentMousePos);
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;
                }
                else if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = vkCode;
                    actionItem.EventType = (ushort)wParam;
                }

                // Add the action item to the queue for processing
                if (_inputQueue != null && !_inputQueue.IsAddingCompleted)
                {
                    try
                    {
                        _inputQueue.Add(actionItem);
                    }
                    catch (InvalidOperationException)
                    {
                        // The collection has been marked as complete
                        LogDebug("Queue is marked complete in HookCallback and cannot accept new items.");
                    }
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }

        private async Task TrackMouseMovements()
        {
            Stopwatch frameTimer = new Stopwatch();
            frameTimer.Start();

            while (_isActive)
            {
                try
                {
                    GetCursorPos(out POINT currentMousePos);

                    // More efficient throttling logic for mouse movements
                    TimeSpan timeSinceLastMove = DateTime.UtcNow - _lastMouseMoveSent;
                    bool positionChanged = currentMousePos.X != _lastMousePos.X || currentMousePos.Y != _lastMousePos.Y;

                    // Either throttle by time or by distance
                    if (positionChanged &&
                        (timeSinceLastMove.TotalMilliseconds >= MOUSE_MOVEMENT_THROTTLE_MS))
                    {
                        var actionItem = new ActionItem
                        {
                            EventType = WM_MOUSEMOVE,
                            Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y },
                            Timestamp = DateTime.UtcNow
                        };

                        _lastMousePos = currentMousePos;
                        _lastMouseMoveSent = DateTime.UtcNow;
                        _lastProcessedMousePos = currentMousePos;

                        // Add the action item to the queue for processing
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

                    // More efficient adaptive delay
                    int elapsedMs = (int)frameTimer.ElapsedMilliseconds;
                    int delayMs = 1; // Minimum delay for responsiveness
                    frameTimer.Restart();

                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    LogDebug($"Error tracking mouse movements: {ex.Message}");
                    await Task.Delay(20); // Short delay on error
                }
            }

            frameTimer.Stop();
        }
#endif

        private void ProcessInputQueue(CancellationToken cancellationToken)
        {
            try
            {
                // Use more efficient batch processing
                const int batchSize = 15; // Increased from 10 to 15
                List<ActionItem> batch = new List<ActionItem>(batchSize);

                // Continue processing until cancellation is requested or queue is completed
                while (!cancellationToken.IsCancellationRequested && (_inputQueue != null && !_inputQueue.IsCompleted))
                {
                    try
                    {
                        batch.Clear();
                        int count = 0;

                        // Try to take multiple items at once with shorter timeout
                        while (count < batchSize && _inputQueue.TryTake(out ActionItem item, 20))
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
                        }
                        else
                        {
                            // Shorter sleep if no items - don't block too long
                            Thread.Sleep(5);
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
    }
}
