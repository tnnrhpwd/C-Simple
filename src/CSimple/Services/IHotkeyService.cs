using System;
using System.Windows.Input;

namespace CSimple.Services
{
    public interface IHotkeyService
    {
        /// <summary>
        /// Register a global hotkey
        /// </summary>
        /// <param name="key">The key combination</param>
        /// <param name="action">The action to execute when the hotkey is pressed</param>
        void RegisterHotkey(string key, Action action);

        /// <summary>
        /// Unregister a global hotkey
        /// </summary>
        /// <param name="key">The key combination to unregister</param>
        void UnregisterHotkey(string key);

        /// <summary>
        /// Unregister all hotkeys
        /// </summary>
        void UnregisterAllHotkeys();
    }
}
