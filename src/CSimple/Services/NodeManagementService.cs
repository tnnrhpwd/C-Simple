using CSimple.ViewModels;
using CSimple.Models;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

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
            var newNode = new NodeViewModel(
                Guid.NewGuid().ToString(),
                modelName,
                modelType,
                position
            )
            {
                Size = new SizeF(180, 60),
                ModelPath = modelId
            };

            nodes.Add(newNode);
            Debug.WriteLine($"Added node: {newNode.Name} at position {position.X},{position.Y}");
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
            var webcamImageNode = new NodeViewModel(Guid.NewGuid().ToString(), "Webcam Image", NodeType.Input, new PointF(startX, startY))
            {
                Size = defaultSize,
                DataType = "image" // Add data type for better classification
            };

            var screenImageNode = new NodeViewModel(Guid.NewGuid().ToString(), "Screen Image", NodeType.Input, new PointF(startX + spacingX, startY))
            {
                Size = defaultSize,
                DataType = "image" // Add data type for better classification
            };

            // Audio inputs (middle row)
            var pcAudioNode = new NodeViewModel(Guid.NewGuid().ToString(), "PC Audio", NodeType.Input, new PointF(startX, startY + spacingY))
            {
                Size = defaultSize,
                DataType = "audio" // Add data type for better classification
            };

            var webcamAudioNode = new NodeViewModel(Guid.NewGuid().ToString(), "Webcam Audio", NodeType.Input, new PointF(startX + spacingX, startY + spacingY))
            {
                Size = defaultSize,
                DataType = "audio" // Add data type for better classification
            };

            // Text inputs (bottom row)
            var keyboardTextNode = new NodeViewModel(Guid.NewGuid().ToString(), "Keyboard Text", NodeType.Input, new PointF(startX, startY + 2 * spacingY))
            {
                Size = defaultSize,
                DataType = "text" // Add data type for better classification
            };

            var mouseTextNode = new NodeViewModel(Guid.NewGuid().ToString(), "Mouse Text", NodeType.Input, new PointF(startX + spacingX, startY + 2 * spacingY))
            {
                Size = defaultSize,
                DataType = "text" // Add data type for better classification
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
        }
    }
}
