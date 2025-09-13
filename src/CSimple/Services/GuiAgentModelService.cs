using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using CSimple.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;

namespace CSimple.Services
{
    /// <summary>
    /// Specialized service for handling GUI agent models like GUI-OWL-7B
    /// Provides enhanced multimodal (vision + text) input processing and GUI-specific output parsing
    /// </summary>
    public class GuiAgentModelService
    {
        private readonly IServiceProvider _serviceProvider;

        public GuiAgentModelService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Determines if a model is a GUI agent model based on its properties
        /// </summary>
        public bool IsGuiAgentModel(NeuralNetworkModel model)
        {
            if (model == null) return false;

            var modelName = (model.Name ?? "").ToLowerInvariant();
            var modelId = (model.HuggingFaceModelId ?? "").ToLowerInvariant();

            // Check for GUI agent model patterns
            return model.InputType == ModelInputType.Multimodal &&
                   (modelName.Contains("gui-owl") ||
                    modelName.Contains("gui-agent") ||
                    modelName.Contains("screen-agent") ||
                    modelName.Contains("ui-agent") ||
                    modelId.Contains("gui-owl") ||
                    modelId.Contains("gui-agent") ||
                    modelName.Contains("visual-ui") ||
                    modelName.Contains("gui-automation"));
        }

        /// <summary>
        /// Prepares multimodal input for GUI agent models (vision + text)
        /// </summary>
        public async Task<string> PrepareGuiAgentInputAsync(
            List<byte[]> screenshots,
            string textInstruction,
            Dictionary<string, object> context = null)
        {
            var inputBuilder = new List<string>();

            // Add GUI-specific context
            inputBuilder.Add("=== GUI AUTOMATION TASK ===");

            // Add screenshot information
            if (screenshots?.Any() == true)
            {
                inputBuilder.Add($"Screen captures available: {screenshots.Count}");
                inputBuilder.Add("Analyze the current screen state for UI elements and layout.");
            }
            else
            {
                inputBuilder.Add("No screen capture available - use text context only.");
            }

            // Add task instruction
            if (!string.IsNullOrEmpty(textInstruction))
            {
                inputBuilder.Add("TASK INSTRUCTION:");
                inputBuilder.Add(textInstruction);
            }

            // Add application context if available
            if (context?.ContainsKey("activeWindow") == true)
            {
                inputBuilder.Add($"Active Application: {context["activeWindow"]}");
            }

            if (context?.ContainsKey("availableElements") == true)
            {
                inputBuilder.Add($"UI Elements: {context["availableElements"]}");
            }

            // Add output format specification
            inputBuilder.Add("");
            inputBuilder.Add("OUTPUT FORMAT: Single GUI action command only");
            inputBuilder.Add("Examples: 'click login_button', 'type username_field hello', 'scroll down', 'none'");

            return string.Join("\n", inputBuilder);
        }

        /// <summary>
        /// Parses GUI agent model output into structured action commands
        /// </summary>
        public GuiAction ParseGuiAgentOutput(string modelOutput)
        {
            if (string.IsNullOrEmpty(modelOutput))
            {
                return new GuiAction { Type = GuiActionType.Wait, Target = "", Value = "" };
            }

            var cleanOutput = CleanGuiModelOutput(modelOutput);
            Debug.WriteLine($"[GuiAgentModelService] Parsing output: '{cleanOutput}'");

            // Parse different GUI action types
            if (cleanOutput.StartsWith("click "))
            {
                var target = cleanOutput.Substring(6).Trim();
                return new GuiAction { Type = GuiActionType.Click, Target = target, Value = "" };
            }
            else if (cleanOutput.StartsWith("right_click "))
            {
                var target = cleanOutput.Substring(12).Trim();
                return new GuiAction { Type = GuiActionType.RightClick, Target = target, Value = "" };
            }
            else if (cleanOutput.StartsWith("double_click "))
            {
                var target = cleanOutput.Substring(13).Trim();
                return new GuiAction { Type = GuiActionType.DoubleClick, Target = target, Value = "" };
            }
            else if (cleanOutput.StartsWith("type "))
            {
                var parts = cleanOutput.Substring(5).Split(' ', 2);
                var target = parts.Length > 0 ? parts[0] : "";
                var value = parts.Length > 1 ? parts[1] : "";
                return new GuiAction { Type = GuiActionType.Type, Target = target, Value = value };
            }
            else if (cleanOutput.StartsWith("key "))
            {
                var keyName = cleanOutput.Substring(4).Trim();
                return new GuiAction { Type = GuiActionType.Key, Target = "", Value = keyName };
            }
            else if (cleanOutput.StartsWith("scroll "))
            {
                var direction = cleanOutput.Substring(7).Trim();
                return new GuiAction { Type = GuiActionType.Scroll, Target = "", Value = direction };
            }
            else if (cleanOutput.StartsWith("drag "))
            {
                var parts = cleanOutput.Substring(5).Split(' ');
                var from = parts.Length > 0 ? parts[0] : "";
                var to = parts.Length > 1 ? parts[1] : "";
                return new GuiAction { Type = GuiActionType.Drag, Target = from, Value = to };
            }
            else if (cleanOutput.StartsWith("select "))
            {
                var target = cleanOutput.Substring(7).Trim();
                return new GuiAction { Type = GuiActionType.Select, Target = target, Value = "" };
            }
            else if (cleanOutput == "wait" || cleanOutput == "none" || cleanOutput == "no action")
            {
                return new GuiAction { Type = GuiActionType.Wait, Target = "", Value = "" };
            }

            // Fallback - try to extract any actionable command
            Debug.WriteLine($"[GuiAgentModelService] No specific pattern matched, defaulting to wait");
            return new GuiAction { Type = GuiActionType.Wait, Target = "", Value = "" };
        }

        /// <summary>
        /// Cleans GUI model output for parsing
        /// </summary>
        private string CleanGuiModelOutput(string output)
        {
            if (string.IsNullOrEmpty(output)) return "";

            var cleaned = output.ToLowerInvariant()
                .Replace("action:", "")
                .Replace("gui action:", "")
                .Replace("ui action:", "")
                .Replace("output:", "")
                .Replace("result:", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("`", "")
                .Replace("[", "")
                .Replace("]", "")
                .Trim();

            // Take first line if multi-line
            var lines = cleaned.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.FirstOrDefault()?.Trim() ?? "";
        }

        /// <summary>
        /// Converts GUI action to legacy action format for compatibility
        /// </summary>
        public string ConvertToLegacyActionFormat(GuiAction guiAction)
        {
            switch (guiAction.Type)
            {
                case GuiActionType.Click:
                    return $"click on {guiAction.Target}";
                case GuiActionType.RightClick:
                    return $"right click on {guiAction.Target}";
                case GuiActionType.DoubleClick:
                    return $"double click on {guiAction.Target}";
                case GuiActionType.Type:
                    return string.IsNullOrEmpty(guiAction.Target) ?
                           $"type {guiAction.Value}" :
                           $"type {guiAction.Value}";
                case GuiActionType.Key:
                    return $"press {guiAction.Value}";
                case GuiActionType.Scroll:
                    return $"scroll {guiAction.Value}";
                case GuiActionType.Drag:
                    return $"drag from {guiAction.Target} to {guiAction.Value}";
                case GuiActionType.Select:
                    return $"select {guiAction.Target}";
                case GuiActionType.Wait:
                default:
                    return "none";
            }
        }
    }

    /// <summary>
    /// Represents a GUI action command from a GUI agent model
    /// </summary>
    public class GuiAction
    {
        public GuiActionType Type { get; set; }
        public string Target { get; set; } = "";
        public string Value { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Types of GUI actions supported by GUI agent models
    /// </summary>
    public enum GuiActionType
    {
        Click,
        RightClick,
        DoubleClick,
        Type,
        Key,
        Scroll,
        Drag,
        Select,
        Wait
    }
}