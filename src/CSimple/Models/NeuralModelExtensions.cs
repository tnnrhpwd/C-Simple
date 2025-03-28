using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using System.Text.Json;
using CSimple.Models;

namespace CSimple.Models
{
    /// <summary>
    /// Extension methods for working with neural models
    /// </summary>
    public static class NeuralModelExtensions
    {
        /// <summary>
        /// Serializes a neural model to JSON
        /// </summary>
        public static string ToJson(this NeuralModel model)
        {
            if (model == null) return null;

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            return JsonSerializer.Serialize(model, options);
        }

        /// <summary>
        /// Deserializes a neural model from JSON
        /// </summary>
        public static NeuralModel FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;

            try
            {
                return JsonSerializer.Deserialize<NeuralModel>(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deserializing neural model: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a lightweight exportable version of the model suitable for sharing
        /// </summary>
        public static ShareableModel ToShareable(this NeuralModel model, List<ActionGroup> actions = null)
        {
            if (model == null) return null;

            var shareable = new ShareableModel
            {
                Id = model.Id,
                Name = model.Name,
                Description = model.Description,
                Architecture = model.Architecture,
                CreatedDate = model.CreatedDate,
                LastModified = DateTime.Now,
                UsesScreenData = model.UsesScreenData,
                UsesAudioData = model.UsesAudioData,
                UsesTextData = model.UsesTextData,
                TrainingDataPoints = model.TrainingDataPoints,
                Accuracy = model.Accuracy,
                Author = GetCurrentUsername()
            };

            // Include actions if provided
            if (actions != null && actions.Count > 0)
            {
                shareable.AssociatedActions = actions.Select(a => new SharedAction
                {
                    ActionName = a.ActionName,
                    ActionType = a.ActionType,
                    Description = a.Description,
                    ActionArray = a.ActionArray
                }).ToList();
            }

            return shareable;
        }

        /// <summary>
        /// Merges another model into this one, combining their capabilities
        /// </summary>
        public static NeuralModel MergeWith(this NeuralModel baseModel, NeuralModel secondaryModel)
        {
            if (baseModel == null || secondaryModel == null) return baseModel;

            var result = baseModel.Clone();

            // Average out the parameters
            result.Accuracy = (baseModel.Accuracy + secondaryModel.Accuracy) / 2;
            result.TrainingEpochs = Math.Max(baseModel.TrainingEpochs, secondaryModel.TrainingEpochs);
            result.LearningRate = (baseModel.LearningRate + secondaryModel.LearningRate) / 2;
            result.BatchSize = Math.Max(baseModel.BatchSize, secondaryModel.BatchSize);
            result.DropoutRate = (baseModel.DropoutRate + secondaryModel.DropoutRate) / 2;

            // Combine data sources
            result.UsesScreenData = baseModel.UsesScreenData || secondaryModel.UsesScreenData;
            result.UsesAudioData = baseModel.UsesAudioData || secondaryModel.UsesAudioData;
            result.UsesTextData = baseModel.UsesTextData || secondaryModel.UsesTextData;

            // Update training stats
            result.TrainingDataPoints = baseModel.TrainingDataPoints + secondaryModel.TrainingDataPoints;
            result.TrainingDuration = baseModel.TrainingDuration + secondaryModel.TrainingDuration;

            // Update metadata
            result.LastTrainedDate = DateTime.Now;
            result.Description += $"\nMerged with {secondaryModel.Name} on {DateTime.Now:g}";

            return result;
        }

        /// <summary>
        /// Estimates the model's compatibility for a specific task
        /// </summary>
        public static double EstimateTaskCompatibility(this NeuralModel model, string taskType)
        {
            if (model == null || string.IsNullOrEmpty(taskType)) return 0;

            // Basic heuristic for compatibility
            var baseScore = model.Accuracy;

            // Adjust based on model architecture
            if (model.Architecture == taskType) baseScore *= 1.5;
            else if (model.Architecture == "General Assistant") baseScore *= 0.8;

            // Adjust based on data sources that match the task
            switch (taskType.ToLower())
            {
                case "visual task":
                case "screen automation":
                    baseScore *= model.UsesScreenData ? 1.3 : 0.5;
                    break;

                case "audio task":
                case "voice command":
                    baseScore *= model.UsesAudioData ? 1.3 : 0.5;
                    break;

                case "text processing":
                case "document automation":
                    baseScore *= model.UsesTextData ? 1.3 : 0.5;
                    break;
            }

            // Larger models with more training might be better
            baseScore *= (1 + Math.Log10(Math.Max(1, model.TrainingDataPoints)) / 10);

            // Clamp to [0,1] range
            return Math.Clamp(baseScore, 0, 1);
        }

        /// <summary>
        /// Gets a list of suggested tasks that this model would be good at
        /// </summary>
        public static List<SuggestedTask> GetSuggestedTasks(this NeuralModel model)
        {
            var tasks = new List<SuggestedTask>();

            if (model == null) return tasks;

            // Check model capabilities and suggest appropriate tasks
            if (model.UsesScreenData)
            {
                tasks.Add(new SuggestedTask
                {
                    TaskName = "Screen Navigation",
                    Compatibility = EstimateTaskCompatibility(model, "screen automation"),
                    Description = "Navigate through UI elements and applications"
                });

                tasks.Add(new SuggestedTask
                {
                    TaskName = "Data Entry",
                    Compatibility = EstimateTaskCompatibility(model, "document automation"),
                    Description = "Automated form filling and data entry"
                });
            }

            if (model.UsesAudioData)
            {
                tasks.Add(new SuggestedTask
                {
                    TaskName = "Voice Commands",
                    Compatibility = EstimateTaskCompatibility(model, "voice command"),
                    Description = "Execute commands based on voice input"
                });

                tasks.Add(new SuggestedTask
                {
                    TaskName = "Audio Transcription",
                    Compatibility = EstimateTaskCompatibility(model, "audio task"),
                    Description = "Transcribe audio to text"
                });
            }

            if (model.UsesTextData)
            {
                tasks.Add(new SuggestedTask
                {
                    TaskName = "Document Processing",
                    Compatibility = EstimateTaskCompatibility(model, "text processing"),
                    Description = "Extract information from documents"
                });

                tasks.Add(new SuggestedTask
                {
                    TaskName = "Email Management",
                    Compatibility = EstimateTaskCompatibility(model, "text processing"),
                    Description = "Categorize and respond to emails"
                });
            }

            // Add general tasks for all models
            tasks.Add(new SuggestedTask
            {
                TaskName = "Workflow Automation",
                Compatibility = model.Accuracy * 0.9,
                Description = "Automate repetitive workflows"
            });

            return tasks.OrderByDescending(t => t.Compatibility).ToList();
        }

        /// <summary>
        /// Helper method to get the current system username
        /// </summary>
        private static string GetCurrentUsername()
        {
            try
            {
                return Environment.UserName;
            }
            catch
            {
                return "Unknown User";
            }
        }
    }

    /// <summary>
    /// Lightweight, shareable version of a neural model
    /// </summary>
    public class ShareableModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Architecture { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime LastModified { get; set; }
        public double Accuracy { get; set; }
        public bool UsesScreenData { get; set; }
        public bool UsesAudioData { get; set; }
        public bool UsesTextData { get; set; }
        public int TrainingDataPoints { get; set; }
        public string Author { get; set; }
        public List<SharedAction> AssociatedActions { get; set; } = new List<SharedAction>();
    }

    /// <summary>
    /// Simplified action for sharing
    /// </summary>
    public class SharedAction
    {
        public string ActionName { get; set; }
        public string ActionType { get; set; }
        public string Description { get; set; }
        public List<ActionItem> ActionArray { get; set; } = new List<ActionItem>();
    }

    /// <summary>
    /// Represents a task suggestion for a model
    /// </summary>
    public class SuggestedTask
    {
        public string TaskName { get; set; }
        public double Compatibility { get; set; }
        public string Description { get; set; }

        // Display-friendly compatibility percentage
        public string CompatibilityPercent => $"{Compatibility:P0}";
    }
}
