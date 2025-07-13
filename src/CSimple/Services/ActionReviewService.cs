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
                Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Attempting to load action: {selectedActionName ?? "null"}");

                var actionReviewData = new ActionReviewData();

                if (_actionService != null && !string.IsNullOrEmpty(selectedActionName))
                {
                    var allDataItems = await _actionService.LoadAllDataItemsAsync();
                    var selectedDataItem = allDataItems.FirstOrDefault(item =>
                        item?.Data?.ActionGroupObject?.ActionName == selectedActionName);

                    if (selectedDataItem?.Data?.ActionGroupObject != null)
                    {
                        actionReviewData.ActionItems = selectedDataItem.Data.ActionGroupObject.ActionArray ?? new List<ActionItem>();
                        Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Loaded '{selectedActionName}' with {actionReviewData.ActionItems.Count} global action items.");

                        var actionGroupFiles = selectedDataItem.Data.ActionGroupObject.Files;
                        Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] ActionGroup has {actionGroupFiles.Count} associated files.");

                        // Populate ActionSteps for input nodes
                        foreach (var nodeVM in nodes.Where(n => n.Type == NodeType.Input))
                        {
                            nodeVM.ActionSteps.Clear();
                            Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Populating ActionSteps for Input Node: {nodeVM.Name} (Node DataType: {nodeVM.DataType})");

                            foreach (var actionItem in actionReviewData.ActionItems)
                            {
                                string actionDescription = actionItem.ToString();
                                bool added = false;

                                if (nodeVM.Name == "Keyboard Text (Input)" && (actionItem.EventType == 256 || actionItem.EventType == 257))
                                {
                                    nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: actionDescription));
                                    added = true;
                                }
                                else if (nodeVM.Name == "Mouse Text (Input)" && (actionItem.EventType == 512 || actionItem.EventType == 0x0200))
                                {
                                    nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: actionDescription));
                                    added = true;
                                }
                                else if (nodeVM.DataType == "image")
                                {
                                    var imageFile = actionGroupFiles.FirstOrDefault(f => actionDescription.ToLower().Contains(f.Filename.ToLower()));

                                    if (imageFile != null)
                                    {
                                        nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: imageFile.Data));
                                        added = true;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] No image file found for action item: {actionDescription}");
                                        nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: actionDescription));
                                        added = true;
                                    }
                                }
                                else
                                {
                                    var matchingFile = actionGroupFiles.FirstOrDefault(f => actionDescription.ToLower().Contains(f.Filename.ToLower()));
                                    if (matchingFile != null)
                                    {
                                        nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: matchingFile.Filename));
                                        added = true;
                                    }
                                }

                                if (!added)
                                {
                                    nodeVM.ActionSteps.Add((Type: nodeVM.DataType, Value: actionDescription));
                                }
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Action '{selectedActionName}' not found or has no ActionGroupObject.");
                    }
                }
                else
                {
                    Debug.WriteLine("[ActionReviewService.LoadSelectedActionAsync] ActionService is null or selectedActionName is empty.");
                }

                return actionReviewData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ActionReviewService.LoadSelectedActionAsync] Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
                return new ActionReviewData();
            }
        }

        public Task LoadActionStepDataAsync(int currentActionStep, List<ActionItem> currentActionItems)
        {
            try
            {
                Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] Called for CurrentActionStep (0-indexed): {currentActionStep}");
                if (currentActionItems == null || !currentActionItems.Any())
                {
                    Debug.WriteLine("[ActionReviewService.LoadActionStepDataAsync] No global action items loaded (currentActionItems is null or empty).");
                    return Task.CompletedTask;
                }

                if (currentActionStep < 0 || currentActionStep >= currentActionItems.Count)
                {
                    Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] CurrentActionStep {currentActionStep} is out of bounds for currentActionItems (Count: {currentActionItems.Count}).");
                    return Task.CompletedTask;
                }

                var globalActionItem = currentActionItems[currentActionStep];
                Debug.WriteLine($"[ActionReviewService.LoadActionStepDataAsync] Global ActionItem at index {currentActionStep}: {globalActionItem?.ToString() ?? "null"}");

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
            Debug.WriteLine("[ActionReviewService.UpdateStepContent] Called.");

            var stepContentData = new StepContentData();

            if (selectedReviewActionName == null)
            {
                stepContentData.Content = "No action selected for review.";
                stepContentData.ContentType = "text";
                return stepContentData;
            }

            if (selectedNode == null)
            {
                stepContentData.Content = "No node selected. Please select a node to view its step content.";
                stepContentData.ContentType = "text";
                return stepContentData;
            }

            // Handle model nodes
            if (selectedNode.Type == NodeType.Model)
            {
                int stepForNodeContent = currentActionStep + 1; // Convert to 1-based index
                var (storedOutputType, storedOutputValue) = selectedNode.GetStepOutput(stepForNodeContent);
                if (!string.IsNullOrEmpty(storedOutputValue))
                {
                    stepContentData.Content = storedOutputValue;
                    stepContentData.ContentType = storedOutputType;
                    return stepContentData;
                }
                else
                {
                    stepContentData.Content = $"Model: {selectedNode.Name}\nNo output generated yet. Use 'Generate' or 'Run All Models' to process inputs.";
                    stepContentData.ContentType = "text";
                    return stepContentData;
                }
            }

            // Handle input nodes
            if (selectedNode.Type == NodeType.Input)
            {
                int stepForNodeContent = currentActionStep + 1; // Convert to 1-based index
                var (contentType, contentValue) = selectedNode.GetStepContent(stepForNodeContent);

                stepContentData.Content = contentValue ?? "No data available for this step.";
                stepContentData.ContentType = contentType ?? "text";

                // Handle specific content types
                if (stepContentData.ContentType == "image")
                {
                    if (currentActionStep >= 0 && currentActionStep < currentActionItems.Count)
                    {
                        string imageFileName = selectedNode.FindClosestImageFile(contentValue, contentType);
                        if (!string.IsNullOrEmpty(imageFileName))
                        {
                            stepContentData.Content = imageFileName;
                        }
                        else
                        {
                            stepContentData.Content = "No image file available for this step.";
                            stepContentData.ContentType = "text";
                        }
                    }
                }

                if (selectedNode.DataType?.ToLower() == "audio" && !string.IsNullOrEmpty(selectedReviewActionName))
                {
                    if (currentActionStep >= 0 && currentActionStep < currentActionItems.Count)
                    {
                        string audioSegmentPath = selectedNode.GetAudioSegment(DateTime.MinValue, DateTime.MinValue);
                        if (!string.IsNullOrEmpty(audioSegmentPath))
                        {
                            stepContentData.Content = audioSegmentPath;
                            stepContentData.ContentType = "audio";
                        }
                        else
                        {
                            stepContentData.Content = "No audio segment available for this step.";
                            stepContentData.ContentType = "text";
                        }
                    }
                    else
                    {
                        stepContentData.Content = "Invalid step for audio content.";
                        stepContentData.ContentType = "text";
                    }
                }
            }

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
