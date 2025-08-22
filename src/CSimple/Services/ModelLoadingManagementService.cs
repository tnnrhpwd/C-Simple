using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CSimple.Models;
using CSimple.ViewModels;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace CSimple.Services
{
    public interface IModelLoadingManagementService
    {
        Task LoadAvailableModelsAsync(
            ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels,
            FileService fileService,
            NetPageViewModel netPageViewModel);

        void AddDefaultInputNodesToAvailableModels(ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels);
        void AddDefaultModelExamples(ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels);

        Task AddModelNodeAsync(
            ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels,
            ObservableCollection<NodeViewModel> nodes,
            CSimple.Models.HuggingFaceModel model,
            NodeManagementService nodeManagementService,
            Func<string, string, string, Task> showAlert,
            Action invalidatePipelineStateCache,
            Action updateEnsembleCounts,
            Action updateRunAllModelsCommandCanExecute,
            Action updateRunAllNodesCommandCanExecute,
            Func<Task> saveCurrentPipelineAsync,
            Action updateExecutionStatusFromPipeline);
    }

    public class ModelLoadingManagementService : IModelLoadingManagementService
    {
        public async Task LoadAvailableModelsAsync(
            ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels,
            FileService fileService,
            NetPageViewModel netPageViewModel)
        {
            try
            {
                Debug.WriteLine("Loading available HuggingFace models...");
                availableModels.Clear();

                // Get the NetPageViewModel and ensure it has loaded its models
                var netPageVM = netPageViewModel ?? ((App)Application.Current).NetPageViewModel;
                if (netPageVM != null)
                {
                    // Ensure NetPageViewModel has loaded its models for execution
                    if (netPageVM.AvailableModels == null || netPageVM.AvailableModels.Count == 0)
                    {
                        Debug.WriteLine("LoadAvailableModelsAsync: NetPageViewModel has no models, forcing load for execution");
                        await netPageVM.LoadDataAsync();
                        Debug.WriteLine($"LoadAvailableModelsAsync: After LoadDataAsync, NetPageViewModel has {netPageVM.AvailableModels?.Count ?? 0} models");
                    }
                    else
                    {
                        Debug.WriteLine($"LoadAvailableModelsAsync: NetPageViewModel already has {netPageVM.AvailableModels.Count} models loaded");
                    }
                }

                // First, try to load models from FileService like NetPageViewModel does
                var persistedModels = await fileService.LoadHuggingFaceModelsAsync();

                // Also check if NetPageViewModel has already loaded models that we can use
                if (netPageVM?.AvailableModels != null && netPageVM.AvailableModels.Count > 0)
                {
                    Debug.WriteLine($"Found {netPageVM.AvailableModels.Count} models in NetPageViewModel");
                    // If we got fewer models from FileService, prefer the NetPageViewModel's models
                    if (persistedModels == null || persistedModels.Count < netPageVM.AvailableModels.Count)
                    {
                        Debug.WriteLine("Using NetPageViewModel's models as they are more complete");
                        persistedModels = netPageVM.AvailableModels.ToList();
                    }
                }

                if (persistedModels != null && persistedModels.Count > 0)
                {
                    // Filter to just get unique HuggingFace models
                    var uniqueHfModels = new Dictionary<string, NeuralNetworkModel>();

                    foreach (var model in persistedModels)
                    {
                        string key = model.IsHuggingFaceReference && !string.IsNullOrEmpty(model.HuggingFaceModelId)
                            ? model.HuggingFaceModelId
                            : model.Id;

                        if (!uniqueHfModels.ContainsKey(key))
                        {
                            uniqueHfModels.Add(key, model);
                        }
                    }

                    // Convert NeuralNetworkModel to HuggingFaceModel and add to collection
                    foreach (var model in uniqueHfModels.Values)
                    {
                        var hfModel = new CSimple.Models.HuggingFaceModel
                        {
                            Id = model.Id,
                            ModelId = model.IsHuggingFaceReference ? model.HuggingFaceModelId : model.Name,
                            Description = model.Description ?? "No description available",
                            Author = "Imported Model" // Default author if not available
                        };

                        availableModels.Add(hfModel);
                    }

                    Debug.WriteLine($"Loaded {availableModels.Count} available models from persisted data.");
                }

                // If no models were loaded from persistence, add some defaults as fallback
                if (availableModels.Count == 0)
                {
                    Debug.WriteLine("No persisted models found. Adding default examples.");
                    AddDefaultModelExamples(availableModels);
                }

                // Verify NetPageViewModel still has the models needed for execution
                if (netPageVM?.AvailableModels?.Count > 0)
                {
                    Debug.WriteLine($"LoadAvailableModelsAsync: Confirmed NetPageViewModel has {netPageVM.AvailableModels.Count} execution-ready models");
                }
                else
                {
                    Debug.WriteLine("LoadAvailableModelsAsync: WARNING - NetPageViewModel has no execution-ready models!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading models: {ex.Message}");
                // Fall back to default examples if loading fails
                availableModels.Clear();
                AddDefaultModelExamples(availableModels);
            }

            // Add input nodes to the AvailableModels list
            AddDefaultInputNodesToAvailableModels(availableModels);
        }

        public void AddDefaultInputNodesToAvailableModels(ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels)
        {
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "webcam_image", ModelId = "Webcam Image (Input)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "screen_image", ModelId = "Screen Image (Input)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "pc_audio", ModelId = "PC Audio (Input)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "webcam_audio", ModelId = "Webcam Audio (Input)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "keyboard_text", ModelId = "Keyboard Text (Input)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "mouse_text", ModelId = "Mouse Text (Input)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "goals_node", ModelId = "Goals (File)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "memory_node", ModelId = "Memory (File)" });
        }

        public void AddDefaultModelExamples(ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels)
        {
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "gpt2", ModelId = "Text Generator (GPT-2)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "resnet-50", ModelId = "Image Classifier (ResNet)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "openai/whisper-base", ModelId = "Audio Recognizer (Whisper)" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "deepseek-ai/deepseek-coder-1.3b-instruct", ModelId = "DeepSeek Coder" });
            availableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "meta-llama/Meta-Llama-3-8B-Instruct", ModelId = "Llama 3 8B Instruct" });
        }

        public async Task AddModelNodeAsync(
            ObservableCollection<CSimple.Models.HuggingFaceModel> availableModels,
            ObservableCollection<NodeViewModel> nodes,
            CSimple.Models.HuggingFaceModel model,
            NodeManagementService nodeManagementService,
            Func<string, string, string, Task> showAlert,
            Action invalidatePipelineStateCache,
            Action updateEnsembleCounts,
            Action updateRunAllModelsCommandCanExecute,
            Action updateRunAllNodesCommandCanExecute,
            Func<Task> saveCurrentPipelineAsync,
            Action updateExecutionStatusFromPipeline)
        {
            if (model == null)
            {
                await showAlert?.Invoke("Error", "No model selected.", "OK");
                return;
            }

            // Improve model node creation with HuggingFace info
            var modelId = model.ModelId ?? model.Id;
            var modelType = nodeManagementService.InferNodeTypeFromName(modelId);
            var modelName = nodeManagementService.GetFriendlyModelName(modelId);

            // Generate a reasonable position for the new node
            // Find a vacant spot in the middle area of the canvas
            float x = 300 + (nodes.Count % 3) * 180;
            float y = 200 + (nodes.Count / 3) * 100;

            // Use the NodeManagementService to add the node
            await nodeManagementService.AddModelNodeAsync(nodes, model.Id, modelName, modelType, new PointF(x, y));

            // Execute all the callbacks to maintain state consistency
            invalidatePipelineStateCache?.Invoke(); // Invalidate cache when structure changes
            updateEnsembleCounts?.Invoke(); // Update counts after adding node

            Debug.WriteLine($"🔄 [AddModelNode] Updating RunAllModelsCommand CanExecute - Model nodes count: {nodes.Count(n => n.Type == NodeType.Model)}");
            updateRunAllModelsCommandCanExecute?.Invoke(); // Update Run All Models button state
            updateRunAllNodesCommandCanExecute?.Invoke(); // Update Run All Nodes button state

            await saveCurrentPipelineAsync?.Invoke(); // Save after adding

            // Update execution status
            updateExecutionStatusFromPipeline?.Invoke();
        }
    }
}
