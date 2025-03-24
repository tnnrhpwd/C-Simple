using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CSimple.Services
{
    public class InputCaptureService
    {
        #region Events
        public event Action<string> InputCaptured;
        public event Action<string> DebugMessageLogged;
        #endregion

        #region Properties
        private Dictionary<ushort, ActionItem> _activeKeyPresses = new Dictionary<ushort, ActionItem>();
        private Dictionary<ushort, DateTime> _keyPressDownTimestamps = new Dictionary<ushort, DateTime>();
        private DateTime _mouseLeftButtonDownTimestamp;
        private DateTime _lastMouseEventTime = DateTime.MinValue;
        private bool _isActive = false;
        private bool _previewModeActive = false;
        private CancellationTokenSource _previewCts;
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

        public void StartCapturing()
        {
#if WINDOWS
            if (!_isActive)
            {
                _keyboardProc = HookCallback;
                _mouseProc = HookCallback;
                _keyboardHookID = SetHook(_keyboardProc, WH_KEYBOARD_LL);
                _mouseHookID = SetHook(_mouseProc, WH_MOUSE_LL);
                GetCursorPos(out _lastMousePos);
                _isActive = true;
                LogDebug("Input capture started");
            }
#endif
        }

        public void StopCapturing()
        {
#if WINDOWS
            if (_isActive)
            {
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
                _isActive = false;
                _activeKeyPresses.Clear();
                _keyPressDownTimestamps.Clear();
                LogDebug("Input capture stopped");
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
                var currentTime = DateTime.UtcNow.ToString("o");
                var actionItem = new ActionItem
                {
                    Timestamp = DateTime.Parse(currentTime)
                };

                if (wParam == (IntPtr)WM_MOUSEMOVE)
                {
                    var currentMouseEventTime = DateTime.UtcNow;
                    if ((currentMouseEventTime - _lastMouseEventTime).TotalMilliseconds < 500)
                    {
                        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                    }
                    _lastMouseEventTime = currentMouseEventTime;

                    GetCursorPos(out POINT currentMousePos);
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = WM_MOUSEMOVE;
                }
                else if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONDOWN)
                {
                    GetCursorPos(out POINT currentMousePos);
                    var buttonCode = (wParam == (IntPtr)WM_LBUTTONDOWN) ? (ushort)WM_LBUTTONDOWN : (ushort)WM_RBUTTONDOWN;
                    _mouseLeftButtonDownTimestamp = DateTime.UtcNow;
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;
                    actionItem.Duration = 0;

                    if (!_activeKeyPresses.ContainsKey(buttonCode))
                    {
                        _activeKeyPresses[buttonCode] = actionItem;
                    }

                    NotifyInputUpdate(actionItem);
                }
                else if (wParam == (IntPtr)WM_LBUTTONUP || wParam == (IntPtr)WM_RBUTTONUP)
                {
                    GetCursorPos(out POINT currentMousePos);
                    var duration = (DateTime.UtcNow - _mouseLeftButtonDownTimestamp).TotalMilliseconds;
                    actionItem.Duration = duration > 0 ? (int)duration : 1;
                    actionItem.Coordinates = new Coordinates { X = currentMousePos.X, Y = currentMousePos.Y };
                    actionItem.EventType = (ushort)wParam;

                    var buttonCode = (wParam == (IntPtr)WM_LBUTTONUP) ? (ushort)WM_LBUTTONDOWN : (ushort)WM_RBUTTONDOWN;
                    _activeKeyPresses.Remove(buttonCode);

                    NotifyInputUpdate(actionItem);
                }
                else if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = (ushort)vkCode;

                    if (!_activeKeyPresses.ContainsKey(actionItem.KeyCode))
                    {
                        _keyPressDownTimestamps[(ushort)vkCode] = DateTime.UtcNow;
                        actionItem.EventType = WM_KEYDOWN;
                        actionItem.Duration = 0;

                        _activeKeyPresses[actionItem.KeyCode] = actionItem;

                        NotifyInputUpdate(actionItem);
                    }
                    else
                    {
                        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    actionItem.KeyCode = (ushort)vkCode;

                    if (_keyPressDownTimestamps.TryGetValue((ushort)vkCode, out DateTime keyDownTimestamp))
                    {
                        var duration = (DateTime.UtcNow - keyDownTimestamp).TotalMilliseconds;
                        actionItem.Duration = duration > 0 ? (int)duration : 1;
                        actionItem.EventType = WM_KEYUP;

                        _activeKeyPresses.Remove(actionItem.KeyCode);
                        _keyPressDownTimestamps.Remove((ushort)vkCode);

                        NotifyInputUpdate(actionItem);
                    }
                }
            }

            return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
        }
#endif

        private void NotifyInputUpdate(ActionItem actionItem)
        {
            string inputJson = JsonConvert.SerializeObject(actionItem);
            InputCaptured?.Invoke(inputJson);
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
    }
}
