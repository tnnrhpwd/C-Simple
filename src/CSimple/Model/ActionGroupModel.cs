using System;
using System.Collections.Generic;

namespace CSimple
{
    // Define missing properties and functionality for ActionGroup
    public partial class ActionGroup
    {
        // These are the properties that need to be added based on error messages
        public string Category { get; set; } = "Productivity";
        public DateTime? CreatedAt { get; set; } = DateTime.Now;
        public string ActionType { get; set; } = "Custom Action";
        public int UsageCount { get; set; } = 0;
        public double SuccessRate { get; set; } = 0.85; // 85% default success rate
        public bool IsPartOfTraining { get; set; } = false;
        public bool IsChained { get; set; } = false;
        public bool HasMetrics { get; set; } = false;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; } = false;
        public string ChainName { get; set; } = string.Empty;
        public object Tag { get; set; } = null;
    }

    // Rename this class to avoid the conflict with an existing ModelAssignment class
    public class ActionModelAssignment
    {
        public string ModelId { get; set; }
        public string ModelName { get; set; }
        public string ModelType { get; set; }
        public DateTime AssignedDate { get; set; }
    }
}
