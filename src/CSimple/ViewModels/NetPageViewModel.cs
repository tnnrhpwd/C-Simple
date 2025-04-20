using CSimple.Models;
using CSimple.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSimple.ViewModels
{
    public class NetPageViewModel : INotifyPropertyChanged
    {
        private readonly FileService _fileService;
        private readonly HuggingFaceService _huggingFaceService;
        // Consider injecting navigation and dialog services for better testability

        // --- Backing Fields ---
        private bool _isGeneralModeActive = true;
        private bool _isSpecificModeActive = false;
        private string _currentModelStatus = "Idle";
        private string _lastModelOutput = "No recent outputs";
        private bool _isLoading = false;
        private bool _isModelCommunicating = false;
        private string _huggingFaceSearchQuery = "";
        private string _selectedHuggingFaceCategory = "All Categories"; // Default value

        // --- Observable Properties ---
        public ObservableCollection<NeuralNetworkModel> AvailableModels { get; } = new();
        public ObservableCollection<NeuralNetworkModel> ActiveModels { get; } = new();
        public ObservableCollection<SpecificGoal> AvailableGoals { get; } = new();

        public bool IsGeneralModeActive
        {
            get => _isGeneralModeActive;
            set => SetProperty(ref _isGeneralModeActive, value, onChanged: EnsureConsistentModeState);
        }
        public bool IsSpecificModeActive
        {
            get => _isSpecificModeActive;
            set => SetProperty(ref _isSpecificModeActive, value, onChanged: EnsureConsistentModeState);
        }
        public string CurrentModelStatus
        {
            get => _currentModelStatus;
            set => SetProperty(ref _currentModelStatus, value);
        }
        public string LastModelOutput
        {
            get => _lastModelOutput;
            set => SetProperty(ref _lastModelOutput, value);
        }
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        public bool IsModelCommunicating
        {
            get => _isModelCommunicating;
            set => SetProperty(ref _isModelCommunicating, value);
        }
        public int ActiveModelsCount => ActiveModels.Count;

        public string HuggingFaceSearchQuery
        {
            get => _huggingFaceSearchQuery;
            set => SetProperty(ref _huggingFaceSearchQuery, value);
        }

        public string SelectedHuggingFaceCategory
        {
            get => _selectedHuggingFaceCategory;
            set => SetProperty(ref _selectedHuggingFaceCategory, value);
        }

        public List<string> HuggingFaceCategories { get; }

        // --- Commands ---
        public ICommand ToggleGeneralModeCommand { get; }
        public ICommand ToggleSpecificModeCommand { get; }
        public ICommand ActivateModelCommand { get; }
        public ICommand DeactivateModelCommand { get; }
        public ICommand LoadSpecificGoalCommand { get; }
        public ICommand ShareModelCommand { get; }
        public ICommand CommunicateWithModelCommand { get; }
        public ICommand ExportModelCommand { get; }
        public ICommand ImportModelCommand { get; } // Triggered by View
        public ICommand ManageTrainingCommand { get; } // Triggered by View
        public ICommand ViewModelPerformanceCommand { get; }
        public ICommand HuggingFaceSearchCommand { get; } // Triggered by View
        public ICommand ImportFromHuggingFaceCommand { get; } // Triggered by View

        // --- Constructor ---
        public NetPageViewModel(FileService fileService)
        {
            _fileService = fileService;
            _huggingFaceService = new HuggingFaceService(); // Instantiate HF service

            // Initialize Commands
            ToggleGeneralModeCommand = new Command(ToggleGeneralMode);
            ToggleSpecificModeCommand = new Command(ToggleSpecificMode);
            ActivateModelCommand = new Command<NeuralNetworkModel>(ActivateModel);
            DeactivateModelCommand = new Command<NeuralNetworkModel>(DeactivateModel);
            LoadSpecificGoalCommand = new Command<SpecificGoal>(LoadSpecificGoal);
            ShareModelCommand = new Command<NeuralNetworkModel>(ShareModel);
            CommunicateWithModelCommand = new Command<string>(CommunicateWithModel);
            ExportModelCommand = new Command<NeuralNetworkModel>(async (model) => await ExportModel(model)); // Wrapper for async
            ImportModelCommand = new Command(async () => await ImportModelAsync()); // Wrapper for async
            ManageTrainingCommand = new Command(async () => await ManageTrainingAsync()); // Wrapper for async
            ViewModelPerformanceCommand = new Command(ViewModelPerformance);
            HuggingFaceSearchCommand = new Command(async () => await SearchHuggingFaceAsync()); // Wrapper for async
            ImportFromHuggingFaceCommand = new Command(async () => await ImportDirectFromHuggingFaceAsync()); // Wrapper for async

            // Populate categories
            HuggingFaceCategories = new List<string> { "All Categories" };
            HuggingFaceCategories.AddRange(_huggingFaceService.GetModelCategoryFilters().Keys);

            // Load initial data
            // Note: Loading is triggered by OnAppearing in the View
        }

        // --- Public Methods (called from View or Commands) ---

        public async Task LoadDataAsync()
        {
            await LoadPersistedModelsAsync();
            LoadSampleGoals(); // Load sample goals separately
            SubscribeToInputNotifications(); // Start background simulation
        }

        public async Task SearchHuggingFaceAsync()
        {
            try
            {
                CurrentModelStatus = "Searching HuggingFace models...";
                IsLoading = true;

                string query = HuggingFaceSearchQuery?.Trim() ?? "";
                string categoryFilter = null;

                // Get selected category filter
                if (SelectedHuggingFaceCategory != "All Categories")
                {
                    var categoryFilters = _huggingFaceService.GetModelCategoryFilters();
                    if (categoryFilters.ContainsKey(SelectedHuggingFaceCategory))
                    {
                        categoryFilter = categoryFilters[SelectedHuggingFaceCategory];
                    }
                }

                var searchResults = await _huggingFaceService.SearchModelsAsync(query, categoryFilter, 10);

                if (searchResults.Count > 0)
                {
                    // Let the View handle the selection UI
                    var selectedModel = await ShowModelSelectionDialog(searchResults);
                    if (selectedModel != null)
                    {
                        await ShowModelDetailsAndImportAsync(selectedModel);
                    }
                    else
                    {
                        CurrentModelStatus = "Model selection canceled";
                    }
                }
                else
                {
                    await ShowAlert("No Results", "No models found matching your search criteria.", "OK");
                    CurrentModelStatus = "No HuggingFace models found";
                }
            }
            catch (Exception ex)
            {
                HandleError("Error searching HuggingFace", ex);
                await ShowAlert("Search Error", $"Failed to search HuggingFace: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ImportDirectFromHuggingFaceAsync()
        {
            try
            {
                string modelId = await ShowPrompt("Import HuggingFace Model", "Enter the model ID (e.g., 'openai/whisper-small')", "Import", "Cancel", "");

                if (string.IsNullOrEmpty(modelId)) return;

                CurrentModelStatus = $"Getting details for {modelId}...";
                IsLoading = true;

                var modelDetails = await _huggingFaceService.GetModelDetailsAsync(modelId);

                if (modelDetails != null)
                {
                    await ShowModelDetailsAndImportAsync(modelDetails);
                }
                else
                {
                    await ShowAlert("Model Not Found", $"Could not find model with ID: {modelId}", "OK");
                    CurrentModelStatus = "Model not found";
                }
            }
            catch (Exception ex)
            {
                HandleError("Error importing from HuggingFace", ex);
                await ShowAlert("Import Error", $"Failed to import model: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ImportModelAsync()
        {
            Debug.WriteLine("ViewModel: Import Model triggered");
            try
            {
                CurrentModelStatus = "Opening file picker...";
                var fileResult = await PickFile(); // Delegate file picking to the view/platform

                if (fileResult == null)
                {
                    Debug.WriteLine("File selection canceled");
                    CurrentModelStatus = "Model import canceled";
                    return;
                }

                Debug.WriteLine($"File selected: {fileResult.FileName}");
                IsLoading = true;
                await ProcessSelectedModelFile(fileResult);
            }
            catch (Exception ex)
            {
                HandleError("Error during model import", ex);
                await ShowAlert("Import Error", $"An error occurred during import: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // --- Command Implementations ---

        private void ToggleGeneralMode()
        {
            // Logic moved from NetPage.xaml.cs
            try
            {
                IsGeneralModeActive = !IsGeneralModeActive; // This will trigger EnsureConsistentModeState via setter
                CurrentModelStatus = IsGeneralModeActive ? "General mode activated" : "General mode deactivated";

                if (!IsGeneralModeActive)
                {
                    DeactivateModelsOfType(ModelType.General);
                }
                Debug.WriteLine($"ToggleGeneralMode: State is now {IsGeneralModeActive}");
            }
            catch (Exception ex)
            {
                HandleError("Error toggling general mode", ex);
                // Consider reverting state if needed
            }
        }

        private void ToggleSpecificMode()
        {
            // Logic moved from NetPage.xaml.cs
            try
            {
                IsSpecificModeActive = !IsSpecificModeActive; // This will trigger EnsureConsistentModeState via setter
                CurrentModelStatus = IsSpecificModeActive ? "Specific mode activated" : "Specific mode deactivated";

                if (!IsSpecificModeActive)
                {
                    DeactivateModelsOfType(ModelType.GoalSpecific);
                }
                else
                {
                    // Ensure goals are loaded if switching to specific mode
                    if (AvailableGoals.Count == 0)
                    {
                        LoadSampleGoals(); // Or load persisted goals
                    }
                }
                Debug.WriteLine($"ToggleSpecificMode: State is now {IsSpecificModeActive}");
            }
            catch (Exception ex)
            {
                HandleError("Error toggling specific mode", ex);
                // Consider reverting state if needed
            }
        }

        private void ActivateModel(NeuralNetworkModel model)
        {
            // Logic moved from NetPage.xaml.cs
            if (model == null) return;
            try
            {
                if ((model.Type == ModelType.General && !IsGeneralModeActive) ||
                    (model.Type == ModelType.GoalSpecific && !IsSpecificModeActive))
                {
                    CurrentModelStatus = $"Cannot activate {model.Name}: Incompatible mode";
                    return;
                }

                if (!ActiveModels.Any(m => m?.Id == model.Id))
                {
                    ActiveModels.Add(model);
                    model.IsActive = true; // Update model state
                    CurrentModelStatus = $"Model '{model.Name}' activated";
                    StartModelMonitoring(model); // Simulate starting monitoring
                    OnPropertyChanged(nameof(ActiveModelsCount)); // Notify count changed
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error activating model: {model?.Name}", ex);
            }
        }

        private void DeactivateModel(NeuralNetworkModel model)
        {
            // Logic moved from NetPage.xaml.cs
            if (model == null) return;
            try
            {
                var modelToRemove = ActiveModels.FirstOrDefault(m => m?.Id == model.Id);
                if (modelToRemove != null)
                {
                    StopModelMonitoring(modelToRemove); // Simulate stopping monitoring
                    ActiveModels.Remove(modelToRemove);
                    modelToRemove.IsActive = false; // Update model state
                    CurrentModelStatus = $"Model '{model.Name}' deactivated";
                    OnPropertyChanged(nameof(ActiveModelsCount)); // Notify count changed
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error deactivating model: {model?.Name}", ex);
            }
        }

        private void LoadSpecificGoal(SpecificGoal goal)
        {
            // Logic moved from NetPage.xaml.cs
            if (goal == null) return;
            try
            {
                var goalModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = $"Goal: {goal.Name}",
                    Description = goal.Description,
                    Type = ModelType.GoalSpecific,
                    AssociatedGoalId = goal.Id
                };

                if (!AvailableModels.Any(m => m.Name == goalModel.Name)) // Avoid duplicates
                {
                    AvailableModels.Add(goalModel);
                }

                if (IsSpecificModeActive)
                {
                    ActivateModel(goalModel);
                }
                CurrentModelStatus = $"Loaded goal '{goal.Name}'";
            }
            catch (Exception ex)
            {
                HandleError($"Error loading goal: {goal?.Name}", ex);
            }
        }

        private void ShareModel(NeuralNetworkModel model)
        {
            // Logic moved from NetPage.xaml.cs
            if (model == null) return;
            try
            {
                var shareCode = $"SHARE-{model.Id.Substring(0, 8)}";
                CurrentModelStatus = $"Model '{model.Name}' shared with code: {shareCode}";
                // In real app, call sharing service
            }
            catch (Exception ex)
            {
                HandleError($"Error sharing model: {model?.Name}", ex);
            }
        }

        private void CommunicateWithModel(string message)
        {
            // Logic moved from NetPage.xaml.cs
            if (string.IsNullOrEmpty(message) || ActiveModels.Count == 0)
            {
                CurrentModelStatus = ActiveModels.Count == 0 ? "No active models to communicate with" : CurrentModelStatus;
                return;
            }
            try
            {
                LastModelOutput = $"Response to '{message}': Processing your request...";
                IsModelCommunicating = true; // Simulate start

                // Simulate async response
                Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ =>
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        LastModelOutput = $"Response to '{message}': I'll assist with that task right away.";
                        IsModelCommunicating = false; // Simulate end
                    });
                });
            }
            catch (Exception ex)
            {
                HandleError("Error communicating with model", ex);
                IsModelCommunicating = false;
            }
        }

        private async Task ExportModel(NeuralNetworkModel model)
        {
            // Logic moved from NetPage.xaml.cs
            if (model == null) return;
            CurrentModelStatus = $"Exporting model '{model.Name}' for sharing...";
            IsLoading = true;
            try
            {
                await Task.Delay(1000); // Simulate export
                CurrentModelStatus = $"Model '{model.Name}' exported successfully";
                await ShowAlert("Export Successful", $"Model '{model.Name}' has been exported.", "OK");
            }
            catch (Exception ex)
            {
                HandleError($"Error exporting model {model.Name}", ex);
            }
            finally { IsLoading = false; }
        }

        private async Task ManageTrainingAsync()
        {
            // Logic moved from NetPage.xaml.cs
            await ShowAlert("Manage Training Data", "This would open an interface to manage training data.", "OK");
            // Navigate via service or message
            await NavigateTo("///orient");
        }

        private void ViewModelPerformance()
        {
            // Logic moved from NetPage.xaml.cs
            if (ActiveModels.Count == 0)
            {
                ShowAlert("No Active Models", "Activate a model to view performance.", "OK");
                return;
            }
            ShowAlert("Model Performance", "Simulated performance data: 92.7% accuracy, 12% CPU.", "OK");
        }

        // --- Private Helper Methods ---

        private async Task LoadPersistedModelsAsync()
        {
            // Logic moved from NetPage.xaml.cs
            try
            {
                IsLoading = true;
                Debug.WriteLine("ViewModel: Loading persisted HuggingFace models...");
                var loadedModels = await _fileService.LoadHuggingFaceModelsAsync();

                // Filter out duplicate Python references based on HuggingFaceModelId
                var uniquePythonRefs = new Dictionary<string, NeuralNetworkModel>();
                var otherModels = new List<NeuralNetworkModel>();
                bool duplicatesFound = false;

                if (loadedModels != null)
                {
                    foreach (var model in loadedModels)
                    {
                        if (model.IsHuggingFaceReference && !string.IsNullOrEmpty(model.HuggingFaceModelId))
                        {
                            if (!uniquePythonRefs.ContainsKey(model.HuggingFaceModelId))
                            {
                                uniquePythonRefs.Add(model.HuggingFaceModelId, model);
                            }
                            else
                            {
                                duplicatesFound = true; // Mark that we found duplicates
                                Debug.WriteLine($"ViewModel: Duplicate Python reference found and removed for HuggingFaceModelId: {model.HuggingFaceModelId}");
                            }
                        }
                        else
                        {
                            otherModels.Add(model);
                        }
                    }
                }

                var cleanedModels = otherModels.Concat(uniquePythonRefs.Values).ToList();

                // Use dispatcher if modifying ObservableCollection from non-UI thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Clear existing HF models before loading
                    var hfModelsToRemove = AvailableModels.Where(m => m.IsHuggingFaceReference || !string.IsNullOrEmpty(m.HuggingFaceModelId)).ToList();
                    foreach (var model in hfModelsToRemove)
                    {
                        AvailableModels.Remove(model);
                    }

                    if (cleanedModels.Count > 0)
                    {
                        foreach (var model in cleanedModels)
                        {
                            // Double-check uniqueness within AvailableModels before adding
                            if (!AvailableModels.Any(m => m.Id == model.Id ||
                                (m.IsHuggingFaceReference && m.HuggingFaceModelId == model.HuggingFaceModelId) ||
                                (!m.IsHuggingFaceReference && m.Name == model.Name))) // Check name for non-refs
                            {
                                AvailableModels.Add(model);
                            }
                        }
                        Debug.WriteLine($"ViewModel: Loaded {cleanedModels.Count} unique persisted HuggingFace models.");
                    }
                    else
                    {
                        Debug.WriteLine("ViewModel: No persisted HuggingFace models found or loaded.");
                    }
                });

                // If duplicates were found, save the cleaned list back to the file
                if (duplicatesFound)
                {
                    Debug.WriteLine("ViewModel: Saving cleaned model list back to file due to duplicates found.");
                    await SavePersistedModelsAsync(cleanedModels); // Pass the cleaned list
                }
            }
            catch (Exception ex)
            {
                HandleError("Error loading persisted models", ex);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadSampleGoals()
        {
            // Logic moved from NetPage.xaml.cs
            MainThread.BeginInvokeOnMainThread(() =>
            {
                AvailableGoals.Clear();
                AvailableGoals.Add(new SpecificGoal { Id = Guid.NewGuid().ToString(), Name = "Create Weekly Sales Report", Description = "Generates sales reports", Category = "Business", DownloadCount = 1240 });
                AvailableGoals.Add(new SpecificGoal { Id = Guid.NewGuid().ToString(), Name = "Email Processor", Description = "Organizes inbox", Category = "Productivity", DownloadCount = 875 });
                AvailableGoals.Add(new SpecificGoal { Id = Guid.NewGuid().ToString(), Name = "Meeting Scheduler", Description = "Handles scheduling", Category = "Collaboration", DownloadCount = 653 });
            });
        }

        private void EnsureConsistentModeState()
        {
            // Logic moved from NetPage.xaml.cs
            if (IsSpecificModeActive && IsGeneralModeActive)
            {
                // Prioritize the mode that was just set to true
                // This logic might need refinement based on exact desired behavior
                // For now, assume Specific takes precedence if both are somehow true
                // _isGeneralModeActive = false; // Modify backing field directly to avoid loop
                // OnPropertyChanged(nameof(IsGeneralModeActive));
                Debug.WriteLine("Warning: Both modes were active, check toggle logic.");
            }
        }

        private void DeactivateModelsOfType(ModelType type)
        {
            try
            {
                var modelsToDeactivate = ActiveModels.Where(m => m?.Type == type).ToList();
                Debug.WriteLine($"Deactivating {modelsToDeactivate.Count} models of type {type}");
                foreach (var model in modelsToDeactivate)
                {
                    DeactivateModel(model);
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error deactivating models of type {type}", ex);
            }
        }

        private async Task ShowModelDetailsAndImportAsync(HuggingFaceModel model)
        {
            // Logic moved from NetPage.xaml.cs
            try
            {
                bool importConfirmed = await ShowConfirmation("Model Details",
                    $"Name: {model.ModelId ?? model.Id}\nAuthor: {model.Author}\nType: {model.Pipeline_tag}\nDownloads: {model.Downloads}\n\nImport this model?",
                    "Import", "Cancel");

                if (!importConfirmed)
                {
                    CurrentModelStatus = "Import canceled";
                    return;
                }

                CurrentModelStatus = $"Preparing to import {model.ModelId ?? model.Id}...";
                IsLoading = true;

                HuggingFaceModelDetails modelDetails = model as HuggingFaceModelDetails ?? await _huggingFaceService.GetModelDetailsAsync(model.ModelId ?? model.Id);
                List<string> filesToDownload = new List<string>();
                bool isPythonReference = false;

                if (modelDetails == null || modelDetails.Files == null || modelDetails.Files.Count == 0)
                {
                    string option = await ShowActionSheet("Model Import Options", "Cancel", null, new[] { "Enter File Name Manually", "Import Model Configuration", "Use Python Script" });
                    switch (option)
                    {
                        case "Enter File Name Manually":
                            string filename = await ShowPrompt("Model File", "Enter file name (e.g., 'pytorch_model.bin')", "OK", "Cancel", "pytorch_model.bin");
                            if (!string.IsNullOrEmpty(filename)) filesToDownload.Add(filename);
                            break;
                        case "Import Model Configuration":
                            filesToDownload.Add("config.json");
                            await ShowAlert("Using Python transformers", $"Download config only. Use Python:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\")", "OK");
                            break;
                        case "Use Python Script":
                            isPythonReference = true;
                            await ShowAlert("Python Code", $"Use:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\", trust_remote_code=True)", "OK");
                            // Save reference info locally (optional, could just add to VM list)
                            await SavePythonReferenceInfo(model);
                            break;
                        default: CurrentModelStatus = "Import canceled"; IsLoading = false; return;
                    }
                }
                else
                {
                    var recommendedFiles = GetRecommendedFiles(modelDetails.Files);
                    if (recommendedFiles.Count > 0)
                    {
                        string option = await ShowActionSheet("Select File to Import", "Cancel", "Download ALL Recommended", recommendedFiles.ToArray());
                        if (option == "Download ALL Recommended") filesToDownload.AddRange(recommendedFiles);
                        else if (option != "Cancel" && !string.IsNullOrEmpty(option))
                        {
                            filesToDownload.Add(option);
                            // Optionally ask about config/tokenizer
                        }
                        else { CurrentModelStatus = "Import canceled"; IsLoading = false; return; }
                    }
                    else
                    {
                        string selectedFile = await ShowActionSheet("Select File to Import", "Cancel", null, modelDetails.Files.ToArray());
                        if (selectedFile != "Cancel" && !string.IsNullOrEmpty(selectedFile)) filesToDownload.Add(selectedFile);
                        else { CurrentModelStatus = "Import canceled"; IsLoading = false; return; }
                    }
                }

                // Handle Python Reference Case
                if (isPythonReference)
                {
                    // Check if a Python reference with this HuggingFaceModelId already exists
                    if (AvailableModels.Any(m => m.IsHuggingFaceReference && m.HuggingFaceModelId == (model.ModelId ?? model.Id)))
                    {
                        CurrentModelStatus = $"Python reference for '{model.ModelId ?? model.Id}' already exists.";
                        await ShowAlert("Duplicate Reference", $"A Python reference for this model ID already exists.", "OK");
                        IsLoading = false;
                        return; // Stop processing if duplicate
                    }

                    var pythonReferenceModel = new NeuralNetworkModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = GetFriendlyModelName(model.ModelId ?? model.Id) + " (Python Ref)",
                        Description = model.Description ?? "Imported from HuggingFace (requires Python)",
                        Type = model.RecommendedModelType,
                        IsHuggingFaceReference = true,
                        HuggingFaceModelId = model.ModelId ?? model.Id
                    };

                    // Add the unique reference
                    AvailableModels.Add(pythonReferenceModel);
                    CurrentModelStatus = $"Added reference to {pythonReferenceModel.Name}";
                    await SavePersistedModelsAsync(); // Save the updated list

                    await ShowAlert("Reference Added", $"Reference to '{pythonReferenceModel.Name}' added.", "OK");
                    IsLoading = false;
                    return;
                }

                // Handle File Download Case
                if (filesToDownload.Count == 0)
                {
                    CurrentModelStatus = "No files selected"; IsLoading = false;
                    await ShowAlert("Import Error", "No files selected for download", "OK");
                    return;
                }

                // Show Python usage info before download
                await ShowAlert("Model Usage Information", $"Use in Python:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\")\n\nFor .NET, consider ONNX export.", "Continue Download");

                // Download files
                string modelDirectory = GetModelDirectoryPath(model.ModelId ?? model.Id);
                bool anyDownloadSucceeded = false;
                foreach (var file in filesToDownload)
                {
                    CurrentModelStatus = $"Downloading {file}...";
                    string destinationPath = Path.Combine(modelDirectory, Path.GetFileName(file));
                    bool downloadSuccess = await _huggingFaceService.DownloadModelFileAsync(model.ModelId ?? model.Id, file, destinationPath);
                    if (downloadSuccess) anyDownloadSucceeded = true;
                    else await ShowAlert("Download Notice", $"Could not download {file}", "Continue");
                }

                if (!anyDownloadSucceeded)
                {
                    CurrentModelStatus = "All downloads failed"; IsLoading = false;
                    await ShowAlert("Import Failed", "Could not download any model files", "OK");
                    return;
                }

                // Add downloaded model to list (ensure name uniqueness for non-refs)
                var importedModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = GetFriendlyModelName(model.ModelId ?? model.Id),
                    Description = model.Description ?? "Imported from HuggingFace",
                    Type = model.RecommendedModelType,
                    IsHuggingFaceReference = false, // This is a downloaded model, not just a reference
                    HuggingFaceModelId = model.ModelId ?? model.Id
                };
                if (!AvailableModels.Any(m => m.Name == importedModel.Name)) // Check name for non-refs
                {
                    AvailableModels.Add(importedModel);
                    CurrentModelStatus = $"Successfully imported {importedModel.Name}";
                    await SavePersistedModelsAsync(); // Save updated list
                }
                else { CurrentModelStatus = $"Model {importedModel.Name} already exists."; }

                await ShowAlert("Import Success", $"Model '{importedModel.Name}' imported.\nSaved in: {modelDirectory}", "OK");
            }
            catch (Exception ex)
            {
                HandleError("Error handling model details", ex);
                await ShowAlert("Import Error", $"Failed to import model: {ex.Message}", "OK");
            }
            finally { IsLoading = false; }
        }

        private async Task ProcessSelectedModelFile(FileResult fileResult)
        {
            // Logic moved from NetPage.xaml.cs
            try
            {
                await ShowAlert("File Selected", $"Name: {fileResult.FileName}", "Continue");

                var modelDestinationPath = await CopyModelToAppDirectoryAsync(fileResult);
                if (string.IsNullOrEmpty(modelDestinationPath))
                {
                    CurrentModelStatus = "Failed to copy model file"; return;
                }

                var modelTypeResult = await ShowActionSheet("Select Model Type", "Cancel", null, new[] { "General", "Input Specific", "Goal Specific" });
                if (modelTypeResult == "Cancel" || string.IsNullOrEmpty(modelTypeResult))
                {
                    CurrentModelStatus = "Import canceled - no type selected"; return;
                }

                ModelType modelType = Enum.TryParse(modelTypeResult.Replace(" ", ""), true, out ModelType parsedType) ? parsedType : ModelType.General;

                var modelName = Path.GetFileNameWithoutExtension(fileResult.FileName);
                var importedModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = modelName,
                    Description = $"Imported from {fileResult.FileName}",
                    Type = modelType
                };

                if (!AvailableModels.Any(m => m.Name == importedModel.Name))
                {
                    AvailableModels.Add(importedModel);
                    CurrentModelStatus = $"Model '{importedModel.Name}' imported successfully";
                    // Optionally save persisted models if local import should persist
                    // await SavePersistedModelsAsync();
                }
                else { CurrentModelStatus = $"Model {importedModel.Name} already exists."; }

                await ShowAlert("Import Success", $"Model '{importedModel.Name}' imported.", "OK");
            }
            catch (Exception ex)
            {
                HandleError("Error processing selected file", ex);
                await ShowAlert("Import Failed", $"Error processing model file: {ex.Message}", "OK");
            }
        }

        private async Task SavePersistedModelsAsync(List<NeuralNetworkModel> modelsToSave)
        {
            try
            {
                Debug.WriteLine($"ViewModel: SavePersistedModelsAsync (specific list) starting with {modelsToSave?.Count ?? 0} models.");

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
                                uniquePythonRefs.Add(model.HuggingFaceModelId, model);
                            }
                            // else: Skip duplicate
                        }
                        else
                        {
                            otherModels.Add(model);
                        }
                    }
                }
                var finalModelsToSave = otherModels.Concat(uniquePythonRefs.Values).ToList();

                Debug.WriteLine($"ViewModel: Saving {finalModelsToSave.Count} unique models.");
                await _fileService.SaveHuggingFaceModelsAsync(finalModelsToSave);
                Debug.WriteLine($"ViewModel: Called FileService to save {finalModelsToSave.Count} models.");
            }
            catch (Exception ex) { HandleError("Error saving specific list of persisted models", ex); }
        }

        // Original method now calls the overload
        private async Task SavePersistedModelsAsync()
        {
            // Logic moved from NetPage.xaml.cs
            Debug.WriteLine("ViewModel: SavePersistedModelsAsync (default) starting.");
            var modelsToSave = AvailableModels
                .Where(m => m.IsHuggingFaceReference || !string.IsNullOrEmpty(m.HuggingFaceModelId))
                .ToList();
            await SavePersistedModelsAsync(modelsToSave); // Call the overload
        }

        private List<string> GetRecommendedFiles(List<string> files)
        {
            // Logic moved from NetPage.xaml.cs (simplified)
            var priorityExtensions = new[] { ".bin", ".safetensors", ".onnx", ".gguf", ".pt", ".model" };
            var result = files.Where(f => priorityExtensions.Any(e => f.EndsWith(e))).ToList();
            if (result.Count == 0) result = files.Where(f => f.EndsWith(".json")).ToList();
            return result.OrderBy(f => f.Length).Take(5).ToList();
        }

        private string GetFriendlyModelName(string modelId)
        {
            // Logic moved from NetPage.xaml.cs
            var name = modelId.Contains('/') ? modelId.Split('/').Last() : modelId;
            name = name.Replace("-", " ").Replace("_", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
        }

        private string GetModelDirectoryPath(string modelId)
        {
            string safeModelId = (modelId ?? "unknown_model").Replace("/", "_").Replace("\\", "_");
            var modelDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models", "HuggingFace", safeModelId);
            Directory.CreateDirectory(modelDirectory); // Ensure it exists
            return modelDirectory;
        }

        private async Task SavePythonReferenceInfo(HuggingFaceModel model)
        {
            try
            {
                var infoDirectory = GetModelDirectoryPath(model.ModelId ?? model.Id);
                string infoContent = $"Model ID: {model.ModelId ?? model.Id}\nAuthor: {model.Author}\nType: {model.Pipeline_tag}\nPython:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\", trust_remote_code=True)";
                await File.WriteAllTextAsync(Path.Combine(infoDirectory, "model_info.txt"), infoContent);
                Debug.WriteLine($"Saved Python reference info for {model.ModelId}");
            }
            catch (Exception ex) { HandleError("Error saving Python reference info", ex); }
        }

        private async Task<string> CopyModelToAppDirectoryAsync(FileResult fileResult)
        {
            // Logic moved from NetPage.xaml.cs
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
                HandleError("Error copying model file", ex);
                await ShowAlert("Copy Error", $"Failed to copy model file: {ex.Message}", "OK");
                return null;
            }
        }

        private string EnsureUniqueFileName(string directory, string fileName)
        {
            // Logic moved from NetPage.xaml.cs
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

        private void SubscribeToInputNotifications()
        {
            // Logic moved from NetPage.xaml.cs (Simulated activity)
            var timer = new System.Threading.Timer(_ =>
            {
                if (ActiveModels.Count > 0 && IsGeneralModeActive)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        IsModelCommunicating = true;
                        var messages = new[] { "Detected pattern, suggesting action...", "Analyzing input...", "Processing..." };
                        LastModelOutput = messages[new Random().Next(messages.Length)];
                        Task.Delay(3000).ContinueWith(__ => MainThread.BeginInvokeOnMainThread(() => IsModelCommunicating = false));
                    });
                }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
        }

        private void StartModelMonitoring(NeuralNetworkModel model) => Debug.WriteLine($"VM: Starting monitoring for {model.Name}");
        private void StopModelMonitoring(NeuralNetworkModel model) => Debug.WriteLine($"VM: Stopping monitoring for {model.Name}");
        private void HandleError(string context, Exception ex)
        {
            Debug.WriteLine($"ViewModel Error - {context}: {ex.Message}\n{ex.StackTrace}");
            CurrentModelStatus = $"Error: {context}";
        }

        // --- UI Interaction Abstractions (to be implemented by the View) ---
        // These allow the ViewModel to request UI actions without directly referencing UI elements.
        public Func<string, string, string, Task> ShowAlert { get; set; } = async (t, m, c) => { await Task.CompletedTask; }; // Default no-op
        public Func<string, string, string, string, Task<bool>> ShowConfirmation { get; set; } = async (t, m, a, c) => { await Task.CompletedTask; return false; }; // Default no-op
        public Func<string, string, string, string[], Task<string>> ShowActionSheet { get; set; } = async (t, c, d, b) => { await Task.CompletedTask; return c; }; // Default returns cancel
        public Func<string, string, string, string, string, Task<string>> ShowPrompt { get; set; } = async (t, m, a, c, iv) => { await Task.CompletedTask; return null; }; // Default no-op
        public Func<Task<FileResult>> PickFile { get; set; } = async () => { await Task.CompletedTask; return null; }; // Default no-op
        public Func<string, Task> NavigateTo { get; set; } = async (r) => { await Task.CompletedTask; }; // Default no-op
        public Func<List<HuggingFaceModel>, Task<HuggingFaceModel>> ShowModelSelectionDialog { get; set; } = async (m) => { await Task.CompletedTask; return null; }; // Default no-op


        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            // Also notify dependent properties like ActiveModelsCount
            if (propertyName == nameof(ActiveModels)) OnPropertyChanged(nameof(ActiveModelsCount));
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
