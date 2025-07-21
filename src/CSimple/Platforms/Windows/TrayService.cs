using Hardcodet.Wpf.TaskbarNotification.Interop;
using System.Diagnostics;
using CSimple.Services;
using System;
using System.Runtime.InteropServices;

namespace CSimple.WinUI;

public class TrayService : ITrayService
{
    WindowsTrayIcon tray;
    private bool _isProgressVisible = false;

    public Action ClickHandler { get; set; }
    public Action StartListenHandler { get; set; }
    public Action StopListenHandler { get; set; }
    public Action ShowSettingsHandler { get; set; }
    public Action QuitApplicationHandler { get; set; }
    public Func<bool> IsListeningCallback { get; set; }

    // Win32 API for context menu
    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll")]
    private static extern bool AppendMenu(IntPtr hMenu, uint uFlags, uint uIDNewItem, string lpNewItem);

    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

    [DllImport("user32.dll")]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr SetForegroundWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint MF_STRING = 0x00000000;
    private const uint MF_SEPARATOR = 0x00000800;
    private const uint TPM_RIGHTBUTTON = 0x0002;
    private const uint TPM_RETURNCMD = 0x0100;

    // Menu item IDs
    private const uint ID_START_LISTEN = 1001;
    private const uint ID_STOP_LISTEN = 1002;
    private const uint ID_SETTINGS = 1003;
    private const uint ID_QUIT = 1004;

    public void Initialize()
    {
        try
        {
            tray = new WindowsTrayIcon("Platforms/Windows/trayicon.ico");
            tray.LeftClick = () =>
            {
                Debug.WriteLine("Tray icon clicked - bringing window to front");
                WindowExtensions.BringToFront();
                ClickHandler?.Invoke();
            };

            // Set up right-click context menu
            tray.RightClick = () =>
            {
                Debug.WriteLine("=== TRAY ICON RIGHT-CLICKED ===");
                Debug.WriteLine("Attempting to show context menu...");
                try
                {
                    ShowContextMenu();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in RightClick handler: {ex.Message}");
                }
                Debug.WriteLine("=== RIGHT-CLICK HANDLER COMPLETED ===");
            };

            // Set initial tooltip to show "CSimple" - add a small delay to ensure icon is created
            Task.Delay(100).ContinueWith(_ =>
            {
                try
                {
                    tray?.UpdateTooltip("CSimple");
                    Debug.WriteLine("Tray tooltip set to 'CSimple'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error setting tray tooltip: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing tray service: {ex.Message}");
        }
    }

    private void ShowContextMenu()
    {
        try
        {
            Debug.WriteLine("ShowContextMenu called - creating popup menu");

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero)
            {
                Debug.WriteLine("Failed to create popup menu");
                return;
            }

            // Check if listening is active
            bool isListening = IsListeningCallback?.Invoke() ?? false;
            Debug.WriteLine($"Intelligence listening status: {isListening}");

            // Add menu items in the requested order
            if (isListening)
            {
                AppendMenu(hMenu, MF_STRING, ID_STOP_LISTEN, "Stop Listen");
                Debug.WriteLine("Added 'Stop Listen' menu item");
            }
            else
            {
                AppendMenu(hMenu, MF_STRING, ID_START_LISTEN, "Start Listen");
                Debug.WriteLine("Added 'Start Listen' menu item");
            }

            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, ID_SETTINGS, "Settings");
            AppendMenu(hMenu, MF_SEPARATOR, 0, null);
            AppendMenu(hMenu, MF_STRING, ID_QUIT, "Quit");
            Debug.WriteLine("Added all menu items");

            // Get cursor position
            if (!GetCursorPos(out POINT point))
            {
                Debug.WriteLine("Failed to get cursor position");
                DestroyMenu(hMenu);
                return;
            }
            Debug.WriteLine($"Cursor position: X={point.X}, Y={point.Y}");

            // Get the message window handle from the tray icon
            IntPtr windowHandle = tray?.MessageWindowHandle ?? IntPtr.Zero;
            Debug.WriteLine($"Using window handle: {windowHandle}");

            // Set foreground window to ensure menu appears properly
            if (windowHandle != IntPtr.Zero)
            {
                SetForegroundWindow(windowHandle);
            }

            // Show the context menu
            Debug.WriteLine("Calling TrackPopupMenu...");
            uint selectedItem = TrackPopupMenu(hMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, point.X, point.Y, 0, windowHandle, IntPtr.Zero);
            Debug.WriteLine($"TrackPopupMenu returned: {selectedItem}");

            // Handle the selected menu item
            switch (selectedItem)
            {
                case ID_START_LISTEN:
                    Debug.WriteLine("Start Listen selected from tray menu");
                    StartListenHandler?.Invoke();
                    break;
                case ID_STOP_LISTEN:
                    Debug.WriteLine("Stop Listen selected from tray menu");
                    StopListenHandler?.Invoke();
                    break;
                case ID_SETTINGS:
                    Debug.WriteLine("Settings selected from tray menu");
                    ShowSettingsHandler?.Invoke();
                    break;
                case ID_QUIT:
                    Debug.WriteLine("Quit selected from tray menu");
                    QuitApplicationHandler?.Invoke();
                    break;
                case 0:
                    Debug.WriteLine("No menu item selected (user clicked outside menu)");
                    break;
                default:
                    Debug.WriteLine($"Unknown menu item selected: {selectedItem}");
                    break;
            }

            // Clean up the menu
            DestroyMenu(hMenu);
            Debug.WriteLine("Context menu cleanup completed");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error showing context menu: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    public void ShowProgress(string title, string message, double progress)
    {
        try
        {
            _isProgressVisible = true;
            var progressPercent = (int)(progress * 100);
            Debug.WriteLine($"TrayService: Showing progress - {title}: {message} ({progressPercent}%)");

            // Update tray icon tooltip to show progress
            if (tray != null)
            {
                tray.UpdateTooltip($"{title}: {progressPercent}%");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TrayService ShowProgress error: {ex.Message}");
        }
    }

    public void UpdateProgress(double progress, string message = null)
    {
        try
        {
            if (_isProgressVisible)
            {
                var progressPercent = (int)(progress * 100);
                // Debug.WriteLine($"TrayService: Updating progress - {progressPercent}% {message ?? ""}");

                // Update tray icon tooltip
                if (tray != null)
                {
                    var statusText = message != null ? $"{message} ({progressPercent}%)" : $"{progressPercent}%";
                    tray.UpdateTooltip($"Download: {statusText}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TrayService UpdateProgress error: {ex.Message}");
        }
    }

    public void HideProgress()
    {
        try
        {
            _isProgressVisible = false;
            Debug.WriteLine("TrayService: Hiding progress");

            // Reset tray icon tooltip
            if (tray != null)
            {
                tray.UpdateTooltip("CSimple");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TrayService HideProgress error: {ex.Message}");
        }
    }

    public void ShowCompletionNotification(string title, string message)
    {
        try
        {
            Debug.WriteLine($"TrayService: Showing completion notification - {title}: {message}");

            // Update tray icon tooltip
            if (tray != null)
            {
                tray.UpdateTooltip($"{title}: {message}");
            }

            // Auto-hide after a delay
            Task.Delay(3000).ContinueWith(_ => HideProgress());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"TrayService ShowCompletionNotification error: {ex.Message}");
        }
    }
}
