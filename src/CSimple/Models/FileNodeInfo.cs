using System;

namespace CSimple.Models
{
    /// <summary>
    /// Represents information about a file node that can be added to the pipeline
    /// </summary>
    public class FileNodeInfo
    {
        /// <summary>
        /// The display name of the file node (e.g., "Goals", "Memory", "Custom")
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The data type that this file node provides (e.g., "text", "json", "csv")
        /// </summary>
        public string DataType { get; set; }

        /// <summary>
        /// The actual file name or path if applicable (e.g., "goals.json", "memory.txt")
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Description of what this file node contains
        /// </summary>
        public string Description { get; set; }

        public FileNodeInfo(string name, string dataType, string fileName = null, string description = null)
        {
            Name = name;
            DataType = dataType;
            FileName = fileName;
            Description = description;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
