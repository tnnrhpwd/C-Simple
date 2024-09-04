public class ActionGroup
{
    public string ActionName { get; set; }
    public List<ActionArrayItem> ActionArray { get; set; } = new List<ActionArrayItem>();
    public bool IsSimulating { get; set; }

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
