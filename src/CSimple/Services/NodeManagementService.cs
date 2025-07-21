using CSimple.ViewModels;
using CSimple.Models;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;

namespace CSimple.Services
{
    public class NodeManagementService
    {
        private readonly FileService _fileService;

        public NodeManagementService(FileService fileService)
        {
            _fileService = fileService;
        }

        public async Task AddModelNodeAsync(ObservableCollection<NodeViewModel> nodes, string modelId, string modelName, NodeType modelType, PointF position)
        {
            // Determine if this is a file node to pass appropriate saveFilePath
            string saveFilePath = modelType == NodeType.File ? null : null; // File nodes start with no file selected

            var newNode = new NodeViewModel(
                Guid.NewGuid().ToString(),
                modelName,
                modelType,
                position,
                DetermineDataTypeFromName(modelName), // Set DataType based on name
                modelId, // originalModelId
                modelId, // modelPath
                null, // classification
                null, // originalName
                saveFilePath // saveFilePath for file nodes
            )
            {
                Size = new SizeF(180, 60)
            };

            nodes.Add(newNode);
            Debug.WriteLine($"Added node: {newNode.Name} (Type: {modelType}) at position {position.X},{position.Y}");
            // await SaveCurrentPipelineAsync(); // Ensure this is called from the ViewModel
        }

        public async Task DeleteSelectedNodeAsync(ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, NodeViewModel selectedNode, Action InvalidateCanvas)
        {
            if (selectedNode != null)
            {
                // Remove connections associated with the node
                var connectionsToRemove = connections
                    .Where(c => c.SourceNodeId == selectedNode.Id || c.TargetNodeId == selectedNode.Id)
                    .ToList();
                foreach (var conn in connectionsToRemove)
                {
                    connections.Remove(conn); // Remove connection
                }

                // Remove the node itself
                nodes.Remove(selectedNode);
                Debug.WriteLine($"Deleted node: {selectedNode.Name}");
                // SelectedNode = null; // Ensure this is handled in the ViewModel
                // UpdateEnsembleCounts(); // Ensure this is called from the ViewModel
                // await SaveCurrentPipelineAsync(); // Ensure this is called from the ViewModel
                InvalidateCanvas?.Invoke(); // Ensure redraw after potential count update
            }
            else
            {
                Debug.WriteLine("Info: No node selected to delete.");
            }
        }

        public void UpdateNodePosition(NodeViewModel node, PointF newPosition)
        {
            if (node != null)
            {
                node.Position = newPosition;
            }
        }

        public void CompleteConnection(ObservableCollection<ConnectionViewModel> connections, NodeViewModel sourceNode, NodeViewModel targetNode, Action InvalidateCanvas)
        {
            if (sourceNode != null && targetNode != null && sourceNode.Id != targetNode.Id)
            {
                // Check if connection already exists
                bool exists = connections.Any(c =>
                    (c.SourceNodeId == sourceNode.Id && c.TargetNodeId == targetNode.Id) ||
                    (c.SourceNodeId == targetNode.Id && c.TargetNodeId == sourceNode.Id));

                if (!exists)
                {
                    // Use the ConnectionViewModel constructor
                    var newConnection = new ConnectionViewModel(
                        Guid.NewGuid().ToString(), // Generate string ID
                        sourceNode.Id,
                        targetNode.Id
                    );
                    connections.Add(newConnection);
                    Debug.WriteLine($"Completed connection from {sourceNode.Name} to {targetNode.Name}");
                    // UpdateEnsembleCounts(); // Ensure this is called from the ViewModel
                    // await SaveCurrentPipelineAsync(); // Ensure this is called from the ViewModel
                }
                else
                {
                    Debug.WriteLine("Connection already exists.");
                }
            }
            else
            {
                Debug.WriteLine($"Failed to complete connection. StartNode: {sourceNode?.Name}, TargetNode: {targetNode?.Name}");
            }
            InvalidateCanvas?.Invoke(); // ADDED: Ensure redraw after potential count update
        }

        public NodeViewModel GetNodeAtPoint(ObservableCollection<NodeViewModel> nodes, PointF point)
        {
            // Check nodes in reverse order so top-most node is selected
            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                var node = nodes[i];
                var nodeRect = new RectF(node.Position, node.Size);
                if (nodeRect.Contains(point))
                {
                    return node;
                }
            }
            return null;
        }

        public NodeType InferNodeTypeFromName(string name)
        {
            string lowerName = name.ToLower();

            // Input node detection with more specific categories
            if (lowerName.Contains("webcam") || lowerName.Contains("screen") ||
                lowerName.Contains("keyboard") || lowerName.Contains("mouse") ||
                lowerName.Contains("audio") || lowerName.Contains("input"))
                return NodeType.Input;

            if (lowerName.Contains("output") || lowerName.Contains("display") || lowerName.Contains("speaker"))
                return NodeType.Output;

            // Memory node detection - now classified as File type
            if (lowerName.Contains("memory") || lowerName.Contains("file"))
                return NodeType.File;

            return NodeType.Model; // Default to Model
        }

        // Helper to determine a more friendly model name
        public string GetFriendlyModelName(string modelId)
        {
            // Similar to NetPageViewModel implementation
            var name = modelId.Contains('/') ? modelId.Split('/').Last() : modelId;
            name = name.Replace("-", " ").Replace("_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
        }

        // Helper to infer data type from node name as a fallback
        public string DetermineDataTypeFromName(string nodeName)
        {
            string lowerName = nodeName.ToLowerInvariant();

            // Memory nodes (typically handle mixed data types)
            if (lowerName.Contains("memory"))
                return "text"; // Default to text for memory nodes, can be changed via UI

            // Text models
            if (lowerName.Contains("text") ||
                lowerName.Contains("gpt") ||
                lowerName.Contains("llama") ||
                lowerName.Contains("llm") ||
                lowerName.Contains("bert") ||
                lowerName.Contains("token") ||
                lowerName.Contains("deepseek") ||
                lowerName.Contains("mistral") ||
                lowerName.Contains("chat"))
                return "text";

            // Image models
            if (lowerName.Contains("image") ||
                lowerName.Contains("vision") ||
                lowerName.Contains("yolo") ||
                lowerName.Contains("resnet") ||
                lowerName.Contains("clip") ||
                lowerName.Contains("diffusion") ||
                lowerName.Contains("stable") ||
                lowerName.Contains("gan"))
                return "image";

            // Audio models
            if (lowerName.Contains("audio") ||
                lowerName.Contains("speech") ||
                lowerName.Contains("whisper") ||
                lowerName.Contains("wav2vec") ||
                lowerName.Contains("sound") ||
                lowerName.Contains("voice"))
                return "audio";

            return "unknown";
        }

        // New helper method for finding corresponding model with better matching logic
        public NeuralNetworkModel FindCorrespondingModel(ObservableCollection<NeuralNetworkModel> availableModels, NodeViewModel node)
        {
            // First try exact match by ID (most precise)
            var exactIdMatch = availableModels.FirstOrDefault(m =>
                (!string.IsNullOrEmpty(m.Id) && m.Id == node.ModelPath) ||
                (!string.IsNullOrEmpty(m.HuggingFaceModelId) && m.HuggingFaceModelId == node.ModelPath));

            if (exactIdMatch != null)
            {
                Debug.WriteLine($"Found exact ID match for node {node.Name}");
                return exactIdMatch;
            }

            // Try matching by name (second best)
            var nameMatch = availableModels.FirstOrDefault(m =>
                string.Equals(m.Name, node.Name, StringComparison.OrdinalIgnoreCase));

            if (nameMatch != null)
            {
                Debug.WriteLine($"Found name match for node {node.Name}");
                return nameMatch;
            }

            // Try fuzzy name matching (as a last resort)
            string nodeName = node.Name.ToLowerInvariant();
            var fuzzyMatch = availableModels.FirstOrDefault(m =>
                (m.Name != null && m.Name.ToLowerInvariant().Contains(nodeName)) ||
                (nodeName.Length > 5 && m.Name != null && nodeName.Contains(m.Name.ToLowerInvariant())));

            if (fuzzyMatch != null)
            {
                Debug.WriteLine($"Found fuzzy name match for node {node.Name} -> {fuzzyMatch.Name}");
            }

            return fuzzyMatch; // May be null if no match found
        }

        public void AddDefaultInputNodes(ObservableCollection<NodeViewModel> Nodes, string CurrentPipelineName)
        {
            // Create more organized default layout
            float startX = 50;
            float startY = 50;
            float spacingY = 80; // Vertical spacing between nodes
            float spacingX = 170; // Horizontal spacing between nodes (Node Width + Gap)
            SizeF defaultSize = new SizeF(150, 50); // Default size for input nodes

            // Group by type: Visual inputs, Audio inputs, Text inputs

            // Visual inputs (top row)
            var webcamImageNode = new NodeViewModel(Guid.NewGuid().ToString(), "Webcam Image", NodeType.Input, new PointF(startX, startY), DetermineDataTypeFromName("Webcam Image"))
            {
                Size = defaultSize,
            };

            var screenImageNode = new NodeViewModel(Guid.NewGuid().ToString(), "Screen Image", NodeType.Input, new PointF(startX + spacingX, startY), DetermineDataTypeFromName("Screen Image"))
            {
                Size = defaultSize,
            };

            // Audio inputs (middle row)
            var pcAudioNode = new NodeViewModel(Guid.NewGuid().ToString(), "PC Audio", NodeType.Input, new PointF(startX, startY + spacingY), DetermineDataTypeFromName("PC Audio"))
            {
                Size = defaultSize,
            };

            var webcamAudioNode = new NodeViewModel(Guid.NewGuid().ToString(), "Webcam Audio", NodeType.Input, new PointF(startX + spacingX, startY + spacingY), DetermineDataTypeFromName("Webcam Audio"))
            {
                Size = defaultSize,
            };

            // Text inputs (bottom row)
            var keyboardTextNode = new NodeViewModel(Guid.NewGuid().ToString(), "Keyboard Text", NodeType.Input, new PointF(startX, startY + 2 * spacingY), DetermineDataTypeFromName("Keyboard Text"))
            {
                Size = defaultSize,
            };

            var mouseTextNode = new NodeViewModel(Guid.NewGuid().ToString(), "Mouse Text", NodeType.Input, new PointF(startX + spacingX, startY + 2 * spacingY), DetermineDataTypeFromName("Mouse Text"))
            {
                Size = defaultSize,
            };

            // Add all nodes to the collection
            Nodes.Add(webcamImageNode);
            Nodes.Add(screenImageNode);
            Nodes.Add(pcAudioNode);
            Nodes.Add(webcamAudioNode);
            Nodes.Add(keyboardTextNode);
            Nodes.Add(mouseTextNode);

            Debug.WriteLine($"Added specialized input nodes to '{CurrentPipelineName}'.");
        }

        public void LoadPipelineData(ObservableCollection<NodeViewModel> Nodes, ObservableCollection<ConnectionViewModel> Connections, PipelineData data, Action InvalidateCanvas)
        {
            if (data == null) return;

            Nodes.Clear();
            Connections.Clear();

            if (data.Nodes != null)
            {
                foreach (var nodeData in data.Nodes)
                {
                    Nodes.Add(nodeData.ToViewModel());
                }
            }

            if (data.Connections != null)
            {
                foreach (var connData in data.Connections)
                {
                    // Ensure source and target nodes exist before adding connection
                    if (Nodes.Any(n => n.Id == connData.SourceNodeId.ToString()) &&
                        Nodes.Any(n => n.Id == connData.TargetNodeId.ToString()))
                    {
                        Connections.Add(connData.ToViewModel());
                    }
                    else
                    {
                        Debug.WriteLine($"Warning: Skipping connection {connData.Id} due to missing source/target node during load.");
                    }
                }
            }

            InvalidateCanvas?.Invoke(); // Redraw canvas

            // Update ensemble counts after loading pipeline data
            UpdateEnsembleCounts(Nodes, Connections, InvalidateCanvas);
        }

        public async Task UpdateNodeClassificationsAsync(ObservableCollection<NodeViewModel> Nodes, ObservableCollection<NeuralNetworkModel> AvailableModels, Action InvalidateCanvas, Func<string, string> DetermineDataTypeFromName, Func<Task> SaveCurrentPipelineAsync)
        {
            bool pipelineChanged = false;

            if (AvailableModels == null)
            {
                Debug.WriteLine("UpdateNodeClassificationsAsync: NetPageViewModel or AvailableModels is null. Cannot update.");
                return;
            }

            Debug.WriteLine($"UpdateNodeClassificationsAsync: Found {AvailableModels.Count} models in NetPageViewModel.");

            // Iterate through the nodes in the current pipeline
            foreach (var node in Nodes)
            {
                // Only update nodes that represent models (not Input/Output nodes)
                if (node.Type == NodeType.Model)
                {
                    // Improved model matching logic with better debugging
                    var correspondingNetModel = FindCorrespondingModel(AvailableModels, node);

                    if (correspondingNetModel != null)
                    {
                        Debug.WriteLine($"Found corresponding NetModel '{correspondingNetModel.Name}' for Node '{node.Name}' (ModelPath: {node.ModelPath})");

                        // Get InputType directly from the model - this is the key part we want to ensure is working
                        var inputType = correspondingNetModel.InputType;
                        Debug.WriteLine($"Model '{correspondingNetModel.Name}' has InputType: {inputType}");

                        // Convert ModelInputType enum to string for DataType
                        string newDataType = inputType switch
                        {
                            ModelInputType.Text => "text",
                            ModelInputType.Image => "image",
                            ModelInputType.Audio => "audio",
                            _ => "unknown" // Default or Unknown
                        };

                        Debug.WriteLine($"Converted InputType {inputType} to DataType '{newDataType}'");

                        // Check if the DataType needs updating
                        if (node.DataType != newDataType)
                        {
                            Debug.WriteLine($"Updating DataType for Node '{node.Name}' from '{node.DataType}' to '{newDataType}' based on NetModel InputType '{correspondingNetModel.InputType}'.");
                            node.DataType = newDataType;
                            pipelineChanged = true; // Mark that a change occurred
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find corresponding NetModel for Node '{node.Name}' with ModelPath '{node.ModelPath}'.");
                        // Try to determine data type based on node name as fallback
                        string inferredDataType = DetermineDataTypeFromName(node.Name);
                        if (inferredDataType != "unknown" && node.DataType != inferredDataType)
                        {
                            Debug.WriteLine($"Using inferred data type '{inferredDataType}' for node '{node.Name}' based on name");
                            node.DataType = inferredDataType;
                            pipelineChanged = true;
                        }
                    }
                }
            }

            // Save the pipeline only if any node's DataType was actually changed
            if (pipelineChanged)
            {
                Debug.WriteLine("UpdateNodeClassificationsAsync: Pipeline data changed, saving...");
                await SaveCurrentPipelineAsync();

                // Force redraw of the canvas to reflect potential color changes
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    InvalidateCanvas?.Invoke();
                    Debug.WriteLine("Requested canvas invalidation via InvalidateCanvas action.");
                });
            }
            else
            {
                Debug.WriteLine("UpdateNodeClassificationsAsync: No pipeline data changed, skipping save.");
            }
        }

        public void SetNodeClassification(NodeViewModel node, string classification, Action InvalidateCanvas)
        {
            if (node != null && node.IsTextModel)
            {
                // This will automatically update the node's display name via the property setter
                node.Classification = classification;

                // Request redraw to show the updated name
                InvalidateCanvas?.Invoke();

                Debug.WriteLine($"Set node '{node.OriginalName}' classification to '{classification}'");
            }
        }

        public void UpdateEnsembleCounts(ObservableCollection<NodeViewModel> Nodes, ObservableCollection<ConnectionViewModel> Connections, Action InvalidateCanvas)
        {
            Debug.WriteLine("Updating ensemble counts...");
            bool countsChanged = false;
            var inputCounts = Connections
                .GroupBy(c => c.TargetNodeId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var node in Nodes)
            {
                int newCount = inputCounts.TryGetValue(node.Id, out var count) ? count : 0;
                if (node.EnsembleInputCount != newCount)
                {
                    node.EnsembleInputCount = newCount;
                    countsChanged = true;
                    Debug.WriteLine($"Node '{node.Name}' input count set to {newCount}");
                }
            }

            if (countsChanged)
            {
                Debug.WriteLine("Ensemble counts changed, invalidating canvas.");
                InvalidateCanvas?.Invoke(); // Trigger redraw if any count changed
            }
            else
            {
                Debug.WriteLine("No ensemble counts changed.");
            }
        }
    }
}
