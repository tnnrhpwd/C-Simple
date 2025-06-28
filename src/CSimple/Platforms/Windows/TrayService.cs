using Hardcodet.Wpf.TaskbarNotification.Interop;
using System.Diagnostics;
using CSimple.Services;
using System;

namespace CSimple.WinUI;

public class TrayService : ITrayService
{
    WindowsTrayIcon tray;
    private bool _isProgressVisible = false;

    public Action ClickHandler { get; set; }
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
