using CSimple.Models;
using CSimple.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class ActionReviewService
    {
        private readonly ActionService _actionService;

        public ActionReviewService(ActionService actionService)
        {
            _actionService = actionService;
        }

        public async Task<List<string>> LoadAvailableActionsAsync()
        {
            try
            {
                var actionNames = new List<string>();

                if (_actionService != null)
                {
                    var actionItems = await _actionService.LoadDataItemsFromFile();

                    // Sort actions by date (newest first)
                    actionItems = actionItems
                        .OrderByDescending(item => item?.createdAt ?? DateTime.MinValue)
                        .ToList();

                    // Extract action names and add to collection
                    foreach (var item in actionItems)
                    {
                        if (item?.Data?.ActionGroupObject?.ActionName != null)
                        {
                            actionNames.Add(item.Data.ActionGroupObject.ActionName);
                        }
                    }

                    Debug.WriteLine($"Loaded {actionNames.Count} actions for review");
                }

                return actionNames;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading actions: {ex.Message}");
                return new List<string>();
            }
        }

        public async Task<ActionReviewData> LoadSelectedActionAsync(string selectedActionName, ObservableCollection<NodeViewModel> nodes)
        {
            try
            {
                // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Attempting to load action: {selectedActionName ?? "null"}");

                var actionReviewData = new ActionReviewData();

                if (_actionService != null && !string.IsNullOrEmpty(selectedActionName))
                {
                    var allDataItems = await _actionService.LoadAllDataItemsAsync();
                    var selectedDataItem = allDataItems.FirstOrDefault(item =>
                        item?.Data?.ActionGroupObject?.ActionName == selectedActionName);

                    if (selectedDataItem?.Data?.ActionGroupObject != null)
                    {
                        actionReviewData.ActionItems = selectedDataItem.Data.ActionGroupObject.ActionArray ?? new List<ActionItem>();
                        // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Loaded '{selectedActionName}' with {actionReviewData.ActionItems.Count} global action items.");

                        var actionGroupFiles = selectedDataItem.Data.ActionGroupObject.Files;
                        // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] ActionGroup has {actionGroupFiles.Count} associated files.");

                        // Log all available files for debugging
                        if (actionGroupFiles.Count > 0)
                        {
                            // Debug.WriteLine("[ActionReviewService.LoadSelectedActionAsync] Available files in action group:");
                            foreach (var file in actionGroupFiles)
                            {
                                Debug.WriteLine($"  File: {file.Filename}");
                            }
                        }

                        // Populate ActionSteps for input nodes
                        // Each ActionStep should represent content for one specific action step (action item)
                        // The index in ActionSteps should match the step number (0-based)
                        foreach (var nodeVM in nodes.Where(n => n.Type == NodeType.Input))
                        {
                            nodeVM.ActionSteps.Clear();
                            // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] === Populating ActionSteps for Input Node: {nodeVM.Name} (Node DataType: {nodeVM.DataType}) ===");

                            // Create one ActionStep entry for each action item (step)
                            for (int stepIndex = 0; stepIndex < actionReviewData.ActionItems.Count; stepIndex++)
                            {
                                var actionItem = actionReviewData.ActionItems[stepIndex];
                                string actionDescription = actionItem.ToString();

                                // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Processing step {stepIndex}: EventType={actionItem.EventType}, Description={actionDescription?.Substring(0, Math.Min(100, actionDescription?.Length ?? 0))}...");

                                // Determine step-specific content for this node based on its type and the action item
                                string stepContent = GetStepSpecificContent(nodeVM, actionItem, actionDescription, actionGroupFiles);

                                // Add the step content for this specific step
                                nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: stepContent));
                                // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Added ActionStep[{stepIndex}] for {nodeVM.Name}: Type={nodeVM.DataType}, Content={stepContent?.Substring(0, Math.Min(50, stepContent?.Length ?? 0))}...");
                            }

                            // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Completed ActionSteps population for {nodeVM.Name}. Total steps: {nodeVM.ActionSteps.Count}");
                        }
                    }
                    else
                    {
                        // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Action '{selectedActionName}' not found or has no ActionGroupObject.");
                    }
                }
                else
                {
                    // Debug.WriteLine("[ActionReviewService.LoadSelectedActionAsync] ActionService is null or selectedActionName is empty.");
                }

                return actionReviewData;
            }
            catch (Exception ex)
            {
                // Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return new ActionReviewData();
            }
        }

        public Task LoadActionStepDataAsync(int currentActionStep, List<ActionItem> currentActionItems)
        {
            try
            {
                // Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] Called for CurrentActionStep (0-indexed): {currentActionStep}");
                if (currentActionItems == null || !currentActionItems.Any())
                {
                    Debug.WriteLine("[ActionReviewService.LoadActionStepDataAsync] No global action items loaded (currentActionItems is null or empty).");
                    return Task.CompletedTask;
                }

                if (currentActionStep < 0 || currentActionStep >= currentActionItems.Count)
                {
                    // Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] CurrentActionStep {currentActionStep} is out of bounds for currentActionItems (Count: {currentActionItems.Count}).");
                    return Task.CompletedTask;
                }

                var globalActionItem = currentActionItems[currentActionStep];
                // Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] Global ActionItem at index {currentActionStep}: {globalActionItem?.ToString() ?? "null"}");

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] Error: {ex.Message}");
                return Task.CompletedTask;
            }
        }

        public StepContentData UpdateStepContent(NodeViewModel selectedNode, int currentActionStep, List<ActionItem> currentActionItems, string selectedReviewActionName)
        {
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] === DEBUGGING SESSION START ===");
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Selected Action: {selectedReviewActionName}");
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Current Step: {currentActionStep} (0-based)");
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Selected Node: {selectedNode?.Name} (Type: {selectedNode?.Type}, DataType: {selectedNode?.DataType})");
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] ActionItems Count: {currentActionItems?.Count ?? 0}");
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Node ActionSteps Count: {selectedNode?.ActionSteps?.Count ?? 0}");

            var stepContentData = new StepContentData();

            if (selectedReviewActionName == null)
            {
                stepContentData.Content = "No action selected for review.";
                stepContentData.ContentType = "text";
                // Debug.WriteLine("[ActionReviewService.UpdateStepContent] No action selected, returning default message.");
                return stepContentData;
            }

            if (selectedNode == null)
            {
                stepContentData.Content = "No node selected. Please select a node to view its step content.";
                stepContentData.ContentType = "text";
                // Debug.WriteLine("[ActionReviewService.UpdateStepContent] No node selected, returning default message.");
                return stepContentData;
            }

            // Handle model nodes
            if (selectedNode.Type == NodeType.Model)
            {
                int stepForNodeContent = currentActionStep + 1; // Convert to 1-based index
                // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Model node: Looking for stored output at step {stepForNodeContent} (1-based)");
                var (storedOutputType, storedOutputValue) = selectedNode.GetStepOutput(stepForNodeContent);
                if (!string.IsNullOrEmpty(storedOutputValue))
                {
                    stepContentData.Content = storedOutputValue;
                    stepContentData.ContentType = storedOutputType;
                    // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Found stored model output: {storedOutputValue?.Substring(0, Math.Min(100, storedOutputValue?.Length ?? 0))}...");
                    return stepContentData;
                }
                else
                {
                    stepContentData.Content = $"Model: {selectedNode.Name}\nNo output generated yet. Use 'Generate' or 'Run All Models' to process inputs.";
                    stepContentData.ContentType = "text";
                    Debug.WriteLine("[ActionReviewService.UpdateStepContent] No stored model output found.");
                    return stepContentData;
                }
            }

            // Handle input nodes
            if (selectedNode.Type == NodeType.Input)
            {
                int stepForNodeContent = currentActionStep + 1; // Convert to 1-based index
                // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Input node: Getting step content at step {stepForNodeContent} (1-based)");

                // FIXED: Get the ActionItem timestamp for precise file correlation
                DateTime? actionItemTimestamp = null;
                if (currentActionItems != null && currentActionStep >= 0 && currentActionStep < currentActionItems.Count)
                {
                    var currentActionItem = currentActionItems[currentActionStep];
                    if (currentActionItem?.Timestamp != null)
                    {
                        if (currentActionItem.Timestamp is DateTime directTimestamp)
                        {
                            actionItemTimestamp = directTimestamp;
                        }
                        else if (DateTime.TryParse(currentActionItem.Timestamp.ToString(), out DateTime parsedTimestamp))
                        {
                            actionItemTimestamp = parsedTimestamp;
                        }
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] ActionItem timestamp: {actionItemTimestamp?.ToString("yyyy-MM-dd HH:mm:ss.fff") ?? "Failed to parse"}");
                    }
                    // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Current ActionItem[{currentActionStep}]: {currentActionItem?.ToString()?.Substring(0, Math.Min(100, currentActionItem?.ToString()?.Length ?? 0))}...");
                }

                // Get the raw step content from the node with ActionItem timestamp
                var (contentType, contentValue) = selectedNode.GetStepContent(stepForNodeContent, actionItemTimestamp);
                // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Raw step content - Type: {contentType}, Value: {contentValue?.Substring(0, Math.Min(100, contentValue?.Length ?? 0))}...");

                stepContentData.Content = contentValue ?? "No data available for this step.";
                stepContentData.ContentType = contentType ?? "text";

                // For debugging: show which ActionStep is being accessed
                if (selectedNode.ActionSteps != null && selectedNode.ActionSteps.Count > 0)
                {
                    // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Node ActionSteps debugging:");
                    for (int i = 0; i < Math.Min(5, selectedNode.ActionSteps.Count); i++)
                    {
                        var debugStep = selectedNode.ActionSteps[i];
                        // Debug.WriteLine($"  ActionSteps[{i}]: Type='{debugStep.Type}', Value='{debugStep.Value?.Substring(0, Math.Min(50, debugStep.Value?.Length ?? 0))}...'");
                    }
                }

                // For image and audio nodes, if we have step-specific content, use it directly
                // Only fall back to file searching if the step content is empty or looks like a description
                if (selectedNode.DataType?.ToLower() == "image")
                {
                    // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Processing image node...");
                    // If contentValue looks like a file path or base64 data, use it directly
                    if (!string.IsNullOrEmpty(contentValue) &&
                        (contentValue.Contains("\\") || contentValue.Contains("/") || contentValue.StartsWith("data:") || contentValue.Length > 200))
                    {
                        // This looks like actual image data or file path, use it directly
                        stepContentData.Content = contentValue;
                        stepContentData.ContentType = "image";
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using step-specific image content directly");
                    }
                    else if (actionItemTimestamp.HasValue)
                    {
                        // FIXED: Use the ActionItem timestamp for precise file correlation
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using ActionItem timestamp for image search: {actionItemTimestamp.Value:yyyy-MM-dd HH:mm:ss.fff}");
                        string imageFileName = selectedNode.FindClosestImageFile(contentValue, contentType, actionItemTimestamp);
                        if (!string.IsNullOrEmpty(imageFileName))
                        {
                            stepContentData.Content = imageFileName;
                            stepContentData.ContentType = "image";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using timestamp-based image file: {imageFileName}");
                        }
                        else
                        {
                            stepContentData.ContentType = "text";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] No image file found, falling back to text");
                        }
                    }
                    else
                    {
                        // Fall back to original logic if no timestamp
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] No ActionItem timestamp available, falling back to description-based search: {contentValue}");
                        string imageFileName = selectedNode.FindClosestImageFile(contentValue, contentType);
                        if (!string.IsNullOrEmpty(imageFileName))
                        {
                            stepContentData.Content = imageFileName;
                            stepContentData.ContentType = "image";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using description-based image file: {imageFileName}");
                        }
                        else
                        {
                            stepContentData.ContentType = "text";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] No image file found, falling back to text");
                        }
                    }
                }

                if (selectedNode.DataType?.ToLower() == "audio" && !string.IsNullOrEmpty(selectedReviewActionName))
                {
                    // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Processing audio node...");
                    // If contentValue looks like a file path, use it directly
                    if (!string.IsNullOrEmpty(contentValue) &&
                        (contentValue.Contains("\\") || contentValue.Contains("/")) &&
                        (contentValue.EndsWith(".wav") || contentValue.EndsWith(".mp3") || contentValue.EndsWith(".aac")))
                    {
                        // This looks like an audio file path, use it directly
                        stepContentData.Content = contentValue;
                        stepContentData.ContentType = "audio";
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using step-specific audio content directly: {contentValue}");
                    }
                    else if (actionItemTimestamp.HasValue)
                    {
                        // FIXED: Use the ActionItem timestamp for precise audio file correlation
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using ActionItem timestamp for audio search: {actionItemTimestamp.Value:yyyy-MM-dd HH:mm:ss.fff}");
                        string audioFilePath = selectedNode.FindClosestAudioFile(actionItemTimestamp.Value);
                        if (!string.IsNullOrEmpty(audioFilePath))
                        {
                            stepContentData.Content = audioFilePath;
                            stepContentData.ContentType = "audio";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using timestamp-based audio file: {audioFilePath}");
                        }
                        else
                        {
                            stepContentData.Content = "No audio file found for this step.";
                            stepContentData.ContentType = "text";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] No audio file found");
                        }
                    }
                    else
                    {
                        // Fall back to generic audio segment if no timestamp
                        // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] No ActionItem timestamp available, falling back to generic audio segment");
                        string audioSegmentPath = selectedNode.GetAudioSegment(DateTime.MinValue, DateTime.MinValue);
                        if (!string.IsNullOrEmpty(audioSegmentPath))
                        {
                            stepContentData.Content = audioSegmentPath;
                            stepContentData.ContentType = "audio";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Using generic audio segment: {audioSegmentPath}");
                        }
                        else
                        {
                            stepContentData.Content = "No audio segment available for this step.";
                            stepContentData.ContentType = "text";
                            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] No audio segment found");
                        }
                    }
                }
            }

            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] Final result - Type: {stepContentData.ContentType}, Content: {stepContentData.Content?.Substring(0, Math.Min(100, stepContentData.Content?.Length ?? 0))}...");
            // Debug.WriteLine($"[ActionReviewService.UpdateStepContent] === DEBUGGING SESSION END ===");
            return stepContentData;
        }

        private string GetFileTypeFromName(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "unknown";
            string ext = Path.GetExtension(filename).ToLowerInvariant();
            switch (ext)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    return "image";
                case ".wav":
                case ".mp3":
                case ".aac":
                    return "audio";
                case ".txt":
                case ".md":
                case ".json":
                case ".xml":
                    return "text";
                default:
                    // Basic content sniffing for text if no extension
                    if (!string.IsNullOrEmpty(filename) && (filename.StartsWith("text:") || filename.Length < 255 && !filename.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')))
                        return "text"; // Crude check for text content
                    return "unknown";
            }
        }

        /// <summary>
        /// Gets step-specific content for a node based on the action item and node type
        /// </summary>
        private string GetStepSpecificContent(NodeViewModel nodeVM, ActionItem actionItem, string actionDescription, List<ActionFile> actionGroupFiles)
        {
            // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Node: {nodeVM.Name} (DataType: {nodeVM.DataType}), EventType: {actionItem.EventType}, Description: {actionDescription?.Substring(0, Math.Min(100, actionDescription?.Length ?? 0))}...");

            // For keyboard input nodes, only include keyboard-related events
            if (nodeVM.Name == "Keyboard Text (Input)" && (actionItem.EventType == 256 || actionItem.EventType == 257))
            {
                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Keyboard event matched for {nodeVM.Name}");
                return actionDescription;
            }

            // For mouse input nodes, only include mouse-related events  
            if (nodeVM.Name == "Mouse Text (Input)" && (actionItem.EventType == 512 || actionItem.EventType == 0x0200))
            {
                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Mouse event matched for {nodeVM.Name}");
                return actionDescription;
            }

            // For image nodes, try multiple approaches to find content
            if (nodeVM.DataType == "image")
            {
                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Processing image node...");

                // First, try to find associated image files by filename
                var imageFile = actionGroupFiles.FirstOrDefault(f => actionDescription.ToLower().Contains(f.Filename.ToLower()));
                if (imageFile != null)
                {
                    // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Found matching image file: {imageFile.Filename}");
                    return imageFile.Data; // Return the actual image data
                }

                // Second, try to find files with common image extensions
                var imageFileByExtension = actionGroupFiles.FirstOrDefault(f =>
                    f.Filename.ToLower().EndsWith(".jpg") ||
                    f.Filename.ToLower().EndsWith(".jpeg") ||
                    f.Filename.ToLower().EndsWith(".png") ||
                    f.Filename.ToLower().EndsWith(".bmp") ||
                    f.Filename.ToLower().EndsWith(".gif"));

                if (imageFileByExtension != null)
                {
                    // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Found image file by extension: {imageFileByExtension.Filename}");
                    return imageFileByExtension.Data;
                }

                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] No specific image file found, returning action description for timestamp matching");
                // If no specific image file found for this action, return the action description
                // This allows the GetStepContent method to attempt timestamp-based image matching
                return actionDescription;
            }

            // For audio and other file-based nodes, try to find matching files
            if (nodeVM.DataType == "audio" || nodeVM.DataType == "file")
            {
                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Processing audio/file node...");

                // First, try exact filename match
                var matchingFile = actionGroupFiles.FirstOrDefault(f => actionDescription.ToLower().Contains(f.Filename.ToLower()));
                if (matchingFile != null)
                {
                    // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Found matching audio file: {matchingFile.Filename}");
                    return matchingFile.Filename;
                }

                // Second, try to find files with common audio extensions
                var audioFile = actionGroupFiles.FirstOrDefault(f =>
                    f.Filename.ToLower().EndsWith(".wav") ||
                    f.Filename.ToLower().EndsWith(".mp3") ||
                    f.Filename.ToLower().EndsWith(".aac") ||
                    f.Filename.ToLower().EndsWith(".m4a") ||
                    f.Filename.ToLower().EndsWith(".ogg"));

                if (audioFile != null)
                {
                    // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Found audio file by extension: {audioFile.Filename}");
                    return audioFile.Filename;
                }

                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] No specific audio file found, returning action description");
                return actionDescription;
            }

            // For nodes that don't match specific event types, return empty content
            // This prevents them from showing irrelevant action descriptions
            if (nodeVM.Name == "Keyboard Text (Input)" && !(actionItem.EventType == 256 || actionItem.EventType == 257))
            {
                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Non-keyboard event on keyboard node, returning empty");
                return ""; // Empty content for non-keyboard events on keyboard nodes
            }

            if (nodeVM.Name == "Mouse Text (Input)" && !(actionItem.EventType == 512 || actionItem.EventType == 0x0200))
            {
                // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Non-mouse event on mouse node, returning empty");
                return ""; // Empty content for non-mouse events on mouse nodes  
            }

            // Default: return action description for other cases
            // Debug.WriteLine($"[ActionReviewService.GetStepSpecificContent] Default case, returning action description");
            return actionDescription;
        }
    }

    // Data transfer objects for the service
    public class ActionReviewData
    {
        public List<ActionItem> ActionItems { get; set; } = new List<ActionItem>();
    }

    public class StepContentData
    {
        public string Content { get; set; } = "";
        public string ContentType { get; set; } = "text";
    }
}
