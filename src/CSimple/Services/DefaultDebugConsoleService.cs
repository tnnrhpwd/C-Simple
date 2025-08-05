using System;

namespace CSimple.Services
{
    /// <summary>
    /// Default implementation of IDebugConsoleService for platforms that don't support console windows
    /// </summary>
    public class DefaultDebugConsoleService : IDebugConsoleService
    {
        public bool IsVisible => false;

        public event EventHandler ConsoleClosed;

        public void Initialize()
        {
            // No-op for platforms that don't support console windows
        }

        public void Show()
        {
            // No-op for platforms that don't support console windows
        }

        public void Hide()
        {
            // No-op for platforms that don't support console windows
        }

        public void WriteLine(string message)
        {
            // Fallback to debug output
            System.Diagnostics.Debug.WriteLine($"[CONSOLE] {message}");
        }

        public void WriteLine(string level, string message)
        {
            // Fallback to debug output
            System.Diagnostics.Debug.WriteLine($"[CONSOLE][{level}] {message}");
        }

        public void Clear()
        {
            // No-op for platforms that don't support console windows
        }

        public void Close()
        {
            ConsoleClosed?.Invoke(this, EventArgs.Empty);
        }
    }
}
