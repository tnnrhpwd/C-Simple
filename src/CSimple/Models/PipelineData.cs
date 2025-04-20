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
        }

        // Method to convert back to NodeViewModel
        public NodeViewModel ToViewModel()
        {
            // Call the constructor with required arguments
            var vm = new NodeViewModel(
                this.Id.ToString(), // Convert Guid back to string
                this.Name,
                this.Type,
                new PointF(this.PositionX, this.PositionY)
            )
            {
                // Set properties not handled by constructor
                Size = new SizeF(this.SizeWidth, this.SizeHeight),
                ModelPath = this.ModelPath // Restore ModelPath
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
