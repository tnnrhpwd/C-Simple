using System;
using System.Collections.Generic;
using System.Linq;
using CSimple.Models; // Assuming DataItem is in CSimple.Models

namespace CSimple.Utils
{
    public static class ActionServiceUtils
    {
        public static void ParseDataItemText(DataItem dataItem)
        {
            if (string.IsNullOrEmpty(dataItem?.Data?.Text)) return;

            var parts = dataItem.Data.Text.Split('|', StringSplitOptions.RemoveEmptyEntries);
            var creatorPart = parts.FirstOrDefault(p => p.StartsWith("Creator:"));
            var actionPart = parts.FirstOrDefault(p => p.StartsWith("Action:"));
            var publicPart = parts.FirstOrDefault(p => p.StartsWith("IsPublic:"));

            dataItem.Creator = (creatorPart != null) ? creatorPart.Substring("Creator:".Length).Trim() : "";
            if (dataItem.Data.ActionGroupObject != null)
            {
                var actionGroup = dataItem.Data.ActionGroupObject;
                actionGroup.ActionName = (actionPart != null && actionPart.Contains("\"ActionName\":\""))
                    ? ExtractStringBetween(actionPart, "\"ActionName\":\"", "\",")
                    : (actionPart != null
                        ? (actionPart.Length > 50
                            ? actionPart.Substring(0, 50) + "..."
                            : actionPart.Substring("Action:".Length).Trim())
                        : "");
            }
            if (publicPart != null)
            {
                try
                {
                    dataItem.IsPublic = publicPart.Substring("IsPublic:".Length).Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                }
                catch (ArgumentOutOfRangeException) // Or IndexOutOfRangeException depending on implementation
                {
                    dataItem.IsPublic = false;
                }
                catch (Exception ex) // Catch broader exceptions if needed
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing IsPublic: {ex.Message}");
                    dataItem.IsPublic = false;
                }
            }
            else
            {
                dataItem.IsPublic = false;
            }
        }

        private static string ExtractStringBetween(string source, string start, string end)
        {
            int startIndex = source.IndexOf(start);
            if (startIndex < 0) return "";
            startIndex += start.Length;
            int endIndex = source.IndexOf(end, startIndex);
            if (endIndex < 0) return ""; // Or handle case where end string is not found
            return source.Substring(startIndex, endIndex - startIndex);
        }

        public static List<DataItem> SortDataItems(List<DataItem> items, int sortIndex)
        {
            switch (sortIndex)
            {
                case 0: // CreatedAt Ascending
                    return items.OrderBy(d => d.createdAt).ToList();
                case 1: // CreatedAt Descending
                    return items.OrderByDescending(d => d.createdAt).ToList();
                case 2: // Creator Ascending
                    return items.OrderBy(d => d.Creator, StringComparer.OrdinalIgnoreCase).ToList();
                case 3: // Creator Descending
                    return items.OrderByDescending(d => d.Creator, StringComparer.OrdinalIgnoreCase).ToList();
                case 4: // ActionName Ascending
                    return items.OrderBy(d => d.Data?.ActionGroupObject?.ActionName, StringComparer.OrdinalIgnoreCase).ToList();
                case 5: // ActionName Descending
                    return items.OrderByDescending(d => d.Data?.ActionGroupObject?.ActionName, StringComparer.OrdinalIgnoreCase).ToList();
                default:
                    return items; // No sorting or return original list
            }
        }

        public static string DetermineCategory(ActionGroup actionGroup)
        {
            if (actionGroup == null) return "Unknown";
            string name = actionGroup.ActionName?.ToLowerInvariant() ?? "";
            // Consider analyzing ActionArray steps for more accuracy if needed
            // string steps = actionGroup.ActionArray?.FirstOrDefault()?.ToString()?.ToLowerInvariant() ?? "";

            if (name.Contains("excel") || name.Contains("spreadsheet")) return "Data Analysis";
            if (name.Contains("word") || name.Contains("document")) return "Document Editing";
            if (name.Contains("browser") || name.Contains("chrome") || name.Contains("firefox") || name.Contains("edge") || name.Contains("navigate")) return "Browser";
            if (name.Contains("file") || name.Contains("folder") || name.Contains("copy") || name.Contains("move") || name.Contains("explorer")) return "File Management";
            if (name.Contains("email") || name.Contains("outlook") || name.Contains("teams") || name.Contains("slack") || name.Contains("mail")) return "Communication";
            if (name.Contains("code") || name.Contains("visual studio") || name.Contains("vs code") || name.Contains("develop") || name.Contains("debug")) return "Development";
            if (name.Contains("system") || name.Contains("settings") || name.Contains("control panel") || name.Contains("admin")) return "System";
            if (name.Contains("game") || name.Contains("play") || name.Contains("steam")) return "Gaming"; // Example category

            // Default category
            return "Productivity";
        }

        public static string DetermineActionTypeFromSteps(ActionGroup actionGroup)
        {
            if (actionGroup?.ActionArray == null || !actionGroup.ActionArray.Any())
                return "Unknown";

            int keyboardActions = 0;
            int mouseClickActions = 0;
            int mouseMoveActions = 0;
            // int applicationActions = 0; // Example: if you track app launch actions

            foreach (var action in actionGroup.ActionArray)
            {
                // Use EventType codes for more reliable detection
                switch (action.EventType)
                {
                    case 0x0100: // WM_KEYDOWN
                    case 0x0101: // WM_KEYUP
                        keyboardActions++;
                        break;
                    case 0x0201: // WM_LBUTTONDOWN
                    case 0x0202: // WM_LBUTTONUP
                    case 0x0204: // WM_RBUTTONDOWN
                    case 0x0205: // WM_RBUTTONUP
                    case 0x0207: // WM_MBUTTONDOWN
                    case 0x0208: // WM_MBUTTONUP
                        mouseClickActions++;
                        break;
                    case 0x0200: // WM_MOUSEMOVE (or your custom code like 512)
                        mouseMoveActions++;
                        break;
                        // Add cases for other event types if needed
                }
            }

            int totalActions = keyboardActions + mouseClickActions + mouseMoveActions;
            if (totalActions == 0) return "Empty";

            // Prioritize based on counts
            if (keyboardActions > mouseClickActions + mouseMoveActions) return "Keyboard Heavy";
            if (mouseClickActions > keyboardActions + mouseMoveActions) return "Click Heavy";
            if (mouseMoveActions > keyboardActions + mouseClickActions) return "Movement Heavy";

            if (keyboardActions > 0 && (mouseClickActions > 0 || mouseMoveActions > 0)) return "Mixed Input";
            if (keyboardActions > 0) return "Keyboard Only";
            if (mouseClickActions > 0 || mouseMoveActions > 0) return "Mouse Only";

            return "Custom Action"; // Fallback
        }
    }
}
