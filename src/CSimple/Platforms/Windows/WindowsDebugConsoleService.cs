#if WINDOWS
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace CSimple.Platforms.Windows
{
    public class WindowsDebugConsoleService : CSimple.Services.IDebugConsoleService
    {
        private bool _consoleAllocated = false;
        private bool _isVisible = false;
        private IntPtr _consoleWindow = IntPtr.Zero;

        public bool IsVisible => _isVisible;

        public event EventHandler ConsoleClosed;

        // Windows API imports
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        // Constants for ShowWindow
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;

        // Constants for SetWindowPos
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        // Constants for GetSystemMetrics
        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public void Initialize()
        {
            if (!_consoleAllocated)
            {
                // Allocate a console for this application
                AllocConsole();
                _consoleAllocated = true;

                // Get the console window handle
                _consoleWindow = GetConsoleWindow();

                if (_consoleWindow != IntPtr.Zero)
                {
                    // Set console title
                    SetWindowText(_consoleWindow, "CSimple Debug Console");

                    // Redirect console output
                    RedirectConsoleOutput();

                    // Position the console window
                    PositionConsoleWindow();

                    _isVisible = true;

                    // Write initial message
                    WriteColoredLine("INFO", "CSimple Debug Console initialized", ConsoleColor.Green);
                    WriteColoredLine("INFO", "Console ready for debugging output", ConsoleColor.Cyan);
                    WriteColoredLine("DEBUG", $"Console window handle: {_consoleWindow}", ConsoleColor.Gray);
                    Console.WriteLine(new string('=', 60));
                }
            }
        }

        public void Show()
        {
            if (_consoleWindow != IntPtr.Zero)
            {
                ShowWindow(_consoleWindow, SW_SHOW);
                _isVisible = true;
            }
        }

        public void Hide()
        {
            if (_consoleWindow != IntPtr.Zero)
            {
                ShowWindow(_consoleWindow, SW_HIDE);
                _isVisible = false;
            }
        }

        public void WriteLine(string message)
        {
            WriteLine("INFO", message);
        }

        public void WriteLine(string level, string message)
        {
            if (_consoleAllocated)
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
                var formattedMessage = $"[{timestamp}] {message}";

                WriteColoredLine(level, formattedMessage, GetColorForLevel(level));
            }
        }

        public void Clear()
        {
            if (_consoleAllocated)
            {
                Console.Clear();
                WriteColoredLine("INFO", "Console cleared", ConsoleColor.Yellow);
            }
        }

        public void Close()
        {
            if (_consoleAllocated)
            {
                WriteLine("INFO", "Closing debug console...");

                ConsoleClosed?.Invoke(this, EventArgs.Empty);

                // Small delay to allow the message to be displayed
                System.Threading.Thread.Sleep(500);

                FreeConsole();
                _consoleAllocated = false;
                _isVisible = false;
                _consoleWindow = IntPtr.Zero;
            }
        }

        private void RedirectConsoleOutput()
        {
            try
            {
                // Redirect standard output to the console
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
                Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error redirecting console output: {ex.Message}");
            }
        }

        private void PositionConsoleWindow()
        {
            try
            {
                if (_consoleWindow != IntPtr.Zero)
                {
                    // Get screen dimensions using Windows API
                    var screenWidth = GetSystemMetrics(SM_CXSCREEN);
                    var screenHeight = GetSystemMetrics(SM_CYSCREEN);

                    var consoleWidth = 600;
                    var consoleHeight = 400;
                    var consoleX = screenWidth - consoleWidth - 20; // 20px margin from right
                    var consoleY = 50; // 50px from top

                    SetWindowPos(_consoleWindow, IntPtr.Zero, consoleX, consoleY, consoleWidth, consoleHeight, 0);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error positioning console window: {ex.Message}");
            }
        }

        private void WriteColoredLine(string level, string message, ConsoleColor color)
        {
            try
            {
                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
                Console.WriteLine($"[{level}] {message}");
                Console.ForegroundColor = originalColor;
            }
            catch (Exception ex)
            {
                // Fallback to regular console output if coloring fails
                Console.WriteLine($"[{level}] {message}");
                Debug.WriteLine($"Error writing colored console output: {ex.Message}");
            }
        }

        private ConsoleColor GetColorForLevel(string level)
        {
            return level.ToUpper() switch
            {
                "ERROR" => ConsoleColor.Red,
                "WARNING" => ConsoleColor.Yellow,
                "INFO" => ConsoleColor.White,
                "DEBUG" => ConsoleColor.Gray,
                "SUCCESS" => ConsoleColor.Green,
                _ => ConsoleColor.White
            };
        }
    }
}
#endif
