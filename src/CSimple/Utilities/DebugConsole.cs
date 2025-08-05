using System;
using System.Diagnostics;
using CSimple.Services;

namespace CSimple.Utilities
{
    /// <summary>
    /// Enhanced debug utility that outputs to both Visual Studio debug console and custom debug console window
    /// </summary>
    public static class DebugConsole
    {
        private static IDebugConsoleService _consoleService;
        private static bool _isInitialized = false;

        /// <summary>
        /// Initialize the debug console with the provided service
        /// </summary>
        /// <param name="consoleService">The debug console service to use</param>
        public static void Initialize(IDebugConsoleService consoleService)
        {
            _consoleService = consoleService;
            _isInitialized = true;

            if (_consoleService != null)
            {
                _consoleService.Initialize();
            }
        }

        /// <summary>
        /// Write a message to both debug outputs
        /// </summary>
        /// <param name="message">The message to write</param>
        public static void WriteLine(string message)
        {
            WriteLine("DEBUG", message);
        }

        /// <summary>
        /// Write a message to both debug outputs with a specific level
        /// </summary>
        /// <param name="level">The log level</param>
        /// <param name="message">The message to write</param>
        public static void WriteLine(string level, string message)
        {
            // Always write to Visual Studio debug output
            Debug.WriteLine($"[{level}] {message}");

            // Also write to custom console if available
            if (_isInitialized && _consoleService != null)
            {
                try
                {
                    _consoleService.WriteLine(level, message);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error writing to debug console: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Write an info message
        /// </summary>
        /// <param name="message">The message to write</param>
        public static void Info(string message)
        {
            WriteLine("INFO", message);
        }

        /// <summary>
        /// Write a warning message
        /// </summary>
        /// <param name="message">The message to write</param>
        public static void Warning(string message)
        {
            WriteLine("WARNING", message);
        }

        /// <summary>
        /// Write an error message
        /// </summary>
        /// <param name="message">The message to write</param>
        public static void Error(string message)
        {
            WriteLine("ERROR", message);
        }

        /// <summary>
        /// Write a success message
        /// </summary>
        /// <param name="message">The message to write</param>
        public static void Success(string message)
        {
            WriteLine("SUCCESS", message);
        }

        /// <summary>
        /// Show the debug console window
        /// </summary>
        public static void Show()
        {
            if (_isInitialized && _consoleService != null)
            {
                _consoleService.Show();
            }
        }

        /// <summary>
        /// Hide the debug console window
        /// </summary>
        public static void Hide()
        {
            if (_isInitialized && _consoleService != null)
            {
                _consoleService.Hide();
            }
        }

        /// <summary>
        /// Clear the debug console
        /// </summary>
        public static void Clear()
        {
            if (_isInitialized && _consoleService != null)
            {
                _consoleService.Clear();
            }
        }

        /// <summary>
        /// Toggle the visibility of the debug console
        /// </summary>
        public static void Toggle()
        {
            if (_isInitialized && _consoleService != null)
            {
                if (_consoleService.IsVisible)
                {
                    _consoleService.Hide();
                }
                else
                {
                    _consoleService.Show();
                }
            }
        }

        /// <summary>
        /// Check if the debug console is visible
        /// </summary>
        public static bool IsVisible => _isInitialized && _consoleService != null && _consoleService.IsVisible;

        /// <summary>
        /// Get the console service instance
        /// </summary>
        public static IDebugConsoleService ConsoleService => _consoleService;
    }
}
