using CSimple.Models;
using CSimple.Services;
using CSimple.Services.AppModeService;
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
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSimple.ViewModels
{
    public class NetPageViewModel : INotifyPropertyChanged
    {
        private readonly FileService _fileService;
        private readonly HuggingFaceService _huggingFaceService;
        private readonly PythonBootstrapper _pythonBootstrapper; // Changed from PythonDependencyManager
        private readonly AppModeService _appModeService; // Add offline mode service
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

        // Declare the missing fields
        private string _pythonExecutablePath = "python"; // Default value
        private string _huggingFaceScriptPath = string.Empty; // Default value        // Add these new properties
        private bool _useFallbackScript = false;
        private string _fallbackScriptPath;        // Chat-related backing fields
        private string _currentMessage = string.Empty;
        private bool _isAiTyping = false;

        // Media input backing fields
        private string _selectedImagePath = null;
        private string _selectedImageName = null;
        private string _selectedAudioPath = null;
        private string _selectedAudioName = null;

        // --- Observable Properties ---
        public ObservableCollection<NeuralNetworkModel> AvailableModels { get; } = new();
        public ObservableCollection<NeuralNetworkModel> ActiveModels { get; } = new();
        public ObservableCollection<SpecificGoal> AvailableGoals { get; } = new();
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

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

        // Chat-related properties
        public string CurrentMessage
        {
            get => _currentMessage;
            set => SetProperty(ref _currentMessage, value);
        }

        public bool IsAiTyping
        {
            get => _isAiTyping;
            set => SetProperty(ref _isAiTyping, value);
        }

        public bool CanSendMessage => !string.IsNullOrWhiteSpace(CurrentMessage) && !IsAiTyping && ActiveModelsCount > 0;

        // Media input properties
        public string SelectedImagePath
        {
            get => _selectedImagePath;
            set => SetProperty(ref _selectedImagePath, value);
        }

        public string SelectedImageName
        {
            get => _selectedImageName;
            set => SetProperty(ref _selectedImageName, value);
        }

        public string SelectedAudioPath
        {
            get => _selectedAudioPath;
            set => SetProperty(ref _selectedAudioPath, value);
        }

        public string SelectedAudioName
        {
            get => _selectedAudioName;
            set => SetProperty(ref _selectedAudioName, value);
        }

        // Computed properties for UI state
        public bool HasSelectedImage => !string.IsNullOrEmpty(SelectedImagePath);
        public bool HasSelectedAudio => !string.IsNullOrEmpty(SelectedAudioPath);
        public bool HasSelectedMedia => HasSelectedImage || HasSelectedAudio;        // Input mode intelligence based on active models
        public bool SupportsTextInput => ActiveModels.Any(m => m.InputType == ModelInputType.Text);
        public bool SupportsImageInput => ActiveModels.Any(m => m.InputType == ModelInputType.Image);
        public bool SupportsAudioInput => ActiveModels.Any(m => m.InputType == ModelInputType.Audio);

        public string CurrentInputModeDescription
        {
            get
            {
                if (ActiveModelsCount == 0)
                    return "No active models - activate a model to start chatting";

                var supportedTypes = new List<string>();
                if (SupportsTextInput) supportedTypes.Add("Text");
                if (SupportsImageInput) supportedTypes.Add("Image");
                if (SupportsAudioInput) supportedTypes.Add("Audio");

                if (supportedTypes.Count == 0)
                    return "Active models don't support standard input types";

                if (HasSelectedMedia)
                {
                    if (HasSelectedImage && HasSelectedAudio)
                        return "🎛️ Multimodal: Text + Image + Audio";
                    else if (HasSelectedImage)
                        return "🖼️ Vision Mode: Text + Image";
                    else if (HasSelectedAudio)
                        return "🎧 Audio Mode: Text + Audio";
                }

                return $"💬 Available: {string.Join(", ", supportedTypes)}";
            }
        }

        public string SupportedInputTypesText
        {
            get
            {
                if (ActiveModelsCount == 0)
                    return "";

                var activeInputTypes = ActiveModels
                    .Select(m => m.InputType.ToString())
                    .Distinct()
                    .ToList();

                return $"Active model types: {string.Join(", ", activeInputTypes)}";
            }
        }

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
        public ICommand HuggingFaceSearchCommand { get; } // Triggered by View
        public ICommand ImportFromHuggingFaceCommand { get; } // Triggered by View
        public ICommand GoToOrientCommand { get; } // ADDED: Command to navigate
        public ICommand UpdateModelInputTypeCommand { get; } // ADDED: Command to update input type        // Chat-related commands
        public ICommand SendMessageCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand EditMessageCommand { get; } // Add Edit Message Command
        public ICommand SaveMessageCommand { get; } // Add Save Message Command

        // Media input commands
        public ICommand SelectImageCommand { get; }
        public ICommand SelectAudioCommand { get; }
        public ICommand ClearMediaCommand { get; }

        // Debug command for testing chat UI
        public ICommand TestAddChatMessageCommand { get; }

        // New: Download model command
        public ICommand DownloadModelCommand { get; }
        // New: Delete model command
        public ICommand DeleteModelCommand { get; }

        // Helper: Get download/delete button text for a model
        public string GetDownloadOrDeleteButtonText(string modelId)
        {
            return IsModelDownloaded(modelId) ? "Delete from device" : "Download to device";
        }

        // Command for download/delete toggle button
        public ICommand DownloadOrDeleteModelCommand { get; }
        // Command for deleting the reference (removes from UI, not just device)
        public ICommand DeleteModelReferenceCommand { get; }

        // --- Constructor ---
        public NetPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, PythonBootstrapper pythonBootstrapper, AppModeService appModeService)
        {
            _fileService = fileService;
            _huggingFaceService = huggingFaceService;
            _pythonBootstrapper = pythonBootstrapper;
            _appModeService = appModeService;

            _pythonBootstrapper.ProgressChanged += (s, progress) =>
            {
                Debug.WriteLine($"Python setup progress: {progress:P0}");
            };

            // Initialize Commands
            ToggleGeneralModeCommand = new Command(ToggleGeneralMode);
            ToggleSpecificModeCommand = new Command(ToggleSpecificMode);
            ActivateModelCommand = new Command<NeuralNetworkModel>(ActivateModel);
            DeactivateModelCommand = new Command<NeuralNetworkModel>(DeactivateModel);
            LoadSpecificGoalCommand = new Command<SpecificGoal>(LoadSpecificGoal);
            ShareModelCommand = new Command<NeuralNetworkModel>(ShareModel);
            CommunicateWithModelCommand = new Command<string>(async (message) => await CommunicateWithModelAsync(message)); // Make async
            ExportModelCommand = new Command<NeuralNetworkModel>(async (model) => await ExportModel(model)); // Wrapper for async
            ImportModelCommand = new Command(async () => await ImportModelAsync()); // Wrapper for async
            HuggingFaceSearchCommand = new Command(async () => await SearchHuggingFaceAsync()); // Wrapper for async
            ImportFromHuggingFaceCommand = new Command(async () => await ImportDirectFromHuggingFaceAsync()); // Wrapper for async
            GoToOrientCommand = new Command<NeuralNetworkModel>(async (model) =>
            {
                if (model != null && !string.IsNullOrEmpty(model.Id))
                {
                    Debug.WriteLine($"Navigating to Orient page for model: {model.Name} (ID: {model.Id})");
                    // Navigate to the 'orient' route and pass the model ID as a query parameter
                    await NavigateTo($"///orient?modelId={model.Id}");
                }
                else
                {
                    Debug.WriteLine("Cannot navigate to Orient page: Model or Model ID is null/empty.");
                    await ShowAlert("Navigation Error", "Cannot navigate without a valid model selected.", "OK");
                }
            });

            // Add new command for updating model input type
            UpdateModelInputTypeCommand = new Command<(NeuralNetworkModel, ModelInputType)>(
                param => UpdateModelInputType(param.Item1, param.Item2));

            // Initialize chat commands
            SendMessageCommand = new Command(async () => await SendMessageAsync(), () => CanSendMessage);
            ClearChatCommand = new Command(ClearChat);
            EditMessageCommand = new Command<ChatMessage>(EditMessage); // Initialize Edit Message Command
            SaveMessageCommand = new Command<ChatMessage>(SaveMessage); // Initialize Save Message Command

            // Initialize media commands
            SelectImageCommand = new Command(async () => await SelectImageAsync());
            SelectAudioCommand = new Command(async () => await SelectAudioAsync());
            ClearMediaCommand = new Command(ClearMedia);

            // HuggingFace model download/delete commands
            DownloadModelCommand = new Command<NeuralNetworkModel>(async (model) => await DownloadModelAsync(model));
            DeleteModelCommand = new Command<NeuralNetworkModel>(async (model) => await DeleteModelAsync(model));
            DownloadOrDeleteModelCommand = new Command<NeuralNetworkModel>(async (model) => await DownloadOrDeleteModelAsync(model));
            DeleteModelReferenceCommand = new Command<NeuralNetworkModel>(async (model) => await DeleteModelReferenceAsync(model));

            // Check cache directory
            EnsureHFModelCacheDirectoryExists();

            // Populate categories
            HuggingFaceCategories = new List<string> { "All Categories" };
            HuggingFaceCategories.AddRange(_huggingFaceService.GetModelCategoryFilters().Keys);

            // Initialize downloaded models state
            _downloadedModelIds = new HashSet<string>();
            RefreshDownloadedModelsList();

            // Load initial data
            // Note: Loading is triggered by OnAppearing in the View
        }        // New: Track downloaded model IDs for UI state
        private HashSet<string> _downloadedModelIds = new HashSet<string>();

        // New: Public property to expose downloaded state for each model
        public bool IsModelDownloaded(string modelId)
        {
            return _downloadedModelIds.Contains(modelId);
        }        // Integrate HuggingFaceService cache and download wrappers
        private void EnsureHFModelCacheDirectoryExists()
        {
            _huggingFaceService.EnsureHFModelCacheDirectoryExists();
        }

        private void RefreshDownloadedModelsList()
        {
            var files = _huggingFaceService.RefreshDownloadedModelsList();
            _downloadedModelIds = new HashSet<string>(files.Select(f => Path.GetFileNameWithoutExtension(f)));
        }

        private async Task DownloadModelAsync(NeuralNetworkModel model)
        {
            var modelPath = Path.Combine(FileSystem.AppDataDirectory, "Models", "HuggingFace", (model.HuggingFaceModelId ?? model.Id).Replace("/", "_") + ".bin");
            await _huggingFaceService.DownloadModelAsync(model.HuggingFaceModelId, modelPath);
            _downloadedModelIds.Add(model.HuggingFaceModelId);
        }

        private async Task DeleteModelAsync(NeuralNetworkModel model)
        {
            var modelPath = Path.Combine(FileSystem.AppDataDirectory, "Models", "HuggingFace", (model.HuggingFaceModelId ?? model.Id).Replace("/", "_") + ".bin");
            await _huggingFaceService.DeleteModelAsync(model.HuggingFaceModelId, modelPath);
            _downloadedModelIds.Remove(model.HuggingFaceModelId);
        }

        private async Task DownloadOrDeleteModelAsync(NeuralNetworkModel model)
        {
            if (IsModelDownloaded(model.HuggingFaceModelId))
                await DeleteModelAsync(model);
            else
                await DownloadModelAsync(model);
        }

        private async Task DeleteModelReferenceAsync(NeuralNetworkModel model)
        {
            if (IsModelDownloaded(model.HuggingFaceModelId))
                await DeleteModelAsync(model);
            AvailableModels.Remove(model);
            await SavePersistedModelsAsync();
        }

        // ADDED: List of available input types for binding to dropdown
        public Array ModelInputTypes => Enum.GetValues(typeof(ModelInputType));

        // Configuration for Python execution (Consider moving to a config file/service)
        private const string PythonExecutablePath = "python"; // Or full path e.g., @"C:\Python311\python.exe"
        private const string HuggingFaceScriptPath = @"c:\Users\tanne\Documents\Github\C-Simple\scripts\run_hf_model.py"; // Updated path to the script

        // --- Public Methods (called from View or Commands) ---

        public async Task LoadDataAsync()
        {
            await SetupPythonEnvironmentAsync();
            await LoadPersistedModelsAsync();
            LoadSampleGoals(); // Load sample goals separately
            SubscribeToInputNotifications(); // Start background simulation
        }

        private async Task SetupPythonEnvironmentAsync()
        {
            try
            {
                IsLoading = true;
                CurrentModelStatus = "Checking for Python installation...";

                // Look for Python installations on the system
                bool pythonFound = await _pythonBootstrapper.InitializeAsync();

                if (!pythonFound)
                {
                    // Don't fall back to API mode - instead show clear instructions
                    CurrentModelStatus = "Python not found. Local models require Python to run.";
                    await ShowAlert("Python Required",
                        "Python 3.8 to 3.11 is required to run HuggingFace models locally.\n\n" +
                        "1. Download Python from https://python.org/downloads/\n" +
                        "2. We recommend Python 3.10 for best compatibility with AI libraries\n" +
                        "3. Avoid Python 3.12+ as some AI libraries may have compatibility issues\n" +
                        "4. During installation, check 'Add Python to PATH'\n" +
                        "5. Restart this application after installation", "OK");

                    // Set a flag that Python is not available
                    _pythonExecutablePath = null;
                    return;
                }

                // Get the Python executable path from the bootstrapper
                _pythonExecutablePath = _pythonBootstrapper.PythonExecutablePath;

                // Check multiple possible script locations
                var possibleScriptPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, "Scripts", "run_hf_model.py"),
                    Path.Combine(FileSystem.AppDataDirectory, "Scripts", "run_hf_model.py"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents", "CSimple", "Scripts", "run_hf_model.py"),
                    @"c:\Users\tanne\Documents\Github\C-Simple\scripts\run_hf_model.py"
                };

                string foundScriptPath = null;
                foreach (var path in possibleScriptPaths)
                {
                    if (File.Exists(path))
                    {
                        foundScriptPath = path;
                        Debug.WriteLine($"Found script at: {path}");
                        break;
                    }
                }

                if (foundScriptPath != null)
                {
                    _huggingFaceScriptPath = foundScriptPath;
                }
                else
                {
                    // Create the script if it doesn't exist
                    var scriptsDir = Path.Combine(FileSystem.AppDataDirectory, "Scripts");
                    Directory.CreateDirectory(scriptsDir);
                    _huggingFaceScriptPath = Path.Combine(scriptsDir, "run_hf_model.py");

                    // Create a basic Python script for HuggingFace model execution
                    await CreateHuggingFaceScript(_huggingFaceScriptPath);
                }

                // Install required packages
                CurrentModelStatus = "Installing required Python packages...";
                bool packagesInstalled = await _pythonBootstrapper.InstallRequiredPackagesAsync();

                if (!packagesInstalled)
                {
                    CurrentModelStatus = "Failed to install required Python packages.";
                    await ShowAlert("Package Installation Failed",
                        "The application failed to install the required Python packages. " +
                        "You may need to manually install them by running:\n\n" +
                        "pip install transformers torch", "OK");
                }
                else
                {
                    CurrentModelStatus = "Python environment ready";
                }
            }
            catch (Exception ex)
            {
                HandleError("Error setting up Python environment", ex);
                CurrentModelStatus = "Failed to set up Python environment. See error log for details.";

                await ShowAlert("Python Setup Error",
                    "There was an error setting up the Python environment. " +
                    "Please make sure Python is installed correctly and 'pip' is available.\n\n" +
                    $"Error details: {ex.Message}", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreateHuggingFaceScript(string scriptPath)
        {
            try
            {
                string scriptContent = @"#!/usr/bin/env python3
import argparse
import sys
import json
import traceback

def main():
    parser = argparse.ArgumentParser(description='Run HuggingFace model')
    parser.add_argument('--model_id', required=True, help='HuggingFace model ID')
    parser.add_argument('--input', required=True, help='Input text')
    
    args = parser.parse_args()
    
    try:
        # Try to import required libraries
        from transformers import AutoTokenizer, AutoModel, pipeline
        import torch
        
        print(f'Loading model: {args.model_id}')
        
        # Try to use pipeline first (simpler approach)
        try:
            # Determine task type based on model ID
            if 'gpt' in args.model_id.lower() or 'llama' in args.model_id.lower():
                task = 'text-generation'
            elif 'bert' in args.model_id.lower():
                task = 'fill-mask'
            else:
                task = 'text-generation'  # Default
            
            pipe = pipeline(task, model=args.model_id, trust_remote_code=True)
            result = pipe(args.input, max_length=150, do_sample=True, temperature=0.7)
            
            if isinstance(result, list):
                output = result[0].get('generated_text', str(result[0]));
            else:
                output = str(result);
                
            print(output);
            
        except Exception as pipe_error:
            print(f'Pipeline failed, trying manual approach: {pipe_error}');
            
            # Fallback to manual tokenizer/model approach
            tokenizer = AutoTokenizer.from_pretrained(args.model_id, trust_remote_code=True)
            model = AutoModel.from_pretrained(args.model_id, trust_remote_code=True)
            
            inputs = tokenizer(args.input, return_tensors='pt')
            
            with torch.no_grad():
                outputs = model(**inputs)
                
            # Basic response for demonstration
            print(f'Model processed input successfully. Input tokens: {inputs[""input_ids""].shape[1]}')
            
    except ImportError as e:
        print(f'ERROR: Missing required packages. Please install with: pip install transformers torch')
        print(f'Details: {e}')
        sys.exit(1)
    except Exception as e:
        print(f'ERROR: {e}')
        traceback.print_exc()
        sys.exit(1)

if __name__ == '__main__':
    main()
";

                await File.WriteAllTextAsync(scriptPath, scriptContent);
                Debug.WriteLine($"Created HuggingFace script at: {scriptPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating HuggingFace script: {ex.Message}");
                throw;
            }
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
        private async Task CommunicateWithModelAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                CurrentModelStatus = "Cannot send empty message.";
                return;
            }            // Add user message to chat history
            var userMessage = new ChatMessage(message, isFromUser: true, includeInHistory: true);
            ChatMessages.Add(userMessage);
            Debug.WriteLine($"Added user message to chat. ChatMessages count: {ChatMessages.Count}");
            Debug.WriteLine($"User message content: '{userMessage.Content}', IsFromUser: {userMessage.IsFromUser}");

            // Find the best active HuggingFace reference model (prioritize CPU-friendly ones)
            var activeHfModel = GetBestActiveModel(); if (activeHfModel == null)
            {
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    CurrentModelStatus = "No active models available.";
                    LastModelOutput = "In offline mode, local models will be executed on your hardware.\n\n" +
                        "Please activate a model to get started. Models that work well locally include:\n" +
                        "• gpt2 - Fast, lightweight text generation (CPU-friendly)\n" +
                        "• distilgpt2 - Even faster version of GPT-2 (CPU-friendly)\n" +
                        "• microsoft/DialoGPT-small - Good for conversations (CPU-friendly)\n" +
                        "• deepseek-ai/DeepSeek-R1 - Advanced model (requires GPU)\n\n" +
                        "GPU models will attempt to use your graphics card for acceleration.\n" +
                        "Switch to online mode if you prefer to use cloud-based execution.";

                    // Offer to suggest CPU-friendly models
                    await SuggestCpuFriendlyModelsAsync();
                    return;
                }
                else
                {
                    CurrentModelStatus = "No active HuggingFace reference model found.";
                    LastModelOutput = "Please activate a HuggingFace model first to communicate with it.";
                    return;
                }
            }

            // Check if Python is available
            if (string.IsNullOrEmpty(_pythonExecutablePath))
            {
                CurrentModelStatus = "Python is not available. Cannot run models.";
                LastModelOutput = "Python 3.8 to 3.11 is required to run HuggingFace models locally. Please install from python.org and restart the application.";

                await ShowAlert("Python Required",
                    "Python is required to run HuggingFace models locally.\n\n" +
                    "1. Download Python 3.8 to 3.11 from https://python.org/downloads/\n" +
                    "   * We recommend Python 3.10 for best compatibility\n" +
                    "   * Avoid Python 3.12+ as it may have compatibility issues\n" +
                    "2. During installation, check 'Add Python to PATH'\n" +
                    "3. Restart this application after installation", "OK");

                return;
            }
            CurrentModelStatus = $"Sending message to {activeHfModel.Name}...";
            LastModelOutput = $"Processing conversation with {activeHfModel.Name}...";
            IsModelCommunicating = true;            // Build complete chat history for model context
            string fullChatHistory = BuildChatHistoryForModel(); Debug.WriteLine($"Built chat history with {ChatMessages.Where(m => m.IncludeInHistory && !m.IsProcessing).Count()} included messages. History length: {fullChatHistory.Length} characters");
            if (fullChatHistory.Length > 0)
                Debug.WriteLine($"Chat history preview: {fullChatHistory.Substring(0, Math.Min(300, fullChatHistory.Length))}...");
            // Add processing message to chat history
            var processingMessage = new ChatMessage("Processing your request...", isFromUser: false, modelName: activeHfModel.Name, includeInHistory: false)
            {
                IsProcessing = true,
                // Set initial LLM source based on app mode
                LLMSource = _appModeService.CurrentMode == AppMode.Offline ? "local" : "local" // Default to local, will update if API is used
            };
            ChatMessages.Add(processingMessage);
            Debug.WriteLine($"Added processing message to chat. ChatMessages count: {ChatMessages.Count}");

            try
            {
                // Verify the model exists in our persisted models
                var persistedModels = await _fileService.LoadHuggingFaceModelsAsync();
                var modelInFile = persistedModels.FirstOrDefault(m =>
                    m.HuggingFaceModelId == activeHfModel.HuggingFaceModelId ||
                    m.Id == activeHfModel.Id);

                if (modelInFile == null)
                {
                    throw new InvalidOperationException($"Model {activeHfModel.Name} not found in persisted models file.");
                }

                Debug.WriteLine($"Found model in persisted file: {modelInFile.Name} (HF ID: {modelInFile.HuggingFaceModelId})");

                // Check if the script exists
                if (!File.Exists(_huggingFaceScriptPath))
                {
                    Debug.WriteLine($"Script not found at: {_huggingFaceScriptPath}");
                    await CreateHuggingFaceScript(_huggingFaceScriptPath);
                }
                // Show progress indicator for model loading with performance tip
                CurrentModelStatus = $"Loading {activeHfModel.Name} (first run may take longer)...";                // Add performance tip to output for user guidance
                string performanceTip = GetPerformanceTip(activeHfModel.HuggingFaceModelId);
                LastModelOutput = $"Processing conversation with {activeHfModel.Name}...\n\n{performanceTip}";

                // Execute the Python script with enhanced parameters using full chat history
                string result = await ExecuteHuggingFaceModelAsyncEnhanced(modelInFile.HuggingFaceModelId, fullChatHistory, activeHfModel);

                // Determine LLM source based on app mode and execution results
                string llmSource;
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    // In offline mode, everything should be local
                    llmSource = "local";
                }
                else
                {
                    // In online mode, check if the result indicates API usage
                    if (result != null && (result.Contains("used HuggingFace API") ||
                                         result.Contains("api fallback") ||
                                         result.Contains("Falling back to HuggingFace API")))
                    {
                        llmSource = "api";
                    }
                    else
                    {
                        // Default to local if no API indicators found
                        llmSource = "local";
                    }
                }
                Debug.WriteLine($"Determined LLM Source: {llmSource} (App Mode: {_appModeService.CurrentMode})");

                // Update the processing message with the actual response
                if (!string.IsNullOrEmpty(result))
                {
                    processingMessage.Content = result;
                    processingMessage.IsProcessing = false;
                    processingMessage.IncludeInHistory = true; // Include AI response in history
                    processingMessage.LLMSource = llmSource;
                    Debug.WriteLine($"Updated processing message with AI response. LLM Source: {llmSource}");
                    Debug.WriteLine($"Processing message LLMSource after update: '{processingMessage.LLMSource}'");
                    LastModelOutput = $"Response from {activeHfModel.Name}:\n{result}";
                    CurrentModelStatus = $"✓ Response received from {activeHfModel.Name}";
                }
                else
                {
                    processingMessage.Content = "No response received. The model may have executed successfully but produced no output.";
                    processingMessage.IsProcessing = false;
                    processingMessage.IncludeInHistory = true; // Include error message in history
                    processingMessage.LLMSource = llmSource;
                    Debug.WriteLine($"Updated processing message with no response. LLM Source: {llmSource}");
                    Debug.WriteLine($"Processing message LLMSource after update: '{processingMessage.LLMSource}'");
                    LastModelOutput = $"No response received from {activeHfModel.Name}. The model may have executed successfully but produced no output.";
                    CurrentModelStatus = $"Model {activeHfModel.Name} completed but returned no output";
                }

                // Force property change notification to update UI
                processingMessage.OnPropertyChanged(nameof(processingMessage.ModelDisplayNameWithSourcePrefixed));
            }
            catch (Exception ex)
            {
                HandleError($"Error communicating with model {activeHfModel.Name}", ex);
                string errorMessage = ex.Message;

                // Update processing message with error information
                string errorResponse = "";

                // Provide specific guidance for common errors
                if (errorMessage.Contains("accelerate") || errorMessage.Contains("FP8 quantized"))
                {
                    errorResponse = $"Error: {activeHfModel.Name} requires additional packages.\n\n" +
                        "This model needs 'accelerate' for FP8 quantization support.\n" +
                        "Installing required packages...";

                    LastModelOutput = errorResponse;
                    CurrentModelStatus = "Installing accelerate package...";

                    // Try to install accelerate package
                    bool installed = await InstallAcceleratePackageAsync();

                    if (installed)
                    {
                        errorResponse += "\n\nPackages installed successfully. Please try sending your message again.";
                        LastModelOutput += "\n\nPackages installed successfully. Please try sending your message again.";
                        CurrentModelStatus = "Ready - accelerate package installed";
                    }
                    else
                    {
                        errorResponse += "\n\nFailed to install accelerate automatically. Please install manually with:\npip install accelerate";
                        LastModelOutput += "\n\nFailed to install accelerate automatically. Please install manually with:\npip install accelerate";
                        CurrentModelStatus = "Manual package installation required";
                    }
                }
                else if (errorMessage.Contains("ModuleNotFoundError") || errorMessage.Contains("ImportError"))
                {
                    errorResponse = $"Error: Missing Python packages.\n\n" +
                        "Installing required packages: transformers, torch, accelerate...";

                    LastModelOutput = errorResponse;

                    await ShowAlert("Python Packages Required",
                        "Required packages are missing. Installing them now...\n\n" +
                        "This will install: transformers, torch, accelerate",
                        "OK");

                    // Try to install required packages
                    CurrentModelStatus = "Installing required packages...";
                    bool installed = await _pythonBootstrapper.InstallRequiredPackagesAsync();

                    if (installed)
                    {
                        await InstallAcceleratePackageAsync(); // Also install accelerate
                        errorResponse += "\n\nPackages installed successfully. Please try sending your message again.";
                        LastModelOutput += "\n\nPackages installed successfully. Please try sending your message again.";
                        CurrentModelStatus = "Ready - all packages installed";
                    }
                    else
                    {
                        errorResponse += "\n\nFailed to install packages automatically. Please install manually.";
                        LastModelOutput += "\n\nFailed to install packages automatically. Please install manually.";
                        CurrentModelStatus = "Manual package installation required";
                    }
                }
                else if (errorMessage.Contains("malicious code") || errorMessage.Contains("double-check"))
                {
                    errorResponse = $"Security Warning: {activeHfModel.Name} downloaded new code files.\n\n" +
                        "This is normal for some models but requires acknowledgment for security.\n" +
                        "The model execution was blocked for safety. You can try again if you trust the model source.";
                    LastModelOutput = errorResponse;
                    CurrentModelStatus = "Model blocked due to security warning";
                }
                else if (errorMessage.Contains("not found in persisted models"))
                {
                    errorResponse = $"Error: The model may have been removed from the persisted models file.\n\n" +
                        "Please re-import the model from HuggingFace.";
                    LastModelOutput = errorResponse;
                    CurrentModelStatus = "Model reference lost - please re-import";
                }
                else
                {
                    errorResponse = $"Error processing message with {activeHfModel.Name}: {errorMessage}";
                    LastModelOutput = errorResponse;
                    CurrentModelStatus = "Model execution failed";
                }

                // Determine LLM source for error cases
                string errorLlmSource = _appModeService.CurrentMode == AppMode.Offline ? "local" : "local";                // Update the processing message with the error
                processingMessage.Content = errorResponse;
                processingMessage.IsProcessing = false;
                processingMessage.IncludeInHistory = true; // Include error message in history
                processingMessage.LLMSource = errorLlmSource;
                Debug.WriteLine($"Set error LLM Source: {errorLlmSource}");

                // Force property change notification to update UI
                processingMessage.OnPropertyChanged(nameof(processingMessage.ModelDisplayNameWithSourcePrefixed));
            }
            finally
            {
                IsModelCommunicating = false;
            }
        }        // Helper method to build chat history for model input
        private string BuildChatHistoryForModel()
        {
            if (ChatMessages.Count == 0)
                return string.Empty;

            var historyBuilder = new StringBuilder();

            // Get ALL messages that should be included in history (both user and AI responses)
            // Exclude only processing messages
            var allMessages = ChatMessages
                .Where(msg => msg.IncludeInHistory && !msg.IsProcessing)
                .ToList();

            Debug.WriteLine($"BuildChatHistoryForModel: Total ChatMessages: {ChatMessages.Count}, Filtered messages: {allMessages.Count}");

            // Debug each message
            foreach (var msg in allMessages)
            {
                Debug.WriteLine($"Message - IsFromUser: {msg.IsFromUser}, Content: '{msg.Content.Substring(0, Math.Min(50, msg.Content.Length))}...', IncludeInHistory: {msg.IncludeInHistory}");
            }

            // Limit to last 20 messages to prevent extremely long context
            const int maxMessages = 20;
            if (allMessages.Count > maxMessages)
            {
                allMessages = allMessages.Skip(allMessages.Count - maxMessages).ToList();
                historyBuilder.AppendLine("(Conversation history truncated to recent exchanges)");
                historyBuilder.AppendLine();
            }

            if (allMessages.Any())
            {
                // Add all messages without prefixes, separated only by line breaks
                for (int i = 0; i < allMessages.Count; i++)
                {
                    var msg = allMessages[i];
                    historyBuilder.AppendLine(msg.Content);

                    // Add blank line between messages, but not after the last one
                    if (i < allMessages.Count - 1)
                    {
                        historyBuilder.AppendLine();
                    }
                }
            }

            string result = historyBuilder.ToString();
            Debug.WriteLine($"BuildChatHistoryForModel - Final result length: {result.Length}");
            return result;
        }

        // Helper method to find the best active model for local execution
        private NeuralNetworkModel GetBestActiveModel()
        {
            var activeHfModels = ActiveModels.Where(m => m.IsHuggingFaceReference && !string.IsNullOrEmpty(m.HuggingFaceModelId)).ToList();

            if (!activeHfModels.Any())
                return null;

            // Define CPU-friendly models (prioritize these for local execution)
            var cpuFriendlyModels = new[]
            {
                "gpt2", "distilgpt2", "microsoft/DialoGPT-small", "microsoft/DialoGPT-medium",
                "huggingface/CodeBERTa-small-v1", "distilbert-base-uncased", "bert-base-uncased"
            };

            // Define models that require GPU acceleration (but can work with proper hardware)
            var gpuRequiredModels = new[]
            {
                "deepseek-ai/DeepSeek-R1", "deepseek-ai/deepseek-r1",
                "microsoft/DialoGPT-large", "facebook/opt-66b", "EleutherAI/gpt-j-6B"
            };

            // In offline mode, check if we should exclude GPU-required models
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                // Only exclude GPU-required models if no GPU is detected
                // For now, we'll be more permissive and allow GPU models in offline mode
                // The Python script will handle the actual GPU availability check
                Debug.WriteLine("Offline mode: Allowing GPU models - hardware compatibility will be checked during execution");
            }

            // First, try to find a CPU-friendly model
            var bestModel = activeHfModels.FirstOrDefault(m =>
                cpuFriendlyModels.Any(cpu => m.HuggingFaceModelId.Contains(cpu, StringComparison.OrdinalIgnoreCase)));

            // If no CPU-friendly model found, return the first active model
            // Let the Python script handle hardware compatibility
            return bestModel ?? activeHfModels.First();
        }

        // Enhanced model execution with better error handling and performance optimizations
        private async Task<string> ExecuteHuggingFaceModelAsyncEnhanced(string modelId, string inputText, NeuralNetworkModel model)
        {
            // Ensure Python path is set
            if (string.IsNullOrEmpty(_pythonExecutablePath))
            {
                throw new InvalidOperationException("Python is not available. Please install Python and restart the application.");
            }

            if (!File.Exists(_huggingFaceScriptPath))
            {
                throw new FileNotFoundException($"HuggingFace script not found at: {_huggingFaceScriptPath}");
            }

            try
            {
                Debug.WriteLine($"Executing Python script with model: {modelId}");
                Debug.WriteLine($"Script path: {_huggingFaceScriptPath}");
                Debug.WriteLine($"Python path: {_pythonExecutablePath}");

                // Escape quotes in input text and handle special characters
                string escapedInput = inputText.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");                // Build arguments with enhanced parameters
                var argumentsBuilder = new StringBuilder();
                argumentsBuilder.Append($"\"{_huggingFaceScriptPath}\" --model_id \"{modelId}\" --input \"{escapedInput}\"");

                // Add CPU optimization flag for better local performance
                argumentsBuilder.Append(" --cpu_optimize");

                // Add max length parameter to prevent overly long responses
                int maxLength = Math.Min(200, inputText.Split(' ').Length + 100);
                argumentsBuilder.Append($" --max_length {maxLength}");

                // Add offline mode flag when in offline mode to disable API fallback
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    argumentsBuilder.Append(" --offline_mode");
                }

                string arguments = argumentsBuilder.ToString();

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _pythonExecutablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    // Set working directory to script location for better relative path handling
                    WorkingDirectory = Path.GetDirectoryName(_huggingFaceScriptPath)
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Determine timeout based on model type (CPU-friendly models get less time)
                var cpuFriendlyModels = new[] { "gpt2", "distilgpt2", "microsoft/DialoGPT" };
                bool isCpuFriendly = cpuFriendlyModels.Any(cpu => modelId.Contains(cpu, StringComparison.OrdinalIgnoreCase));
                int timeoutMs = isCpuFriendly ? 120000 : 300000; // 2 minutes for CPU-friendly, 5 minutes for others

                // Wait for process to complete with dynamic timeout
                bool completed = process.WaitForExit(timeoutMs);

                if (!completed)
                {
                    process.Kill();
                    throw new TimeoutException($"Model execution timed out after {timeoutMs / 1000} seconds. " +
                        (isCpuFriendly ? "Try a shorter input message." : "Large models may require more time on first run."));
                }

                string output = await outputTask;
                string error = await errorTask;
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Process completed with exit code: {exitCode}");
                Debug.WriteLine($"Output: {output}");
                Debug.WriteLine($"Error: {error}"); if (exitCode != 0)
                {
                    // Enhanced error handling with specific suggestions
                    if (error.Contains("compute capability") && error.Contains("FP8"))
                    {
                        throw new Exception($"Model '{modelId}' requires FP8 quantization (GPU compute capability 8.9+). " +
                            "Your RTX 3090 (8.6) is detected but doesn't support FP8. Switch to online mode or try a different model variant.");
                    }
                    else if (error.Contains("No GPU or XPU found") && error.Contains("FP8 quantization"))
                    {
                        throw new Exception($"Model '{modelId}' requires GPU acceleration for FP8 quantization. " +
                            "Consider trying a CPU-friendly model like 'gpt2' or 'distilgpt2' for local execution.");
                    }
                    else if (error.Contains("accelerate") && error.Contains("FP8"))
                    {
                        throw new Exception("Model requires 'accelerate' package for FP8 quantization support. " +
                            "Installing this package automatically...");
                    }
                    else if (error.Contains("ModuleNotFoundError") || error.Contains("ImportError"))
                    {
                        throw new Exception("Required Python packages are missing. Please install transformers and torch.");
                    }
                    else if (error.Contains("OutOfMemoryError") || error.Contains("CUDA out of memory"))
                    {
                        throw new Exception($"Model '{modelId}' is too large for available memory. " +
                            "Try closing other applications or switching to a smaller model like 'distilgpt2'.");
                    }
                    else if (error.Contains("AUTHENTICATION_ERROR") || error.Contains("AUTHENTICATION_REQUIRED"))
                    {
                        if (_appModeService.CurrentMode == AppMode.Offline)
                        {
                            throw new Exception($"Model '{modelId}' requires authentication and cannot be used in offline mode. " +
                                "Switch to online mode or try a public model like 'gpt2' or 'distilgpt2'.");
                        }
                        else
                        {
                            throw new Exception($"Model '{modelId}' requires a HuggingFace API key for access. " +
                                "Get a free API key from https://huggingface.co/settings/tokens or try a public model like 'gpt2'.");
                        }
                    }
                    else if (error.Contains("API Error 401") || error.Contains("Invalid username or password"))
                    {
                        if (_appModeService.CurrentMode == AppMode.Offline)
                        {
                            throw new Exception($"Authentication failed for model '{modelId}' and API fallback is disabled in offline mode. " +
                                "Switch to online mode or try a public model like 'gpt2' or 'distilgpt2'.");
                        }
                        else
                        {
                            throw new Exception($"Authentication failed for model '{modelId}'. " +
                                "This model requires a HuggingFace API key or try a public model like 'gpt2' or 'distilgpt2'.");
                        }
                    }
                    else
                    {
                        throw new Exception($"Script failed with exit code {exitCode}. Error: {error}");
                    }
                }

                // Enhanced output processing
                if (string.IsNullOrWhiteSpace(output))
                {
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        return "Model processed the input but generated no text output. Try rephrasing your input or using a different model.";
                    }
                    else
                    {
                        // Handle various warning/info scenarios
                        if (error.Contains("AUTHENTICATION_ERROR") || error.Contains("AUTHENTICATION_REQUIRED"))
                        {
                            throw new Exception($"Model requires authentication. Get a HuggingFace API key from https://huggingface.co/settings/tokens or try a public model like 'gpt2'.");
                        }
                        else if (error.Contains("API Error") || error.Contains("Falling back to HuggingFace API"))
                        {
                            if (_appModeService.CurrentMode == AppMode.Offline)
                            {
                                throw new Exception($"Model '{modelId}' failed to run locally and API fallback is disabled in offline mode. " +
                                    "Switch to online mode or try a CPU-friendly model like 'gpt2' or 'distilgpt2'.");
                            }
                            else
                            {
                                // Extract meaningful response from API fallback
                                var lines = error.Split('\n');
                                var responseLine = lines.FirstOrDefault(l => l.Contains("Response:") || l.Contains("Output:"));
                                if (!string.IsNullOrEmpty(responseLine))
                                {
                                    return responseLine.Substring(responseLine.IndexOf(':') + 1).Trim();
                                }
                                return $"Local execution failed, used HuggingFace API: {error}";
                            }
                        }
                        else
                        {
                            return $"Model execution completed with warnings: {error}";
                        }
                    }
                }

                // Clean up and format the output
                string cleanedOutput = output.Trim();

                // Remove any debug prefixes that might have been added
                if (cleanedOutput.StartsWith("Loading model:") || cleanedOutput.StartsWith("Model:"))
                {
                    var lines = cleanedOutput.Split('\n');
                    cleanedOutput = string.Join('\n', lines.Skip(1)).Trim();
                }
                return cleanedOutput;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running model: {ex.Message}");
                throw; // Re-throw to be handled by caller
            }
        }

        // Helper method to suggest CPU-friendly models to the user
        private async Task SuggestCpuFriendlyModelsAsync()
        {
            var suggestedModels = new[]
            {
                "gpt2 - Fast, lightweight text generation",
                "distilgpt2 - Even faster version of GPT-2",
                "microsoft/DialoGPT-small - Good for conversations",
                "microsoft/DialoGPT-medium - Better conversations (larger)",
                "distilbert-base-uncased - Good for text understanding"
            };

            var message = "For better local performance, consider using these CPU-friendly models:\n\n" +
                string.Join("\n", suggestedModels) +
                "\n\nThese models run faster on CPU and don't require GPU acceleration.";

            await ShowAlert("CPU-Friendly Model Suggestions", message, "OK");
        }

        // Helper method to provide model-specific performance tips
        private string GetPerformanceTip(string modelId)
        {
            if (modelId.Contains("gpt2", StringComparison.OrdinalIgnoreCase))
            {
                return "💡 Tip: GPT-2 models work great on CPU and load quickly!";
            }
            else if (modelId.Contains("distil", StringComparison.OrdinalIgnoreCase))
            {
                return "💡 Tip: Distilled models are optimized for speed and efficiency!";
            }
            else if (modelId.Contains("DialoGPT", StringComparison.OrdinalIgnoreCase))
            {
                return "💡 Tip: DialoGPT is designed for conversations and works well locally!";
            }
            else if (modelId.Contains("bert", StringComparison.OrdinalIgnoreCase))
            {
                return "💡 Tip: BERT models are excellent for understanding text!";
            }
            else if (modelId.Contains("deepseek", StringComparison.OrdinalIgnoreCase) ||
                     modelId.Contains("llama", StringComparison.OrdinalIgnoreCase))
            {
                return "⚠️ Note: This model may require significant resources. Consider trying GPT-2 for faster local execution.";
            }
            else
            {
                return "💡 Tip: For faster local execution, try CPU-friendly models like GPT-2 or DistilGPT-2!";
            }
        }

        // Helper method to install the accelerate package specifically
        private async Task<bool> InstallAcceleratePackageAsync()
        {
            try
            {
                Debug.WriteLine("Installing accelerate package...");

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _pythonExecutablePath,
                    Arguments = "-m pip install accelerate",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                bool completed = process.WaitForExit(60000); // 1 minute timeout

                if (!completed)
                {
                    process.Kill();
                    Debug.WriteLine("Accelerate installation timed out");
                    return false;
                }

                string output = await outputTask;
                string error = await errorTask;
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Accelerate installation completed with exit code: {exitCode}");
                Debug.WriteLine($"Output: {output}");
                Debug.WriteLine($"Error: {error}");

                return exitCode == 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error installing accelerate package: {ex.Message}");
                return false;
            }
        }

        // Helper method to execute the Python script - enhanced error handling
        private async Task<string> ExecuteHuggingFaceModelAsync(string modelId, string inputText)
        {
            // Ensure Python path is set
            if (string.IsNullOrEmpty(_pythonExecutablePath))
            {
                throw new InvalidOperationException("Python is not available. Please install Python and restart the application.");
            }

            if (!File.Exists(_huggingFaceScriptPath))
            {
                throw new FileNotFoundException($"HuggingFace script not found at: {_huggingFaceScriptPath}");
            }

            try
            {
                Debug.WriteLine($"Executing Python script with model: {modelId}");
                Debug.WriteLine($"Script path: {_huggingFaceScriptPath}");
                Debug.WriteLine($"Python path: {_pythonExecutablePath}");

                // Escape quotes in input text
                string escapedInput = inputText.Replace("\"", "\\\"");
                string arguments = $"\"{_huggingFaceScriptPath}\" --model_id \"{modelId}\" --input \"{escapedInput}\"";

                var processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _pythonExecutablePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Debug.WriteLine($"Starting process: {processStartInfo.FileName} {processStartInfo.Arguments}");

                using var process = new System.Diagnostics.Process { StartInfo = processStartInfo };
                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                // Wait for process to complete with timeout
                bool completed = process.WaitForExit(180000); // 3 minutes timeout for large models

                if (!completed)
                {
                    process.Kill();
                    throw new TimeoutException("Model execution timed out after 3 minutes. Large models may take longer to load initially.");
                }

                string output = await outputTask;
                string error = await errorTask;
                int exitCode = process.ExitCode;

                Debug.WriteLine($"Process completed with exit code: {exitCode}");
                Debug.WriteLine($"Output: {output}");
                Debug.WriteLine($"Error: {error}"); if (exitCode != 0)
                {
                    // Check for specific error patterns and provide better messages
                    if (error.Contains("No GPU or XPU found") && error.Contains("FP8 quantization"))
                    {
                        throw new Exception($"Model '{modelId}' requires GPU acceleration for FP8 quantization, but no compatible GPU was found. The script will attempt to use HuggingFace API as fallback.");
                    }
                    else if (error.Contains("accelerate") && error.Contains("FP8"))
                    {
                        throw new Exception("Model requires 'accelerate' package for FP8 quantization support.");
                    }
                    else if (error.Contains("ModuleNotFoundError") || error.Contains("ImportError"))
                    {
                        throw new Exception("Required Python packages are missing. Please ensure transformers and torch are installed.");
                    }
                    else if (error.Contains("OutOfMemoryError") || error.Contains("CUDA out of memory"))
                    {
                        throw new Exception("Model is too large for available memory. Try using a smaller model or closing other applications.");
                    }
                    else if (error.Contains("torch.cuda.is_available()"))
                    {
                        throw new Exception("CUDA/GPU support is required for this model but not available on this system.");
                    }
                    else if (error.Contains("AUTHENTICATION_ERROR") || error.Contains("AUTHENTICATION_REQUIRED"))
                    {
                        throw new Exception($"Model '{modelId}' requires a HuggingFace API key for access. This is likely a gated model. Get a free API key from https://huggingface.co/settings/tokens and configure it in the app settings.");
                    }
                    else if (error.Contains("API Error 401") || error.Contains("Invalid username or password"))
                    {
                        throw new Exception($"Authentication failed for model '{modelId}'. This model requires a HuggingFace API key. Get one from https://huggingface.co/settings/tokens or try a public model like 'gpt2' or 'distilgpt2'.");
                    }
                    else
                    {
                        throw new Exception($"Script failed with exit code {exitCode}. Error: {error}");
                    }
                }

                // Check if we got valid output
                if (string.IsNullOrWhiteSpace(output))
                {
                    // If no output but no error, the model might have processed but returned empty
                    if (string.IsNullOrWhiteSpace(error))
                    {
                        return "Model processed the input but generated no text output. This can happen with some model types or configurations.";
                    }
                    else
                    {                        // If there's error output but exit code was 0, it might be warnings
                        if (error.Contains("AUTHENTICATION_ERROR") || error.Contains("AUTHENTICATION_REQUIRED"))
                        {
                            throw new Exception($"Model requires authentication. Get a HuggingFace API key from https://huggingface.co/settings/tokens or try a public model like 'gpt2'.");
                        }
                        else if (error.Contains("API Error") || error.Contains("Falling back to HuggingFace API"))
                        {
                            return $"Local execution failed, using HuggingFace API: {error}";
                        }
                        else
                        {
                            return $"Model execution completed with warnings: {error}";
                        }
                    }
                }

                return output.Trim();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error running model: {ex.Message}");
                throw; // Re-throw to be handled by caller
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

        private async Task GoToOrientAsync(NeuralNetworkModel model)
        {
            if (model == null)
            {
                await ShowAlert("Navigation Error", "No model selected to orient/train.", "OK");
                return;
            }
            try
            {
                // Navigate to OrientPage, passing the model ID as a query parameter
                await NavigateTo($"///orient?modelId={model.Id}");
            }
            catch (Exception ex)
            {
                HandleError($"Error navigating to Orient page for model {model.Name}", ex);
                await ShowAlert("Navigation Error", $"Could not navigate to training page: {ex.Message}", "OK");
            }
        }

        // ADDED: New method to update model input type
        private void UpdateModelInputType(NeuralNetworkModel model, ModelInputType inputType)
        {
            if (model == null) return;

            try
            {
                Debug.WriteLine($"Updating input type for model {model.Name} to {inputType}");

                // Check if the input type is actually changing
                bool isChanged = model.InputType != inputType;

                model.InputType = inputType;

                // Save the updated model to persistent storage
                _ = SavePersistedModelsAsync();

                // Explicitly notify that AvailableModels collection has changed
                // This will trigger the OrientPageViewModel's PropertyChanged handler
                if (isChanged)
                {
                    OnPropertyChanged(nameof(AvailableModels));
                    Debug.WriteLine($"Notified PropertyChanged for AvailableModels after updating input type");
                }

                CurrentModelStatus = $"Updated input type for '{model.Name}' to {inputType}";
            }
            catch (Exception ex)
            {
                HandleError($"Error updating input type for model: {model?.Name}", ex);
            }
        }

        // Method to guess the input type based on model information
        // Changed from private to public to allow access from OrientPageViewModel
        public ModelInputType GuessInputType(HuggingFaceModel model)
        {
            if (model == null) return ModelInputType.Unknown;

            try
            {
                string pipelineTag = model.Pipeline_tag?.ToLowerInvariant() ?? "";
                string modelId = (model.ModelId ?? model.Id ?? "").ToLowerInvariant();
                string description = (model.Description ?? "").ToLowerInvariant();

                // Check for text-related models
                if (pipelineTag.Contains("text") ||
                    pipelineTag.Contains("token") ||
                    pipelineTag.Contains("sentence") ||
                    pipelineTag.Contains("question") ||
                    pipelineTag.Contains("summarization") ||
                    pipelineTag.Contains("conversational") ||
                    pipelineTag == "text-generation" ||
                    pipelineTag == "text-classification" ||
                    pipelineTag == "text2text-generation" ||
                    pipelineTag == "translation" ||
                    pipelineTag == "summarization" ||
                    pipelineTag == "conversational" ||
                    modelId.Contains("gpt") ||
                    modelId.Contains("bert") ||
                    modelId.Contains("t5") || // Corrected typo: contains -> Contains
                    modelId.Contains("llama") ||
                    modelId.Contains("bloom") ||
                    modelId.Contains("bart") ||
                    modelId.Contains("roberta"))
                {
                    return ModelInputType.Text;
                }

                // Check for image-related models
                if (pipelineTag.Contains("image") ||
                    pipelineTag.Contains("vision") ||
                    pipelineTag == "image-classification" ||
                    pipelineTag == "object-detection" ||
                    pipelineTag == "image-segmentation" ||
                    pipelineTag == "depth-estimation" ||
                    modelId.Contains("vit") ||
                    modelId.Contains("clip") ||
                    modelId.Contains("resnet") ||
                    modelId.Contains("diffusion") ||
                    modelId.Contains("stable-diffusion") ||
                    modelId.Contains("yolo"))
                {
                    return ModelInputType.Image;
                }

                // Check for audio-related models
                if (pipelineTag.Contains("audio") ||
                    pipelineTag.Contains("speech") ||
                    pipelineTag == "automatic-speech-recognition" ||
                    pipelineTag == "audio-classification" ||
                    pipelineTag == "text-to-speech" ||
                    modelId.Contains("wav2vec") ||
                    modelId.Contains("whisper") ||
                    modelId.Contains("hubert") ||
                    modelId.Contains("audio"))
                {
                    return ModelInputType.Audio;
                }

                // Check description as a fallback
                if (description.Contains("text") ||
                    description.Contains("language") ||
                    description.Contains("chat"))
                {
                    return ModelInputType.Text;
                }
                else if (description.Contains("image") ||
                         description.Contains("vision") ||
                         description.Contains("picture") ||
                         description.Contains("photo"))
                {
                    return ModelInputType.Image;
                }
                else if (description.Contains("audio") ||
                         description.Contains("speech") ||
                         description.Contains("voice") ||
                         description.Contains("sound"))
                {
                    return ModelInputType.Audio;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error guessing input type: {ex.Message}");
            }

            return ModelInputType.Unknown;
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

                // Log loaded input types for debugging
                foreach (var model in cleanedModels)
                {
                    Debug.WriteLine($"Loaded model: {model.Name}, Input Type: {model.InputType}");
                }

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
                                // Ensure InputType is properly set
                                if (model.InputType == ModelInputType.Unknown)
                                {
                                    if (model.IsHuggingFaceReference && !string.IsNullOrEmpty(model.HuggingFaceModelId))
                                    {
                                        // Try to guess if it's unknown
                                        var hfModel = new HuggingFaceModel
                                        {
                                            ModelId = model.HuggingFaceModelId,
                                            Description = model.Description,
                                            Pipeline_tag = null // We don't have this info anymore
                                        };
                                        model.InputType = GuessInputType(hfModel);
                                        Debug.WriteLine($"Re-guessed input type for {model.Name}: {model.InputType}");
                                    }
                                }

                                AvailableModels.Add(model);
                                Debug.WriteLine($"Added model to UI: {model.Name}, Input Type: {model.InputType}");
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

        private async Task ShowModelDetailsAndImportAsync(CSimple.Models.HuggingFaceModel model)
        {
            // Logic moved from NetPage.xaml.cs
            try
            {
                bool importConfirmed = await ShowConfirmation("Model Details",
                    $"Name: {model.ModelId ?? model.Id}\nAuthor: {model.Author}\nType: {model.Pipeline_tag}\nDownloads: {model.Downloads}\n\nImport this model as a Python Reference?", // Modified confirmation text
                    "Import Reference", "Cancel"); // Modified button text

                if (!importConfirmed)
                {
                    CurrentModelStatus = "Import canceled";
                    return;
                }

                CurrentModelStatus = $"Preparing Python reference for {model.ModelId ?? model.Id}...";
                IsLoading = true;

                // *** MODIFIED: Directly set to create Python reference ***
                bool isPythonReference = true;
                List<string> filesToDownload = new List<string>(); // Keep list, but it won't be used for download

                // Optional: Still fetch details if needed for GuessInputType or other metadata
                HuggingFaceModelDetails modelDetails = model as HuggingFaceModelDetails ?? await _huggingFaceService.GetModelDetailsAsync(model.ModelId ?? model.Id);
                Debug.WriteLine($"ShowModelDetailsAndImportAsync: Importing '{model.ModelId ?? model.Id}' as Python Reference.");

                // *** REMOVED/COMMENTED OUT: File selection logic ***
                /*
                // Prepare options for the action sheet
                var actionSheetOptions = new List<string>();
                actionSheetOptions.Add("Use Python Script"); // Always add this option
                // ... (rest of the action sheet population logic removed) ...

                // Show the combined action sheet
                string selectedOption = await ShowActionSheet("Select Import Method or File", "Cancel", null, actionSheetOptions.ToArray());

                // Handle the selected option
                if (selectedOption == "Cancel" || string.IsNullOrEmpty(selectedOption))
                {
                    // ... (cancel logic) ...
                }
                else if (selectedOption == "Use Python Script")
                {
                    // ... (python ref logic - now default) ...
                }
                                                             // ... (other file download options handling removed) ...
                */
                // *** END REMOVED/COMMENTED OUT ***


                // Handle Python Reference Case (This will always execute now)
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

                    // Use modelDetails if fetched, otherwise use the basic model info
                    var description = modelDetails?.Description ?? model.Description ?? "Imported from HuggingFace (requires Python)";
                    var inputType = GuessInputType(modelDetails ?? model); // Guess input type

                    var pythonReferenceModel = new NeuralNetworkModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = GetFriendlyModelName(model.ModelId ?? model.Id) + " (Python Ref)",
                        Description = description,
                        Type = model.RecommendedModelType, // Keep original type guess if available
                        IsHuggingFaceReference = true,
                        HuggingFaceModelId = model.ModelId ?? model.Id,
                        InputType = inputType
                    };

                    // Add the unique reference
                    AvailableModels.Add(pythonReferenceModel);
                    CurrentModelStatus = $"Added reference to {pythonReferenceModel.Name}";
                    await SavePersistedModelsAsync(); // Save the updated list

                    // Show Python usage info
                    await ShowAlert("Reference Added & Usage", $"Reference to '{pythonReferenceModel.Name}' added.\n\nUse in Python:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{pythonReferenceModel.HuggingFaceModelId}\", trust_remote_code=True)", "OK");

                    IsLoading = false;
                    return; // Python reference added, workflow complete
                }                // *** REMOVED/COMMENTED OUT: File Download Case ***
                /*
                // Handle File Download Case (Only if not a Python Reference)
                if (filesToDownload.Count == 0 && !isPythonReference)
                {
                    // ... (no files selected logic) ...
                }

                // Show Python usage info before download (if downloading files)
                if (filesToDownload.Count > 0)
                {
                    // ... (show alert) ...
                }

                // Download files with safety checks
                foreach (var fileUrl in filesToDownload)
                {
                    var fileName = Path.GetFileName(fileUrl);
                    
                    // Use safety check before downloading
                    bool shouldDownload = await ShouldProceedWithDownloadAsync(model.ModelId ?? model.Id, fileName);
                    if (!shouldDownload)
                    {
                        continue; // Skip this download
                    }
                    
                    // Proceed with actual download logic here...
                    // ... (download implementation) ...
                }

                if (!anyDownloadSucceeded)
                {
                   // ... (download failed logic) ...
                }

                // Add downloaded model to list
                // ... (add model logic) ...

                if (anyDownloadSucceeded)
                {
                    // ... (show success alert) ...
                }
                */
                // *** END REMOVED/COMMENTED OUT ***
            }
            catch (Exception ex)
            {
                HandleError("Error handling model details", ex);
                await ShowAlert("Import Error", $"Failed to import model reference: {ex.Message}", "OK");
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

            // Log the model directory for user awareness
            Debug.WriteLine($"[Model Directory] {modelId} -> {modelDirectory}");

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

        // --- Chat Methods ---
        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentMessage) || IsAiTyping || ActiveModelsCount == 0)
                return;

            var userMessage = CurrentMessage.Trim();
            CurrentMessage = string.Empty; // Clear input

            Debug.WriteLine($"Chat: Sending message '{userMessage}' to active models (count: {ActiveModelsCount})");

            // Update UI properties
            IsAiTyping = true;
            OnPropertyChanged(nameof(CanSendMessage));

            try
            {
                // Use the existing CommunicateWithModelAsync method which handles the full chat flow
                await CommunicateWithModelAsync(userMessage);
                Debug.WriteLine($"Chat: Message processed successfully. ChatMessages count: {ChatMessages.Count}");
            }
            catch (Exception ex)
            {                // Add error message to chat if something goes wrong
                var errorMessage = new ChatMessage($"Sorry, I encountered an error: {ex.Message}", false, "System", includeInHistory: true);
                ChatMessages.Add(errorMessage);

                LastModelOutput = $"Error: {ex.Message}";
                Debug.WriteLine($"Chat error: {ex}");
            }
            finally
            {
                IsAiTyping = false;
                OnPropertyChanged(nameof(CanSendMessage));

                // Scroll to bottom of chat (to be handled by view)
                ScrollToBottom?.Invoke();
            }
        }
        private void ClearChat()
        {
            ChatMessages.Clear();
            LastModelOutput = "Chat history cleared.";
        }

        private void EditMessage(ChatMessage message)
        {
            if (message == null) return;

            try
            {
                // Set IsEditing to true for the selected message
                message.IsEditing = true;
                Debug.WriteLine($"Started editing message: {message.Content}");
            }
            catch (Exception ex)
            {
                HandleError($"Error starting edit for message: {message?.Content}", ex);
            }
        }

        private void SaveMessage(ChatMessage message)
        {
            if (message == null) return;

            try
            {
                // Set IsEditing to false for the selected message
                message.IsEditing = false;
                Debug.WriteLine($"Saved edited message: {message.Content}");
            }
            catch (Exception ex)
            {
                HandleError($"Error saving edited message: {message?.Content}", ex);
            }
        }

        // --- Media Methods ---
        private async Task SelectImageAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    SelectedImagePath = result.FullPath;
                    SelectedImageName = result.FileName;

                    // Clear audio if image is selected (for simplicity, can be multimodal later)
                    SelectedAudioPath = null;
                    SelectedAudioName = null;

                    // Trigger UI updates
                    OnPropertyChanged(nameof(HasSelectedImage));
                    OnPropertyChanged(nameof(HasSelectedAudio));
                    OnPropertyChanged(nameof(HasSelectedMedia));
                    OnPropertyChanged(nameof(CurrentInputModeDescription));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting image: {ex}");
                await ShowAlert?.Invoke("Error", "Failed to select image. Please try again.", "OK");
            }
        }

        private async Task SelectAudioAsync()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an audio file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.audio" } },
                        { DevicePlatform.Android, new[] { "audio/*" } },
                        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".aac" } },
                        { DevicePlatform.Tizen, new[] { "audio/*" } },
                        { DevicePlatform.macOS, new[] { "mp3", "wav", "m4a", "aac" } },
                    })
                });

                if (result != null)
                {
                    SelectedAudioPath = result.FullPath;
                    SelectedAudioName = result.FileName;

                    // Clear image if audio is selected (for simplicity, can be multimodal later)
                    SelectedImagePath = null;
                    SelectedImageName = null;

                    // Trigger UI updates
                    OnPropertyChanged(nameof(HasSelectedImage));
                    OnPropertyChanged(nameof(HasSelectedAudio));
                    OnPropertyChanged(nameof(HasSelectedMedia));
                    OnPropertyChanged(nameof(CurrentInputModeDescription));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting audio: {ex}");
                await ShowAlert?.Invoke("Error", "Failed to select audio file. Please try again.", "OK");
            }
        }

        private void ClearMedia()
        {
            SelectedImagePath = null;
            SelectedImageName = null;
            SelectedAudioPath = null;
            SelectedAudioName = null;

            // Trigger UI updates
            OnPropertyChanged(nameof(HasSelectedImage));
            OnPropertyChanged(nameof(HasSelectedAudio));
            OnPropertyChanged(nameof(HasSelectedMedia));
            OnPropertyChanged(nameof(CurrentInputModeDescription));
        }

        // Action to scroll chat to bottom (to be set by view)
        public Action ScrollToBottom { get; set; }

        private void HandleError(string context, Exception ex)
        {
            Debug.WriteLine($"ViewModel Error - {context}: {ex.Message}\n{ex.StackTrace}");
            CurrentModelStatus = $"Error: {context}";
        }        /// <summary>
                 /// Checks if a download should proceed based on offline mode, file existence, and file size
                 /// </summary>
        private async Task<bool> ShouldProceedWithDownloadAsync(string modelId, string fileName, long sizeBytes = -1)
        {
            // Check offline mode first
            if (_appModeService?.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine($"[Download Check] Offline mode enabled - blocking download of {fileName}");
                await ShowAlert("Offline Mode", $"Cannot download '{fileName}' while in offline mode. You can still reference the model for Python usage.", "OK");
                return false;
            }

            // Check if file already exists
            var modelDir = GetModelDirectoryPath(modelId);
            var filePath = Path.Combine(modelDir, fileName);
            if (File.Exists(filePath))
            {
                Debug.WriteLine($"[Download Check] File already exists: {filePath}");
                await ShowAlert("File Exists", $"'{fileName}' already exists locally and will not be downloaded again.", "OK");
                return false;
            }

            // Check for large files (>1GB) and ask user confirmation
            if (sizeBytes > 1_000_000_000)
            {
                var sizeGB = sizeBytes / 1_073_741_824.0;
                Debug.WriteLine($"[Download Check] Large file detected: {fileName} ({sizeGB:F2} GB)");
                bool proceed = await ShowConfirmation(
                    "Large Download Warning",
                    $"The file '{fileName}' is {sizeGB:F2} GB. This is a large download that may take significant time and storage space.\n\nDo you want to proceed?",
                    "Download", "Cancel");

                if (!proceed)
                {
                    Debug.WriteLine($"[Download Check] User cancelled large download for {fileName}");
                    return false;
                }
            }

            return true;
        }

        // --- UI Interaction Abstractions (to be implemented by the View) ---
        // These allow the ViewModel to request UI actions without directly referencing UI elements.
        public Func<string, string, string, Task> ShowAlert { get; set; } = async (t, m, c) => { await Task.CompletedTask; }; // Default no-op
        public Func<string, string, string, string, Task<bool>> ShowConfirmation { get; set; } = async (t, m, a, c) => { await Task.CompletedTask; return false; }; // Default no-op
        public Func<string, string, string, string[], Task<string>> ShowActionSheet { get; set; } = async (t, c, d, b) => { await Task.CompletedTask; return c; }; // Default returns cancel
        public Func<string, string, string, string, string, Task<string>> ShowPrompt { get; set; } = async (t, m, a, c, iv) => { await Task.CompletedTask; return null; }; // Default no-op
        public Func<Task<FileResult>> PickFile { get; set; } = async () => { await Task.CompletedTask; return null; }; // Default no-op
        public Func<string, Task> NavigateTo { get; set; } = async (r) => { await Task.CompletedTask; }; // Default no-op
        public Func<List<CSimple.Models.HuggingFaceModel>, Task<CSimple.Models.HuggingFaceModel>> ShowModelSelectionDialog { get; set; } = async (m) => { await Task.CompletedTask; return null; }; // Default no-op


        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged; protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);            // Also notify dependent properties
            if (propertyName == nameof(ActiveModels))
            {
                OnPropertyChanged(nameof(ActiveModelsCount));
                OnPropertyChanged(nameof(SupportsTextInput));
                OnPropertyChanged(nameof(SupportsImageInput));
                OnPropertyChanged(nameof(SupportsAudioInput));
                OnPropertyChanged(nameof(CurrentInputModeDescription));
                OnPropertyChanged(nameof(SupportedInputTypesText));
            }
            if (propertyName == nameof(CurrentMessage) || propertyName == nameof(IsAiTyping) || propertyName == nameof(ActiveModels))
                OnPropertyChanged(nameof(CanSendMessage));

            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
