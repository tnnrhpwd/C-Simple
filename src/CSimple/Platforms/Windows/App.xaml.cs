using Microsoft.UI.Xaml;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace CSimple.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    private static Mutex _mutex = null;
    private const string MUTEX_NAME = "CSimple_SingleInstance_Mutex";

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

            // Check for and handle existing instances
            HandleSingleInstance();

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

    /// <summary>
    /// Handles single instance logic by checking for and closing existing instances
    /// </summary>
    private void HandleSingleInstance()
    {
        try
        {
            // Try to create or open the mutex
            bool createdNew;
            _mutex = new Mutex(true, MUTEX_NAME, out createdNew);

            if (!createdNew)
            {
                // Another instance is running, so let's close it
                CloseExistingInstances();

                // Wait a moment for the other instance to close
                Thread.Sleep(2000);

                // Try to acquire the mutex again
                if (_mutex.WaitOne(5000, false))
                {
                    // Successfully acquired the mutex after closing the other instance
                    Debug.WriteLine("Successfully acquired mutex after closing existing instance");
                }
                else
                {
                    // Still couldn't acquire the mutex, force close any remaining processes
                    ForceCloseExistingInstances();
                    Thread.Sleep(1000);

                    // Try one more time
                    if (!_mutex.WaitOne(1000, false))
                    {
                        Debug.WriteLine("Warning: Could not acquire single instance mutex");
                    }
                }
            }
            else
            {
                Debug.WriteLine("Successfully created new mutex - no existing instances");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in HandleSingleInstance: {ex.Message}");
            // Continue anyway - don't prevent the app from starting
        }
    }

    /// <summary>
    /// Gracefully closes existing CSimple instances
    /// </summary>
    private void CloseExistingInstances()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var existingProcesses = Process.GetProcessesByName("CSimple")
                .Where(p => p.Id != currentProcess.Id && !p.HasExited)
                .ToArray();

            Debug.WriteLine($"Found {existingProcesses.Length} existing CSimple processes to close");

            foreach (var process in existingProcesses)
            {
                try
                {
                    Debug.WriteLine($"Attempting to close process ID: {process.Id}");

                    // Try to close gracefully first
                    process.CloseMainWindow();

                    // Give it a moment to close gracefully
                    if (!process.WaitForExit(3000))
                    {
                        Debug.WriteLine($"Process {process.Id} didn't close gracefully, will force close if needed");
                    }
                    else
                    {
                        Debug.WriteLine($"Process {process.Id} closed gracefully");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing process {process.Id}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in CloseExistingInstances: {ex.Message}");
        }
    }

    /// <summary>
    /// Force closes any remaining CSimple instances that didn't close gracefully
    /// </summary>
    private void ForceCloseExistingInstances()
    {
        try
        {
            var currentProcess = Process.GetCurrentProcess();
            var existingProcesses = Process.GetProcessesByName("CSimple")
                .Where(p => p.Id != currentProcess.Id && !p.HasExited)
                .ToArray();

            Debug.WriteLine($"Force closing {existingProcesses.Length} remaining CSimple processes");

            foreach (var process in existingProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        Debug.WriteLine($"Force killing process ID: {process.Id}");
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error force closing process {process.Id}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in ForceCloseExistingInstances: {ex.Message}");
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

    /// <summary>
    /// Clean up the mutex when the application exits
    /// </summary>
    ~App()
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
    }
}
