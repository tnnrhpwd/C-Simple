using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Json;
using CSimple.Models;

namespace CSimple.Services
{
    /// <summary>
    /// Service for sharing neural models between users
    /// </summary>
    public class ModelSharingService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl;
        private readonly string _localStorageDirectory;

        public ModelSharingService(string apiBaseUrl)
        {
            _httpClient = new HttpClient();
            _apiBaseUrl = apiBaseUrl;

            // Set up local storage for exported/imported models
            _localStorageDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CSimple",
                "SharedModels");

            Directory.CreateDirectory(_localStorageDirectory); // Ensure directory exists
        }

        /// <summary>
        /// Export a model to a file that can be shared
        /// </summary>
        public async Task<string> ExportModelAsync(NeuralModel model, List<CSimple.Models.ActionGroup> actions = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            try
            {
                // Convert to shareable format
                var shareableModel = model.ToShareable(actions);

                // Serialize to JSON
                var json = JsonSerializer.Serialize(shareableModel, new JsonSerializerOptions { WriteIndented = true });

                // Save to file
                var filename = $"{model.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.csmodel";
                var filePath = Path.Combine(_localStorageDirectory, filename);
                await File.WriteAllTextAsync(filePath, json);

                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error exporting model: {ex.Message}");
                throw new Exception("Failed to export model", ex);
            }
        }

        /// <summary>
        /// Import a model from a file
        /// </summary>
        public async Task<ImportResult> ImportModelAsync(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Model file not found", filePath);

            try
            {
                // Read the JSON content
                var json = await File.ReadAllTextAsync(filePath);

                // Deserialize
                var shareableModel = JsonSerializer.Deserialize<ShareableModel>(json);

                // Convert to full model
                var model = ConvertToNeuralModel(shareableModel);

                // Extract actions
                var actions = shareableModel.AssociatedActions?.Select(sa => new ActionGroup
                {
                    Id = Guid.NewGuid(),
                    ActionName = sa.ActionName,
                    ActionType = sa.ActionType,
                    Description = sa.Description,
                    ActionArray = sa.ActionArray?.Select(a => new CSimple.ActionItem 
                    {
                        // Map properties from CSimple.Models.ActionItem to CSimple.ActionItem
                        // ActionType = a.ActionType,
                        // Either use the correct property name instead of ActionData
                        // or add ActionData to the ActionItem class definition
                        // For now, commenting out the problematic line:
                        // ActionData = a.ActionData,
                        // Add any other properties that need to be mapped
                    }).ToList(),
                    CreatedAt = DateTime.Now
                }).ToList() ?? new List<ActionGroup>();

                return new ImportResult
                {
                    Model = model,
                    Actions = actions,
                    ImportSource = filePath,
                    ImportDate = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing model: {ex.Message}");
                throw new Exception("Failed to import model", ex);
            }
        }

        /// <summary>
        /// Upload a model to the shared repository
        /// </summary>
        public async Task<bool> UploadModelToRepositoryAsync(ShareableModel model)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/models", model);
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error uploading model: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Download models from the repository
        /// </summary>
        public async Task<List<ShareableModel>> GetSharedModelsAsync(string filter = null)
        {
            try
            {
                var url = $"{_apiBaseUrl}/models";
                if (!string.IsNullOrEmpty(filter))
                {
                    url += $"?filter={Uri.EscapeDataString(filter)}";
                }

                var models = await _httpClient.GetFromJsonAsync<List<ShareableModel>>(url);
                return models ?? new List<ShareableModel>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting shared models: {ex.Message}");
                return new List<ShareableModel>();
            }
        }

        /// <summary>
        /// Converts a shared model to a full neural model
        /// </summary>
        private NeuralModel ConvertToNeuralModel(ShareableModel shareableModel)
        {
            if (shareableModel == null) return null;

            // Create a new neural model with properties from the shareable model
            var model = new NeuralModel
            {
                Id = shareableModel.Id,
                Name = shareableModel.Name,
                Description = $"{shareableModel.Description}\n\nImported model created by {shareableModel.Author}",
                Architecture = shareableModel.Architecture,
                Accuracy = shareableModel.Accuracy,
                CreatedDate = shareableModel.CreatedDate,
                LastTrainedDate = shareableModel.LastModified,
                UsesScreenData = shareableModel.UsesScreenData,
                UsesAudioData = shareableModel.UsesAudioData,
                UsesTextData = shareableModel.UsesTextData,
                TrainingDataPoints = shareableModel.TrainingDataPoints,

                // Default values for parameters not included in shareable model
                TrainingEpochs = 10,
                LearningRate = 0.001,
                BatchSize = 32,
                DropoutRate = 0.2,
                IsActive = false
            };

            return model;
        }
    }

    /// <summary>
    /// Result of importing a model
    /// </summary>
    public class ImportResult
    {
        public NeuralModel Model { get; set; }
        public List<ActionGroup> Actions { get; set; } = new List<ActionGroup>();
        public string ImportSource { get; set; }
        public DateTime ImportDate { get; set; }
    }
}
