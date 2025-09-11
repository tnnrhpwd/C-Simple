using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using CSimple.Models;

namespace CSimple.Services
{
    /// <summary>
    /// Service for generating executable action strings from text commands and resolving coordinates
    /// </summary>
    public class ActionStringGenerationService
    {
        private readonly WindowDetectionService _windowDetectionService;
        private readonly ScreenAnalysisService _screenAnalysisService;

        public ActionStringGenerationService()
        {
            _windowDetectionService = new WindowDetectionService();
            _screenAnalysisService = new ScreenAnalysisService();
        }

        /// <summary>
        /// Converts a text command into an executable action string with resolved coordinates
        /// </summary>
        public async Task<string> GenerateExecutableActionString(string textCommand, string planContext = null)
        {
            try
            {
                Debug.WriteLine($"[ActionStringGeneration] Processing command: {textCommand}");

                var actionCommand = ParseTextCommand(textCommand);
                if (actionCommand == null)
                {
                    Debug.WriteLine($"[ActionStringGeneration] Could not parse command: {textCommand}");
                    return null;
                }

                // Resolve coordinates if needed
                if (actionCommand.RequiresCoordinates)
                {
                    var coordinates = await ResolveCoordinates(actionCommand, planContext);
                    if (coordinates.HasValue)
                    {
                        actionCommand.X = coordinates.Value.X;
                        actionCommand.Y = coordinates.Value.Y;
                    }
                    else
                    {
                        Debug.WriteLine($"[ActionStringGeneration] Could not resolve coordinates for: {textCommand}");
                        return null; // Cannot execute without coordinates
                    }
                }

                // Generate the executable action string
                return GenerateActionString(actionCommand);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionStringGeneration] Error generating action string: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Parses text commands into structured action commands
        /// </summary>
        private ActionCommand ParseTextCommand(string textCommand)
        {
            var command = textCommand.ToLowerInvariant().Trim();

            // Mouse actions
            if (command.Contains("double click"))
            {
                return new ActionCommand
                {
                    ActionType = "DoubleClick",
                    RequiresCoordinates = true,
                    TargetDescription = ExtractTarget(command)
                };
            }

            if (command.Contains("click") || command.Contains("left click"))
            {
                return new ActionCommand
                {
                    ActionType = "LeftClick",
                    RequiresCoordinates = true,
                    TargetDescription = ExtractTarget(command)
                };
            }

            if (command.Contains("right click"))
            {
                return new ActionCommand
                {
                    ActionType = "RightClick",
                    RequiresCoordinates = true,
                    TargetDescription = ExtractTarget(command)
                };
            }

            if (command.Contains("move mouse") || command.Contains("move to"))
            {
                return new ActionCommand
                {
                    ActionType = "MouseMove",
                    RequiresCoordinates = true,
                    TargetDescription = ExtractTarget(command)
                };
            }

            // Keyboard actions
            if (command.Contains("press") || command.Contains("key"))
            {
                var key = ExtractKey(command);
                return new ActionCommand
                {
                    ActionType = "KeyPress",
                    KeyCode = key,
                    RequiresCoordinates = false
                };
            }

            if (command.Contains("type") || command.Contains("enter text"))
            {
                var text = ExtractText(command);
                return new ActionCommand
                {
                    ActionType = "TypeText",
                    Text = text,
                    RequiresCoordinates = false
                };
            }

            // Drag actions
            if (command.Contains("drag"))
            {
                return new ActionCommand
                {
                    ActionType = "Drag",
                    RequiresCoordinates = true,
                    TargetDescription = ExtractTarget(command),
                    RequiresEndCoordinates = true
                };
            }

            // Scroll actions
            if (command.Contains("scroll"))
            {
                var direction = command.Contains("up") ? "Up" : command.Contains("down") ? "Down" : "Up";
                return new ActionCommand
                {
                    ActionType = "Scroll",
                    ScrollDirection = direction,
                    RequiresCoordinates = true,
                    TargetDescription = ExtractTarget(command)
                };
            }

            return null; // Unknown command
        }

        /// <summary>
        /// Extracts target description from command text
        /// </summary>
        private string ExtractTarget(string command)
        {
            // Extract text after "on" keyword
            if (command.Contains(" on "))
            {
                var parts = command.Split(" on ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    return parts[1].Trim();
                }
            }

            // Extract text after "at" keyword
            if (command.Contains(" at "))
            {
                var parts = command.Split(" at ", StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1)
                {
                    return parts[1].Trim();
                }
            }

            return "screen"; // Default target
        }

        /// <summary>
        /// Extracts key from keyboard command
        /// </summary>
        private string ExtractKey(string command)
        {
            // Common key patterns
            var keyMappings = new Dictionary<string, string>
            {
                { "enter", "Return" }, { "return", "Return" },
                { "space", "Space" }, { "spacebar", "Space" },
                { "tab", "Tab" }, { "escape", "Escape" }, { "esc", "Escape" },
                { "ctrl", "Control" }, { "control", "Control" },
                { "alt", "Alt" }, { "shift", "Shift" },
                { "delete", "Delete" }, { "backspace", "BackSpace" },
                { "f1", "F1" }, { "f2", "F2" }, { "f3", "F3" }, { "f4", "F4" },
                { "f5", "F5" }, { "f6", "F6" }, { "f7", "F7" }, { "f8", "F8" },
                { "f9", "F9" }, { "f10", "F10" }, { "f11", "F11" }, { "f12", "F12" }
            };

            foreach (var mapping in keyMappings)
            {
                if (command.Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }

            // Extract single character keys
            var words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                if (word.Length == 1 && char.IsLetterOrDigit(word[0]))
                {
                    return word.ToUpperInvariant();
                }
            }

            return "Space"; // Default key
        }

        /// <summary>
        /// Extracts text to type from command
        /// </summary>
        private string ExtractText(string command)
        {
            if (command.Contains("\""))
            {
                var start = command.IndexOf("\"") + 1;
                var end = command.LastIndexOf("\"");
                if (start < end)
                {
                    return command.Substring(start, end - start);
                }
            }

            // Extract text after "type" or "enter"
            var keywords = new[] { "type ", "enter text ", "enter " };
            foreach (var keyword in keywords)
            {
                if (command.Contains(keyword))
                {
                    var index = command.IndexOf(keyword) + keyword.Length;
                    return command.Substring(index).Trim();
                }
            }

            return "";
        }

        /// <summary>
        /// Resolves coordinates for target description
        /// </summary>
        private async Task<Point?> ResolveCoordinates(ActionCommand actionCommand, string planContext)
        {
            try
            {
                var target = actionCommand.TargetDescription;

                // Try window detection first
                if (target.Contains("window"))
                {
                    var windowName = ExtractWindowName(target);
                    var windowCoords = await _windowDetectionService.GetWindowCenterAsync(windowName);
                    if (windowCoords.HasValue)
                    {
                        Debug.WriteLine($"[ActionStringGeneration] Found window '{windowName}' at {windowCoords.Value}");
                        return windowCoords.Value;
                    }
                }

                // Try UI element detection
                if (target.Contains("button") || target.Contains("menu") || target.Contains("icon"))
                {
                    var elementCoords = await _screenAnalysisService.FindUIElementAsync(target, planContext);
                    if (elementCoords.HasValue)
                    {
                        Debug.WriteLine($"[ActionStringGeneration] Found UI element '{target}' at {elementCoords.Value}");
                        return elementCoords.Value;
                    }
                }

                // Fallback to screen center for generic targets
                if (target == "screen" || string.IsNullOrEmpty(target))
                {
                    var screenBounds = Screen.PrimaryScreen.Bounds;
                    return new Point(screenBounds.Width / 2, screenBounds.Height / 2);
                }

                return null; // Could not resolve coordinates
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionStringGeneration] Error resolving coordinates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts window name from target description
        /// </summary>
        private string ExtractWindowName(string target)
        {
            // Remove common words to extract application name
            var cleanTarget = target.Replace("window", "").Replace("the", "").Trim();

            // Common application mappings
            var appMappings = new Dictionary<string, string>
            {
                { "minecraft", "Minecraft" },
                { "notepad", "Notepad" },
                { "browser", "Chrome" },
                { "chrome", "Chrome" },
                { "firefox", "Firefox" },
                { "explorer", "File Explorer" },
                { "calculator", "Calculator" }
            };

            foreach (var mapping in appMappings)
            {
                if (cleanTarget.ToLowerInvariant().Contains(mapping.Key))
                {
                    return mapping.Value;
                }
            }

            return cleanTarget; // Return as-is if no mapping found
        }

        /// <summary>
        /// Generates the final executable action string
        /// </summary>
        private string GenerateActionString(ActionCommand actionCommand)
        {
            var actionData = new
            {
                ActionType = actionCommand.ActionType,
                X = actionCommand.X,
                Y = actionCommand.Y,
                EndX = actionCommand.EndX,
                EndY = actionCommand.EndY,
                KeyCode = actionCommand.KeyCode,
                Text = actionCommand.Text,
                ScrollDirection = actionCommand.ScrollDirection,
                Duration = actionCommand.Duration ?? 100, // Default 100ms duration
                Timestamp = DateTime.Now
            };

            // Return as JSON string that ActionService can parse
            return JsonSerializer.Serialize(actionData, new JsonSerializerOptions { WriteIndented = false });
        }
    }

    /// <summary>
    /// Represents a parsed action command
    /// </summary>
    public class ActionCommand
    {
        public string ActionType { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int EndX { get; set; }
        public int EndY { get; set; }
        public string KeyCode { get; set; }
        public string Text { get; set; }
        public string ScrollDirection { get; set; }
        public int? Duration { get; set; }
        public bool RequiresCoordinates { get; set; }
        public bool RequiresEndCoordinates { get; set; }
        public string TargetDescription { get; set; }
    }
}
