using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CSimple.Models;
using CSimple.ViewModels;

namespace CSimple.Services
{
    public interface IStepContentManagementService
    {
        StepContentUpdateResult UpdateStepContent(NodeViewModel selectedNode, int currentActionStep, List<ActionItem> currentActionItems, string selectedReviewActionName);
        List<string> ProcessImageContent(string content);
        void RefreshAllNodeStepContent(IEnumerable<NodeViewModel> nodes);
        List<string> ValidateImagePaths(List<string> imagePaths);
        StepContentProperties GetStepContentProperties(List<string> stepContentImages);
    }

    public class StepContentManagementService : IStepContentManagementService
    {
        private readonly ActionReviewService _actionReviewService;

        public StepContentManagementService(ActionReviewService actionReviewService)
        {
            _actionReviewService = actionReviewService;
        }

        public StepContentUpdateResult UpdateStepContent(NodeViewModel selectedNode, int currentActionStep, List<ActionItem> currentActionItems, string selectedReviewActionName)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.UpdateStepContent] Called - SelectedNode: {selectedNode?.Name ?? "null"}, CurrentActionStep: {currentActionStep}, SelectedAction: {selectedReviewActionName ?? "null"}");

            try
            {
                var stepContentData = _actionReviewService.UpdateStepContent(selectedNode, currentActionStep, currentActionItems, selectedReviewActionName);

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.UpdateStepContent] Retrieved content - Type: {stepContentData.ContentType}, Content: {stepContentData.Content?.Substring(0, Math.Min(100, stepContentData.Content?.Length ?? 0))}...");

                var result = new StepContentUpdateResult
                {
                    ContentType = stepContentData.ContentType,
                    Content = stepContentData.Content,
                    Images = new List<string>(),
                    HasMultipleImages = false
                };

                // Handle multiple images for screen capture nodes with enhanced error checking
                if (stepContentData.ContentType?.ToLower() == "image" && !string.IsNullOrEmpty(stepContentData.Content))
                {
                    result.Images = ProcessImageContent(stepContentData.Content);
                    result.HasMultipleImages = result.Images.Count > 1;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.UpdateStepContent] Error: {ex.Message}");

                // Return safe defaults on error
                return new StepContentUpdateResult
                {
                    ContentType = null,
                    Content = null,
                    Images = new List<string>(),
                    HasMultipleImages = false
                };
            }
        }

        public List<string> ProcessImageContent(string content)
        {
            try
            {
                if (content.Contains(';'))
                {
                    // Multiple images - split and store separately with validation
                    var imagePaths = content.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(path => path.Trim())
                                            .Where(path => !string.IsNullOrEmpty(path))
                                            .ToList();

                    // Validate each image path and log missing files
                    var validImagePaths = new List<string>();
                    foreach (var imagePath in imagePaths)
                    {
                        if (File.Exists(imagePath))
                        {
                            validImagePaths.Add(imagePath);
                        }
                        else
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ProcessImageContent] Warning: Image file not found: {imagePath}");
                        }
                    }

                    if (validImagePaths.Count > 0)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ProcessImageContent] Found {validImagePaths.Count} valid image(s) out of {imagePaths.Count} total paths");
                        return validImagePaths;
                    }
                    else
                    {
                        // No valid images found after filtering
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ProcessImageContent] No valid images found after filtering {imagePaths.Count} paths");
                        return new List<string>();
                    }
                }
                else
                {
                    // Single image - validate it exists
                    if (File.Exists(content))
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ProcessImageContent] Found single valid image: {content}");
                        return new List<string> { content };
                    }
                    else
                    {
                        // Image file doesn't exist
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ProcessImageContent] Single image file not found: {content}");
                        return new List<string>();
                    }
                }
            }
            catch (Exception imageEx)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ProcessImageContent] Error processing images: {imageEx.Message}");
                return new List<string>();
            }
        }

        public void RefreshAllNodeStepContent(IEnumerable<NodeViewModel> nodes)
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.RefreshAllNodeStepContent] Refreshing step content for all nodes");

            foreach (var node in nodes)
            {
                // Force a refresh by triggering property change notifications
                if (node.Type == NodeType.Input || node.Type == NodeType.Model)
                {
                    // If the node has ActionSteps, we want to ensure UI elements that depend on them are refreshed
                    if (node.ActionSteps != null && node.ActionSteps.Count > 0)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.RefreshAllNodeStepContent] Node {node.Name} has {node.ActionSteps.Count} ActionSteps");
                    }
                }
            }
        }

        public List<string> ValidateImagePaths(List<string> imagePaths)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                return new List<string>();
            }

            var validPaths = new List<string>();
            foreach (var path in imagePaths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            validPaths.Add(path);
                        }
                        else
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ValidateImagePaths] Filtering out non-existent image: {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [StepContentManagementService.ValidateImagePaths] Error validating image path '{path}': {ex.Message}");
                    }
                }
            }

            return validPaths;
        }

        public StepContentProperties GetStepContentProperties(List<string> stepContentImages)
        {
            return new StepContentProperties
            {
                FirstImage = stepContentImages?.Count > 0 ? stepContentImages[0] : null,
                SecondImage = stepContentImages?.Count > 1 ? stepContentImages[1] : null,
                HasFirstImage = stepContentImages?.Count > 0 && !string.IsNullOrEmpty(stepContentImages[0]),
                HasSecondImage = stepContentImages?.Count > 1 && !string.IsNullOrEmpty(stepContentImages[1]),
                HasMultipleImages = stepContentImages?.Count > 1
            };
        }
    }

    public class StepContentUpdateResult
    {
        public string ContentType { get; set; }
        public string Content { get; set; }
        public List<string> Images { get; set; } = new List<string>();
        public bool HasMultipleImages { get; set; }
    }

    public class StepContentProperties
    {
        public string FirstImage { get; set; }
        public string SecondImage { get; set; }
        public bool HasFirstImage { get; set; }
        public bool HasSecondImage { get; set; }
        public bool HasMultipleImages { get; set; }
    }
}
