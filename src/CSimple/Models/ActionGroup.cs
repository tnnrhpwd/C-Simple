public class ActionGroup
{
    public string ActionName { get; set; }
    public List<ActionArrayItem> ActionArray { get; set; } = new List<ActionArrayItem>();
    public List<ActionModifier> ActionModifiers { get; set; } = new List<ActionModifier>();
    public bool IsSimulating { get; set; }
    public string Creator { get; set; }
    public string ActionArrayFormatted { get; set; }
}

public class ActionArrayItem
{
    public string Timestamp { get; set; }
    public ushort KeyCode { get; set; } // Key Code: 49 for execute key press
    public int EventType { get; set; } // Event type: 0x0000 for keydown
    public int Duration { get; set; } // Duration: key press duration in milliseconds
    public Coordinates Coordinates { get; set; } // Optional, used for mouse events
}

public class Coordinates
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class ActionModifier
{
    public string ModifierName { get; set; } // Example: "DelayModifier"
    public string Description { get; set; } // Example: "Adds a delay before executing the action"
    public int Priority { get; set; } // Example: 1 (Higher priority modifiers are applied first)
    public Func<ActionArrayItem, int> Condition { get; set; } // Example: item => item.KeyCode == 49 (Apply only if the KeyCode is 49)
    public Action<ActionArrayItem> ModifyAction { get; set; } // Example: item => item.Duration += 1000 (Add 1000 milliseconds to the duration)
}
