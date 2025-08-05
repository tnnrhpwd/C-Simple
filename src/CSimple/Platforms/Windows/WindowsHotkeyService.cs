#if WINDOWS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CSimple.Platforms.Windows
{
    public class WindowsHotkeyService : CSimple.Services.IHotkeyService
    {
        private readonly Dictionary<string, int> _registeredHotkeys = new();
        private readonly Dictionary<int, Action> _hotkeyActions = new();
        private int _hotkeyCounter = 1;
        private IntPtr _windowHandle;

        // Windows API imports
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // Modifier keys
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;

        // Virtual key codes
        private const uint VK_F12 = 0x7B;
        private const uint VK_OEM_3 = 0xC0; // Tilde key (~)

        public void Initialize(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
        }

        public void RegisterHotkey(string key, Action action)
        {
            if (_windowHandle == IntPtr.Zero)
            {
                Debug.WriteLine("Warning: Window handle not set for hotkey service");
                return;
            }

            try
            {
                var hotkeyId = _hotkeyCounter++;
                uint modifiers = 0;
                uint virtualKey = 0;

                // Parse the key combination
                ParseKeyString(key, out modifiers, out virtualKey);

                if (RegisterHotKey(_windowHandle, hotkeyId, modifiers, virtualKey))
                {
                    _registeredHotkeys[key] = hotkeyId;
                    _hotkeyActions[hotkeyId] = action;
                    Debug.WriteLine($"Registered hotkey: {key} with ID {hotkeyId}");
                }
                else
                {
                    Debug.WriteLine($"Failed to register hotkey: {key}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering hotkey {key}: {ex.Message}");
            }
        }

        public void UnregisterHotkey(string key)
        {
            if (_registeredHotkeys.TryGetValue(key, out int hotkeyId))
            {
                UnregisterHotKey(_windowHandle, hotkeyId);
                _registeredHotkeys.Remove(key);
                _hotkeyActions.Remove(hotkeyId);
                Debug.WriteLine($"Unregistered hotkey: {key}");
            }
        }

        public void UnregisterAllHotkeys()
        {
            foreach (var kvp in _registeredHotkeys)
            {
                UnregisterHotKey(_windowHandle, kvp.Value);
            }
            _registeredHotkeys.Clear();
            _hotkeyActions.Clear();
        }

        public void HandleHotkeyMessage(int hotkeyId)
        {
            if (_hotkeyActions.TryGetValue(hotkeyId, out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error executing hotkey action: {ex.Message}");
                }
            }
        }

        private void ParseKeyString(string key, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            var parts = key.ToLower().Split('+');

            foreach (var part in parts)
            {
                switch (part.Trim())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "alt":
                        modifiers |= MOD_ALT;
                        break;
                    case "shift":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= MOD_WIN;
                        break;
                    case "f12":
                        virtualKey = VK_F12;
                        break;
                    case "~":
                    case "tilde":
                        virtualKey = VK_OEM_3;
                        break;
                    default:
                        // Try to parse as a character
                        if (part.Length == 1)
                        {
                            virtualKey = (uint)char.ToUpper(part[0]);
                        }
                        break;
                }
            }
        }
    }
}
#endif
