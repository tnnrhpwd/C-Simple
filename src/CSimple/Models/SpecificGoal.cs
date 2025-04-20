using System;

namespace CSimple.Models
{
    public class SpecificGoal
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Default ID
        public string Name { get; set; }
        public string Description { get; set; }
        public string Category { get; set; } = "General";
        public int DownloadCount { get; set; } = 0;

        // Calculated property for display
        public string DownloadCountDisplay => DownloadCount > 1000 ? $"{DownloadCount / 1000.0:F1}K" : DownloadCount.ToString();
    }
}
