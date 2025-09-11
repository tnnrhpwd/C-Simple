using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace CSimple.Services
{
    /// <summary>
    /// Service for detecting and locating windows on the screen
    /// </summary>
    public class WindowDetectionService
    {
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder strText, int maxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        private List<WindowInfo> _detectedWindows = new List<WindowInfo>();

        /// <summary>
        /// Finds the center coordinates of a window by name
        /// </summary>
        public async Task<Point?> GetWindowCenterAsync(string windowName)
        {
            try
            {
                Debug.WriteLine($"[WindowDetection] Searching for window: {windowName}");
                
                await Task.Run(() => RefreshWindowList());
                
                var window = _detectedWindows.FirstOrDefault(w => 
                    w.Title.ToLowerInvariant().Contains(windowName.ToLowerInvariant()));

                if (window != null)
                {
                    var center = new Point(
                        window.Bounds.Left + window.Bounds.Width / 2,
                        window.Bounds.Top + window.Bounds.Height / 2
                    );
                    
                    Debug.WriteLine($"[WindowDetection] Found window '{window.Title}' at center {center}");
                    return center;
                }

                Debug.WriteLine($"[WindowDetection] Window '{windowName}' not found");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error finding window: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the bounds of a window by name
        /// </summary>
        public async Task<Rectangle?> GetWindowBoundsAsync(string windowName)
        {
            try
            {
                await Task.Run(() => RefreshWindowList());
                
                var window = _detectedWindows.FirstOrDefault(w => 
                    w.Title.ToLowerInvariant().Contains(windowName.ToLowerInvariant()));

                return window?.Bounds;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error getting window bounds: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Brings a window to the foreground by name
        /// </summary>
        public async Task<bool> BringWindowToForegroundAsync(string windowName)
        {
            try
            {
                await Task.Run(() => RefreshWindowList());
                
                var window = _detectedWindows.FirstOrDefault(w => 
                    w.Title.ToLowerInvariant().Contains(windowName.ToLowerInvariant()));

                if (window != null)
                {
                    SetForegroundWindow(window.Handle);
                    Debug.WriteLine($"[WindowDetection] Brought window '{window.Title}' to foreground");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error bringing window to foreground: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets all visible windows
        /// </summary>
        public async Task<List<WindowInfo>> GetAllWindowsAsync()
        {
            try
            {
                await Task.Run(() => RefreshWindowList());
                return new List<WindowInfo>(_detectedWindows);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error getting all windows: {ex.Message}");
                return new List<WindowInfo>();
            }
        }

        /// <summary>
        /// Refreshes the list of detected windows
        /// </summary>
        private void RefreshWindowList()
        {
            _detectedWindows.Clear();
            EnumWindows(EnumWindowCallback, IntPtr.Zero);
            
            Debug.WriteLine($"[WindowDetection] Found {_detectedWindows.Count} visible windows");
            foreach (var window in _detectedWindows.Take(5)) // Log first 5 for debugging
            {
                Debug.WriteLine($"[WindowDetection] Window: '{window.Title}' - {window.Bounds}");
            }
        }

        /// <summary>
        /// Callback for enumerating windows
        /// </summary>
        private bool EnumWindowCallback(IntPtr hWnd, IntPtr lParam)
        {
            try
            {
                if (!IsWindowVisible(hWnd))
                    return true; // Continue enumeration

                var title = new System.Text.StringBuilder(256);
                GetWindowText(hWnd, title, title.Capacity);
                
                if (string.IsNullOrWhiteSpace(title.ToString()))
                    return true; // Skip windows without titles

                if (GetWindowRect(hWnd, out RECT rect))
                {
                    var bounds = new Rectangle(rect.Left, rect.Top, 
                        rect.Right - rect.Left, rect.Bottom - rect.Top);

                    // Filter out very small windows (likely UI elements)
                    if (bounds.Width > 50 && bounds.Height > 50)
                    {
                        _detectedWindows.Add(new WindowInfo
                        {
                            Handle = hWnd,
                            Title = title.ToString(),
                            Bounds = bounds
                        });
                    }
                }

                return true; // Continue enumeration
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error in enum callback: {ex.Message}");
                return true; // Continue enumeration even on error
            }
        }

        /// <summary>
        /// Finds windows by partial title match
        /// </summary>
        public async Task<List<WindowInfo>> FindWindowsByPartialTitleAsync(string partialTitle)
        {
            try
            {
                await Task.Run(() => RefreshWindowList());
                
                return _detectedWindows.Where(w => 
                    w.Title.ToLowerInvariant().Contains(partialTitle.ToLowerInvariant())).ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error finding windows by partial title: {ex.Message}");
                return new List<WindowInfo>();
            }
        }

        /// <summary>
        /// Gets the currently active window
        /// </summary>
        public async Task<WindowInfo?> GetActiveWindowAsync()
        {
            try
            {
                var foregroundWindow = GetForegroundWindow();
                if (foregroundWindow == IntPtr.Zero)
                    return null;

                await Task.Run(() => RefreshWindowList());
                
                return _detectedWindows.FirstOrDefault(w => w.Handle == foregroundWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WindowDetection] Error getting active window: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// Information about a detected window
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; }
        public Rectangle Bounds { get; set; }

        public Point Center => new Point(
            Bounds.Left + Bounds.Width / 2,
            Bounds.Top + Bounds.Height / 2
        );

        public override string ToString()
        {
            return $"{Title} ({Bounds.Width}x{Bounds.Height} at {Bounds.X},{Bounds.Y})";
        }
    }
}
