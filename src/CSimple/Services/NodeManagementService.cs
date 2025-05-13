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
    }
}
