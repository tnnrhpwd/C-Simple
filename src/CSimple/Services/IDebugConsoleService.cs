using System;

namespace CSimple.Services
{
    public interface IDebugConsoleService
    {
        /// <summary>
        /// Initialize the debug console window
        /// </summary>
        void Initialize();

        /// <summary>
        /// Show the debug console window
        /// </summary>
        void Show();

        /// <summary>
        /// Hide the debug console window
        /// </summary>
        void Hide();

        /// <summary>
        /// Write a message to the debug console
        /// </summary>
        /// <param name="message">The message to write</param>
        void WriteLine(string message);

        /// <summary>
        /// Write a message to the debug console with a specific log level
        /// </summary>
        /// <param name="level">The log level (INFO, WARNING, ERROR, DEBUG)</param>
        /// <param name="message">The message to write</param>
        void WriteLine(string level, string message);

        /// <summary>
        /// Clear the console output
        /// </summary>
        void Clear();

        /// <summary>
        /// Close and dispose of the console window
        /// </summary>
        void Close();

        /// <summary>
        /// Gets or sets whether the console is visible
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Event fired when the console window is closed
        /// </summary>
        event EventHandler ConsoleClosed;
    }
}
