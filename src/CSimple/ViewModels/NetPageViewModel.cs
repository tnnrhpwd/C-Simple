using CSimple.Models;
using CSimple.Services;
using CSimple.Services.AppModeService;
using Microsoft.Maui.Controls;
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
        private readonly ModelImportExportService _modelImportExportService;
        private readonly ITrayService _trayService; // Add tray service for progress notifications
        private readonly IModelDownloadService _modelDownloadService; // Add model download service
        private readonly IModelImportService _modelImportService; // Add model import service
        private readonly IChatManagementService _chatManagementService; // Add chat management service
        private readonly IMediaSelectionService _mediaSelectionService; // Add media selection service
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

        public bool CanSendMessage =>
            (!string.IsNullOrWhiteSpace(CurrentMessage) || HasCompatibleMediaSelected) &&
            !IsAiTyping &&
            ActiveModelsCount > 0;

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
        public bool HasSelectedMedia => HasSelectedImage || HasSelectedAudio;

        // Check if we have media selected that's compatible with active models
        public bool HasCompatibleMediaSelected =>
            (HasSelectedImage && SupportsImageInput) ||
            (HasSelectedAudio && SupportsAudioInput);

        // Input mode intelligence based on active models
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
        public ICommand SelectFileCommand { get; }
        public ICommand ClearMediaCommand { get; }

        // Debug command for testing chat UI
        public ICommand TestAddChatMessageCommand { get; }

        // New: Download model command
        public ICommand DownloadModelCommand { get; }
        // New: Delete model command
        public ICommand DeleteModelCommand { get; }

        // Command for download/delete toggle button
        public ICommand DownloadOrDeleteModelCommand { get; }
        // Command for deleting the reference (removes from UI, not just device)
        public ICommand DeleteModelReferenceCommand { get; }
        public ICommand OpenModelInExplorerCommand { get; }

        // Helper: Get download/delete button text for a model
        public string GetDownloadOrDeleteButtonText(string modelId)
        {
            return IsModelDownloaded(modelId) ? "Delete from device" : "Download to device";
        }

        // --- Constructor ---
        // Note: PythonEnvironmentService handles Python setup and script creation (extracted for maintainability)
        // Note: ModelCommunicationService handles model communication logic (extracted for maintainability)
        // Note: ModelExecutionService handles model execution with enhanced error handling (extracted for maintainability)
        // Note: ModelImportExportService handles model import/export and file operations (extracted for maintainability)
        public NetPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, PythonBootstrapper pythonBootstrapper, AppModeService appModeService, PythonEnvironmentService pythonEnvironmentService, ModelCommunicationService modelCommunicationService, ModelExecutionService modelExecutionService, ModelImportExportService modelImportExportService, ITrayService trayService, IModelDownloadService modelDownloadService, IModelImportService modelImportService, IChatManagementService chatManagementService, IMediaSelectionService mediaSelectionService)
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
            _modelDownloadService = modelDownloadService;
            _modelImportService = modelImportService;
            _chatManagementService = chatManagementService;
            _mediaSelectionService = mediaSelectionService;

            // Configure the media selection service UI delegates
            _mediaSelectionService.ShowAlert = ShowAlert;
            _mediaSelectionService.UpdateStatus = status => CurrentModelStatus = status;

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
#if DEBUG
                Debug.WriteLine($"Python setup progress: {progress:P0}");
#endif
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

            // Add new command for updating model input type - FIXED: Use async method with proper error handling
            UpdateModelInputTypeCommand = new Command<(NeuralNetworkModel, ModelInputType)>(
                param =>
                {
                    Debug.WriteLine($"🚀 UpdateModelInputTypeCommand TRIGGERED with model: '{param.Item1?.Name}', InputType: {param.Item2}");
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Debug.WriteLine($"🔥 EXECUTING UpdateModelInputTypeAsync in Task.Run");
                            await UpdateModelInputTypeAsync(param.Item1, param.Item2);
                            Debug.WriteLine($"✅ UpdateModelInputTypeAsync COMPLETED successfully");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ CRITICAL ERROR in UpdateModelInputTypeCommand: {ex.Message}");
                            Debug.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                            CurrentModelStatus = $"ERROR: {ex.Message}";
                        }
                    });
                });            // Initialize chat commands
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
            SelectFileCommand = new Command(async () => await SelectFileAsync());
            ClearMediaCommand = new Command(ClearMedia);

            // HuggingFace model download/delete commands
            DownloadModelCommand = new Command<NeuralNetworkModel>(async (model) => await DownloadModelAsync(model));
            DeleteModelCommand = new Command<NeuralNetworkModel>(async (model) => await DeleteModelAsync(model));
            DownloadOrDeleteModelCommand = new Command<NeuralNetworkModel>(async (model) => await DownloadOrDeleteModelAsync(model));
            DeleteModelReferenceCommand = new Command<NeuralNetworkModel>(async (model) => await DeleteModelReferenceAsync(model));
            OpenModelInExplorerCommand = new Command<NeuralNetworkModel>(OpenModelInExplorer);

            // Check cache directory
            EnsureHFModelCacheDirectoryExists();

            // Populate categories
            HuggingFaceCategories = new List<string> { "All Categories" };
            HuggingFaceCategories.AddRange(_huggingFaceService.GetModelCategoryFilters().Keys);

            // Initialize downloaded models state
            RefreshDownloadedModelsList();

            // Load initial data
            // Note: Loading is triggered by OnAppearing in the View
        }        // Check if a model is downloaded by examining the actual directory size
        public bool IsModelDownloaded(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return false;

            try
            {
                string cacheDirectory = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";

                if (!Directory.Exists(cacheDirectory))
                    return false;

                // Check for model directory by trying both naming conventions
                var possibleDirNames = new[]
                {
                    modelId.Replace("/", "_"),           // org/model -> org_model
                    $"models--{modelId.Replace("/", "--")}"  // org/model -> models--org--model
                };

                foreach (var dirName in possibleDirNames)
                {
                    var modelPath = Path.Combine(cacheDirectory, dirName);

                    if (Directory.Exists(modelPath))
                    {
                        // Calculate total directory size
                        long totalSize = GetDirectorySize(modelPath);

                        // Consider downloaded if > 5KB (5120 bytes)
                        bool isDownloaded = totalSize > 5120;

#if DEBUG
                        if (totalSize > 0)
                        {
                            Debug.WriteLine($"Model '{modelId}' directory size: {totalSize:N0} bytes ({totalSize / 1024.0:F1} KB) - Downloaded: {isDownloaded}");
                        }
#endif
                        return isDownloaded;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking if model '{modelId}' is downloaded: {ex.Message}");
                return false;
            }
        }

        private long GetDirectorySize(string directoryPath)
        {
            try
            {
                if (!Directory.Exists(directoryPath))
                    return 0;

                long totalSize = 0;

                // Get size of all files in directory and subdirectories
                var files = Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        totalSize += fileInfo.Length;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error getting size of file '{file}': {ex.Message}");
                    }
                }

                return totalSize;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating directory size for '{directoryPath}': {ex.Message}");
                return 0;
            }
        }// Integrate HuggingFaceService cache and download wrappers
        private void EnsureHFModelCacheDirectoryExists()
        {
            _huggingFaceService.EnsureHFModelCacheDirectoryExists();
        }
        private void RefreshDownloadedModelsList()
        {
            // No longer maintain a cached list - IsModelDownloaded now checks directly
            // This method remains for compatibility but just triggers UI updates

            // Update all models' download button text
            UpdateAllModelsDownloadButtonText();
        }
        private void UpdateAllModelsDownloadButtonText()
        {
            foreach (var model in AvailableModels)
            {
                if (!string.IsNullOrEmpty(model.HuggingFaceModelId))
                {
                    string newText;

                    if (model.IsDownloading)
                    {
                        newText = "Stop Download";
                    }
                    else
                    {
                        bool isDownloaded = IsModelDownloaded(model.HuggingFaceModelId);
                        newText = isDownloaded ? "Remove from Device" : "Download to Device";
                    }

                    // Only update and log if the text actually changed
                    if (model.DownloadButtonText != newText)
                    {
                        model.DownloadButtonText = newText;
                        Debug.WriteLine($"Updated model '{model.Name}' button text to '{newText}' (downloading: {model.IsDownloading}, downloaded: {IsModelDownloaded(model.HuggingFaceModelId)})");
                    }
                }
            }
        }
        private async Task DownloadModelAsync(NeuralNetworkModel model)
        {
            await ModelDownloadServiceHelper.DownloadModelAsync(
                _modelDownloadService,
                model,
                modelId => _huggingFaceService.GetModelDetailsAsync(modelId),
                GetModelDownloadSizeAsync,
                ShowConfirmation,
                status => CurrentModelStatus = status,
                isLoading => IsLoading = isLoading,
                UpdateAllModelsDownloadButtonText,
                NotifyModelDownloadStatusChanged
            );

            // Refresh downloaded models list from disk to sync with actual state
            RefreshDownloadedModelsList();
        }

        private async Task DeleteModelAsync(NeuralNetworkModel model)
        {
            await ModelDownloadServiceHelper.DeleteModelAsync(
                _modelDownloadService,
                model,
                status => CurrentModelStatus = status,
                isLoading => IsLoading = isLoading,
                RefreshDownloadedModelsList,
                NotifyModelDownloadStatusChanged
            );
        }

        private async Task DownloadOrDeleteModelAsync(NeuralNetworkModel model)
        {
            var modelId = model.HuggingFaceModelId ?? model.Id;

            if (model.IsDownloading)
            {
                // Stop the download
                await StopModelDownloadAsync(model);
            }
            else if (IsModelDownloaded(modelId))
            {
                // Delete the downloaded model
                await DeleteModelAsync(model);
            }
            else
            {
                // Start the download
                await DownloadModelAsync(model);
            }
        }

        private async Task StopModelDownloadAsync(NeuralNetworkModel model)
        {
            _modelDownloadService.StopModelDownload(model);

            // Reset the model state
            model.IsDownloading = false;
            model.DownloadProgress = 0.0;
            model.DownloadStatus = "Download cancelled";

            CurrentModelStatus = $"Download of {model.Name} cancelled";

            // Hide tray progress
            _trayService?.HideProgress();

            // Update button text
            UpdateAllModelsDownloadButtonText();

            await Task.Delay(100); // Small delay to ensure UI updates
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
#if DEBUG
                Debug.WriteLine($"NotifyModelDownloadStatusChanged: Triggering UI refresh for {AvailableModels.Count} models");
#endif
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
        }

        // --- Public Methods (called from View or Commands) ---

        public async Task LoadDataAsync()
        {
            // Only setup Python environment once per application session
            lock (_pythonSetupLock)
            {
                if (!_isPythonEnvironmentSetup)
                {
                    Debug.WriteLine("NetPageViewModel: First-time Python environment setup required");
                }
                else
                {
                    Debug.WriteLine("NetPageViewModel: Python environment already set up, skipping setup");
                    // Use previously set paths
                    _pythonExecutablePath = _pythonEnvironmentService.PythonExecutablePath;
                    _huggingFaceScriptPath = _pythonEnvironmentService.HuggingFaceScriptPath;
                    Debug.WriteLine($"NetPageViewModel: Using existing Python environment paths. Python path: '{_pythonExecutablePath}', Script path: '{_huggingFaceScriptPath}'");
                }
            }

            // Only call the service if we haven't set up Python yet
            if (!_isPythonEnvironmentSetup)
            {
                await _pythonEnvironmentService.SetupPythonEnvironmentAsync(ShowAlert);
                _pythonExecutablePath = _pythonEnvironmentService.PythonExecutablePath;
                _huggingFaceScriptPath = _pythonEnvironmentService.HuggingFaceScriptPath;

                Debug.WriteLine($"NetPageViewModel: Python environment setup completed. Python path: '{_pythonExecutablePath}', Script path: '{_huggingFaceScriptPath}'");

                lock (_pythonSetupLock)
                {
                    _isPythonEnvironmentSetup = true;
                    Debug.WriteLine("NetPageViewModel: Python environment setup completed");
                }
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
                else
                {
                    // When general mode is activated, restore previously active general models
                    RestoreActiveModelsOfType(ModelType.General);
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

                    // When specific mode is activated, restore previously active goal-specific models
                    RestoreActiveModelsOfType(ModelType.GoalSpecific);
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

                    // Save the active state to persist across app restarts
                    SavePersistedModelsDebounced();
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

                    // Save the inactive state to persist across app restarts
                    SavePersistedModelsDebounced();
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

            // Handle media-only messages using the chat management service
            if (string.IsNullOrWhiteSpace(message) && HasSelectedMedia)
            {
                // Ensure Python environment is properly initialized before processing media
                if (string.IsNullOrEmpty(_pythonExecutablePath) || _pythonExecutablePath == "python")
                {
                    Debug.WriteLine("NetPageViewModel: Python environment not properly initialized for media processing. Re-initializing...");
                    _pythonExecutablePath = _pythonEnvironmentService.PythonExecutablePath;
                    _huggingFaceScriptPath = _pythonEnvironmentService.HuggingFaceScriptPath;
                    Debug.WriteLine($"NetPageViewModel: Re-initialized Python paths. Python path: '{_pythonExecutablePath}', Script path: '{_huggingFaceScriptPath}'");
                }

                await _chatManagementService.CommunicateWithModelAsync(
                    message,
                    activeHfModel,
                    ChatMessages,
                    HasSelectedImage,
                    HasSelectedAudio,
                    SelectedImageName,
                    SelectedImagePath,
                    SelectedAudioName,
                    SelectedAudioPath,
                    GetLocalModelPath,
                    ShowAlert,
                    ClearMedia
                );
                return;
            }

            // Regular text message processing
            await _modelCommunicationService.CommunicateWithModelAsync(message,
                activeHfModel,
                ChatMessages,
                _pythonExecutablePath,
                _huggingFaceScriptPath,
                ShowAlert,
                () => Task.CompletedTask, /* CPU-friendly models suggestion handled by service */
                async () => await _modelExecutionService.InstallAcceleratePackageAsync(_pythonExecutablePath),
                (modelId) => "Consider using smaller models for better CPU performance.",
                async (modelId, inputText, model) =>
                {
                    string localModelPath = GetLocalModelPath(modelId);
                    return await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(modelId, inputText, model, _pythonExecutablePath, _huggingFaceScriptPath, localModelPath);
                });
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
            string localModelPath = GetLocalModelPath(modelId);
            return await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(
                modelId, inputText, model, _pythonExecutablePath, _huggingFaceScriptPath, localModelPath);
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
        }        // FIXED: Async method to update model input type with IMMEDIATE save
        private async Task UpdateModelInputTypeAsync(NeuralNetworkModel model, ModelInputType inputType)
        {
            Console.WriteLine($"🔥🔥🔥 UpdateModelInputTypeAsync CALLED - Model: '{model?.Name}', InputType: {inputType}");

            if (model == null)
            {
                Console.WriteLine("❌ Model is null, returning");
                return;
            }

            try
            {
                // Log current model state for debugging
                Console.WriteLine($"📊 CURRENT MODEL STATE: Name='{model.Name}', CurrentInputType={model.InputType}");

                // Check if the input type is actually changing
                bool isChanged = model.InputType != inputType;
                Console.WriteLine($"🔍 InputType changing? {isChanged} (from {model.InputType} to {inputType})");

                // FORCE SAVE - Let's bypass the no-change check temporarily to test the save chain
                if (!isChanged)
                {
                    Console.WriteLine("⚠️ NO CHANGE DETECTED - but forcing save anyway to test the save chain");
                    // Don't return - continue with save to test the chain
                }
                else
                {
                    Console.WriteLine($"🔄 CHANGE DETECTED - continuing with save");
                }

                Console.WriteLine($"🔄 STARTING InputType change for '{model.Name}' from {model.InputType} to {inputType}");
                Debug.WriteLine($"🔄 STARTING InputType change for '{model.Name}' from {model.InputType} to {inputType}");

                model.InputType = inputType;
                Console.WriteLine($"✏️ Model.InputType updated to: {model.InputType}");

                // IMMEDIATE save for InputType changes - NO DEBOUNCING
                CurrentModelStatus = $"Saving '{model.Name}' InputType = {inputType} to file...";
                OnPropertyChanged(nameof(AvailableModels));

                try
                {
                    Console.WriteLine($"💾 CALLING SavePersistedModelsAsync for '{model.Name}' InputType change");
                    Debug.WriteLine($"💾 CALLING SavePersistedModelsAsync for '{model.Name}' InputType change");

                    await SavePersistedModelsAsync();

                    Console.WriteLine($"✅ SUCCESSFULLY SAVED InputType change for '{model.Name}' to huggingFaceModels.json");
                    Debug.WriteLine($"✅ SUCCESSFULLY SAVED InputType change for '{model.Name}' to huggingFaceModels.json");
                    CurrentModelStatus = $"✅ Saved '{model.Name}' InputType = {inputType} to file";
                }
                catch (Exception saveEx)
                {
                    Console.WriteLine($"❌ ERROR saving InputType change: {saveEx.Message}");
                    Console.WriteLine($"❌ Stack trace: {saveEx.StackTrace}");
                    Debug.WriteLine($"❌ ERROR saving InputType change: {saveEx.Message}");
                    Debug.WriteLine($"❌ Stack trace: {saveEx.StackTrace}");
                    CurrentModelStatus = $"❌ Error saving InputType: {saveEx.Message}";
                    throw; // Re-throw to ensure error is visible
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR in UpdateModelInputTypeAsync: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                Debug.WriteLine($"❌ ERROR in UpdateModelInputTypeAsync: {ex.Message}");
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

                // Check for image-related models first (to prioritize image-to-text models)
                if (pipelineTag.Contains("image") ||
                    pipelineTag.Contains("vision") ||
                    pipelineTag == "image-classification" ||
                    pipelineTag == "object-detection" ||
                    pipelineTag == "image-segmentation" ||
                    pipelineTag == "depth-estimation" ||
                    pipelineTag == "image-to-text" ||  // Added specific check for image-to-text models like BLIP
                    modelId.Contains("vit") ||
                    modelId.Contains("clip") ||
                    modelId.Contains("resnet") ||
                    modelId.Contains("diffusion") ||
                    modelId.Contains("stable-diffusion") ||
                    modelId.Contains("blip") ||
                    modelId.Contains("yolo"))
                {
                    return ModelInputType.Image;
                }

                // Check for text-related models (excluding image-to-text which should be Image type)
                if ((pipelineTag.Contains("text") && !pipelineTag.Contains("image-to-text")) ||
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

                // Update AvailableModels on the main thread
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableModels.Clear();
                    foreach (var model in persistedModels ?? new List<NeuralNetworkModel>())
                    {
                        AvailableModels.Add(model);
                    }                // Restore previously activated models based on their IsActive state
                    RestoreActiveModels();

                    // Debug the active models after restoration
#if DEBUG
                    Debug.WriteLine($"=== DEBUG Active Models After Restoration ===");
                    Debug.WriteLine($"ActiveModels count: {ActiveModels.Count}");
                    foreach (var model in ActiveModels)
                    {
                        Debug.WriteLine($"Active Model: {model.Name}, Type: {model.Type}, ID: {model.Id}");
                    }
                    Debug.WriteLine($"Models with IsActive=true in AvailableModels:");
                    foreach (var model in AvailableModels.Where(m => m.IsActive))
                    {
                        Debug.WriteLine($"IsActive Model: {model.Name}, Type: {model.Type}, ID: {model.Id}, InActiveCollection: {ActiveModels.Any(am => am?.Id == model.Id)}");
                    }
                    Debug.WriteLine($"Current Mode - General: {IsGeneralModeActive}, Specific: {IsSpecificModeActive}");
                    Debug.WriteLine($"=== END DEBUG ===");
#endif

                    // Debug the input types again after adding to collection
#if DEBUG
                    Debug.WriteLine($"=== DEBUG AvailableModels Collection ===");
                    foreach (var model in AvailableModels)
                    {
                        Debug.WriteLine($"Model in collection: {model.Name}, InputType: {model.InputType} ({(int)model.InputType}), IsActive: {model.IsActive}");
                    }
                    Debug.WriteLine($"=== END DEBUG ===");
#endif

                    // DebugModelInputTypes();
#if DEBUG
                    DebugModelInputTypes();
                    TestInputTypeClassification();
#endif

                    // Update download button text for all loaded models
                    UpdateAllModelsDownloadButtonText();

                    // DISABLED: Re-classify existing models to fix any incorrect classifications
                    // This was overriding user's manual input type changes when navigating pages
                    // _ = Task.Run(async () => await ReclassifyExistingModelsAsync());
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

        /// <summary>
        /// Restores previously activated models of a specific type when a mode is re-enabled
        /// </summary>
        private void RestoreActiveModelsOfType(ModelType type)
        {
            try
            {
                var modelsToActivate = AvailableModels
                    .Where(m => m.IsActive && m.Type == type && !ActiveModels.Any(am => am?.Id == m.Id))
                    .ToList();

                Debug.WriteLine($"RestoreActiveModelsOfType: Found {modelsToActivate.Count} models of type {type} to restore");

                foreach (var model in modelsToActivate)
                {
                    // Add to ActiveModels collection without triggering save (to avoid excessive saves)
                    ActiveModels.Add(model);
                    StartModelMonitoring(model);
                    Debug.WriteLine($"RestoreActiveModelsOfType: Restored active state for model '{model.Name}' of type {type}");
                }

                // Update UI property notifications
                OnPropertyChanged(nameof(ActiveModelsCount));

                if (modelsToActivate.Count > 0)
                {
                    CurrentModelStatus = $"Restored {modelsToActivate.Count} {type} model(s)";
                    Debug.WriteLine($"RestoreActiveModelsOfType: Successfully restored {modelsToActivate.Count} models of type {type}");
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error restoring active models of type {type}", ex);
            }
        }

        private async Task ShowModelDetailsAndImportAsync(CSimple.Models.HuggingFaceModel model)
        {
            await _modelImportService.ShowModelDetailsAndImportAsync(
                model,
                GuessInputType,
                GetFriendlyModelName,
                modelId => _huggingFaceService.GetModelDetailsAsync(modelId),
                ShowConfirmation,
                ShowAlert,
                status => CurrentModelStatus = status,
                isLoading => IsLoading = isLoading,
                () => AvailableModels,
                availableModel => AvailableModels.Add(availableModel),
                SavePersistedModelsAsync,
                searchQuery => HuggingFaceSearchQuery = searchQuery
            );
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
        }

        // Original method now calls the overload
        private async Task SavePersistedModelsAsync()
        {
            // Logic moved from NetPage.xaml.cs
            // Reduced logging to minimize console noise
            var modelsToSave = AvailableModels
                .Where(m => m.IsHuggingFaceReference || !string.IsNullOrEmpty(m.HuggingFaceModelId))
                .ToList();

            Console.WriteLine($"🔍 SavePersistedModelsAsync: Found {modelsToSave.Count} models to save out of {AvailableModels?.Count ?? 0} available models");
            Debug.WriteLine($"🔍 SavePersistedModelsAsync: Found {modelsToSave.Count} models to save out of {AvailableModels?.Count ?? 0} available models");

            // Log InputType values before saving
            foreach (var model in modelsToSave)
            {
                Console.WriteLine($"📋 Model '{model.Name}' - InputType: {model.InputType}, IsHuggingFaceReference: {model.IsHuggingFaceReference}, HuggingFaceModelId: '{model.HuggingFaceModelId}'");
                Debug.WriteLine($"📋 Model '{model.Name}' - InputType: {model.InputType}, IsHuggingFaceReference: {model.IsHuggingFaceReference}, HuggingFaceModelId: '{model.HuggingFaceModelId}'");
            }

            Console.WriteLine($"💾 Calling SavePersistedModelsAsync overload with {modelsToSave.Count} models");
            await SavePersistedModelsAsync(modelsToSave); // Call the overload
            Console.WriteLine($"✅ SavePersistedModelsAsync overload completed");
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
            // Check if we have either text or compatible media to send
            bool hasText = !string.IsNullOrWhiteSpace(CurrentMessage);
            bool hasCompatibleMedia = HasSelectedMedia && await ValidateMediaUploadAsync();

            if ((!hasText && !hasCompatibleMedia) || IsAiTyping || ActiveModelsCount == 0)
                return;

            // For media-only messages, validate that we have compatible models
            if (!hasText && hasCompatibleMedia)
            {
                // Double-check media validation since we're sending without text
                if (!await ValidateMediaUploadAsync())
                    return;
            }

            var userMessage = hasText ? CurrentMessage.Trim() : string.Empty;
            CurrentMessage = string.Empty; // Clear input

            Debug.WriteLine($"Chat: Sending {(hasText ? $"message '{userMessage}'" : "media-only message")} to active models (count: {ActiveModelsCount})");

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

        /// <summary>
        /// Validates that appropriate model types are active for any selected media uploads
        /// </summary>
        private async Task<bool> ValidateMediaUploadAsync()
        {
            return await _chatManagementService.ValidateMediaUploadAsync(
                HasSelectedImage,
                HasSelectedAudio,
                SupportsImageInput,
                SupportsAudioInput,
                ShowAlert
            );
        }

        /// <summary>
        /// Suggests models based on media type for better user experience
        /// </summary>
        private async Task SuggestModelsForMediaTypeAsync(List<string> missingTypes)
        {
            try
            {
                var suggestions = new List<string>();

                foreach (var mediaType in missingTypes)
                {
                    switch (mediaType.ToLower())
                    {
                        case "image":
                            suggestions.AddRange(new[] {
                                "• microsoft/DiT (Image Understanding)",
                                "• google/vit-base-patch16-224 (Vision Transformer)",
                                "• openai/clip-vit-base-patch32 (CLIP Vision)",
                                "• facebook/detr-resnet-50 (Object Detection)"
                            });
                            break;
                        case "audio":
                            suggestions.AddRange(new[] {
                                "• openai/whisper-base (Speech Recognition)",
                                "• facebook/wav2vec2-base-960h (Audio Processing)",
                                "• microsoft/speecht5_asr (Speech-to-Text)",
                                "• facebook/hubert-base-ls960 (Audio Understanding)"
                            });
                            break;
                    }
                }

                if (suggestions.Any())
                {
                    var suggestionText = string.Join("\n", suggestions);
                    await ShowAlert?.Invoke(
                        "Recommended Models",
                        $"Here are some recommended models for the media types you're trying to upload:\n\n{suggestionText}\n\n" +
                        "You can search for these models using the HuggingFace search feature.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error suggesting models: {ex.Message}");
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
            var (imagePath, imageName) = await _mediaSelectionService.SelectImageAsync();

            if (!string.IsNullOrEmpty(imagePath))
            {
                SelectedImagePath = imagePath;
                SelectedImageName = imageName;

                // Clear audio if image is selected (for simplicity, can be multimodal later)
                SelectedAudioPath = null;
                SelectedAudioName = null;

                // Trigger UI updates
                OnPropertyChanged(nameof(HasSelectedImage));
                OnPropertyChanged(nameof(HasSelectedAudio));
                OnPropertyChanged(nameof(HasSelectedMedia));
                OnPropertyChanged(nameof(HasCompatibleMediaSelected));
                OnPropertyChanged(nameof(CanSendMessage));
                OnPropertyChanged(nameof(CurrentInputModeDescription));

                // Check if there are active image models and provide feedback
                await _mediaSelectionService.CheckModelCompatibilityForMediaAsync("image", imageName, ActiveModels);
            }
        }

        private async Task SelectAudioAsync()
        {
            var (audioPath, audioName) = await _mediaSelectionService.SelectAudioAsync();

            if (!string.IsNullOrEmpty(audioPath))
            {
                SelectedAudioPath = audioPath;
                SelectedAudioName = audioName;

                // Clear image if audio is selected (for simplicity, can be multimodal later)
                SelectedImagePath = null;
                SelectedImageName = null;

                // Trigger UI updates
                OnPropertyChanged(nameof(HasSelectedImage));
                OnPropertyChanged(nameof(HasSelectedAudio));
                OnPropertyChanged(nameof(HasSelectedMedia));
                OnPropertyChanged(nameof(HasCompatibleMediaSelected));
                OnPropertyChanged(nameof(CanSendMessage));
                OnPropertyChanged(nameof(CurrentInputModeDescription));

                // Check if there are active audio models and provide feedback
                await _mediaSelectionService.CheckModelCompatibilityForMediaAsync("audio", audioName, ActiveModels);
            }
        }

        /// <summary>
        /// Generic file selector that automatically detects file type and sets appropriate media
        /// Filters file types based on currently activated models
        /// </summary>
        private async Task SelectFileAsync()
        {
            var (filePath, fileName, fileType) = await _mediaSelectionService.SelectFileAsync(
                SupportsImageInput,
                SupportsAudioInput,
                ActiveModels);

            if (!string.IsNullOrEmpty(filePath))
            {
                if (fileType == "image")
                {
                    SelectedImagePath = filePath;
                    SelectedImageName = fileName;
                    // Clear audio
                    SelectedAudioPath = null;
                    SelectedAudioName = null;
                }
                else if (fileType == "audio")
                {
                    SelectedAudioPath = filePath;
                    SelectedAudioName = fileName;
                    // Clear image
                    SelectedImagePath = null;
                    SelectedImageName = null;
                }

                // Trigger UI updates
                OnPropertyChanged(nameof(HasSelectedImage));
                OnPropertyChanged(nameof(HasSelectedAudio));
                OnPropertyChanged(nameof(HasSelectedMedia));
                OnPropertyChanged(nameof(HasCompatibleMediaSelected));
                OnPropertyChanged(nameof(CanSendMessage));
                OnPropertyChanged(nameof(CurrentInputModeDescription));
            }
        }

        /// <summary>
        /// Checks model compatibility when media is selected and provides immediate feedback
        /// </summary>
        internal async Task CheckModelCompatibilityForMediaAsync(string mediaType, string fileName)
        {
            await _mediaSelectionService.CheckModelCompatibilityForMediaAsync(mediaType, fileName, ActiveModels);
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
            OnPropertyChanged(nameof(HasCompatibleMediaSelected));
            OnPropertyChanged(nameof(CanSendMessage));
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
                OnPropertyChanged(nameof(HasCompatibleMediaSelected));
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

        /// <summary>
        /// Formats file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">File size in bytes</param>
        /// <returns>Formatted size string (e.g., "1.5 GB", "256 MB")</returns>
        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";

            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Gets the total download size for a model by summing all file sizes
        /// </summary>
        /// <param name="model">The neural network model to check</param>
        /// <returns>Formatted total size string and size in bytes</returns>
        private async Task<(string formattedSize, long totalBytes)> GetModelDownloadSizeAsync(NeuralNetworkModel model)
        {
            try
            {
                var modelId = model.HuggingFaceModelId ?? model.Id;
                var filesWithSizes = await _huggingFaceService.GetModelFilesWithSizeAsync(modelId);

                long totalBytes = filesWithSizes.Sum(f => (long)f.Size);
                string formattedSize = FormatFileSize(totalBytes);

                Debug.WriteLine($"Model {modelId} total size: {formattedSize} ({totalBytes} bytes)");
                return (formattedSize, totalBytes);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating model download size: {ex.Message}");
                return ("Unknown size", 0);
            }
        }

        /// <summary>
        /// Opens the model's directory in Windows File Explorer
        /// </summary>
        /// <param name="model">The neural network model to open in explorer</param>
        private void OpenModelInExplorer(NeuralNetworkModel model)
        {
            try
            {
                if (model == null)
                {
                    Debug.WriteLine("OpenModelInExplorer: Model is null");
                    return;
                }

                string modelPath = null;

                // Determine the model path based on whether it's a HuggingFace model or local model
                if (model.IsHuggingFaceReference && !string.IsNullOrEmpty(model.HuggingFaceModelId))
                {
                    // For HuggingFace models, use the HF cache directory
                    modelPath = GetModelDirectoryPath(model.HuggingFaceModelId);
                }
                else if (!string.IsNullOrEmpty(model.Name))
                {
                    // For local models, try to construct path from model name
                    modelPath = GetModelDirectoryPath(model.Name);
                }
                else if (!string.IsNullOrEmpty(model.Id))
                {
                    // Fallback: try to construct path from model ID
                    modelPath = GetModelDirectoryPath(model.Id);
                }

                if (!string.IsNullOrEmpty(modelPath) && Directory.Exists(modelPath))
                {
                    Debug.WriteLine($"Opening model directory in explorer: {modelPath}");

                    // Use Windows-specific command to open File Explorer
                    var processInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{modelPath}\"",
                        UseShellExecute = true
                    };

                    System.Diagnostics.Process.Start(processInfo);
                }
                else
                {
                    Debug.WriteLine($"Model directory not found: {modelPath}");
                    ShowAlert?.Invoke("Directory Not Found",
                        $"Could not find the directory for model '{model.Name}'. The model files may not be downloaded yet.",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error opening model in explorer: {ex.Message}");
                ShowAlert?.Invoke("Error",
                    $"Failed to open model directory in File Explorer: {ex.Message}",
                    "OK");
            }
        }

        /// <summary>
        /// Gets the local model directory path for a given model ID
        /// This is used to force local-only execution and avoid HuggingFace API calls
        /// </summary>
        /// <param name="modelId">The HuggingFace model ID</param>
        /// <returns>The local directory path where the model is cached</returns>
        public string GetLocalModelPath(string modelId)
        {
            return GetModelDirectoryPath(modelId);
        }

        /// <summary>
        /// Re-classifies existing models that may have been incorrectly classified.
        /// This fixes models that were imported before the InputType classification logic was corrected.
        /// </summary>
        public async Task ReclassifyExistingModelsAsync()
        {
            try
            {
                Debug.WriteLine("ReclassifyExistingModelsAsync: Starting re-classification of existing models...");
                bool hasChanges = false;

                foreach (var model in AvailableModels.Where(m => m.IsHuggingFaceReference))
                {
                    // Create a temporary HuggingFaceModel to use with GuessInputType
                    var tempHFModel = new HuggingFaceModel
                    {
                        ModelId = model.HuggingFaceModelId,
                        Id = model.HuggingFaceModelId,
                        Description = model.Description,
                        // Try to infer pipeline_tag from model name patterns
                        Pipeline_tag = InferPipelineTagFromModelId(model.HuggingFaceModelId)
                    };

                    var correctInputType = GuessInputType(tempHFModel);

                    if (model.InputType != correctInputType)
                    {
                        Debug.WriteLine($"Re-classifying model '{model.Name}': {model.InputType} -> {correctInputType}");
                        model.InputType = correctInputType;
                        hasChanges = true;
                    }
                }

                if (hasChanges)
                {
                    Debug.WriteLine("ReclassifyExistingModelsAsync: Changes detected, saving updated models...");
                    await SavePersistedModelsAsync();
                    OnPropertyChanged(nameof(AvailableModels));
                    OnPropertyChanged(nameof(SupportsTextInput));
                    OnPropertyChanged(nameof(SupportsImageInput));
                    OnPropertyChanged(nameof(SupportsAudioInput));
                    OnPropertyChanged(nameof(SupportedInputTypesText));
                    CurrentModelStatus = "Model classifications updated";
                }
                else
                {
                    Debug.WriteLine("ReclassifyExistingModelsAsync: No changes needed");
                }
            }
            catch (Exception ex)
            {
                HandleError("Error re-classifying existing models", ex);
            }
        }

        /// <summary>
        /// Infers the pipeline tag from a model ID to help with re-classification
        /// </summary>
        private string InferPipelineTagFromModelId(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return "";

            var lowerModelId = modelId.ToLowerInvariant();

            // Image-related models
            if (lowerModelId.Contains("blip")) return "image-to-text";
            if (lowerModelId.Contains("clip")) return "image-classification";
            if (lowerModelId.Contains("vit")) return "image-classification";
            if (lowerModelId.Contains("diffusion")) return "text-to-image";
            if (lowerModelId.Contains("yolo")) return "object-detection";

            // Audio-related models
            if (lowerModelId.Contains("whisper")) return "automatic-speech-recognition";
            if (lowerModelId.Contains("wav2vec")) return "audio-classification";
            if (lowerModelId.Contains("hubert")) return "audio-classification";

            // Text-related models
            if (lowerModelId.Contains("gpt")) return "text-generation";
            if (lowerModelId.Contains("bert")) return "text-classification";
            if (lowerModelId.Contains("t5")) return "text2text-generation";
            if (lowerModelId.Contains("llama")) return "text-generation";
            if (lowerModelId.Contains("bloom")) return "text-generation";
            if (lowerModelId.Contains("bart")) return "text2text-generation";
            if (lowerModelId.Contains("roberta")) return "text-classification";

            // Default fallback
            return "text-generation";
        }

        // --- Debug Methods ---
        /// <summary>
        /// Debug method to manually test the GuessInputType method with specific models
        /// </summary>
        public void TestInputTypeClassification()
        {
#if DEBUG
            Debug.WriteLine("=== TESTING INPUT TYPE CLASSIFICATION ===");

            // Test BLIP model
            var blipModel = new HuggingFaceModel
            {
                ModelId = "Salesforce/blip-image-captioning-base",
                Pipeline_tag = "image-to-text",
                Description = "BLIP model for image captioning"
            };
            var blipInputType = GuessInputType(blipModel);
            Debug.WriteLine($"BLIP model InputType: {blipInputType} ({(int)blipInputType}) - Should be Image (1)");

            // Test a regular text model
            var gptModel = new HuggingFaceModel
            {
                ModelId = "openai-community/gpt2",
                Pipeline_tag = "text-generation",
                Description = "GPT-2 text generation model"
            };
            var gptInputType = GuessInputType(gptModel);
            Debug.WriteLine($"GPT-2 model InputType: {gptInputType} ({(int)gptInputType}) - Should be Text (0)");

            // Test a vision model
            var vitModel = new HuggingFaceModel
            {
                ModelId = "google/vit-base-patch16-224",
                Pipeline_tag = "image-classification",
                Description = "Vision Transformer for image classification"
            };
            var vitInputType = GuessInputType(vitModel);
            Debug.WriteLine($"ViT model InputType: {vitInputType} ({(int)vitInputType}) - Should be Image (1)");

            Debug.WriteLine("=== END TESTING ===");
#endif
        }

        // Public method to execute a model from OrientPageViewModel
        public async Task<string> ExecuteModelAsync(string modelId, string inputText)
        {
            Console.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Executing model: {modelId} with input length: {inputText?.Length ?? 0}");
            Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Executing model: {modelId} with input length: {inputText?.Length ?? 0}");

            try
            {
                // Find the model in available models
                var model = AvailableModels.FirstOrDefault(m =>
                    m.HuggingFaceModelId == modelId ||
                    m.Name == modelId ||
                    m.Id == modelId);

                if (model == null)
                {
                    throw new InvalidOperationException($"Model '{modelId}' not found in available models");
                }

                // Use the existing model execution infrastructure
                string localModelPath = GetLocalModelPath(modelId);
                var result = await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(
                    modelId, inputText, model, _pythonExecutablePath, _huggingFaceScriptPath, localModelPath);

                Console.WriteLine($"✅ [NetPageViewModel.ExecuteModelAsync] Model execution successful, result length: {result?.Length ?? 0}");
                Debug.WriteLine($"✅ [NetPageViewModel.ExecuteModelAsync] Model execution successful, result length: {result?.Length ?? 0}");

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [NetPageViewModel.ExecuteModelAsync] Model execution failed: {ex.Message}");
                Debug.WriteLine($"❌ [NetPageViewModel.ExecuteModelAsync] Model execution failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Restores previously activated models based on their IsActive state
        /// </summary>
        private void RestoreActiveModels()
        {
            try
            {
                var modelsToActivate = AvailableModels.Where(m => m.IsActive).ToList();

                Debug.WriteLine($"RestoreActiveModels: Found {modelsToActivate.Count} models marked as active");

                foreach (var model in modelsToActivate)
                {
                    // Verify the model can be activated based on current mode
                    if ((model.Type == ModelType.General && IsGeneralModeActive) ||
                        (model.Type == ModelType.GoalSpecific && IsSpecificModeActive))
                    {
                        // Add to ActiveModels collection without triggering save (to avoid infinite loop)
                        if (!ActiveModels.Any(m => m?.Id == model.Id))
                        {
                            ActiveModels.Add(model);
                            StartModelMonitoring(model);
                            Debug.WriteLine($"RestoreActiveModels: Restored active state for model '{model.Name}'");
                            CurrentModelStatus = $"Restored '{model.Name}' to active state";
                        }
                    }
                    else
                    {
                        // Model is marked as active but incompatible with current mode - just log it, don't reset the state
                        // This preserves the user's intention to keep it active when the compatible mode is enabled
                        Debug.WriteLine($"RestoreActiveModels: Skipping incompatible model '{model.Name}' (Type: {model.Type}, GeneralMode: {IsGeneralModeActive}, SpecificMode: {IsSpecificModeActive}) - will restore when mode is enabled");
                    }
                }

                // Update UI property notifications
                OnPropertyChanged(nameof(ActiveModelsCount));

                if (ActiveModels.Count > 0)
                {
                    CurrentModelStatus = $"Restored {ActiveModels.Count} active model(s)";
                    Debug.WriteLine($"RestoreActiveModels: Successfully restored {ActiveModels.Count} active models");
                }
                else
                {
                    Debug.WriteLine("RestoreActiveModels: No models were restored to active state");
                }
            }
            catch (Exception ex)
            {
                HandleError("Error restoring active models", ex);
            }
        }
    }
}
