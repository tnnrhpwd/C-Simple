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

        public EnsembleModelService(NetPageViewModel netPageViewModel)
        {
            _netPageViewModel = netPageViewModel ?? throw new ArgumentNullException(nameof(netPageViewModel));
        }

        /// <summary>
        /// Gets all input nodes connected to the specified model node
        /// </summary>
        public List<NodeViewModel> GetConnectedInputNodes(NodeViewModel modelNode, ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
        {
            Console.WriteLine($"🔍 [GetConnectedInputNodes] Finding inputs for model node: {modelNode.Name}");
            Debug.WriteLine($"🔍 [GetConnectedInputNodes] Finding inputs for model node: {modelNode.Name}");

            var connectedNodes = new List<NodeViewModel>();

            // Find all connections that target this model node
            var incomingConnections = connections.Where(c => c.TargetNodeId == modelNode.Id).ToList();
            Console.WriteLine($"🔗 [GetConnectedInputNodes] Found {incomingConnections.Count} incoming connections");
            Debug.WriteLine($"🔗 [GetConnectedInputNodes] Found {incomingConnections.Count} incoming connections");

            foreach (var connection in incomingConnections)
            {
                var sourceNode = nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                if (sourceNode != null)
                {
                    Console.WriteLine($"🔗 [GetConnectedInputNodes] Connected node: {sourceNode.Name} (Type: {sourceNode.Type}, DataType: {sourceNode.DataType})");
                    Debug.WriteLine($"🔗 [GetConnectedInputNodes] Connected node: {sourceNode.Name} (Type: {sourceNode.Type}, DataType: {sourceNode.DataType})");
                    connectedNodes.Add(sourceNode);
                }
                else
                {
                    Console.WriteLine($"⚠️ [GetConnectedInputNodes] Warning: Source node with ID {connection.SourceNodeId} not found");
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
            Console.WriteLine($"🔀 [CombineStepContents] Combining {stepContents.Count} contents using method: {ensembleMethod}");
            Debug.WriteLine($"🔀 [CombineStepContents] Combining {stepContents.Count} contents using method: {ensembleMethod}");

            if (stepContents == null || stepContents.Count == 0)
            {
                Console.WriteLine("❌ [CombineStepContents] No content to combine");
                Debug.WriteLine("❌ [CombineStepContents] No content to combine");
                return string.Empty;
            }

            // Check if we're dealing with image file paths (simple heuristic: check if first item looks like a file path)
            bool isImageContent = stepContents.Count > 0 &&
                                  (stepContents[0].Contains(@"\") || stepContents[0].Contains("/")) &&
                                  (stepContents[0].EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                   stepContents[0].EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                   stepContents[0].EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));

            // Check if we're dealing with audio file paths
            bool isAudioContent = stepContents.Count > 0 &&
                                  (stepContents[0].Contains(@"\") || stepContents[0].Contains("/")) &&
                                  (stepContents[0].EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                                   stepContents[0].EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                   stepContents[0].EndsWith(".aac", StringComparison.OrdinalIgnoreCase) ||
                                   stepContents[0].EndsWith(".flac", StringComparison.OrdinalIgnoreCase));

            if (isImageContent)
            {
                Console.WriteLine("🖼️ [CombineStepContents] Detected image content, using first image for model input");
                Debug.WriteLine("🖼️ [CombineStepContents] Detected image content, using first image for model input");
                // For image models, use the first image path (most image models process one image at a time)
                // In the future, this could be enhanced to support multi-image ensemble methods
                return stepContents[0];
            }

            if (isAudioContent)
            {
                Console.WriteLine("🔊 [CombineStepContents] Detected audio content, using first audio file for model input");
                Debug.WriteLine("🔊 [CombineStepContents] Detected audio content, using first audio file for model input");
                // For audio models, use the first audio file path (most audio models process one file at a time)
                // In the future, this could be enhanced to support multi-audio ensemble methods
                return stepContents[0];
            }

            switch (ensembleMethod?.ToLowerInvariant())
            {
                case "concatenation":
                case "concat":
                case null:
                default:
                    Console.WriteLine("🔗 [CombineStepContents] Using concatenation method");
                    Debug.WriteLine("🔗 [CombineStepContents] Using concatenation method");
                    return string.Join("\n\n", stepContents);

                case "average":
                case "averaging":
                    Console.WriteLine("📊 [CombineStepContents] Using averaging method (fallback to concatenation for text)");
                    Debug.WriteLine("📊 [CombineStepContents] Using averaging method (fallback to concatenation for text)");
                    // For text content, averaging doesn't make much sense, so we concatenate with averaging context
                    return $"[Ensemble Average of {stepContents.Count} inputs]:\n\n" + string.Join("\n\n", stepContents);

                case "voting":
                case "majority":
                    Console.WriteLine("🗳️ [CombineStepContents] Using voting method (fallback to concatenation for text)");
                    Debug.WriteLine("🗳️ [CombineStepContents] Using voting method (fallback to concatenation for text)");
                    // For text content, voting doesn't make much sense, so we concatenate with voting context
                    return $"[Ensemble Voting of {stepContents.Count} inputs]:\n\n" + string.Join("\n\n", stepContents);

                case "weighted":
                    Console.WriteLine("⚖️ [CombineStepContents] Using weighted method (fallback to concatenation for text)");
                    Debug.WriteLine("⚖️ [CombineStepContents] Using weighted method (fallback to concatenation for text)");
                    // For text content, weighting doesn't make much sense, so we concatenate with weighting context
                    return $"[Ensemble Weighted of {stepContents.Count} inputs]:\n\n" + string.Join("\n\n", stepContents);
            }
        }

        /// <summary>
        /// Executes a model with the provided input using NetPageViewModel
        /// </summary>
        public async Task<string> ExecuteModelWithInput(NeuralNetworkModel model, string input)
        {
            Console.WriteLine($"🤖 [ExecuteModelWithInput] Executing model: {model.Name} with input length: {input?.Length ?? 0}");
            Debug.WriteLine($"🤖 [ExecuteModelWithInput] Executing model: {model.Name} with input length: {input?.Length ?? 0}");

            try
            {
                // Use the NetPageViewModel's public ExecuteModelAsync method
                if (string.IsNullOrEmpty(model.HuggingFaceModelId))
                {
                    Console.WriteLine("❌ [ExecuteModelWithInput] Model has no HuggingFace ID");
                    Debug.WriteLine("❌ [ExecuteModelWithInput] Model has no HuggingFace ID");
                    throw new InvalidOperationException("Model does not have a valid HuggingFace model ID");
                }

                var result = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, input);

                Console.WriteLine($"✅ [ExecuteModelWithInput] Model execution successful, result length: {result?.Length ?? 0}");
                Debug.WriteLine($"✅ [ExecuteModelWithInput] Model execution successful, result length: {result?.Length ?? 0}");

                return result ?? "No output generated";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ExecuteModelWithInput] Model execution failed: {ex.Message}");
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
                    var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);
                    if (!string.IsNullOrEmpty(contentValue))
                    {
                        stepContents.Add(contentType?.ToLowerInvariant() == "image" || contentType?.ToLowerInvariant() == "audio"
                                       ? contentValue
                                       : $"[{inputNode.Name}]: {contentValue}");
                    }
                }
                return CombineStepContents(stepContents, modelNode.SelectedEnsembleMethod);
            }
            else
            {
                // Use single input
                var inputNode = connectedInputNodes.First();
                int stepForNodeContent = currentActionStep + 1;
                var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);
                return contentValue ?? "";
            }
        }

        /// <summary>
        /// Executes a single model node with input preparation and output storage
        /// </summary>
        public async Task ExecuteSingleModelNodeAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            try
            {
                Console.WriteLine($"🔧 [ExecuteSingleModelNodeAsync] Executing single model: {modelNode.Name}");
                Debug.WriteLine($"🔧 [ExecuteSingleModelNodeAsync] Executing single model: {modelNode.Name}");

                // Get input from connected nodes or use default/empty input
                var connectedInputNodes = GetConnectedInputNodes(modelNode, nodes, connections);
                string input = PrepareModelInput(modelNode, connectedInputNodes, currentActionStep);

                // Execute the model
                string result = await ExecuteModelWithInput(correspondingModel, input);

                // Determine result content type
                string resultContentType = DetermineResultContentType(correspondingModel, result);

                // Store the generated output in the model node
                int currentStep = currentActionStep + 1;
                modelNode.SetStepOutput(currentStep, resultContentType, result);

                Console.WriteLine($"💾 [ExecuteSingleModelNodeAsync] Stored output in model node '{modelNode.Name}' at step {currentStep}");
                Debug.WriteLine($"💾 [ExecuteSingleModelNodeAsync] Stored output in model node '{modelNode.Name}' at step {currentStep}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [ExecuteSingleModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
                Debug.WriteLine($"❌ [ExecuteSingleModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
                throw; // Re-throw to be handled by the caller
            }
        }
    }
}
