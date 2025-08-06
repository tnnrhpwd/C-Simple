using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CSimple.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        try
        {
            // Create log file for debugging
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "CSimple_Debug.log");
            File.AppendAllText(logPath, $"[{DateTime.Now}] App constructor starting\n");

            this.UnhandledException += OnUnhandledException;

            File.AppendAllText(logPath, $"[{DateTime.Now}] App constructor completed\n");
        }
        catch (Exception ex)
        {
            // If even this fails, try to write to a different location
            try
            {
                var tempLogPath = Path.Combine(Path.GetTempPath(), "CSimple_Error.log");
                File.AppendAllText(tempLogPath, $"[{DateTime.Now}] App constructor error: {ex}\n");
            }
            catch { /* Silent fail if we can't even write to temp */ }
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"Unhandled exception: {e.Exception}");
        e.Handled = true;
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        base.OnLaunched(args);

        //Microsoft.Maui.Essentials.Platform.OnLaunched(args);
    }
}
