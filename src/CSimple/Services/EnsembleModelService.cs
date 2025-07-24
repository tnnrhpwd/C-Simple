using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSimple.Models;
using CSimple.ViewModels;

namespace CSimple.Services
{
    /// <summary>
    /// Service for handling ensemble model execution and related operations
    /// </summary>
    public class EnsembleModelService
    {
        private readonly NetPageViewModel _netPageViewModel;
        private readonly Dictionary<string, NodeViewModel> _nodeCache = new Dictionary<string, NodeViewModel>();
        private readonly object _nodeCacheLock = new object(); // Thread safety for node cache

        // Cache for step content to avoid repeated expensive GetStepContent calls
        private readonly Dictionary<string, string> _stepContentCache = new Dictionary<string, string>();
        private readonly object _stepContentCacheLock = new object(); // Thread safety for step content cache

        // Model execution optimization: batch similar models together
        private readonly Dictionary<string, List<(NodeViewModel node, string input)>> _batchedExecutions = new Dictionary<string, List<(NodeViewModel, string)>>();
        private readonly object _batchedExecutionsLock = new object(); // Thread safety for batched executions

        public EnsembleModelService(NetPageViewModel netPageViewModel)
        {
            _netPageViewModel = netPageViewModel ?? throw new ArgumentNullException(nameof(netPageViewModel));
        }

        public void ClearStepContentCache()
        {
            lock (_nodeCacheLock)
            {
                _nodeCache.Clear();
            }

            lock (_stepContentCacheLock)
            {
                _stepContentCache.Clear();
            }

            lock (_batchedExecutionsLock)
            {
                _batchedExecutions.Clear();
            }
        }

        /// <summary>
        /// Gets all input nodes connected to the specified model node
        /// </summary>
        public List<NodeViewModel> GetConnectedInputNodes(NodeViewModel modelNode, IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections)
        {
            // Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [GetConnectedInputNodes] Finding inputs for model node: {modelNode.Name}");

            var connectedNodes = new List<NodeViewModel>();
            var nodesList = nodes.ToList(); // Convert to list for efficient access
            var connectionsList = connections.ToList(); // Convert to list for efficient access

            // Build node cache with thread safety
            lock (_nodeCacheLock)
            {
                if (_nodeCache.Count != nodesList.Count)
                {
                    _nodeCache.Clear();
                    foreach (var node in nodesList)
                    {
                        _nodeCache[node.Id] = node;
                    }
                }
            }

            // Find all connections that target this model node
            var incomingConnections = connectionsList.Where(c => c.TargetNodeId == modelNode.Id).ToList();
            // Debug.WriteLine($"🔗 [{DateTime.Now:HH:mm:ss.fff}] [GetConnectedInputNodes] Found {incomingConnections.Count} incoming connections");

            foreach (var connection in incomingConnections)
            {
                lock (_nodeCacheLock)
                {
                    if (_nodeCache.TryGetValue(connection.SourceNodeId, out var sourceNode))
                    {
                        // Debug.WriteLine($"🔗 [{DateTime.Now:HH:mm:ss.fff}] [GetConnectedInputNodes] Connected node: {sourceNode.Name} (Type: {sourceNode.Type}, DataType: {sourceNode.DataType})");
                        connectedNodes.Add(sourceNode);
                    }
                    else
                    {
                        // Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [GetConnectedInputNodes] Warning: Source node with ID {connection.SourceNodeId} not found");
                    }
                }
            }

            return connectedNodes;
        }

        /// <summary>
        /// Combines multiple step contents using the specified ensemble method (optimized for performance)
        /// </summary>
        public string CombineStepContents(List<string> stepContents, string ensembleMethod)
        {
            if (stepContents == null || stepContents.Count == 0)
            {
                return string.Empty;
            }

            // Quick content type detection using optimized checks
            var firstContent = stepContents[0];
            bool isImageContent = firstContent.IndexOf('.') > 0 &&
                                  (firstContent.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   firstContent.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   firstContent.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

            bool isAudioContent = firstContent.IndexOf('.') > 0 &&
                                  (firstContent.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                   firstContent.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                   firstContent.EndsWith(".aac", StringComparison.OrdinalIgnoreCase) ||
                                   firstContent.EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

            if (isImageContent)
            {
                Debug.WriteLine($"🖼️ [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Detected image content, combining {stepContents.Count} images using method: {ensembleMethod}");
                return CombineImageContents(stepContents, ensembleMethod);
            }

            if (isAudioContent)
            {
                Debug.WriteLine($"🔊 [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Detected audio content, combining {stepContents.Count} audio files using method: {ensembleMethod}");
                return CombineAudioContents(stepContents, ensembleMethod);
            }

            // Fast path for single content
            if (stepContents.Count == 1)
            {
                return firstContent;
            }

            // Reduced debug logging for performance - only log method used
            Debug.WriteLine($"🔀 [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Combining {stepContents.Count} contents using method: {ensembleMethod}");

            switch (ensembleMethod?.ToLowerInvariant())
            {
                case "concatenation":
                case "concat":
                case null:
                default:
                    Debug.WriteLine($"🔗 [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Using concatenation method");
                    return string.Join("\n\n", stepContents);

                case "average":
                case "averaging":
                    Debug.WriteLine($"📊 [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Using averaging method (fallback to concatenation for text)");
                    return string.Join("\n\n", stepContents);

                case "voting":
                case "majority":
                    Debug.WriteLine($"🗳️ [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Using voting method (fallback to concatenation for text)");
                    return string.Join("\n\n", stepContents);

                case "weighted":
                    Debug.WriteLine($"⚖️ [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Using weighted method (fallback to concatenation for text)");
                    return string.Join("\n\n", stepContents);
            }
        }

        /// <summary>
        /// Executes a model with the provided input using NetPageViewModel (optimized)
        /// </summary>
        public async Task<string> ExecuteModelWithInput(NeuralNetworkModel model, string input, List<NodeViewModel> connectedInputNodes = null)
        {
            try
            {
                // Fast validation
                if (string.IsNullOrEmpty(model.HuggingFaceModelId))
                {
                    throw new InvalidOperationException("Model does not have a valid HuggingFace model ID");
                }

                // Check if this is a combined image input that needs special handling
                string processedInput;

                // Check if input is already an ensemble-processed result
                if (input.StartsWith("ENSEMBLE_PROCESSED:"))
                {
                    // Return the already processed ensemble result
                    var ensembleResult = input.Substring("ENSEMBLE_PROCESSED:".Length);
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Returning pre-processed ensemble result ({ensembleResult.Length} chars)");
                    return ensembleResult;
                }

                // Check if this is a multi-image input that should be processed as ensemble
                if (DetectCombinedImageInput(input) && model.InputType == ModelInputType.Image)
                {
                    Debug.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Detected multi-image input for image model, processing as ensemble");
                    processedInput = await ProcessCombinedImageInputAsync(model, input, connectedInputNodes ?? new List<NodeViewModel>());

                    // If it was processed as ensemble, return the result directly
                    if (processedInput.StartsWith("ENSEMBLE_PROCESSED:"))
                    {
                        return processedInput.Substring("ENSEMBLE_PROCESSED:".Length);
                    }
                }
                // Check if this is a multi-audio input that should be processed as ensemble
                else if (DetectCombinedAudioInput(input) && model.InputType == ModelInputType.Audio)
                {
                    Debug.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Detected multi-audio input for audio model, processing as ensemble");
                    // Pass connected nodes for context descriptions
                    processedInput = await ProcessCombinedAudioInputAsync(model, input, connectedInputNodes ?? new List<NodeViewModel>());

                    // If it was processed as ensemble, return the result directly
                    if (processedInput.StartsWith("ENSEMBLE_PROCESSED:"))
                    {
                        return processedInput.Substring("ENSEMBLE_PROCESSED:".Length);
                    }
                }
                else
                {
                    // Use synchronous processing for backward compatibility
                    if (model.InputType == ModelInputType.Image)
                    {
                        processedInput = ProcessCombinedImageInput(model, input);
                    }
                    else if (model.InputType == ModelInputType.Audio)
                    {
                        processedInput = ProcessCombinedAudioInput(model, input);
                    }
                    else
                    {
                        processedInput = input;
                    }
                }

                var result = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, processedInput);
                return result ?? "No output generated";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Model execution failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Determines the content type of model execution result
        /// </summary>
        public string DetermineResultContentType(NeuralNetworkModel model, string result)
        {
            Console.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Analyzing model: {model?.Name}, HF ID: {model?.HuggingFaceModelId}");
            Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Analyzing model: {model?.Name}, HF ID: {model?.HuggingFaceModelId}");

            // Check if this is an image-to-text model based on the HuggingFace model ID or name
            if (model?.HuggingFaceModelId != null)
            {
                string modelId = model.HuggingFaceModelId.ToLowerInvariant();
                if (modelId.Contains("blip") && modelId.Contains("captioning") ||
                    modelId.Contains("image-to-text") ||
                    modelId.Contains("vit-gpt2") ||
                    modelId.Contains("clip-interrogator"))
                {
                    Console.WriteLine($"🖼️➡️📝 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Detected image-to-text model, output type: text");
                    Debug.WriteLine($"🖼️➡️📝 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Detected image-to-text model, output type: text");
                    return "text";
                }
            }

            // Check if the result looks like a file path (for image generation models)
            if (!string.IsNullOrEmpty(result) &&
                (result.Contains(@"\") || result.Contains("/")) &&
                (result.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                 result.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                 result.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"🎨 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Result looks like image file path, output type: image");
                Debug.WriteLine($"🎨 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Result looks like image file path, output type: image");
                return "image";
            }

            // Check if the result looks like audio file path
            if (!string.IsNullOrEmpty(result) &&
                (result.Contains(@"\") || result.Contains("/")) &&
                (result.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                 result.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                 result.EndsWith(".aac", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"🔊 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Result looks like audio file path, output type: audio");
                Debug.WriteLine($"🔊 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Result looks like audio file path, output type: audio");
                return "audio";
            }

            // Default to text for any other output
            Console.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Defaulting to text output type");
            Debug.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Defaulting to text output type");
            return "text";
        }

        /// <summary>
        /// Prepares input for model execution from connected nodes
        /// </summary>
        public string PrepareModelInput(NodeViewModel modelNode, List<NodeViewModel> connectedInputNodes, int currentActionStep, DateTime? actionItemTimestamp = null)
        {
            if (connectedInputNodes.Count == 0)
            {
                return "";
            }

            if (modelNode.EnsembleInputCount > 1)
            {
                // Use ensemble logic for multi-input models
                var stepContents = new List<string>();
                foreach (var inputNode in connectedInputNodes)
                {
                    int stepForNodeContent = currentActionStep + 1;

                    // Use cache key to avoid repeated GetStepContent calls
                    string cacheKey = $"{inputNode.Id}_{stepForNodeContent}_{actionItemTimestamp?.Ticks ?? 0}";

                    string cachedContent;
                    lock (_stepContentCacheLock)
                    {
                        if (!_stepContentCache.TryGetValue(cacheKey, out cachedContent))
                        {
                            // FIXED: Pass ActionItem timestamp for audio/image file correlation
                            var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent, actionItemTimestamp);
                            if (!string.IsNullOrEmpty(contentValue))
                            {
                                cachedContent = contentType?.ToLowerInvariant() == "image" || contentType?.ToLowerInvariant() == "audio"
                                               ? contentValue
                                               : $"[{inputNode.Name}]: {contentValue}";
                            }
                            else
                            {
                                cachedContent = "";
                            }
                            _stepContentCache[cacheKey] = cachedContent;
                        }
                    }

                    if (!string.IsNullOrEmpty(cachedContent))
                    {
                        stepContents.Add(cachedContent);
                    }
                }
                return CombineStepContents(stepContents, modelNode.SelectedEnsembleMethod);
            }
            else
            {
                // Use single input with caching
                var inputNode = connectedInputNodes.First();
                int stepForNodeContent = currentActionStep + 1;

                string cacheKey = $"{inputNode.Id}_{stepForNodeContent}_{actionItemTimestamp?.Ticks ?? 0}";

                string cachedContent;
                lock (_stepContentCacheLock)
                {
                    if (!_stepContentCache.TryGetValue(cacheKey, out cachedContent))
                    {
                        // FIXED: Pass ActionItem timestamp for audio/image file correlation
                        var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent, actionItemTimestamp);
                        cachedContent = contentValue ?? "";
                        _stepContentCache[cacheKey] = cachedContent;
                    }
                }

                return cachedContent;
            }
        }

        /// <summary>
        /// Executes a single model node with input preparation and output storage
        /// <summary>
        /// Executes a single model node with optimized performance
        /// </summary>
        public async Task ExecuteSingleModelNodeAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            IEnumerable<NodeViewModel> nodes, IEnumerable<ConnectionViewModel> connections, int currentActionStep)
        {
            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                // Fast input preparation
                var connectedInputNodes = GetConnectedInputNodes(modelNode, nodes, connections);
                string input = PrepareModelInput(modelNode, connectedInputNodes, currentActionStep);

                // Execute model without excessive logging
                string result = await ExecuteModelWithInput(correspondingModel, input, connectedInputNodes).ConfigureAwait(false);

                // Fast result storage
                string resultContentType = DetermineResultContentType(correspondingModel, result);
                int currentStep = currentActionStep + 1;
                modelNode.SetStepOutput(currentStep, resultContentType, result);

                totalStopwatch.Stop();
                // Only log if execution took longer than 8 seconds (reduced threshold)
                if (totalStopwatch.ElapsedMilliseconds > 8000)
                {
                    Debug.WriteLine($"⏱️ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteSingleModelNodeAsync] '{modelNode.Name}' completed in {totalStopwatch.ElapsedMilliseconds}ms");
                }
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteSingleModelNodeAsync] Error executing model {modelNode.Name} after {totalStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                throw; // Re-throw to be handled by the caller
            }
        }

        /// <summary>
        /// Batched model execution for better performance when multiple nodes use the same model
        /// </summary>
        public async Task ExecuteBatchedModelsAsync(List<(NodeViewModel node, NeuralNetworkModel model, string input)> modelExecutions, int currentActionStep)
        {
            // Group by model ID for batched execution
            var modelGroups = modelExecutions.GroupBy(e => e.model.HuggingFaceModelId).ToList();

            var tasks = modelGroups.Select(async group =>
            {
                var modelId = group.Key;
                var executions = group.ToList();

                // For same-model executions, we can potentially optimize by keeping the model loaded
                foreach (var (node, model, input) in executions)
                {
                    try
                    {
                        var result = await ExecuteModelWithInput(model, input).ConfigureAwait(false);
                        var resultContentType = DetermineResultContentType(model, result);
                        var currentStep = currentActionStep + 1;
                        node.SetStepOutput(currentStep, resultContentType, result);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteBatchedModelsAsync] Error executing {node.Name}: {ex.Message}");
                        // Continue with other executions even if one fails
                    }
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Combines multiple image contents based on the specified ensemble method
        /// </summary>
        /// <param name="stepContents">List of image file paths to combine</param>
        /// <param name="ensembleMethod">The ensemble method to use for combination</param>
        /// <returns>Combined image representation for model input</returns>
        private string CombineImageContents(List<string> stepContents, string ensembleMethod)
        {
            if (stepContents == null || stepContents.Count == 0)
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] No image contents to combine");
                return string.Empty;
            }

            if (stepContents.Count == 1)
            {
                Debug.WriteLine($"📷 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Single image, returning: {stepContents[0]}");
                return stepContents[0];
            }

            Debug.WriteLine($"🎨 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Combining {stepContents.Count} images using method: {ensembleMethod}");

            switch (ensembleMethod?.ToLower())
            {
                case "sequential":
                    // For sequential, we want to process images in order
                    // Return a structured format that indicates multiple images
                    var sequentialResult = string.Join("|", stepContents.Select((img, idx) => $"img{idx + 1}:{img}"));
                    Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Sequential format: {sequentialResult}");
                    return sequentialResult;

                case "concatenate":
                case "blend":
                    // For concatenate/blend, we also want to preserve all images
                    // Return a structured format that the model can understand
                    var concatenatedResult = string.Join(";", stepContents);
                    Debug.WriteLine($"🔗 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Concatenated format: {concatenatedResult}");
                    return concatenatedResult;

                case "average":
                case "weighted":
                    // For averaging/weighting, we need all images to compute the ensemble
                    var averageResult = string.Join("&", stepContents.Select((img, idx) => $"{img}"));
                    Debug.WriteLine($"📊 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Average/Weighted format: {averageResult}");
                    return averageResult;

                default:
                    // Default behavior: use all images in a comma-separated format
                    var defaultResult = string.Join(",", stepContents);
                    Debug.WriteLine($"🔀 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Default format: {defaultResult}");
                    return defaultResult;
            }
        }

        /// <summary>
        /// Combines multiple audio contents based on the specified ensemble method
        /// </summary>
        /// <param name="stepContents">List of audio file paths to combine</param>
        /// <param name="ensembleMethod">The ensemble method to use for combination</param>
        /// <returns>Combined audio representation for model input</returns>
        private string CombineAudioContents(List<string> stepContents, string ensembleMethod)
        {
            if (stepContents == null || stepContents.Count == 0)
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] No audio contents to combine");
                return string.Empty;
            }

            if (stepContents.Count == 1)
            {
                Debug.WriteLine($"🎵 [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] Single audio file, returning: {stepContents[0]}");
                return stepContents[0];
            }

            Debug.WriteLine($"🎶 [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] Combining {stepContents.Count} audio files using method: {ensembleMethod}");

            switch (ensembleMethod?.ToLower())
            {
                case "sequential":
                    // For sequential audio processing
                    var sequentialResult = string.Join("|", stepContents.Select((audio, idx) => $"audio{idx + 1}:{audio}"));
                    Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] Sequential format: {sequentialResult}");
                    return sequentialResult;

                case "concatenate":
                case "mix":
                    // For concatenating/mixing audio files
                    var concatenatedResult = string.Join(";", stepContents);
                    Debug.WriteLine($"🔗 [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] Concatenated format: {concatenatedResult}");
                    return concatenatedResult;

                case "average":
                case "weighted":
                    // For averaging/weighting audio inputs
                    var averageResult = string.Join("&", stepContents);
                    Debug.WriteLine($"📊 [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] Average/Weighted format: {averageResult}");
                    return averageResult;

                default:
                    // Default behavior: use all audio files in a comma-separated format
                    var defaultResult = string.Join(",", stepContents);
                    Debug.WriteLine($"🔀 [{DateTime.Now:HH:mm:ss.fff}] [CombineAudioContents] Default format: {defaultResult}");
                    return defaultResult;
            }
        }

        /// <summary>
        /// Processes combined image input for proper model execution with contextual descriptions
        /// </summary>
        /// <param name="model">The model that will process the input</param>
        /// <param name="input">The combined image input (could be multiple paths)</param>
        /// <param name="connectedInputNodes">The input nodes to get context from</param>
        /// <returns>Processed input suitable for the model</returns>
        private async Task<string> ProcessCombinedImageInputAsync(NeuralNetworkModel model, string input, List<NodeViewModel> connectedInputNodes)
        {
            if (string.IsNullOrEmpty(input))
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Empty input provided");
                return input;
            }

            // Check if this looks like a combined image input format
            bool isCombinedImageInput = DetectCombinedImageInput(input);

            if (!isCombinedImageInput)
            {
                // Regular input, return as-is
                Debug.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Regular input, passing through: {input.Substring(0, Math.Min(50, input.Length))}...");
                return input;
            }

            Debug.WriteLine($"🖼️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Detected combined image input: {input}");

            // Parse the combined image input to extract individual image paths
            var imagePaths = ParseCombinedImageInput(input);

            if (imagePaths.Count == 0)
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] No valid image paths found in input");
                return input;
            }

            if (imagePaths.Count == 1)
            {
                // Single image, return directly
                Debug.WriteLine($"📷 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Single image extracted: {imagePaths[0]}");
                return imagePaths[0];
            }

            // Multiple images - process each separately and combine results
            Debug.WriteLine($"🎨 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Processing {imagePaths.Count} images for ensemble execution");

            var validImagePaths = imagePaths.Where(path => !string.IsNullOrEmpty(path) && IsValidImagePath(path)).ToList();

            if (validImagePaths.Count > 0)
            {
                Debug.WriteLine($"🖼️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Processing {validImagePaths.Count} valid images individually");

                // Process each image separately and collect results with context
                var imageResults = new List<string>();
                for (int i = 0; i < validImagePaths.Count; i++)
                {
                    var imagePath = validImagePaths[i];

                    // Get contextual description from connected node
                    string nodeContext = GetNodeContextDescription(imagePath, connectedInputNodes, "Image");

                    Debug.WriteLine($"📸 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Processing image {i + 1}/{validImagePaths.Count}: {Path.GetFileName(imagePath)} from {nodeContext}");

                    try
                    {
                        // Execute model on individual image
                        var individualResult = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, imagePath);
                        if (!string.IsNullOrEmpty(individualResult))
                        {
                            // Clean up the result - remove duplicate filename references and format properly
                            var cleanResult = individualResult;

                            // Remove duplicate filename if it appears at the start of the caption
                            var filename = Path.GetFileName(imagePath);
                            if (cleanResult.StartsWith($"{filename}: "))
                            {
                                cleanResult = cleanResult.Substring($"{filename}: ".Length);
                            }

                            // Check if the result already contains proper formatting (from Python script)
                            if (cleanResult.StartsWith($"Image {i + 1} (") || cleanResult.Contains("): "))
                            {
                                // Python script already formatted it, but replace with clean node context
                                var formattedResult = cleanResult.Replace($"Image {i + 1} (", $"{nodeContext} (");
                                imageResults.Add(formattedResult);
                            }
                            else
                            {
                                // Add clean formatting with node context (no duplicate filenames)
                                imageResults.Add($"{nodeContext}: {cleanResult}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Error processing image {i + 1}: {ex.Message}");
                        imageResults.Add($"{nodeContext}: Error - {ex.Message}");
                    }
                }

                if (imageResults.Count > 0)
                {
                    // Combine all individual results without extra wrapper text
                    var combinedResult = string.Join("\n\n", imageResults);
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Successfully processed {imageResults.Count} images, combined result length: {combinedResult.Length}");

                    // Return a special marker to indicate this is already processed
                    return $"ENSEMBLE_PROCESSED:{combinedResult}";
                }
            }

            Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] No valid image paths found, returning original input");
            return input;
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility
        /// </summary>
        private string ProcessCombinedImageInput(NeuralNetworkModel model, string input)
        {
            // For simple cases, return the input directly to avoid breaking existing functionality
            if (string.IsNullOrEmpty(input) || !DetectCombinedImageInput(input))
            {
                return input;
            }

            // For combined image inputs, we'll fall back to first image approach for now
            // The async version above should be used for proper ensemble processing
            var imagePaths = ParseCombinedImageInput(input);
            var validImagePaths = imagePaths.Where(path => !string.IsNullOrEmpty(path) && IsValidImagePath(path)).ToList();

            if (validImagePaths.Count > 0)
            {
                Debug.WriteLine($"⚡ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] Sync version: using first image {validImagePaths[0]} (consider using async ensemble processing)");
                return validImagePaths[0];
            }

            return input;
        }

        /// <summary>
        /// Detects if input looks like combined image input
        /// </summary>
        private bool DetectCombinedImageInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            // Check for our ensemble format patterns
            return input.Contains("|") || input.Contains(";") || input.Contains("&") ||
                   (input.Contains(",") && input.Contains(".jpg") || input.Contains(".png") || input.Contains(".jpeg"));
        }

        /// <summary>
        /// Parses combined image input to extract individual image paths
        /// </summary>
        private List<string> ParseCombinedImageInput(string input)
        {
            var imagePaths = new List<string>();

            if (string.IsNullOrEmpty(input)) return imagePaths;

            // Handle different ensemble formats
            if (input.Contains("|"))
            {
                // Sequential format: img1:path1|img2:path2
                var parts = input.Split('|');
                foreach (var part in parts)
                {
                    if (part.Contains(":"))
                    {
                        var pathPart = part.Split(':')[1];
                        imagePaths.Add(pathPart.Trim());
                    }
                    else
                    {
                        imagePaths.Add(part.Trim());
                    }
                }
            }
            else if (input.Contains(";"))
            {
                // Concatenate format: path1;path2
                imagePaths.AddRange(input.Split(';').Select(p => p.Trim()));
            }
            else if (input.Contains("&"))
            {
                // Average/weighted format: path1&path2
                imagePaths.AddRange(input.Split('&').Select(p => p.Trim()));
            }
            else if (input.Contains(","))
            {
                // Default format: path1,path2
                imagePaths.AddRange(input.Split(',').Select(p => p.Trim()));
            }
            else
            {
                // Single path
                imagePaths.Add(input.Trim());
            }

            return imagePaths.Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        /// <summary>
        /// Validates if a path is a valid image file
        /// </summary>
        private bool IsValidImagePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" ||
                   extension == ".bmp" || extension == ".gif" || extension == ".tiff";
        }

        /// <summary>
        /// Processes combined audio input for proper model execution with contextual descriptions
        /// </summary>
        /// <param name="model">The model that will process the input</param>
        /// <param name="input">The combined audio input (could be multiple paths)</param>
        /// <param name="connectedInputNodes">The input nodes to get context from</param>
        /// <returns>Processed input suitable for the model</returns>
        private async Task<string> ProcessCombinedAudioInputAsync(NeuralNetworkModel model, string input, List<NodeViewModel> connectedInputNodes)
        {
            if (string.IsNullOrEmpty(input))
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Empty input provided");
                return input;
            }

            // Check if this looks like a combined audio input format
            bool isCombinedAudioInput = DetectCombinedAudioInput(input);

            if (!isCombinedAudioInput)
            {
                // Regular input, return as-is
                Debug.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Regular input, passing through: {input.Substring(0, Math.Min(50, input.Length))}...");
                return input;
            }

            Debug.WriteLine($"🎵 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Detected combined audio input: {input}");

            // Parse the combined audio input to extract individual audio paths
            var audioPaths = ParseCombinedAudioInput(input);

            if (audioPaths.Count == 0)
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] No valid audio paths found in input");
                return input;
            }

            if (audioPaths.Count == 1)
            {
                // Single audio, return directly
                Debug.WriteLine($"🎧 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Single audio extracted: {audioPaths[0]}");
                return audioPaths[0];
            }

            // Multiple audio files - process each separately with contextual descriptions and combine results
            Debug.WriteLine($"🎼 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Processing {audioPaths.Count} audio files for ensemble execution");

            var validAudioPaths = audioPaths.Where(path => !string.IsNullOrEmpty(path) && IsValidAudioPath(path)).ToList();

            if (validAudioPaths.Count > 0)
            {
                Debug.WriteLine($"🎵 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Processing {validAudioPaths.Count} valid audio files individually");

                // Process each audio separately and collect results with context
                var audioResults = new List<string>();
                for (int i = 0; i < validAudioPaths.Count; i++)
                {
                    var audioPath = validAudioPaths[i];

                    // Get contextual description from connected node
                    string nodeContext = GetNodeContextDescription(audioPath, connectedInputNodes, "Audio");

                    Debug.WriteLine($"🎧 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Processing audio {i + 1}/{validAudioPaths.Count}: {Path.GetFileName(audioPath)} from {nodeContext}");

                    try
                    {
                        // Execute model on individual audio
                        var individualResult = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, audioPath);
                        if (!string.IsNullOrEmpty(individualResult))
                        {
                            // Clean up the result - remove duplicate filename references and format properly
                            var cleanResult = individualResult;

                            // Remove duplicate filename if it appears at the start of the transcription
                            var filename = Path.GetFileName(audioPath);
                            if (cleanResult.StartsWith($"{filename}: "))
                            {
                                cleanResult = cleanResult.Substring($"{filename}: ".Length);
                            }

                            // Check if the result already contains proper formatting (from Python script)
                            if (cleanResult.StartsWith($"Audio {i + 1} (") || cleanResult.Contains("): "))
                            {
                                // Python script already formatted it, but replace with clean node context
                                var formattedResult = cleanResult.Replace($"Audio {i + 1} (", $"{nodeContext} (");
                                audioResults.Add(formattedResult);
                            }
                            else
                            {
                                // Add clean formatting with node context (no duplicate filenames)
                                audioResults.Add($"{nodeContext}: {cleanResult}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Error processing audio {i + 1}: {ex.Message}");
                        audioResults.Add($"{nodeContext}: Error - {ex.Message}");
                    }
                }

                if (audioResults.Count > 0)
                {
                    // Combine all individual results without extra wrapper text
                    var combinedResult = string.Join("\n\n", audioResults);
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Successfully processed {audioResults.Count} audio files, combined result length: {combinedResult.Length}");

                    // Return a special marker to indicate this is already processed
                    return $"ENSEMBLE_PROCESSED:{combinedResult}";
                }
            }

            Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] No valid audio paths found, returning original input");
            return input;
        }

        /// <summary>
        /// Synchronous wrapper for backward compatibility (audio)
        /// </summary>
        private string ProcessCombinedAudioInput(NeuralNetworkModel model, string input, List<NodeViewModel> connectedInputNodes = null)
        {
            // For simple cases, return the input directly to avoid breaking existing functionality
            if (string.IsNullOrEmpty(input) || !DetectCombinedAudioInput(input))
            {
                return input;
            }

            // For combined audio inputs, we'll fall back to first audio approach for now
            // The async version above should be used for proper ensemble processing
            var audioPaths = ParseCombinedAudioInput(input);
            var validAudioPaths = audioPaths.Where(path => !string.IsNullOrEmpty(path) && IsValidAudioPath(path)).ToList();

            if (validAudioPaths.Count > 0)
            {
                Debug.WriteLine($"⚡ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Sync version: using first audio {validAudioPaths[0]} (consider using async ensemble processing)");
                return validAudioPaths[0];
            }

            return input;
        }

        /// <summary>
        /// Gets contextual description for a node based on its name and type
        /// </summary>
        private string GetNodeContextDescription(string filePath, List<NodeViewModel> connectedInputNodes, string mediaType)
        {
            if (connectedInputNodes == null || connectedInputNodes.Count == 0)
            {
                return $"Unknown {mediaType}";
            }

            // Try to find the node that corresponds to this file path
            var matchingNode = connectedInputNodes.FirstOrDefault(node =>
            {
                // Try to match based on the file path or node content
                var nodeContent = node.GetStepContent(1); // Get the latest content
                return nodeContent.Value?.Contains(Path.GetFileName(filePath)) == true;
            });

            string nodeName = null;
            if (matchingNode != null)
            {
                nodeName = matchingNode.Name ?? "Unknown";
            }

            // Enhanced fallback - try to infer from file path regardless of matching node
            string fileName = Path.GetFileNameWithoutExtension(filePath).ToLower();

            // Check filename patterns first for better accuracy
            if (fileName.Contains("webcamaudio") || (fileName.Contains("webcam") && fileName.Contains("audio")))
            {
                return "Webcam Audio";
            }
            else if (fileName.Contains("webcamimage") || (fileName.Contains("webcam") && (fileName.Contains("image") || fileName.Contains("jpg") || fileName.Contains("png"))))
            {
                return "Webcam Image";
            }
            else if (fileName.Contains("pcaudio") || (fileName.Contains("pc") && fileName.Contains("audio")))
            {
                return "PC Audio";
            }
            else if (fileName.Contains("screencapture") || fileName.Contains("screenshot") || (fileName.Contains("screen") && (fileName.Contains("capture") || fileName.Contains("display"))))
            {
                return "Screen Image";
            }
            else if (fileName.Contains("microphone") || fileName.Contains("mic"))
            {
                return "Microphone Audio";
            }
            else if (fileName.Contains("webcam"))
            {
                return $"Webcam {mediaType}";
            }
            else if (fileName.Contains("screen"))
            {
                return $"Screen {mediaType}";
            }
            else if (fileName.Contains("pc") || fileName.Contains("system") || fileName.Contains("desktop"))
            {
                return $"PC {mediaType}";
            }

            // If we have a matching node, use its name with enhanced patterns
            if (!string.IsNullOrEmpty(nodeName) && nodeName != "Unknown")
            {
                string lowerNodeName = nodeName.ToLower();

                // Add descriptive context based on node name with enhanced specificity
                if (lowerNodeName.Contains("webcam") && lowerNodeName.Contains("audio"))
                {
                    return "Webcam Audio";
                }
                else if (lowerNodeName.Contains("webcam") && (lowerNodeName.Contains("image") || lowerNodeName.Contains("video")))
                {
                    return "Webcam Image";
                }
                else if (lowerNodeName.Contains("webcam"))
                {
                    return $"Webcam {mediaType}";
                }
                else if (lowerNodeName.Contains("pc") && lowerNodeName.Contains("audio"))
                {
                    return "PC Audio";
                }
                else if (lowerNodeName.Contains("system") && lowerNodeName.Contains("audio"))
                {
                    return "System Audio";
                }
                else if (lowerNodeName.Contains("desktop") && lowerNodeName.Contains("audio"))
                {
                    return "Desktop Audio";
                }
                else if (lowerNodeName.Contains("screen") && lowerNodeName.Contains("audio"))
                {
                    return "Screen Audio";
                }
                else if (lowerNodeName.Contains("screen") || lowerNodeName.Contains("screenshot"))
                {
                    return "Screen Image";
                }
                else if (lowerNodeName.Contains("microphone") || lowerNodeName.Contains("mic"))
                {
                    return "Microphone Audio";
                }
                else if (lowerNodeName.Contains("file"))
                {
                    return $"File {mediaType}";
                }
                else if (lowerNodeName.Contains("audio"))
                {
                    return $"{nodeName} Audio";
                }
                else if (lowerNodeName.Contains("image") || lowerNodeName.Contains("video") || lowerNodeName.Contains("camera"))
                {
                    return $"{nodeName} Image";
                }
                else
                {
                    return $"{nodeName} {mediaType}";
                }
            }

            // Final fallback to generic description
            return $"Source {mediaType}";
        }

        /// <summary>
        /// Detects if input looks like combined audio input
        /// </summary>
        private bool DetectCombinedAudioInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            // Check for our ensemble format patterns and audio file extensions
            return (input.Contains("|") || input.Contains(";") || input.Contains("&") ||
                   (input.Contains(",") && (input.Contains(".wav") || input.Contains(".mp3") || input.Contains(".m4a") ||
                   input.Contains(".flac") || input.Contains(".ogg") || input.Contains(".aac"))));
        }

        /// <summary>
        /// Parses combined audio input to extract individual audio paths
        /// </summary>
        private List<string> ParseCombinedAudioInput(string input)
        {
            var audioPaths = new List<string>();

            if (string.IsNullOrEmpty(input)) return audioPaths;

            // Handle different ensemble formats (similar to images but for audio)
            if (input.Contains("|"))
            {
                // Sequential format: audio1:path1|audio2:path2
                var parts = input.Split('|');
                foreach (var part in parts)
                {
                    if (part.Contains(":"))
                    {
                        var pathPart = part.Split(':')[1];
                        audioPaths.Add(pathPart.Trim());
                    }
                    else
                    {
                        audioPaths.Add(part.Trim());
                    }
                }
            }
            else if (input.Contains(";"))
            {
                // Concatenate format: path1;path2
                audioPaths.AddRange(input.Split(';').Select(p => p.Trim()));
            }
            else if (input.Contains("&"))
            {
                // Average/weighted format: path1&path2
                audioPaths.AddRange(input.Split('&').Select(p => p.Trim()));
            }
            else if (input.Contains(","))
            {
                // Default format: path1,path2
                audioPaths.AddRange(input.Split(',').Select(p => p.Trim()));
            }
            else
            {
                // Single path
                audioPaths.Add(input.Trim());
            }

            return audioPaths.Where(p => !string.IsNullOrEmpty(p)).ToList();
        }

        /// <summary>
        /// Validates if a path is a valid audio file
        /// </summary>
        private bool IsValidAudioPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            var extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".wav" || extension == ".mp3" || extension == ".m4a" ||
                   extension == ".flac" || extension == ".ogg" || extension == ".aac";
        }
    }
}
