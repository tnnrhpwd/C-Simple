public class ActionGroup
{
    public string ActionName { get; set; }
    public string[] ActionArray { get; set; }
    
    public string ActionArrayFormatted => string.Join(" ", ActionArray); // Changed to space delimiter
}
