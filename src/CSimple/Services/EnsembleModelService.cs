using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        
        // Cache for step content to avoid repeated expensive GetStepContent calls
        private readonly Dictionary<string, string> _stepContentCache = new Dictionary<string, string>();
        
        public EnsembleModelService(NetPageViewModel netPageViewModel)
        {
            _netPageViewModel = netPageViewModel ?? throw new ArgumentNullException(nameof(netPageViewModel));
        }
        
        public void ClearStepContentCache()
        {
            _stepContentCache.Clear();
        }

        /// <summary>
        /// Gets all input nodes connected to the specified model node
        /// </summary>
        public List<NodeViewModel> GetConnectedInputNodes(NodeViewModel modelNode, ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
        {
            Debug.WriteLine($"🔍 [GetConnectedInputNodes] Finding inputs for model node: {modelNode.Name}");

            var connectedNodes = new List<NodeViewModel>();

            // Build node cache if empty or if new nodes were added
            if (_nodeCache.Count != nodes.Count)
            {
                _nodeCache.Clear();
                foreach (var node in nodes)
                {
                    _nodeCache[node.Id] = node;
                }
            }

            // Find all connections that target this model node
            var incomingConnections = connections.Where(c => c.TargetNodeId == modelNode.Id).ToList();
            Debug.WriteLine($"🔗 [GetConnectedInputNodes] Found {incomingConnections.Count} incoming connections");

            foreach (var connection in incomingConnections)
            {
                if (_nodeCache.TryGetValue(connection.SourceNodeId, out var sourceNode))
                {
                    Debug.WriteLine($"🔗 [GetConnectedInputNodes] Connected node: {sourceNode.Name} (Type: {sourceNode.Type}, DataType: {sourceNode.DataType})");
                    connectedNodes.Add(sourceNode);
                }
                else
                {
                    Debug.WriteLine($"⚠️ [GetConnectedInputNodes] Warning: Source node with ID {connection.SourceNodeId} not found");
                }
            }

            return connectedNodes;
        }

        /// <summary>
        /// Combines multiple step contents using the specified ensemble method
        /// </summary>
        public string CombineStepContents(List<string> stepContents, string ensembleMethod)
        {
            Debug.WriteLine($"🔀 [CombineStepContents] Combining {stepContents.Count} contents using method: {ensembleMethod}");

            if (stepContents == null || stepContents.Count == 0)
            {
                Debug.WriteLine("❌ [CombineStepContents] No content to combine");
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
                Debug.WriteLine("🖼️ [CombineStepContents] Detected image content, using first image for model input");
                return firstContent;
            }

            if (isAudioContent)
            {
                Debug.WriteLine("🔊 [CombineStepContents] Detected audio content, using first audio file for model input");
                return firstContent;
            }

            // Fast path for single content
            if (stepContents.Count == 1)
            {
                return firstContent;
            }

            switch (ensembleMethod?.ToLowerInvariant())
            {
                case "concatenation":
                case "concat":
                case null:
                default:
                    Debug.WriteLine("🔗 [CombineStepContents] Using concatenation method");
                    return string.Join("\n\n", stepContents);

                case "average":
                case "averaging":
                    Debug.WriteLine("📊 [CombineStepContents] Using averaging method (fallback to concatenation for text)");
                    return $"[Ensemble Average of {stepContents.Count} inputs]:\n\n" + string.Join("\n\n", stepContents);

                case "voting":
                case "majority":
                    Debug.WriteLine("🗳️ [CombineStepContents] Using voting method (fallback to concatenation for text)");
                    return $"[Ensemble Voting of {stepContents.Count} inputs]:\n\n" + string.Join("\n\n", stepContents);

                case "weighted":
                    Debug.WriteLine("⚖️ [CombineStepContents] Using weighted method (fallback to concatenation for text)");
                    return $"[Ensemble Weighted of {stepContents.Count} inputs]:\n\n" + string.Join("\n\n", stepContents);
            }
        }

        /// <summary>
        /// Executes a model with the provided input using NetPageViewModel (optimized)
        /// </summary>
        public async Task<string> ExecuteModelWithInput(NeuralNetworkModel model, string input)
        {
            try
            {
                // Fast validation
                if (string.IsNullOrEmpty(model.HuggingFaceModelId))
                {
                    throw new InvalidOperationException("Model does not have a valid HuggingFace model ID");
                }

                var result = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, input);
                return result ?? "No output generated";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ExecuteModelWithInput] Model execution failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Determines the content type of model execution result
        /// </summary>
        public string DetermineResultContentType(NeuralNetworkModel model, string result)
        {
            Console.WriteLine($"🔍 [DetermineResultContentType] Analyzing model: {model?.Name}, HF ID: {model?.HuggingFaceModelId}");
            Debug.WriteLine($"🔍 [DetermineResultContentType] Analyzing model: {model?.Name}, HF ID: {model?.HuggingFaceModelId}");

            // Check if this is an image-to-text model based on the HuggingFace model ID or name
            if (model?.HuggingFaceModelId != null)
            {
                string modelId = model.HuggingFaceModelId.ToLowerInvariant();
                if (modelId.Contains("blip") && modelId.Contains("captioning") ||
                    modelId.Contains("image-to-text") ||
                    modelId.Contains("vit-gpt2") ||
                    modelId.Contains("clip-interrogator"))
                {
                    Console.WriteLine($"🖼️➡️📝 [DetermineResultContentType] Detected image-to-text model, output type: text");
                    Debug.WriteLine($"🖼️➡️📝 [DetermineResultContentType] Detected image-to-text model, output type: text");
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
                Console.WriteLine($"🎨 [DetermineResultContentType] Result looks like image file path, output type: image");
                Debug.WriteLine($"🎨 [DetermineResultContentType] Result looks like image file path, output type: image");
                return "image";
            }

            // Check if the result looks like audio file path
            if (!string.IsNullOrEmpty(result) &&
                (result.Contains(@"\") || result.Contains("/")) &&
                (result.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                 result.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                 result.EndsWith(".aac", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"🔊 [DetermineResultContentType] Result looks like audio file path, output type: audio");
                Debug.WriteLine($"🔊 [DetermineResultContentType] Result looks like audio file path, output type: audio");
                return "audio";
            }

            // Default to text for any other output
            Console.WriteLine($"📝 [DetermineResultContentType] Defaulting to text output type");
            Debug.WriteLine($"📝 [DetermineResultContentType] Defaulting to text output type");
            return "text";
        }

        /// <summary>
        /// Prepares input for model execution from connected nodes
        /// </summary>
        public string PrepareModelInput(NodeViewModel modelNode, List<NodeViewModel> connectedInputNodes, int currentActionStep)
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
                    string cacheKey = $"{inputNode.Id}_{stepForNodeContent}";
                    if (!_stepContentCache.TryGetValue(cacheKey, out string cachedContent))
                    {
                        var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);
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
                
                string cacheKey = $"{inputNode.Id}_{stepForNodeContent}";
                if (!_stepContentCache.TryGetValue(cacheKey, out string cachedContent))
                {
                    var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);
                    cachedContent = contentValue ?? "";
                    _stepContentCache[cacheKey] = cachedContent;
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
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                // Fast input preparation
                var connectedInputNodes = GetConnectedInputNodes(modelNode, nodes, connections);
                string input = PrepareModelInput(modelNode, connectedInputNodes, currentActionStep);

                // Execute model without excessive logging
                string result = await ExecuteModelWithInput(correspondingModel, input);

                // Fast result storage
                string resultContentType = DetermineResultContentType(correspondingModel, result);
                int currentStep = currentActionStep + 1;
                modelNode.SetStepOutput(currentStep, resultContentType, result);

                totalStopwatch.Stop();
                Debug.WriteLine($"💾 [ExecuteSingleModelNodeAsync] '{modelNode.Name}' completed in {totalStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Debug.WriteLine($"❌ [ExecuteSingleModelNodeAsync] Error executing model {modelNode.Name} after {totalStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                throw; // Re-throw to be handled by the caller
            }
        }
    }
}
