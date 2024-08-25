public class ActionGroup
{
    public string ActionName { get; set; }
    public List<ActionArrayItem> ActionArray { get; set; } = new List<ActionArrayItem>();
}

public class ActionArrayItem
{
    public string Timestamp { get; set; }
    public int Type { get; set; } // Event type: 256 for key press, 512 for mouse move, etc.
    public int? KeyCode { get; set; } // Optional, used for key events
    public List<int> Modifiers { get; set; } = new List<int>(); // Optional, used for modifier keys
    public List<int> Combination { get; set; } = new List<int>(); // Optional, used for key combinations
    public Coordinates Coordinates { get; set; } // Optional, used for mouse events
    public int? Category { get; set; } // Optional, used to categorize the event (e.g., mouse click, move, etc.)
}

public class Coordinates
{
    public int X { get; set; }
    public int Y { get; set; }
}
