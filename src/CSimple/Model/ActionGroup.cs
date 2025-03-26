namespace CSimple
{
    public partial class ActionGroup
    {
        // Basic properties that we know exist
        public string ActionName { get; set; } = string.Empty;
        public List<ActionItem> ActionArray { get; set; } = new List<ActionItem>();
        public List<ActionModifier> ActionModifiers { get; set; } = new List<ActionModifier>();
        public bool IsSimulating { get; set; } = false;

        // Additional properties needed for enhanced functionality
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
        public List<ActionFile> Files { get; set; } = new List<ActionFile>();
    }

    // Missing related classes that we need to define
    public class ActionModifier
    {
        public string ModifierName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Priority { get; set; } = 0;
    }

    public class ActionItem
    {
        public object Timestamp { get; set; }
        public int EventType { get; set; }
        public int KeyCode { get; set; }
        public int Duration { get; set; }
        public Coordinates Coordinates { get; set; }

        public override string ToString()
        {
            if (EventType == 256 || EventType == 257) // Keyboard events
                return $"Key {KeyCode} {(EventType == 256 ? "Down" : "Up")}";
            else if (EventType == 512) // Mouse move
                return $"Mouse Move to X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0}";
            else if (EventType == 0x0201) // Left mouse button down
                return $"Left Click at X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0}";
            else if (EventType == 0x0204) // Right mouse button down
                return $"Right Click at X:{Coordinates?.X ?? 0}, Y:{Coordinates?.Y ?? 0}";
            else
                return $"Action Type:{EventType} at {Timestamp}";
        }
    }

    public class Coordinates
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    // Move ModelAssignment from ActionGroupModel.cs to here
    public class ModelAssignment
    {
        public string ModelName { get; set; }
        public string ModelType { get; set; }
        public DateTime AssignedDate { get; set; } = DateTime.Now;
    }
}
