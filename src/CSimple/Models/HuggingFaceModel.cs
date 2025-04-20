using System.Collections.Generic;
using System.Text.Json.Serialization; // Required for JsonPropertyName

namespace CSimple.Models
{
    // Example structure - adjust based on your actual HuggingFace API response/data
    public class HuggingFaceModel
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } // Typically the unique identifier like 'gpt2' or 'openai/whisper-base'

        [JsonPropertyName("modelId")]
        public string ModelId { get; set; } // Often the same as Id, or a more descriptive name if available

        [JsonPropertyName("pipeline_tag")]
        public string Pipeline_tag { get; set; } // e.g., 'text-generation', 'image-classification'

        [JsonPropertyName("lastModified")]
        public DateTime LastModified { get; set; }

        [JsonPropertyName("author")]
        public string Author { get; set; }

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; }

        [JsonPropertyName("downloads")]
        public int Downloads { get; set; } // Added Downloads property

        [JsonPropertyName("description")] // Added Description property
        public string Description { get; set; }

        // For display purposes, you might prefer ModelId if it's more readable
        public string Name => GetFriendlyName(); // Use helper method

        // Helper to get a friendlier name
        private string GetFriendlyName()
        {
            var nameToUse = ModelId ?? Id;
            if (string.IsNullOrEmpty(nameToUse)) return "Unnamed Model";
            var name = nameToUse.Contains('/') ? nameToUse.Split('/').Last() : nameToUse;
            name = name.Replace("-", " ").Replace("_", " ");
            // Simple title casing (consider more robust library if needed)
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
        }

        // Helper to recommend a ModelType based on pipeline tag or name
        [JsonIgnore] // Don't serialize this helper property
        public ModelType RecommendedModelType
        {
            get
            {
                // If no pipeline tag is provided, default to General
                if (string.IsNullOrWhiteSpace(Pipeline_tag))
                {
                    return ModelType.General;
                }

                string tag = Pipeline_tag.ToLowerInvariant(); // Use InvariantCulture for consistency
                string nameLower = (ModelId ?? Id ?? "").ToLowerInvariant();

                if (tag.Contains("text-generation") || tag.Contains("fill-mask") || tag.Contains("summarization") || tag.Contains("translation") || nameLower.Contains("gpt") || nameLower.Contains("bert") || nameLower.Contains("llama"))
                    return ModelType.General; // Text-based are often general purpose
                if (tag.Contains("image-classification") || tag.Contains("object-detection") || tag.Contains("image-segmentation") || nameLower.Contains("resnet") || nameLower.Contains("yolo"))
                    return ModelType.InputSpecific; // Image models are input-specific
                if (tag.Contains("audio-classification") || tag.Contains("automatic-speech-recognition") || nameLower.Contains("whisper") || nameLower.Contains("wav2vec"))
                    return ModelType.InputSpecific; // Audio models are input-specific

                return ModelType.General; // Default to General if unsure or tag doesn't match known types
            }
        }
    }

    // Define HuggingFaceModelDetails, inheriting from HuggingFaceModel
    public class HuggingFaceModelDetails : HuggingFaceModel
    {
        [JsonPropertyName("siblings")]
        public List<Sibling> Siblings { get; set; }

        // Convenience property to get just the filenames
        [JsonIgnore] // Don't serialize this derived property
        public List<string> Files => Siblings?.Select(s => s.Rfilename).ToList() ?? new List<string>();

        // Add any other detail-specific properties from the API response
        // e.g., config, readme content, etc.
    }

    // Define the Sibling class used within HuggingFaceModelDetails
    public class Sibling
    {
        [JsonPropertyName("rfilename")]
        public string Rfilename { get; set; } // The relative filename

        // Add other sibling properties if needed (e.g., size, blob_id)
        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }
}
