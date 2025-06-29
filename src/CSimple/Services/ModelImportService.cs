using CSimple.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public interface IModelImportService
    {
        Task<bool> ShowModelDetailsAndImportAsync(
            HuggingFaceModel model,
            Func<HuggingFaceModel, ModelInputType> guessInputType,
            Func<string, string> getFriendlyModelName,
            Func<string, Task<HuggingFaceModelDetails>> getModelDetails,
            Func<string, string, string, string, Task<bool>> showConfirmation,
            Func<string, string, string, Task> showAlert,
            Action<string> updateCurrentStatus,
            Action<bool> setIsLoading,
            Func<IEnumerable<NeuralNetworkModel>> getAvailableModels,
            Action<NeuralNetworkModel> addAvailableModel,
            Func<Task> savePersistedModels,
            Action<string> setHuggingFaceSearchQuery
        );
    }

    public class ModelImportService : IModelImportService
    {
        private readonly HuggingFaceService _huggingFaceService;

        public ModelImportService(HuggingFaceService huggingFaceService)
        {
            _huggingFaceService = huggingFaceService;
        }

        public async Task<bool> ShowModelDetailsAndImportAsync(
            HuggingFaceModel model,
            Func<HuggingFaceModel, ModelInputType> guessInputType,
            Func<string, string> getFriendlyModelName,
            Func<string, Task<HuggingFaceModelDetails>> getModelDetails,
            Func<string, string, string, string, Task<bool>> showConfirmation,
            Func<string, string, string, Task> showAlert,
            Action<string> updateCurrentStatus,
            Action<bool> setIsLoading,
            Func<IEnumerable<NeuralNetworkModel>> getAvailableModels,
            Action<NeuralNetworkModel> addAvailableModel,
            Func<Task> savePersistedModels,
            Action<string> setHuggingFaceSearchQuery)
        {
            try
            {
                bool importConfirmed = await showConfirmation("Model Details",
                    $"Name: {model.ModelId ?? model.Id}\nAuthor: {model.Author}\nType: {model.Pipeline_tag}\nDownloads: {model.Downloads}\n\nImport this model as a Python Reference?",
                    "Import Reference", "Cancel");

                if (!importConfirmed)
                {
                    updateCurrentStatus("Import canceled");
                    return false;
                }

                updateCurrentStatus($"Preparing Python reference for {model.ModelId ?? model.Id}...");
                setIsLoading(true);

                // Optional: Still fetch details if needed for GuessInputType or other metadata
                HuggingFaceModelDetails modelDetails = model as HuggingFaceModelDetails ?? await getModelDetails(model.ModelId ?? model.Id);
                Debug.WriteLine($"ModelImportService: Importing '{model.ModelId ?? model.Id}' as Python Reference.");

                // Check if a Python reference with this HuggingFaceModelId already exists
                var availableModels = getAvailableModels();
                if (availableModels.Any(m => m.IsHuggingFaceReference && m.HuggingFaceModelId == (model.ModelId ?? model.Id)))
                {
                    updateCurrentStatus($"Python reference for '{model.ModelId ?? model.Id}' already exists.");
                    await showAlert("Duplicate Reference", $"A Python reference for this model ID already exists.", "OK");
                    setIsLoading(false);
                    return false; // Stop processing if duplicate
                }

                // Use modelDetails if fetched, otherwise use the basic model info
                var description = modelDetails?.Description ?? model.Description ?? "Imported from HuggingFace (requires Python)";
                var inputType = guessInputType(modelDetails ?? model); // Guess input type

                var pythonReferenceModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = getFriendlyModelName(model.ModelId ?? model.Id) + " (Python Ref)",
                    Description = description,
                    Type = model.RecommendedModelType, // Keep original type guess if available
                    IsHuggingFaceReference = true,
                    HuggingFaceModelId = model.ModelId ?? model.Id,
                    InputType = inputType
                };

                // Add the unique reference
                addAvailableModel(pythonReferenceModel);
                updateCurrentStatus($"Added reference to {pythonReferenceModel.Name}");
                await savePersistedModels(); // Save the updated list

                // Clear the search query after successful import
                setHuggingFaceSearchQuery("");

                // Show Python usage info
                await showAlert("Reference Added & Usage",
                    $"Reference to '{pythonReferenceModel.Name}' added.\n\nUse in Python:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{pythonReferenceModel.HuggingFaceModelId}\", trust_remote_code=True)",
                    "OK");

                setIsLoading(false);
                return true; // Python reference added successfully
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in ModelImportService.ShowModelDetailsAndImportAsync: {ex.Message}");
                await showAlert("Import Error", $"Failed to import model reference: {ex.Message}", "OK");
                setIsLoading(false);
                return false;
            }
        }
    }
}
