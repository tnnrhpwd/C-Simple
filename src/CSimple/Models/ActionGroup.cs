namespace CSimple.Models
{
    public class ActionGroup
    {
        public string ActionName { get; set; }
        public string[] ActionArray { get; set; }

        // Used for display in UI
        public string ActionArrayString => string.Join(", ", ActionArray);
    }
}
