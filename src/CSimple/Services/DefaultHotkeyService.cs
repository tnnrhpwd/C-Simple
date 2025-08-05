using System;

namespace CSimple.Services
{
    /// <summary>
    /// Default implementation of IHotkeyService for platforms that don't support global hotkeys
    /// </summary>
    public class DefaultHotkeyService : IHotkeyService
    {
        public void RegisterHotkey(string key, Action action)
        {
            // No-op for platforms that don't support global hotkeys
            System.Diagnostics.Debug.WriteLine($"Hotkey registration not supported on this platform: {key}");
        }

        public void UnregisterHotkey(string key)
        {
            // No-op for platforms that don't support global hotkeys
        }

        public void UnregisterAllHotkeys()
        {
            // No-op for platforms that don't support global hotkeys
        }
    }
}
