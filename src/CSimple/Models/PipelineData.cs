using CSimple.Models; // Assuming NodeViewModel, ConnectionViewModel, NodeType are here or accessible
using CSimple.ViewModels; // Add this using directive
using Microsoft.Maui.Graphics; // For PointF, SizeF
using System;
using System.Collections.Generic;

namespace CSimple.Models
{
    /// <summary>
    /// Represents the data structure for saving and loading a pipeline state.
    /// </summary>
    public class PipelineData
    {
        public string Name { get; set; }
        public List<SerializableNode> Nodes { get; set; } = new List<SerializableNode>();
        public List<SerializableConnection> Connections { get; set; } = new List<SerializableConnection>();
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// A simplified version of NodeViewModel suitable for JSON serialization.
    /// </summary>
    public class SerializableNode
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public NodeType Type { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float SizeWidth { get; set; }
        public float SizeHeight { get; set; }
        public string ModelPath { get; set; } // Include relevant properties
        public string DataType { get; set; } // Add this if not already present
        public string Classification { get; set; } // ADDED: Store classification
        public string GoalText { get; set; } // ADDED: Store goal text
        public string PlanText { get; set; } // ADDED: Store plan text  
        public string ActionText { get; set; } // ADDED: Store action text
        public string OriginalName { get; set; } // ADDED: Store original name without classification
        public string SaveFilePath { get; set; } // ADDED: Store save file path for file nodes
        public List<(string Type, string Value)> ActionSteps { get; set; } = new List<(string, string)>(); // ADDED: Store generated outputs

        // Parameterless constructor for deserialization
        public SerializableNode() { }

        // Constructor to convert from NodeViewModel
        public SerializableNode(NodeViewModel vm)
        {
            Id = Guid.Parse(vm.Id); // Assume vm.Id is a valid Guid string
            Name = vm.Name;
            Type = vm.Type;
            PositionX = vm.Position.X;
            PositionY = vm.Position.Y;
            SizeWidth = vm.Size.Width;
            SizeHeight = vm.Size.Height;
            ModelPath = vm.ModelPath; // Assign ModelPath
            DataType = vm.DataType;
            Classification = vm.Classification;
            GoalText = vm.GoalText; // Assign goal text
            PlanText = vm.PlanText; // Assign plan text
            ActionText = vm.ActionText; // Assign action text
            OriginalName = vm.OriginalName; // Assign OriginalName
            SaveFilePath = vm.SaveFilePath; // Assign SaveFilePath for file nodes
            ActionSteps = vm.ActionSteps?.ToList() ?? new List<(string, string)>(); // Copy ActionSteps
        }

        // Method to convert back to NodeViewModel
        public NodeViewModel ToViewModel()
        {
            // Call the constructor with required arguments, including new ones
            var vm = new NodeViewModel(
                this.Id.ToString(), // Convert Guid back to string
                this.Name,
                this.Type,
                new PointF(this.PositionX, this.PositionY),
                this.DataType, // Pass DataType
                null, // OriginalModelId - assuming not stored here, pass null or retrieve if needed
                this.ModelPath, // Pass ModelPath
                this.Classification, // Pass Classification
                this.OriginalName, // Pass OriginalName
                this.SaveFilePath, // Pass SaveFilePath for file nodes
                this.GoalText, // Pass GoalText
                this.PlanText, // Pass PlanText
                this.ActionText // Pass ActionText
            )
            {
                // Set properties not handled by constructor (Size is handled by constructor default)
                Size = new SizeF(this.SizeWidth, this.SizeHeight),
                // ModelPath, DataType, Classification, OriginalName, SaveFilePath are now handled by constructor
                ActionSteps = this.ActionSteps?.ToList() ?? new List<(string, string)>() // Restore ActionSteps
            };
            return vm;
        }
    }

    /// <summary>
    /// A simplified version of ConnectionViewModel suitable for JSON serialization.
    /// </summary>
    public class SerializableConnection
    {
        public Guid Id { get; set; }
        public Guid SourceNodeId { get; set; }
        public Guid TargetNodeId { get; set; }

        // Parameterless constructor for deserialization
        public SerializableConnection() { }

        // Constructor to convert from ConnectionViewModel
        public SerializableConnection(ConnectionViewModel vm)
        {
            Id = Guid.Parse(vm.Id); // Assume vm.Id is a valid Guid string
            SourceNodeId = Guid.Parse(vm.SourceNodeId); // Assume vm.SourceNodeId is a valid Guid string
            TargetNodeId = Guid.Parse(vm.TargetNodeId); // Assume vm.TargetNodeId is a valid Guid string
        }

        // Method to convert back to ConnectionViewModel
        public ConnectionViewModel ToViewModel()
        {
            // Call the constructor with required string arguments
            return new ConnectionViewModel(
                this.Id.ToString(), // Convert Guid back to string
                this.SourceNodeId.ToString(), // Convert Guid back to string
                this.TargetNodeId.ToString() // Convert Guid back to string
            );
        }
    }
}
