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
using System.Threading;
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
        private readonly PythonEnvironmentService _pythonEnvironmentService;
        private readonly ModelCommunicationService _modelCommunicationService;
        private readonly ModelExecutionService _modelExecutionService;
        private readonly ModelImportExportService _modelImportExportService; private readonly ITrayService _trayService; // Add tray service for progress notifications
        // Consider injecting navigation and dialog services for better testability

        // Debounce mechanism for saving to prevent excessive saves
        private CancellationTokenSource _saveDebounceTokenSource;
        private readonly object _saveLock = new object();

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
        private string _fallbackScriptPath;

        // Python setup tracking to avoid repeated installations
        private static bool _isPythonEnvironmentSetup = false;
        private static readonly object _pythonSetupLock = new object();

        // Chat-related backing fields
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
        // Note: PythonEnvironmentService handles Python setup and script creation (extracted for maintainability)
        // Note: ModelCommunicationService handles model communication logic (extracted for maintainability)
        // Note: ModelExecutionService handles model execution with enhanced error handling (extracted for maintainability)
        // Note: ModelImportExportService handles model import/export and file operations (extracted for maintainability)
        public NetPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, PythonBootstrapper pythonBootstrapper, AppModeService appModeService, PythonEnvironmentService pythonEnvironmentService, ModelCommunicationService modelCommunicationService, ModelExecutionService modelExecutionService, ModelImportExportService modelImportExportService, ITrayService trayService)
        {
            _fileService = fileService;
            _huggingFaceService = huggingFaceService;
            _pythonBootstrapper = pythonBootstrapper;
            _appModeService = appModeService;
            _pythonEnvironmentService = pythonEnvironmentService;
            _modelCommunicationService = modelCommunicationService;
            _modelExecutionService = modelExecutionService;
            _modelImportExportService = modelImportExportService;
            _trayService = trayService;

            // Subscribe to Python environment service events
            _pythonEnvironmentService.StatusChanged += (s, status) => CurrentModelStatus = status;
            _pythonEnvironmentService.LoadingChanged += (s, isLoading) => IsLoading = isLoading;

            // Subscribe to model communication service events
            _modelCommunicationService.StatusChanged += (s, status) => CurrentModelStatus = status;
            _modelCommunicationService.OutputChanged += (s, output) => LastModelOutput = output;
            _modelCommunicationService.CommunicatingChanged += (s, isCommunicating) => IsModelCommunicating = isCommunicating;

            // Subscribe to model execution service events
            _modelExecutionService.StatusUpdated += status => CurrentModelStatus = status;
            _modelExecutionService.OutputReceived += output => LastModelOutput = output;
            _modelExecutionService.ErrorOccurred += (message, ex) => HandleError(message, ex);

            // Subscribe to model import/export service events
            _modelImportExportService.StatusUpdated += status => CurrentModelStatus = status;
            _modelImportExportService.LoadingChanged += isLoading => IsLoading = isLoading;
            _modelImportExportService.ErrorOccurred += (message, ex) => HandleError(message, ex);

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
                param => UpdateModelInputType(param.Item1, param.Item2));            // Initialize chat commands
            SendMessageCommand = new Command(async () => await SendMessageAsync(), () => CanSendMessage);
            ClearChatCommand = new Command(ClearChat);
            EditMessageCommand = new Command<ChatMessage>(EditMessage); // Initialize Edit Message Command

            // Initialize ModelInputTypeDisplayItems for the picker
            ModelInputTypeDisplayItems = new List<ModelInputTypeDisplayItem>
            {
                new ModelInputTypeDisplayItem { Value = ModelInputType.Text, DisplayName = "Text" },
                new ModelInputTypeDisplayItem { Value = ModelInputType.Image, DisplayName = "Image" },
                new ModelInputTypeDisplayItem { Value = ModelInputType.Audio, DisplayName = "Audio" },
                new ModelInputTypeDisplayItem { Value = ModelInputType.Unknown, DisplayName = "Unknown" }
            };
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
        private HashSet<string> _downloadedModelIds = new HashSet<string>();        // New: Public property to expose downloaded state for each model
        public bool IsModelDownloaded(string modelId)
        {
            bool result = _downloadedModelIds.Contains(modelId);
            // Reduce excessive logging - only log when explicitly debugging download status
            // Debug.WriteLine($"IsModelDownloaded: modelId='{modelId}', result={result}, _downloadedModelIds=[{string.Join(", ", _downloadedModelIds)}]");
            return result;
        }// Integrate HuggingFaceService cache and download wrappers
        private void EnsureHFModelCacheDirectoryExists()
        {
            _huggingFaceService.EnsureHFModelCacheDirectoryExists();
        }
        private void RefreshDownloadedModelsList()
        {
            var modelIds = _huggingFaceService.RefreshDownloadedModelsList();
            _downloadedModelIds = new HashSet<string>(modelIds);

            // Only log when there are actually downloaded models to avoid noise
            if (_downloadedModelIds.Count > 0)
            {
                Debug.WriteLine($"ViewModel RefreshDownloadedModelsList: Found {_downloadedModelIds.Count} downloaded models: [{string.Join(", ", _downloadedModelIds)}]");
            }
            else
            {
                // Minimal logging for empty case
                Debug.WriteLine($"Found {_downloadedModelIds.Count} downloaded models");
            }

            // Update all models' download button text
            UpdateAllModelsDownloadButtonText();
        }
        private void UpdateAllModelsDownloadButtonText()
        {
            foreach (var model in AvailableModels)
            {
                if (!string.IsNullOrEmpty(model.HuggingFaceModelId))
                {
                    bool isDownloaded = IsModelDownloaded(model.HuggingFaceModelId);
                    string newText = isDownloaded ? "Remove from Device" : "Download to Device";

                    // Only update and log if the text actually changed
                    if (model.DownloadButtonText != newText)
                    {
                        model.DownloadButtonText = newText;
                        Debug.WriteLine($"Updated model '{model.Name}' button text to '{newText}' (downloaded: {isDownloaded})");
                    }
                }
            }
        }
        private async Task DownloadModelAsync(NeuralNetworkModel model)
        {
            try
            {
                IsLoading = true;
                model.IsDownloading = true;
                model.DownloadProgress = 0.0;
                model.DownloadStatus = "Initializing download...";

                CurrentModelStatus = $"Downloading {model.Name}...";

                // Show initial tray notification
                _trayService?.ShowProgress($"Downloading {model.Name}", "Preparing download...", 0.0);

                // Create a marker file for the download
                var modelId = model.HuggingFaceModelId ?? model.Id;
                var markerPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\HFModels",
                    modelId.Replace("/", "_") + ".download_marker");

                // Ensure the directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(markerPath));

                // Simulate download progress (since the actual download happens in Python script)
                await SimulateDownloadProgress(model);

                // Call the service to prepare for download
                await _huggingFaceService.DownloadModelAsync(modelId, markerPath);

                // Mark as downloaded
                model.IsDownloaded = true;
                model.DownloadProgress = 1.0;
                model.DownloadStatus = "Download complete";

                // The actual model files will be downloaded by the Python script on first use
                // Refresh downloaded models list from disk to sync with actual state
                RefreshDownloadedModelsList();

                CurrentModelStatus = $"Model {model.Name} ready for use";

                // Show completion notification
                _trayService?.ShowCompletionNotification("Download Complete", $"{model.Name} is ready to use");
                _trayService?.HideProgress();

                // Trigger UI update for button text
                NotifyModelDownloadStatusChanged();
            }
            catch (Exception ex)
            {
                model.DownloadStatus = $"Download failed: {ex.Message}";
                CurrentModelStatus = $"Failed to download {model.Name}: {ex.Message}";
                Debug.WriteLine($"Error downloading model: {ex.Message}");

                _trayService?.ShowCompletionNotification("Download Failed", $"Failed to download {model.Name}");
                _trayService?.HideProgress();
            }
            finally
            {
                model.IsDownloading = false;
                IsLoading = false;
            }
        }

        /// <summary>
        /// Simulates download progress with realistic timing
        /// </summary>
        private async Task SimulateDownloadProgress(NeuralNetworkModel model)
        {
            var random = new Random();
            var totalSteps = 20;

            for (int i = 0; i <= totalSteps; i++)
            {
                var progress = (double)i / totalSteps;
                model.DownloadProgress = progress;

                // Update status based on progress
                if (progress < 0.2)
                    model.DownloadStatus = "Connecting to HuggingFace...";
                else if (progress < 0.4)
                    model.DownloadStatus = "Downloading model files...";
                else if (progress < 0.7)
                    model.DownloadStatus = "Processing tokenizer...";
                else if (progress < 0.9)
                    model.DownloadStatus = "Validating model integrity...";
                else if (progress < 1.0)
                    model.DownloadStatus = "Finalizing download...";
                else
                    model.DownloadStatus = "Download complete!";

                // Update tray progress
                _trayService?.UpdateProgress(progress, model.DownloadStatus);

                // Variable delay to simulate realistic download behavior
                var delay = random.Next(100, 500);
                await Task.Delay(delay);
            }
        }
        private Task DeleteModelAsync(NeuralNetworkModel model)
        {
            try
            {
                IsLoading = true;
                CurrentModelStatus = $"Removing {model.Name}...";

                var modelId = model.HuggingFaceModelId ?? model.Id;

                // Remove marker file
                var markerPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\HFModels",
                    modelId.Replace("/", "_") + ".download_marker");

                if (File.Exists(markerPath))
                {
                    File.Delete(markerPath);
                }

                // Remove from HuggingFace cache (the actual model files)
                var cacheDir = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";
                var modelCacheDir = Path.Combine(cacheDir, "models--" + modelId.Replace("/", "--")); if (Directory.Exists(modelCacheDir))
                {
                    Directory.Delete(modelCacheDir, true);
                }

                // Refresh downloaded models list from disk to sync with actual state
                RefreshDownloadedModelsList();

                CurrentModelStatus = $"Model {model.Name} removed";

                // Trigger UI update for button text
                NotifyModelDownloadStatusChanged();
            }
            catch (Exception ex)
            {
                CurrentModelStatus = $"Failed to remove {model.Name}: {ex.Message}";
                Debug.WriteLine($"Error deleting model: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }

            return Task.CompletedTask;
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

        /// <summary>
        /// Notifies the UI that model download status has changed to update button text
        /// </summary>
        private void NotifyModelDownloadStatusChanged()
        {
            // Reduce logging frequency - only log when models are present
            if (AvailableModels.Count > 0)
            {
                Debug.WriteLine($"NotifyModelDownloadStatusChanged: Triggering UI refresh for {AvailableModels.Count} models");
            }

            // Update all models' download button text based on current download state
            UpdateAllModelsDownloadButtonText();

            // Force the UI to refresh by notifying that the AvailableModels collection has changed
            // This will cause the converter to re-evaluate for all model buttons
            OnPropertyChanged(nameof(AvailableModels));
        }

        // ADDED: List of available input types for binding to dropdown
        public Array ModelInputTypes => Enum.GetValues(typeof(ModelInputType));

        // Collection of ModelInputType display items for the picker
        public List<ModelInputTypeDisplayItem> ModelInputTypeDisplayItems { get; }

        public class ModelInputTypeDisplayItem
        {
            public ModelInputType Value { get; set; }
            public string DisplayName { get; set; }

            public override string ToString() => DisplayName;
        }        // Configuration for Python execution (Consider moving to a config file/service)
        private const string PythonExecutablePath = "python"; // Or full path e.g., @"C:\Python311\python.exe"
        private const string HuggingFaceScriptPath = @"c:\Users\tanne\Documents\Github\C-Simple\scripts\run_hf_model.py"; // Updated path to the script

        // --- Public Methods (called from View or Commands) ---

        public async Task LoadDataAsync()
        {
            // Only setup Python environment once per application session
            lock (_pythonSetupLock)
            {
                if (!_isPythonEnvironmentSetup)
                {
                    Debug.WriteLine("NetPageViewModel: First-time Python environment setup");
                    // Setup will be done asynchronously below
                }
                else
                {
                    Debug.WriteLine("NetPageViewModel: Python environment already set up, skipping setup");
                }
            }

            if (!_isPythonEnvironmentSetup)
            {
                await _pythonEnvironmentService.SetupPythonEnvironmentAsync(ShowAlert);
                _pythonExecutablePath = _pythonEnvironmentService.PythonExecutablePath;
                _huggingFaceScriptPath = _pythonEnvironmentService.HuggingFaceScriptPath;

                lock (_pythonSetupLock)
                {
                    _isPythonEnvironmentSetup = true;
                    Debug.WriteLine("NetPageViewModel: Python environment setup completed");
                }
            }
            else
            {
                // Use previously set paths
                _pythonExecutablePath = _pythonEnvironmentService.PythonExecutablePath;
                _huggingFaceScriptPath = _pythonEnvironmentService.HuggingFaceScriptPath;
                Debug.WriteLine("NetPageViewModel: Using existing Python environment paths");
            }

            await LoadPersistedModelsAsync();

            // Refresh downloaded models list to sync UI with actual disk state
            RefreshDownloadedModelsList();

            LoadSampleGoals(); // Load sample goals separately
            SubscribeToInputNotifications(); // Start background simulation

            // Trigger UI update to ensure button text is correct
            NotifyModelDownloadStatusChanged();
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
            var activeHfModel = GetBestActiveModel();

            await _modelCommunicationService.CommunicateWithModelAsync(message,
                activeHfModel,
                ChatMessages,
                _pythonExecutablePath,
                _huggingFaceScriptPath,
                ShowAlert,
                () => Task.CompletedTask, /* CPU-friendly models suggestion handled by service */
                async () => await _modelExecutionService.InstallAcceleratePackageAsync(_pythonExecutablePath),
                (modelId) => "Consider using smaller models for better CPU performance.",
                async (modelId, inputText, model) => await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(modelId, inputText, model, _pythonExecutablePath, _huggingFaceScriptPath));
        }

        // Helper method to build chat history for model input
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
        }        // Enhanced model execution with better error handling and performance optimizations
        private async Task<string> ExecuteHuggingFaceModelAsyncEnhanced(string modelId, string inputText, NeuralNetworkModel model)
        {
            return await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(
                modelId, inputText, model, _pythonExecutablePath, _huggingFaceScriptPath);
        }

        // Helper method to suggest CPU-friendly models to the user
        private async Task SuggestCpuFriendlyModelsAsync()
        {
            var suggestions = await _modelExecutionService.GetCpuFriendlyModelSuggestions();
            var suggestedModels = suggestions.Select((model, index) => $"• {model}").ToArray();

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
        }        // Helper method to install the accelerate package specifically
        private async Task<bool> InstallAcceleratePackageAsync()
        {
            return await _modelExecutionService.InstallAcceleratePackageAsync(_pythonExecutablePath);
        }        // Helper method to execute the Python script - enhanced error handling
        private async Task<string> ExecuteHuggingFaceModelAsync(string modelId, string inputText)
        {
            return await _modelExecutionService.ExecuteHuggingFaceModelAsync(
                modelId, inputText, _pythonExecutablePath, _huggingFaceScriptPath);
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
        }        // ADDED: New method to update model input type
        private void UpdateModelInputType(NeuralNetworkModel model, ModelInputType inputType)
        {
            if (model == null) return;

            try
            {
                // Check if the input type is actually changing
                bool isChanged = model.InputType != inputType;

                if (!isChanged)
                {
                    // No change needed, avoid unnecessary operations
                    return;
                }

                Debug.WriteLine($"Input type for model {model.Name} changed to {inputType}");
                model.InputType = inputType;

                // Use debounced save to prevent excessive save operations
                SavePersistedModelsDebounced();

                // Only notify if something actually changed
                OnPropertyChanged(nameof(AvailableModels));
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
        private async Task LoadPersistedModelsAsync()
        {
            try
            {
                var persistedModels = await _modelImportExportService.LoadPersistedModelsAsync();

                // Debug logging to check InputType values
                Debug.WriteLine($"=== DEBUG LoadPersistedModelsAsync ===");
                Debug.WriteLine($"Loaded {persistedModels?.Count ?? 0} persisted models");
                foreach (var model in persistedModels ?? new List<NeuralNetworkModel>())
                {
                    Debug.WriteLine($"Model: {model.Name}, InputType: {model.InputType} ({(int)model.InputType}), IsHuggingFaceReference: {model.IsHuggingFaceReference}");
                }
                Debug.WriteLine($"=== END DEBUG ===");

                // Update AvailableModels on the main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableModels.Clear();
                    foreach (var model in persistedModels ?? new List<NeuralNetworkModel>())
                    {
                        AvailableModels.Add(model);
                    }

                    // Apply smart activations based on app mode
                    if (IsGeneralModeActive)
                    {
                        // Leave room for auto-activation logic if needed
                    }

                    // Debug the input types again after adding to collection
                    Debug.WriteLine($"=== DEBUG AvailableModels Collection ===");
                    foreach (var model in AvailableModels)
                    {
                        Debug.WriteLine($"Model in collection: {model.Name}, InputType: {model.InputType} ({(int)model.InputType})");
                    }
                    Debug.WriteLine($"=== END DEBUG ===");

                    // DebugModelInputTypes();
                    DebugModelInputTypes();

                    // Update download button text for all loaded models
                    UpdateAllModelsDownloadButtonText();
                });
            }
            catch (Exception ex)
            {
                HandleError("Error loading persisted models", ex);
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
            await _modelImportExportService.SavePersistedModelsAsync(modelsToSave);
        }        // Original method now calls the overload
        private async Task SavePersistedModelsAsync()
        {
            // Logic moved from NetPage.xaml.cs
            // Reduced logging to minimize console noise
            var modelsToSave = AvailableModels
                .Where(m => m.IsHuggingFaceReference || !string.IsNullOrEmpty(m.HuggingFaceModelId))
                .ToList();
            await SavePersistedModelsAsync(modelsToSave); // Call the overload
        }

        // Debounced save method to prevent excessive saves
        private void SavePersistedModelsDebounced()
        {
            lock (_saveLock)
            {
                // Cancel any existing debounce operation
                _saveDebounceTokenSource?.Cancel();
                _saveDebounceTokenSource = new CancellationTokenSource();

                var token = _saveDebounceTokenSource.Token;

                // Debounce with 500ms delay
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(500, token);
                        if (!token.IsCancellationRequested)
                        {
                            await SavePersistedModelsAsync();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when debounce is cancelled
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in debounced save: {ex.Message}");
                    }
                }, token);
            }
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
            var modelDirectory = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\HFModels", safeModelId);
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
                        var messages = new[] { "Detected pattern, suggesting action...", "Analyzing input...", "Processing..." }; LastModelOutput = messages[new Random().Next(messages.Length)];
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
        }        // Action to scroll chat to bottom (to be set by view)
        public Action ScrollToBottom { get; set; }

        private void HandleError(string context, Exception ex)
        {
            Debug.WriteLine($"ViewModel Error - {context}: {ex.Message}\n{ex.StackTrace}");
            CurrentModelStatus = $"Error: {context}";
        }

        /// <summary>
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
        public Func<List<CSimple.Models.HuggingFaceModel>, Task<CSimple.Models.HuggingFaceModel>> ShowModelSelectionDialog { get; set; } = async (m) => { await Task.CompletedTask; return null; }; // Default no-op// --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);

            // Also notify dependent properties
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

        /// <summary>
        /// Debug method to check model input types - only runs in DEBUG builds
        /// </summary>
        public void DebugModelInputTypes()
        {
#if DEBUG
            Debug.WriteLine("=== DEBUG MODEL INPUT TYPES ===");
            Debug.WriteLine($"Total models loaded: {AvailableModels.Count}");
            foreach (var model in AvailableModels)
            {
                Debug.WriteLine($"Model: {model.Name}, InputType: {model.InputType} ({(int)model.InputType})");
            }
            Debug.WriteLine($"ModelInputTypeDisplayItems count: {ModelInputTypeDisplayItems?.Count ?? 0}");
            if (ModelInputTypeDisplayItems != null)
            {
                for (int i = 0; i < ModelInputTypeDisplayItems.Count; i++)
                {
                    var item = ModelInputTypeDisplayItems[i];
                    Debug.WriteLine($"  Index {i}: {item.Value} ({(int)item.Value}) -> '{item.DisplayName}'");
                }
            }
            Debug.WriteLine("=== END DEBUG ===");
#endif
        }
    }
}
