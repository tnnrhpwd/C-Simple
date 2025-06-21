using CSimple.Models;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class ModelImportExportService
    {
        private readonly FileService _fileService;
        private readonly HuggingFaceService _huggingFaceService;

        // Events for status updates
        public event Action<string> StatusUpdated;
        public event Action<bool> LoadingChanged;
        public event Action<string, Exception> ErrorOccurred;

        public ModelImportExportService(FileService fileService, HuggingFaceService huggingFaceService)
        {
            _fileService = fileService;
            _huggingFaceService = huggingFaceService;
        }

        /// <summary>
        /// Shows model details and handles import of HuggingFace models as Python references
        /// </summary>
        public async Task<NeuralNetworkModel> ShowModelDetailsAndImportAsync(
            HuggingFaceModel model,
            List<NeuralNetworkModel> availableModels,
            Func<string, string, string, string, Task<bool>> showConfirmation,
            Func<string, string, string, Task> showAlert,
            Func<HuggingFaceModel, ModelInputType> guessInputType)
        {
            try
            {
                StatusUpdated?.Invoke("Preparing model details...");
                LoadingChanged?.Invoke(true);

                bool importConfirmed = await showConfirmation("Model Details",
                    $"Name: {model.ModelId ?? model.Id}\nAuthor: {model.Author}\nType: {model.Pipeline_tag}\nDownloads: {model.Downloads}\n\nImport this model as a Python Reference?",
                    "Import Reference", "Cancel");

                if (!importConfirmed)
                {
                    StatusUpdated?.Invoke("Import canceled");
                    return null;
                }

                StatusUpdated?.Invoke($"Preparing Python reference for {model.ModelId ?? model.Id}...");

                // Check if a Python reference with this HuggingFaceModelId already exists
                if (availableModels.Any(m => m.IsHuggingFaceReference && m.HuggingFaceModelId == (model.ModelId ?? model.Id)))
                {
                    StatusUpdated?.Invoke($"Python reference for '{model.ModelId ?? model.Id}' already exists.");
                    await showAlert("Duplicate Reference", $"A Python reference for this model ID already exists.", "OK");
                    return null;
                }

                // Fetch details if needed for input type guessing
                HuggingFaceModelDetails modelDetails = model as HuggingFaceModelDetails ??
                    await _huggingFaceService.GetModelDetailsAsync(model.ModelId ?? model.Id);

                var description = modelDetails?.Description ?? model.Description ?? "Imported from HuggingFace (requires Python)";
                var inputType = guessInputType(modelDetails ?? model);

                var pythonReferenceModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = GetFriendlyModelName(model.ModelId ?? model.Id) + " (Python Ref)",
                    Description = description,
                    Type = model.RecommendedModelType,
                    IsHuggingFaceReference = true,
                    HuggingFaceModelId = model.ModelId ?? model.Id,
                    InputType = inputType
                };

                StatusUpdated?.Invoke($"Added reference to {pythonReferenceModel.Name}");

                // Save Python reference info for user
                await SavePythonReferenceInfo(model);

                // Show Python usage info
                await showAlert("Reference Added & Usage",
                    $"Reference to '{pythonReferenceModel.Name}' added.\n\n" +
                    $"Use in Python:\nfrom transformers import AutoModel\n" +
                    $"model = AutoModel.from_pretrained(\"{pythonReferenceModel.HuggingFaceModelId}\", trust_remote_code=True)", "OK");

                return pythonReferenceModel;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error handling model details", ex);
                await showAlert("Import Error", $"Failed to import model reference: {ex.Message}", "OK");
                return null;
            }
            finally
            {
                LoadingChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Processes a selected model file for import
        /// </summary>
        public async Task<NeuralNetworkModel> ProcessSelectedModelFileAsync(
            FileResult fileResult,
            Func<string, string, string, Task> showAlert,
            Func<string, string, string, string[], Task<string>> showActionSheet)
        {
            try
            {
                await showAlert("File Selected", $"Name: {fileResult.FileName}", "Continue");

                var modelDestinationPath = await CopyModelToAppDirectoryAsync(fileResult);
                if (string.IsNullOrEmpty(modelDestinationPath))
                {
                    StatusUpdated?.Invoke("Failed to copy model file");
                    return null;
                }

                var modelTypeResult = await showActionSheet("Select Model Type", "Cancel", null,
                    new[] { "General", "Input Specific", "Goal Specific" });

                if (modelTypeResult == "Cancel" || string.IsNullOrEmpty(modelTypeResult))
                {
                    StatusUpdated?.Invoke("Import canceled - no type selected");
                    return null;
                }

                ModelType modelType = Enum.TryParse(modelTypeResult.Replace(" ", ""), true, out ModelType parsedType)
                    ? parsedType : ModelType.General;

                var modelName = Path.GetFileNameWithoutExtension(fileResult.FileName);
                var importedModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = modelName,
                    Description = $"Imported from {fileResult.FileName}",
                    Type = modelType
                };

                StatusUpdated?.Invoke($"Model '{importedModel.Name}' imported successfully");
                await showAlert("Import Success", $"Model '{importedModel.Name}' imported.", "OK");

                return importedModel;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error processing selected file", ex);
                await showAlert("Import Failed", $"Error processing model file: {ex.Message}", "OK");
                return null;
            }
        }

        /// <summary>
        /// Exports a model for sharing
        /// </summary>
        public async Task ExportModelAsync(NeuralNetworkModel model, Func<string, string, string, Task> showAlert)
        {
            if (model == null) return;

            try
            {
                StatusUpdated?.Invoke($"Exporting model '{model.Name}' for sharing...");
                LoadingChanged?.Invoke(true);

                await Task.Delay(1000); // Simulate export process

                StatusUpdated?.Invoke($"Model '{model.Name}' exported successfully");
                await showAlert("Export Successful", $"Model '{model.Name}' has been exported.", "OK");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"Error exporting model {model.Name}", ex);
                throw;
            }
            finally
            {
                LoadingChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// Saves and loads persisted HuggingFace model references
        /// </summary>
        public async Task SavePersistedModelsAsync(List<NeuralNetworkModel> modelsToSave)
        {
            try
            {
                Debug.WriteLine($"ModelImportExportService: SavePersistedModelsAsync starting with {modelsToSave?.Count ?? 0} models.");

                // Ensure uniqueness for Python references before saving
                var uniquePythonRefs = new Dictionary<string, NeuralNetworkModel>();
                var otherModels = new List<NeuralNetworkModel>();

                if (modelsToSave != null)
                {
                    foreach (var model in modelsToSave)
                    {
                        if (model.IsHuggingFaceReference && !string.IsNullOrEmpty(model.HuggingFaceModelId))
                        {
                            if (!uniquePythonRefs.ContainsKey(model.HuggingFaceModelId))
                            {
                                uniquePythonRefs[model.HuggingFaceModelId] = model;
                            }
                        }
                        else
                        {
                            otherModels.Add(model);
                        }
                    }
                }

                var finalModelsToSave = otherModels.Concat(uniquePythonRefs.Values).ToList();

                Debug.WriteLine($"ModelImportExportService: Saving {finalModelsToSave.Count} unique models.");
                await _fileService.SaveHuggingFaceModelsAsync(finalModelsToSave);
                Debug.WriteLine($"ModelImportExportService: Called FileService to save {finalModelsToSave.Count} models.");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error saving persisted models", ex);
                throw;
            }
        }

        /// <summary>
        /// Loads persisted models from storage
        /// </summary>
        public async Task<List<NeuralNetworkModel>> LoadPersistedModelsAsync()
        {
            try
            {
                Debug.WriteLine("ModelImportExportService: LoadPersistedModelsAsync starting.");

                var persistedModels = await _fileService.LoadHuggingFaceModelsAsync();
                Debug.WriteLine($"ModelImportExportService: Loaded {persistedModels?.Count ?? 0} persisted models from FileService.");

                return persistedModels ?? new List<NeuralNetworkModel>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ModelImportExportService: Error loading persisted models: {ex.Message}");
                ErrorOccurred?.Invoke("Error loading persisted models", ex);
                return new List<NeuralNetworkModel>();
            }
        }

        /// <summary>
        /// Gets recommended files from a list for model import
        /// </summary>
        public List<string> GetRecommendedFiles(List<string> files)
        {
            var priorityExtensions = new[] { ".bin", ".safetensors", ".onnx", ".gguf", ".pt", ".model" };
            var result = files.Where(f => priorityExtensions.Any(e => f.EndsWith(e))).ToList();

            if (result.Count == 0)
                result = files.Where(f => f.EndsWith(".json")).ToList();

            return result.OrderBy(f => f.Length).Take(5).ToList();
        }

        private string GetFriendlyModelName(string modelId)
        {
            var name = modelId.Contains('/') ? modelId.Split('/').Last() : modelId;
            name = name.Replace("-", " ").Replace("_", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        private string GetModelDirectoryPath(string modelId)
        {
            string safeModelId = (modelId ?? "unknown_model").Replace("/", "_").Replace("\\", "_");
            var modelDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models", "HuggingFace", safeModelId);
            Directory.CreateDirectory(modelDirectory); // Ensure it exists

            Debug.WriteLine($"[Model Directory] {modelId} -> {modelDirectory}");
            return modelDirectory;
        }

        private async Task SavePythonReferenceInfo(HuggingFaceModel model)
        {
            try
            {
                var infoDirectory = GetModelDirectoryPath(model.ModelId ?? model.Id);
                string infoContent = $"Model ID: {model.ModelId ?? model.Id}\n" +
                    $"Author: {model.Author}\n" +
                    $"Type: {model.Pipeline_tag}\n" +
                    $"Python:\n" +
                    $"from transformers import AutoModel\n" +
                    $"model = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\", trust_remote_code=True)";

                await File.WriteAllTextAsync(Path.Combine(infoDirectory, "model_info.txt"), infoContent);
                Debug.WriteLine($"Saved Python reference info for {model.ModelId}");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error saving Python reference info", ex);
            }
        }

        private async Task<string> CopyModelToAppDirectoryAsync(FileResult fileResult)
        {
            try
            {
                var modelsDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models", "ImportedModels");
                Directory.CreateDirectory(modelsDirectory);

                var uniqueFileName = EnsureUniqueFileName(modelsDirectory, fileResult.FileName);
                var destinationPath = Path.Combine(modelsDirectory, uniqueFileName);

                using (var sourceStream = await fileResult.OpenReadAsync())
                using (var destinationStream = File.Create(destinationPath))
                {
                    await sourceStream.CopyToAsync(destinationStream);
                }

                Debug.WriteLine($"Model file copied to: {destinationPath}");
                return destinationPath;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke("Error copying model file", ex);
                return null;
            }
        }

        private string EnsureUniqueFileName(string directory, string fileName)
        {
            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            string finalName = fileName;
            int count = 1;

            while (File.Exists(Path.Combine(directory, finalName)))
            {
                finalName = $"{name}_{count++}{ext}";
            }

            return finalName;
        }
    }
}
