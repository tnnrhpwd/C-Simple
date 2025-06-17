using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSimple.Models;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace CSimple.Helpers
{
    public static class ActionItemParser
    {
        public static List<ActionItem> ParseActionStepsText(string actionStepsText, ActionGroup actionGroup)
        {
            List<ActionItem> parsedActionItems = new List<ActionItem>();

            if (string.IsNullOrWhiteSpace(actionStepsText))
            {
                Debug.WriteLine("ActionStepsText is null or empty");
                return parsedActionItems;
            }

            // Split by lines, but be more flexible about line endings
            var lines = actionStepsText.Split(new[] { Environment.NewLine, "\n", "\r\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            Debug.WriteLine($"Parsing {lines.Length} lines from ActionStepsText");

            foreach (var line in lines)
            {
                try
                {
                    // Skip empty or whitespace-only lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    Debug.WriteLine($"Processing line: {line}");

                    // Try to parse the line - handle both corrupted and proper formats
                    ActionItem actionItem = ParseSingleLine(line);

                    if (actionItem != null)
                    {
                        parsedActionItems.Add(actionItem);
                        Debug.WriteLine($"Created ActionItem with EventType: {actionItem.EventType}");
                    }
                    else
                    {
                        Debug.WriteLine($"Failed to parse line: {line}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing line '{line}': {ex.Message}");
                }
            }

            Debug.WriteLine($"Successfully parsed {parsedActionItems.Count} ActionItems");

            // If we parsed very few items compared to original, something went wrong
            if (actionGroup?.ActionArray != null && parsedActionItems.Count < actionGroup.ActionArray.Count * 0.5)
            {
                Debug.WriteLine($"Warning: Only parsed {parsedActionItems.Count} items from {actionGroup.ActionArray.Count} original items. This might indicate parsing issues.");

                // Optionally, return the original array to prevent data loss
                if (parsedActionItems.Count < 10 && actionGroup.ActionArray.Count > 50)
                {
                    Debug.WriteLine("Preventing potential data loss - returning original action array");
                    return actionGroup.ActionArray.ToList();
                }
            }

            return parsedActionItems;
        }

        private static ActionItem ParseSingleLine(string line)
        {
            try
            {
                // Method 1: Try parsing the standard format with | separators
                if (line.Contains(" | "))
                {
                    return ParseStandardFormat(line);
                }

                // Method 2: Try parsing corrupted format (common when Editor mangles the text)
                if (line.Contains("Description:"))
                {
                    return ParseCorruptedFormat(line);
                }

                // Method 3: Try parsing simple description format
                return ParseSimpleFormat(line);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ParseSingleLine: {ex.Message}");
                return null;
            }
        }

        private static ActionItem ParseStandardFormat(string line)
        {
            // Expected format: "Description: {description} | Key: {keyName} | Code: {keyCode} | MouseButton: {mouseButtonType} {mouseButtonAction} | Timestamp: {timestamp}"
            var parts = line.Split('|');
            if (parts.Length < 3) // Minimum parts needed
                return null;

            string description = ExtractValue(parts[0], "Description:");
            string keyName = parts.Length > 1 ? ExtractValue(parts[1], "Key:") : "";
            string keyCodeStr = parts.Length > 2 ? ExtractValue(parts[2], "Code:") : "0";
            string mouseButtonInfo = parts.Length > 3 ? ExtractValue(parts[3], "MouseButton:") : "";
            string timestampStr = parts.Length > 4 ? ExtractValue(parts[4], "Timestamp:") : "";

            return CreateActionItemFromExtractedData(description, keyName, keyCodeStr, mouseButtonInfo, timestampStr);
        }

        private static ActionItem ParseCorruptedFormat(string line)
        {
            // Handle cases where the line breaks occurred within the formatted text
            // Look for patterns like "Description: something" even if other parts are missing

            string description = "";
            string keyName = "";
            string keyCodeStr = "0";
            string mouseButtonInfo = "";
            string timestampStr = "";

            // Extract description
            if (line.Contains("Description:"))
            {
                int descStart = line.IndexOf("Description:") + 12;
                int descEnd = line.Length;

                // Look for the next field or end of meaningful content
                var nextFields = new[] { " | Key:", " | Code:", " | MouseButton:", " | Timestamp:", "Key:", "Code:", "MouseButton:", "Timestamp:" };
                foreach (var field in nextFields)
                {
                    int fieldIndex = line.IndexOf(field, descStart);
                    if (fieldIndex > descStart && fieldIndex < descEnd)
                    {
                        descEnd = fieldIndex;
                    }
                }

                description = line.Substring(descStart, descEnd - descStart).Trim();
            }

            // Try to extract other fields if they exist
            if (line.Contains("Key:"))
            {
                keyName = ExtractValueFromAnywhere(line, "Key:");
            }

            if (line.Contains("Code:"))
            {
                keyCodeStr = ExtractValueFromAnywhere(line, "Code:");
            }

            if (line.Contains("MouseButton:"))
            {
                mouseButtonInfo = ExtractValueFromAnywhere(line, "MouseButton:");
            }

            if (line.Contains("Timestamp:"))
            {
                timestampStr = ExtractValueFromAnywhere(line, "Timestamp:");
            }

            return CreateActionItemFromExtractedData(description, keyName, keyCodeStr, mouseButtonInfo, timestampStr);
        }

        private static ActionItem ParseSimpleFormat(string line)
        {
            // Handle simple descriptions like "Mouse Move to X:1134, Y:399" or "Left Click at X:100, Y:200"
            var actionItem = new ActionItem();

            if (line.Contains("Mouse Move to X:"))
            {
                actionItem.EventType = 512; // WM_MOUSEMOVE
                actionItem.Coordinates = ExtractCoordinatesFromDescription(line);
                actionItem.Timestamp = DateTime.Now; // Default timestamp
                return actionItem;
            }

            if (line.Contains("Click at X:") || line.Contains("Click"))
            {
                if (line.Contains("Left"))
                {
                    actionItem.EventType = line.Contains("Down") ? 0x0201 : 0x0202;
                }
                else if (line.Contains("Right"))
                {
                    actionItem.EventType = line.Contains("Down") ? 0x0204 : 0x0205;
                }
                else
                {
                    actionItem.EventType = 0x0201; // Default to left click down
                }

                actionItem.Coordinates = ExtractCoordinatesFromDescription(line);
                actionItem.Timestamp = DateTime.Now;
                return actionItem;
            }

            if (line.Contains("Key ") && (line.Contains("Down") || line.Contains("Up")))
            {
                actionItem.EventType = line.Contains("Down") ? 256 : 257;
                actionItem.Timestamp = DateTime.Now;

                // Try to extract key code if present
                var match = System.Text.RegularExpressions.Regex.Match(line, @"Code:\s*(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int keyCode))
                {
                    actionItem.KeyCode = keyCode;
                }

                return actionItem;
            }

            return null; // Couldn't parse this format
        }

        private static string ExtractValueFromAnywhere(string text, string prefix)
        {
            if (!text.Contains(prefix))
                return "";

            int startIndex = text.IndexOf(prefix) + prefix.Length;
            if (startIndex >= text.Length)
                return "";

            // Find the end - look for common separators or end of string
            int endIndex = text.Length;
            var separators = new[] { " | ", "\n", "\r", " Key:", " Code:", " MouseButton:", " Timestamp:" };

            foreach (var sep in separators)
            {
                int sepIndex = text.IndexOf(sep, startIndex);
                if (sepIndex > startIndex && sepIndex < endIndex)
                {
                    endIndex = sepIndex;
                }
            }

            return text.Substring(startIndex, endIndex - startIndex).Trim();
        }

        private static ActionItem CreateActionItemFromExtractedData(string description, string keyName, string keyCodeStr, string mouseButtonInfo, string timestampStr)
        {
            var actionItem = new ActionItem();

            // Parse key code
            int keyCode = 0;
            if (!string.IsNullOrEmpty(keyCodeStr))
            {
                int.TryParse(keyCodeStr, out keyCode);
            }

            // Parse timestamp - be more flexible with timestamp formats
            DateTime? timestamp = null;
            if (!string.IsNullOrEmpty(timestampStr))
            {
                // Try multiple timestamp formats
                var formats = new[]
                {
                    "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                    "yyyy-MM-ddTHH:mm:ss.ffffffZ",
                    "yyyy-MM-ddTHH:mm:ss.fffffZ",
                    "yyyy-MM-ddTHH:mm:ss.ffffZ",
                    "yyyy-MM-ddTHH:mm:ss.fffZ",
                    "yyyy-MM-ddTHH:mm:ss.ffZ",
                    "yyyy-MM-ddTHH:mm:ss.fZ",
                    "yyyy-MM-ddTHH:mm:ssZ",
                    "M/d/yyyy H:mm:ss",
                    "M/d/yyyy h:mm:ss tt",
                    "yyyy-MM-dd HH:mm:ss",
                    "g", // General short date/time pattern
                    "G"  // General long date/time pattern
                };

                foreach (var format in formats)
                {
                    if (DateTime.TryParseExact(timestampStr, format, null, System.Globalization.DateTimeStyles.None, out DateTime parsedTime))
                    {
                        timestamp = parsedTime;
                        break;
                    }
                }

                // Fallback to general parsing
                if (!timestamp.HasValue && DateTime.TryParse(timestampStr, out DateTime fallbackTime))
                {
                    timestamp = fallbackTime;
                }
            }

            // Set timestamp (use current time if parsing failed)
            actionItem.Timestamp = timestamp ?? DateTime.Now;

            // Determine event type based on description and available data
            if (description.Contains("Mouse Move"))
            {
                actionItem.EventType = 512; // WM_MOUSEMOVE
                actionItem.Coordinates = ExtractCoordinatesFromDescription(description);
            }
            else if (!string.IsNullOrEmpty(mouseButtonInfo) || description.Contains("Click"))
            {
                // Mouse button event
                var (buttonType, buttonAction) = ParseMouseButtonInfo(mouseButtonInfo);

                // If we couldn't parse from mouseButtonInfo, try description
                if (string.IsNullOrEmpty(buttonType))
                {
                    if (description.Contains("Left"))
                        buttonType = "Left";
                    else if (description.Contains("Right"))
                        buttonType = "Right";
                    else if (description.Contains("Middle"))
                        buttonType = "Middle";
                }

                if (string.IsNullOrEmpty(buttonAction))
                {
                    if (description.Contains("Down"))
                        buttonAction = "Down";
                    else if (description.Contains("Up"))
                        buttonAction = "Up";
                    else
                        buttonAction = "Down"; // Default
                }

                actionItem.EventType = GetMouseButtonEventType(buttonType, buttonAction);
                actionItem.Coordinates = ExtractCoordinatesFromDescription(description);
            }
            else if (!string.IsNullOrEmpty(keyName) && keyCode > 0)
            {
                // Key event
                bool isKeyDown = description.Contains("Down") || !description.Contains("Up");
                actionItem.EventType = isKeyDown ? 256 : 257; // WM_KEYDOWN or WM_KEYUP
                actionItem.KeyCode = keyCode;
            }
            else if (description.Contains("Key") && keyCode > 0)
            {
                // Fallback for key events
                bool isKeyDown = description.Contains("Down") || !description.Contains("Up");
                actionItem.EventType = isKeyDown ? 256 : 257;
                actionItem.KeyCode = keyCode;
            }
            else
            {
                Debug.WriteLine($"Could not determine event type for: {description}");
                return null;
            }

            return actionItem;
        }

        private static Coordinates ExtractCoordinatesFromDescription(string description)
        {
            try
            {
                if (!description.Contains("X:") || !description.Contains("Y:"))
                    return null;

                int xStart = description.IndexOf("X:") + 2;
                int yStart = description.IndexOf("Y:") + 2;

                // Find the end of X value (next comma or space)
                int xEnd = description.IndexOfAny(new char[] { ',', ' ', ')' }, xStart);
                if (xEnd == -1) xEnd = description.Length;

                // Find the end of Y value
                int yEnd = description.Length;
                for (int i = yStart; i < description.Length; i++)
                {
                    if (!char.IsDigit(description[i]) && description[i] != '-')
                    {
                        yEnd = i;
                        break;
                    }
                }

                string xStr = description.Substring(xStart, xEnd - xStart).Trim(' ', ',');
                string yStr = description.Substring(yStart, yEnd - yStart).Trim(' ', ',', ')');

                if (int.TryParse(xStr, out int x) && int.TryParse(yStr, out int y))
                {
                    return new Coordinates { X = x, Y = y };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting coordinates from '{description}': {ex.Message}");
            }

            return null;
        }

        private static (string buttonType, string buttonAction) ParseMouseButtonInfo(string mouseButtonInfo)
        {
            if (string.IsNullOrEmpty(mouseButtonInfo))
                return ("", "");

            string buttonType = "";
            string buttonAction = "";

            // Extract button type
            if (mouseButtonInfo.Contains("Left"))
                buttonType = "Left";
            else if (mouseButtonInfo.Contains("Right"))
                buttonType = "Right";
            else if (mouseButtonInfo.Contains("Middle"))
                buttonType = "Middle";

            // Extract button action
            if (mouseButtonInfo.Contains("Down"))
                buttonAction = "Down";
            else if (mouseButtonInfo.Contains("Up"))
                buttonAction = "Up";

            return (buttonType, buttonAction);
        }

        private static int GetMouseButtonEventType(string buttonType, string buttonAction)
        {
            bool isDown = buttonAction == "Down";

            return buttonType switch
            {
                "Left" => isDown ? 0x0201 : 0x0202,    // WM_LBUTTONDOWN : WM_LBUTTONUP
                "Right" => isDown ? 0x0204 : 0x0205,   // WM_RBUTTONDOWN : WM_RBUTTONUP
                "Middle" => isDown ? 0x0207 : 0x0208,  // WM_MBUTTONDOWN : WM_MBUTTONUP
                _ => 0
            };
        }

        private static string ExtractValue(string part, string prefix)
        {
            if (string.IsNullOrEmpty(part) || !part.Contains(prefix))
                return string.Empty;

            int startIndex = part.IndexOf(prefix) + prefix.Length;
            if (startIndex >= part.Length)
                return string.Empty;

            return part.Substring(startIndex).Trim();
        }
    }
}
