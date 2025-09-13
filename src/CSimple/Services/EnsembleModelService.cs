using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSimple.Models;
using CSimple.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CSimple.Services
{
    /// <summary>
    /// Service for handling ensemble model execution and related operations
    /// </summary>
    public class EnsembleModelService
    {
        private readonly NetPageViewModel _netPageViewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, NodeViewModel> _nodeCache = new Dictionary<string, NodeViewModel>();
        private readonly object _nodeCacheLock = new object(); // Thread safety for node cache

        // Cache for step content to avoid repeated expensive GetStepContent calls
        private readonly Dictionary<string, string> _stepContentCache = new Dictionary<string, string>();
        private readonly object _stepContentCacheLock = new object(); // Thread safety for step content cache

        // Model execution optimization: batch similar models together
        private readonly Dictionary<string, List<(NodeViewModel node, string input)>> _batchedExecutions = new Dictionary<string, List<(NodeViewModel, string)>>();
        private readonly object _batchedExecutionsLock = new object(); // Thread safety for batched executions

        public EnsembleModelService(NetPageViewModel netPageViewModel = null)
        {
            _netPageViewModel = netPageViewModel; // Allow null to break circular dependency
        }

        public EnsembleModelService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider; // Use service provider to get NetPageViewModel when needed
        }

        private NetPageViewModel GetNetPageViewModel()
        {
            // Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Attempting to get NetPageViewModel...");

            if (_netPageViewModel != null)
            {
                // Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Using cached NetPageViewModel");
                return _netPageViewModel;
            }

            if (_serviceProvider != null)
            {
                try
                {
                    // Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Getting NetPageViewModel from service provider...");
                    var netPageViewModel = _serviceProvider.GetRequiredService<NetPageViewModel>();
                    // Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Successfully retrieved NetPageViewModel from service provider");
                    return netPageViewModel;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Could not get NetPageViewModel from service provider: {ex.Message}");
                    Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Stack trace: {ex.StackTrace}");
                    return null;
                }
            }

            Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [GetNetPageViewModel] Both _netPageViewModel and _serviceProvider are null!");
            return null;
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

            Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ClearStepContentCache] Cleared all ensemble service caches");
        }

        /// <summary>
        /// Clear ActionSteps from specific model nodes to prevent old content artifacts
        /// </summary>
        public void ClearModelNodeActionSteps(IEnumerable<NodeViewModel> nodes)
        {
            try
            {
                if (nodes == null) return;

                foreach (var node in nodes)
                {
                    if (node?.Type == NodeType.Model && node.ActionSteps?.Count > 0)
                    {
                        Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [ClearModelNodeActionSteps] Clearing {node.ActionSteps.Count} ActionSteps from model node: {node.Name}");
                        node.ActionSteps.Clear();
                    }
                }
                Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ClearModelNodeActionSteps] Cleared ActionSteps from model nodes");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ClearModelNodeActionSteps] Error clearing ActionSteps: {ex.Message}");
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
                // Debug.WriteLine($"🖼️ [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Detected image content, combining {stepContents.Count} images using method: {ensembleMethod}");
                return CombineImageContents(stepContents, ensembleMethod);
            }

            if (isAudioContent)
            {
                // Debug.WriteLine($"🔊 [{DateTime.Now:HH:mm:ss.fff}] [CombineStepContents] Detected audio content, combining {stepContents.Count} audio files using method: {ensembleMethod}");
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
            // Debug.WriteLine($"🚀 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] CALLED with model: {model?.Name ?? "NULL"}, input length: {input?.Length ?? 0}");

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
                // Check if this is a multimodal model (vision-language like GUI Owl) or has multimodal input
                else if (model.InputType == ModelInputType.Multimodal ||
                         IsGuiOwlModel(model) ||
                         (IsMultimodalInput(input) && IsVisionLanguageModel(model)))
                {
                    Debug.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Processing multimodal input for vision-language model {model.Name}");
                    processedInput = await ProcessMultimodalInputAsync(model, input, connectedInputNodes ?? new List<NodeViewModel>());

                    // If it was processed as structured input, return the result directly
                    if (processedInput.StartsWith("MULTIMODAL_PROCESSED:"))
                    {
                        return processedInput.Substring("MULTIMODAL_PROCESSED:".Length);
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
                    else
                    {
                        // Fallback: If async processing didn't work, try processing individual files
                        Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Async processing failed, falling back to first audio file");
                        var audioPaths = ParseCombinedAudioInput(input);
                        if (audioPaths.Count > 0 && IsValidAudioPath(audioPaths[0]))
                        {
                            processedInput = audioPaths[0];
                            Debug.WriteLine($"🎧 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Using first audio file: {processedInput}");
                        }
                        else
                        {
                            Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] No valid audio paths found, using original input");
                            processedInput = input;
                        }
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

                var netPageViewModel = GetNetPageViewModel();
                if (netPageViewModel == null)
                {
                    Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] NetPageViewModel is null, returning empty result");
                    return "NetPageViewModel not available";
                }

                var result = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, processedInput);
                var rawResult = result ?? "No output generated";

                // Clean the result to remove concatenated ensemble input before returning
                var cleanedResult = CleanModelResultForDisplay(rawResult, model.Name);
                // Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithInput] Cleaned result for {model.Name}: {cleanedResult?.Substring(0, Math.Min(cleanedResult?.Length ?? 0, 100))}...");

                return cleanedResult;
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
                Debug.WriteLine($"🔊 [{DateTime.Now:HH:mm:ss.fff}] [DetermineResultContentType] Result looks like audio file path, output type: audio");
                return "audio";
            }

            // Default to text for any other output
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
                                               : $"{inputNode.Name}: {contentValue}"; // Use safe formatting without brackets
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
        /// Cleans model result for clean display by removing ensemble formatting prefixes and concatenated input
        /// </summary>
        public string CleanModelResultForDisplay(string result, string modelName)
        {
            if (string.IsNullOrEmpty(result))
                return result;

            // Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Processing result for {modelName}, length: {result.Length}");
            // Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] First 100 chars: {result.Substring(0, Math.Min(100, result.Length))}...");

            // Try to parse as JSON first (for models that output structured data)
            var jsonResult = TryParseJsonOutput(result, modelName);
            if (!string.IsNullOrEmpty(jsonResult))
            {
                Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Successfully parsed JSON output for {modelName}");
                return jsonResult;
            }

            // Remove ENSEMBLE_PROCESSED prefix if present
            if (result.StartsWith("ENSEMBLE_PROCESSED:"))
            {
                result = result.Substring("ENSEMBLE_PROCESSED:".Length);
            }

            // Check if this is a properly formatted ensemble result (should be preserved as-is)
            if (IsProperlyFormattedEnsembleResult(result))
            {
                Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Preserving properly formatted ensemble result");
                return result.Trim();
            }

            // Much more aggressive cleaning - this result appears to be the concatenated ensemble input, not the actual model output
            // The Python script is returning the full input instead of just the generated text

            // For any result that contains ensemble markers, we need to extract ONLY the model's own contribution
            if (result.Contains("Screen Image:") || result.Contains("Webcam Image:") ||
                result.Contains("Goals (File):") || result.Contains("PC Audio:") ||
                result.Contains("Webcam Audio:") || result.Contains("Goal:") ||
                result.Contains("Blip Image Captioning Base:") || result.Contains("Gpt2:") ||
                result.Contains("Whisper Base:"))
            {
                // Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Detected concatenated ensemble result, performing aggressive extraction");

                // Split into sentences and look for the actual model output
                var sentences = result.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var sentence in sentences)
                {
                    string cleanSentence = sentence.Trim();

                    // Skip sentences that are clearly input data or other model outputs
                    if (cleanSentence.Contains("Screen Image:") || cleanSentence.Contains("Webcam Image:") ||
                        cleanSentence.Contains("Goals (File):") || cleanSentence.Contains("PC Audio:") ||
                        cleanSentence.Contains("Webcam Audio:") || cleanSentence.Contains("Goal:") ||
                        cleanSentence.Contains("Priority:") || cleanSentence.Contains("Deadline:") ||
                        cleanSentence.Contains("Description:") || cleanSentence.Contains("Build an app") ||
                        cleanSentence.Contains("Current User Goals") || cleanSentence.Length < 10 ||
                        cleanSentence.Contains("[Client thread/INFO]") || cleanSentence.Contains("Loading skin images") ||
                        cleanSentence.Contains("Config file") || cleanSentence.Contains("AppData") ||
                        cleanSentence.Contains("Roaming") || cleanSentence.Contains("GEMFILE") ||
                        cleanSentence.Contains("ItemID=") || cleanSentence.Contains("CBE") ||
                        cleanSentence.Contains("FFTs are still in play") || cleanSentence.Contains("RUNABLE TO OBJECTIONED") ||
                        cleanSentence.Contains("Dec ") || cleanSentence.Contains("Nov ") || cleanSentence.Contains("Oct ") ||
                        cleanSentence.Contains("If there were any mistakes") || cleanSentence.Contains("sooner rather than later"))
                    {
                        continue;
                    }

                    // Clean any model prefixes from the sentence
                    string[] prefixesToRemove = {
                        "Gpt2 [Goal]:", "Gpt2 [Plan]:", "Gpt2 [Action]:", "Gpt2:",
                        "Blip Image Captioning Base:", "Whisper Base:",
                        modelName + ":"
                    };

                    foreach (var prefix in prefixesToRemove)
                    {
                        if (cleanSentence.StartsWith(prefix))
                        {
                            cleanSentence = cleanSentence.Substring(prefix.Length).Trim();
                            break;
                        }
                    }

                    // If we found a clean sentence that looks like actual generated content, return it
                    if (!string.IsNullOrEmpty(cleanSentence) && cleanSentence.Length > 15 &&
                        !cleanSentence.Contains("Compress PNG") && !cleanSentence.Contains("hello 1124"))
                    {
                        // Debug.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Extracted clean content: {cleanSentence}");
                        return cleanSentence;
                    }
                }

                // If no clean content found, try a different approach - look for meaningful text after the last colon
                var colonIndex = result.LastIndexOf(": ");
                if (colonIndex > 0 && colonIndex < result.Length - 20)
                {
                    var afterColon = result.Substring(colonIndex + 2).Trim();
                    // Remove common input patterns from the end
                    var stopPatterns = new[] { "Goal:", "Description:", "Priority:", "Deadline:", "Current User Goals", "Gpt2:", "Whisper Base:", "Goals (File):" };

                    foreach (var stopPattern in stopPatterns)
                    {
                        var stopIndex = afterColon.IndexOf(stopPattern);
                        if (stopIndex > 0)
                        {
                            afterColon = afterColon.Substring(0, stopIndex).Trim();
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(afterColon) && afterColon.Length > 15)
                    {
                        Debug.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Extracted content after last colon: {afterColon}");
                        return afterColon;
                    }
                }

                // Alternative approach: look for image descriptions (for image captioning models)
                if (modelName.Contains("Blip") || modelName.Contains("Image") || modelName.Contains("Caption"))
                {
                    // Look for content that looks like image descriptions - typically short descriptive phrases
                    var parts = result.Split(new[] { "  ", ": " }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var cleanPart = part.Trim();
                        // Image descriptions are usually about people, objects, scenes
                        if (cleanPart.Length > 20 && cleanPart.Length < 200 &&
                            (cleanPart.Contains("man") || cleanPart.Contains("woman") || cleanPart.Contains("person") ||
                             cleanPart.Contains("sitting") || cleanPart.Contains("standing") || cleanPart.Contains("wearing") ||
                             cleanPart.Contains("desk") || cleanPart.Contains("keyboard") || cleanPart.Contains("computer") ||
                             cleanPart.Contains("room") || cleanPart.Contains("field") || cleanPart.Contains("group")) &&
                            !cleanPart.Contains("Gpt2") && !cleanPart.Contains("Whisper") && !cleanPart.Contains("Goals") &&
                            !cleanPart.Contains("[") && !cleanPart.Contains("Config") && !cleanPart.Contains("thread"))
                        {
                            Debug.WriteLine($"🎯 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Found image description: {cleanPart}");
                            return cleanPart;
                        }
                    }
                }

                // Last resort: return a clean message indicating the model ran
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Could not extract clean content, using fallback");
                return $"Model output processed (content filtering applied due to concatenated input)";
            }

            // Standard cleaning for non-concatenated results
            var lines = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var cleanedLines = new List<string>();

            foreach (var line in lines)
            {
                string cleanedLine = line.Trim();

                // Remove patterns like "Gpt2 [Plan]: content" or "Blip Image Captioning Base: content"
                if (cleanedLine.Contains(": "))
                {
                    var colonIndex = cleanedLine.IndexOf(": ");
                    var prefix = cleanedLine.Substring(0, colonIndex);

                    // Check if the prefix looks like a model name (contains model identifiers)
                    if (prefix.Contains("Gpt") || prefix.Contains("Blip") || prefix.Contains("Image") ||
                        prefix.Contains("[Plan]") || prefix.Contains("[Goal]") || prefix.Contains("[Action]") ||
                        prefix.Contains("Captioning") || prefix.Contains("Base"))
                    {
                        // Extract just the content after the colon
                        cleanedLine = cleanedLine.Substring(colonIndex + 2).Trim();
                    }
                }

                if (!string.IsNullOrEmpty(cleanedLine))
                {
                    cleanedLines.Add(cleanedLine);
                }
            }

            string finalResult = cleanedLines.Count > 0 ? string.Join("\n", cleanedLines) : result;
            // Debug.WriteLine($"🧹 [{DateTime.Now:HH:mm:ss.fff}] [CleanModelResultForDisplay] Final cleaned result: {finalResult}");
            return finalResult;
        }

        /// <summary>
        /// Attempts to parse JSON output from models like GUI Owl that may return structured data
        /// </summary>
        private string TryParseJsonOutput(string result, string modelName)
        {
            if (string.IsNullOrEmpty(result))
                return null;

            // Check for common error patterns first
            var trimmed = result.Trim();
            if (trimmed.Contains("generation error:") || trimmed.Contains("Image features and image tokens do not match"))
            {
                // Return a clean error message for GUI Owl token mismatches
                if (trimmed.Contains("Image features and image tokens do not match"))
                {
                    return "⚠️ GUI model processing error: Image format incompatibility detected. Please check input images.";
                }
                return $"⚠️ Model generation error: {trimmed.Substring(trimmed.IndexOf("error:") + 6).Trim()}";
            }

            try
            {
                // Look for JSON-like structures in the result

                // Check if it starts and ends with JSON markers
                if ((trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                    (trimmed.StartsWith("[") && trimmed.EndsWith("]")))
                {
                    // Try to parse as JSON
                    using var document = System.Text.Json.JsonDocument.Parse(trimmed);
                    var root = document.RootElement;

                    // Handle different JSON structures commonly output by vision-language models
                    if (root.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        // Look for common response fields in order of preference
                        if (root.TryGetProperty("response", out var response) && !string.IsNullOrEmpty(response.GetString()))
                        {
                            return response.GetString();
                        }
                        else if (root.TryGetProperty("analysis", out var analysis) && !string.IsNullOrEmpty(analysis.GetString()))
                        {
                            return analysis.GetString();
                        }
                        else if (root.TryGetProperty("description", out var description) && !string.IsNullOrEmpty(description.GetString()))
                        {
                            return description.GetString();
                        }
                        else if (root.TryGetProperty("text", out var text) && !string.IsNullOrEmpty(text.GetString()))
                        {
                            return text.GetString();
                        }
                        else if (root.TryGetProperty("caption", out var caption) && !string.IsNullOrEmpty(caption.GetString()))
                        {
                            return caption.GetString();
                        }
                        else if (root.TryGetProperty("answer", out var answer) && !string.IsNullOrEmpty(answer.GetString()))
                        {
                            return answer.GetString();
                        }
                        else if (root.TryGetProperty("content", out var content) && !string.IsNullOrEmpty(content.GetString()))
                        {
                            return content.GetString();
                        }
                        else if (root.TryGetProperty("summary", out var summary) && !string.IsNullOrEmpty(summary.GetString()))
                        {
                            return summary.GetString();
                        }
                        else
                        {
                            // Check for GUI-specific structured fields (GUI Owl 7B format)
                            var formattedResponse = new System.Text.StringBuilder();
                            bool hasContent = false;

                            // Primary GUI analysis fields
                            if (root.TryGetProperty("main_application", out var mainApp) && !string.IsNullOrEmpty(mainApp.GetString()))
                            {
                                formattedResponse.AppendLine($"Main Application: {mainApp.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("application", out var app) && !string.IsNullOrEmpty(app.GetString()))
                            {
                                formattedResponse.AppendLine($"Application: {app.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("ui_elements", out var uiElements) && !string.IsNullOrEmpty(uiElements.GetString()))
                            {
                                formattedResponse.AppendLine($"UI Elements: {uiElements.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("elements", out var elements) && !string.IsNullOrEmpty(elements.GetString()))
                            {
                                formattedResponse.AppendLine($"Elements: {elements.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("content_summary", out var contentSummary) && !string.IsNullOrEmpty(contentSummary.GetString()))
                            {
                                formattedResponse.AppendLine($"Content: {contentSummary.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("content", out var contentProp) && !string.IsNullOrEmpty(contentProp.GetString()))
                            {
                                formattedResponse.AppendLine($"Content: {contentProp.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("user_focus", out var userFocus) && !string.IsNullOrEmpty(userFocus.GetString()))
                            {
                                formattedResponse.AppendLine($"User Focus: {userFocus.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("focus", out var focus) && !string.IsNullOrEmpty(focus.GetString()))
                            {
                                formattedResponse.AppendLine($"Focus: {focus.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("next_actions", out var nextActions) && !string.IsNullOrEmpty(nextActions.GetString()))
                            {
                                formattedResponse.AppendLine($"Suggested Actions: {nextActions.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("actions", out var actions) && !string.IsNullOrEmpty(actions.GetString()))
                            {
                                formattedResponse.AppendLine($"Actions: {actions.GetString()}");
                                hasContent = true;
                            }
                            // Additional common GUI analysis fields
                            if (root.TryGetProperty("screen_description", out var screenDesc) && !string.IsNullOrEmpty(screenDesc.GetString()))
                            {
                                formattedResponse.AppendLine($"Screen: {screenDesc.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("window_title", out var windowTitle) && !string.IsNullOrEmpty(windowTitle.GetString()))
                            {
                                formattedResponse.AppendLine($"Window: {windowTitle.GetString()}");
                                hasContent = true;
                            }
                            if (root.TryGetProperty("task", out var task) && !string.IsNullOrEmpty(task.GetString()))
                            {
                                formattedResponse.AppendLine($"Task: {task.GetString()}");
                                hasContent = true;
                            }

                            if (hasContent)
                            {
                                return formattedResponse.ToString().Trim();
                            }

                            // If no standard field found, return formatted JSON
                            return System.Text.Json.JsonSerializer.Serialize(root, new System.Text.Json.JsonSerializerOptions
                            {
                                WriteIndented = true
                            });
                        }
                    }
                    else if (root.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        // Handle array responses (multiple predictions, etc.)
                        var items = new List<string>();
                        foreach (var item in root.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var itemString = item.GetString();
                                if (!string.IsNullOrEmpty(itemString))
                                {
                                    items.Add(itemString);
                                }
                            }
                            else if (item.ValueKind == System.Text.Json.JsonValueKind.Object)
                            {
                                if (item.TryGetProperty("text", out var itemText))
                                {
                                    var textValue = itemText.GetString();
                                    if (!string.IsNullOrEmpty(textValue))
                                    {
                                        items.Add(textValue);
                                    }
                                }
                                else if (item.TryGetProperty("description", out var itemDesc))
                                {
                                    var descValue = itemDesc.GetString();
                                    if (!string.IsNullOrEmpty(descValue))
                                    {
                                        items.Add(descValue);
                                    }
                                }
                                else
                                {
                                    items.Add(item.ToString());
                                }
                            }
                        }
                        return items.Count > 0 ? string.Join("\n", items) : null;
                    }
                }

                // Look for embedded JSON in text (common in LLM outputs)
                var jsonStart = result.IndexOf('{');
                var jsonEnd = result.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart && jsonEnd - jsonStart > 10)
                {
                    var jsonPortion = result.Substring(jsonStart, jsonEnd - jsonStart + 1);
                    return TryParseJsonOutput(jsonPortion, modelName); // Recursive call
                }

                // Look for markdown-style JSON blocks
                var markdownJsonStart = result.IndexOf("```json");
                if (markdownJsonStart >= 0)
                {
                    var jsonContentStart = markdownJsonStart + 7; // Skip "```json"
                    var markdownJsonEnd = result.IndexOf("```", jsonContentStart);
                    if (markdownJsonEnd > jsonContentStart)
                    {
                        var jsonContent = result.Substring(jsonContentStart, markdownJsonEnd - jsonContentStart).Trim();
                        return TryParseJsonOutput(jsonContent, modelName); // Recursive call
                    }
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                // Try to extract partial JSON content for incomplete structures
                try
                {
                    // Look for incomplete JSON - common with GUI models that may cut off mid-response
                    var partialText = result.Trim();
                    if (partialText.StartsWith("{") && !partialText.EndsWith("}"))
                    {
                        // Try to close the JSON and parse key fields
                        var lastCommaIndex = partialText.LastIndexOf(',');
                        if (lastCommaIndex > 0)
                        {
                            var partialJson = partialText.Substring(0, lastCommaIndex) + "}";
                            using var partialDoc = System.Text.Json.JsonDocument.Parse(partialJson);
                            var partialRoot = partialDoc.RootElement;

                            // Extract any available content from partial JSON
                            if (partialRoot.TryGetProperty("description", out var desc) && !string.IsNullOrEmpty(desc.GetString()))
                            {
                                return $"[Partial Response] {desc.GetString()}";
                            }
                            if (partialRoot.TryGetProperty("main_application", out var app) && !string.IsNullOrEmpty(app.GetString()))
                            {
                                return $"[Partial Response] Application: {app.GetString()}";
                            }
                        }
                    }
                }
                catch
                {
                    // If partial parsing fails, continue with original error handling
                }

                Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [TryParseJsonOutput] JSON parsing failed for {modelName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [TryParseJsonOutput] Unexpected error parsing JSON for {modelName}: {ex.Message}");
            }

            return null; // Not valid JSON or couldn't parse
        }

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

                // Fast result storage - store clean model output for display
                string resultContentType = DetermineResultContentType(correspondingModel, result);
                int currentStep = currentActionStep + 1;

                // Clean the result for display - remove any ensemble formatting prefixes
                string cleanResult = CleanModelResultForDisplay(result, modelNode.Name);
                modelNode.SetStepOutput(currentStep, resultContentType, cleanResult);

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

                        // Clean the result for display - remove any ensemble formatting prefixes
                        string cleanResult = CleanModelResultForDisplay(result, node.Name);
                        node.SetStepOutput(currentStep, resultContentType, cleanResult);
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

            // Debug.WriteLine($"🎨 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Combining {stepContents.Count} images using method: {ensembleMethod}");

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
                    // Debug.WriteLine($"🔀 [{DateTime.Now:HH:mm:ss.fff}] [CombineImageContents] Default format: {defaultResult}");
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

            // Clear ActionSteps from model nodes to prevent old content artifacts before processing
            if (connectedInputNodes != null)
            {
                ClearModelNodeActionSteps(connectedInputNodes);
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
                        var netPageViewModel = GetNetPageViewModel();
                        if (netPageViewModel == null)
                        {
                            Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedImageInput] NetPageViewModel is null, skipping image processing");
                            imageResults.Add($"Image {i + 1}: NetPageViewModel not available");
                            continue;
                        }

                        // Execute model on individual image
                        var individualResult = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, imagePath);
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

            // Clear ActionSteps from model nodes to prevent old content artifacts before processing
            if (connectedInputNodes != null)
            {
                ClearModelNodeActionSteps(connectedInputNodes);
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
                        var netPageViewModel = GetNetPageViewModel();
                        if (netPageViewModel == null)
                        {
                            Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] NetPageViewModel is null, skipping audio processing");
                            audioResults.Add($"Audio {i + 1}: NetPageViewModel not available");
                            continue;
                        }

                        // Execute model on individual audio
                        Debug.WriteLine($"🎬 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Executing model {model.HuggingFaceModelId} with single audio path: {audioPath}");
                        var individualResult = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, audioPath);
                        Debug.WriteLine($"🎬 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Model execution result length: {individualResult?.Length ?? 0}, first 100 chars: {individualResult?.Substring(0, Math.Min(100, individualResult?.Length ?? 0)) ?? "null"}");

                        if (!string.IsNullOrEmpty(individualResult))
                        {
                            // Check for garbage output patterns that indicate model execution issues
                            if (IsGarbageOutput(individualResult))
                            {
                                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Detected garbage output from audio {i + 1}, using fallback message");
                                audioResults.Add($"{nodeContext}: Audio processed (no speech detected or transcription error)");
                                continue;
                            }

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
                    Debug.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [ProcessCombinedAudioInput] Combined result content: {combinedResult}");

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
            // Enhanced fallback - try to infer from file path first (most reliable)
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

            // Try to find the node that corresponds to this file path
            var matchingNode = connectedInputNodes?.FirstOrDefault(node =>
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
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Detected Screen Image from node name");
                    return "Screen Image";
                }
                else if (lowerNodeName.Contains("microphone") || lowerNodeName.Contains("mic"))
                {
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Detected Microphone Audio from node name");
                    return "Microphone Audio";
                }
                else if (lowerNodeName.Contains("file"))
                {
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Detected File from node name");
                    return $"File {mediaType}";
                }
                else if (lowerNodeName.Contains("audio"))
                {
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Detected generic Audio from node name");
                    return $"{nodeName} Audio";
                }
                else if (lowerNodeName.Contains("image") || lowerNodeName.Contains("video") || lowerNodeName.Contains("camera"))
                {
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Detected generic Image from node name");
                    return $"{nodeName} Image";
                }
                else
                {
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Using node name as-is");
                    return $"{nodeName} {mediaType}";
                }
            }

            // Final fallback to generic description
            Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [GetNodeContextDescription] Using fallback: Source {mediaType}");
            return $"Source {mediaType}";
        }

        /// <summary>
        /// Detects if result is properly formatted ensemble output that should be preserved
        /// </summary>
        private bool IsProperlyFormattedEnsembleResult(string result)
        {
            if (string.IsNullOrEmpty(result)) return false;

            // Check for patterns that indicate this is a properly formatted ensemble result
            // These should contain source labels followed by meaningful content
            var lines = result.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            // Should have multiple lines for ensemble results
            if (lines.Length < 2) return false;

            int validFormatLines = 0;
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine)) continue;

                // Check if line follows the pattern "Source Type: meaningful content"
                if ((trimmedLine.StartsWith("PC Audio:") ||
                     trimmedLine.StartsWith("Webcam Audio:") ||
                     trimmedLine.StartsWith("Screen Image:") ||
                     trimmedLine.StartsWith("Webcam Image:") ||
                     trimmedLine.StartsWith("Source ")) &&
                    trimmedLine.Contains(": ") &&
                    trimmedLine.Length > 15) // Must have meaningful content after the colon
                {
                    validFormatLines++;
                }
            }

            // Consider it properly formatted if at least 50% of lines follow the expected pattern
            return validFormatLines >= lines.Length * 0.5 && validFormatLines >= 1;
        }

        /// <summary>
        /// Detects if output looks like garbage/corrupted model output
        /// </summary>
        private bool IsGarbageOutput(string output)
        {
            if (string.IsNullOrEmpty(output)) return false;

            // Check for patterns that indicate garbage output
            var trimmedOutput = output.Trim();

            // Pattern 1: Repeated numbers/dashes like "4-5-5-5-5-5..."
            if (System.Text.RegularExpressions.Regex.IsMatch(trimmedOutput, @"^[\d\-]+$") && trimmedOutput.Length > 20)
            {
                return true;
            }

            // Pattern 2: Very repetitive character patterns
            if (trimmedOutput.Length > 10)
            {
                var firstChar = trimmedOutput[0];
                var sameCharCount = trimmedOutput.TakeWhile(c => c == firstChar || c == '-').Count();
                if (sameCharCount > trimmedOutput.Length * 0.8) // 80% same character
                {
                    return true;
                }
            }

            // Pattern 3: Extremely short output that doesn't make sense for audio transcription
            if (trimmedOutput.Length < 5 && !trimmedOutput.ToLower().Contains("no") && !trimmedOutput.ToLower().Contains("error"))
            {
                return true;
            }

            return false;
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

        /// <summary>
        /// Detects if input contains mixed media (images, audio, text) that needs multimodal processing
        /// </summary>
        private bool IsMultimodalInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            // Count different media types in the input
            bool hasImages = input.Contains(".jpg") || input.Contains(".png") || input.Contains(".jpeg") || input.Contains(".bmp");
            bool hasAudio = input.Contains(".wav") || input.Contains(".mp3") || input.Contains(".m4a") || input.Contains(".flac");
            bool hasText = input.Contains("EventType") || input.Contains("Coordinates") || input.Contains("Mouse") || input.Contains("Keyboard");
            bool hasMultiplePaths = input.Count(c => c == ',') >= 2; // Multiple comma-separated items

            // Consider it multimodal if it has multiple media types or multiple file paths
            return (hasImages && hasAudio) || (hasImages && hasText) || (hasAudio && hasText) ||
                   (hasMultiplePaths && (hasImages || hasAudio));
        }

        /// <summary>
        /// Processes multimodal input for vision-language models like GUI Owl
        /// </summary>
        private async Task<string> ProcessMultimodalInputAsync(NeuralNetworkModel model, string input, List<NodeViewModel> connectedInputNodes)
        {
            if (string.IsNullOrEmpty(input))
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Empty input provided");
                return input;
            }

            Debug.WriteLine($"🔀 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Processing multimodal input for vision-language model: {model.Name}");
            Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Input length: {input.Length}, first 200 chars: {input.Substring(0, Math.Min(200, input.Length))}...");

            try
            {
                // Parse the combined input to separate images, text, and audio
                var imagePaths = new List<string>();
                var textInputs = new List<string>();
                var audioPaths = new List<string>();
                var audioTranscriptions = new List<string>();
                var miscInputs = new List<string>();

                // Split input by commas and categorize each component
                var components = input.Split(',').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();

                foreach (var component in components)
                {
                    if (IsValidImagePath(component))
                    {
                        imagePaths.Add(component);
                        Debug.WriteLine($"🖼️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Found image: {Path.GetFileName(component)}");
                    }
                    else if (IsValidAudioPath(component))
                    {
                        audioPaths.Add(component);
                        Debug.WriteLine($"🎵 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Found audio: {Path.GetFileName(component)}");
                    }
                    else if (component.Contains("EventType") || component.Contains("Coordinates") ||
                             component.Contains("Mouse") || component.Contains("Keyboard"))
                    {
                        // Parse interaction data more clearly
                        var interactionText = component.Replace("Mouse Text (Input):", "").Replace("Keyboard Text (Input):", "").Trim();
                        if (interactionText.Contains("EventType"))
                        {
                            // Extract key information from interaction
                            if (interactionText.Contains("LeftButtonDown"))
                            {
                                textInputs.Add("User Action: Left mouse click detected");
                            }
                            else if (interactionText.Contains("RightButtonDown"))
                            {
                                textInputs.Add("User Action: Right mouse click detected");
                            }
                            else if (interactionText.Contains("Coordinates"))
                            {
                                textInputs.Add("User Action: Mouse interaction detected");
                            }
                        }
                        else
                        {
                            textInputs.Add($"User Interaction: {interactionText}");
                        }
                    }
                    else if (component.Contains("PC Audio:") || component.Contains("Webcam Audio:"))
                    {
                        // Extract audio transcriptions
                        var transcription = component.Substring(component.IndexOf(":") + 1).Trim();
                        if (!string.IsNullOrEmpty(transcription) && transcription.Length > 3)
                        {
                            audioTranscriptions.Add(transcription);
                            Debug.WriteLine($"🎤 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Found audio transcription: {transcription}");
                        }
                    }
                    else if (component.Length > 10 && !component.Contains("\\") && !component.Contains("/"))
                    {
                        // Likely text content
                        textInputs.Add(component);
                    }
                    else
                    {
                        miscInputs.Add(component);
                    }
                }

                Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Parsed input: {imagePaths.Count} images, {audioPaths.Count} audio files, {audioTranscriptions.Count} transcriptions, {textInputs.Count} text, {miscInputs.Count} misc");

                // Select the most relevant image for analysis (prefer screen capture over webcam)
                string primaryImagePath = null;
                if (imagePaths.Count > 0)
                {
                    // Prioritize screen captures for GUI analysis
                    var screenImage = imagePaths.FirstOrDefault(img => img.Contains("ScreenCapture") || img.Contains("Screenshot"));
                    primaryImagePath = screenImage ?? imagePaths[0];
                    Debug.WriteLine($"📸 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Selected primary image: {Path.GetFileName(primaryImagePath)}");
                }

                // Build enhanced structured prompt for GUI analysis
                var promptBuilder = new System.Text.StringBuilder();

                // Add audio context if available
                if (audioTranscriptions.Count > 0)
                {
                    promptBuilder.AppendLine("Audio Context:");
                    foreach (var transcription in audioTranscriptions)
                    {
                        promptBuilder.AppendLine($"- \"{transcription}\"");
                    }
                    promptBuilder.AppendLine();
                }

                // Add interaction context
                if (textInputs.Count > 0)
                {
                    promptBuilder.AppendLine("User Interactions:");
                    foreach (var textInput in textInputs)
                    {
                        promptBuilder.AppendLine($"- {textInput}");
                    }
                    promptBuilder.AppendLine();
                }

                // Add specific GUI analysis instructions
                if (!string.IsNullOrEmpty(primaryImagePath))
                {
                    promptBuilder.AppendLine("Analyze the screen image and provide a structured response about:");
                    promptBuilder.AppendLine("1. MAIN_APPLICATION: What application or window is currently active?");
                    promptBuilder.AppendLine("2. UI_ELEMENTS: What key buttons, menus, or interface elements are visible?");
                    promptBuilder.AppendLine("3. CONTENT_SUMMARY: What text or content is displayed on screen?");
                    promptBuilder.AppendLine("4. USER_FOCUS: Where might the user's attention be directed?");
                    promptBuilder.AppendLine("5. NEXT_ACTIONS: What actions could the user take next?");
                    promptBuilder.AppendLine();
                    promptBuilder.AppendLine("Provide concise, actionable insights about the current GUI state.");
                }
                else
                {
                    promptBuilder.AppendLine("No visual input available. Analyze the audio and interaction context to understand the current user session.");
                }

                var structuredPrompt = promptBuilder.ToString().Trim();

                Debug.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Built structured prompt ({structuredPrompt.Length} chars)");

                // Execute the model with the primary image and structured prompt
                var netPageViewModel = GetNetPageViewModel();
                if (netPageViewModel == null)
                {
                    Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] NetPageViewModel is null, returning original input");
                    return input;
                }

                string result;
                if (!string.IsNullOrEmpty(primaryImagePath))
                {
                    // For vision-language models, use the primary image with structured prompt
                    // The Python script expects: image_path + optional text prompt
                    var combinedInput = $"{primaryImagePath}\n\n{structuredPrompt}";
                    Debug.WriteLine($"🚀 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Executing {model.HuggingFaceModelId} with image: {Path.GetFileName(primaryImagePath)}");
                    result = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, combinedInput);
                }
                else
                {
                    // Text-only fallback (shouldn't happen for GUI Owl but handle gracefully)
                    result = await netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, structuredPrompt);
                }

                if (!string.IsNullOrEmpty(result))
                {
                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Model execution successful, result length: {result.Length}");
                    Debug.WriteLine($"📄 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Result preview: {result.Substring(0, Math.Min(150, result.Length))}...");

                    // Try to parse JSON output first
                    var jsonResult = TryParseJsonOutput(result, model.Name);
                    if (!string.IsNullOrEmpty(jsonResult))
                    {
                        Debug.WriteLine($"📋 [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Successfully parsed JSON output");
                        return $"MULTIMODAL_PROCESSED:{jsonResult}";
                    }

                    return $"MULTIMODAL_PROCESSED:{result}";
                }
                else
                {
                    Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Model returned empty result");
                    return "Vision-language model processing completed but returned no output";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ProcessMultimodalInput] Error processing multimodal input: {ex.Message}");
                return $"Vision-language model encountered generation error: {ex.Message}";
            }
        }

        /// <summary>
        /// Checks if the model is GUI Owl specifically
        /// </summary>
        private bool IsGuiOwlModel(NeuralNetworkModel model)
        {
            if (model == null) return false;

            var modelName = (model.Name ?? "").ToLowerInvariant();
            var modelId = (model.HuggingFaceModelId ?? "").ToLowerInvariant();

            return modelName.Contains("gui owl") || modelName.Contains("gui-owl") ||
                   modelId.Contains("gui-owl") || modelId.Contains("mplug/gui-owl");
        }

        /// <summary>
        /// Checks if the model is a vision-language model that can process multimodal input
        /// </summary>
        private bool IsVisionLanguageModel(NeuralNetworkModel model)
        {
            if (model == null) return false;

            var modelName = (model.Name ?? "").ToLowerInvariant();
            var modelId = (model.HuggingFaceModelId ?? "").ToLowerInvariant();

            // Check for common vision-language model patterns
            return modelName.Contains("gui") || modelName.Contains("visual") || modelName.Contains("vision") ||
                   modelName.Contains("llava") || modelName.Contains("blip") || modelName.Contains("clip") ||
                   modelId.Contains("gui-owl") || modelId.Contains("llava") || modelId.Contains("blip") ||
                   modelId.Contains("visual") || modelId.Contains("vision") ||
                   // Exclude text-only models explicitly
                   !(modelName.Contains("qwen") && !modelName.Contains("vl")) &&
                   !(modelName.Contains("gpt") && !modelName.Contains("vision"));
        }
    }
}
