using CSimple.Models;
using CSimple.Services;
using CSimple.Services.AppModeService;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using Newtonsoft.Json;
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
        private readonly PipelineManagementService _pipelineManagementService; // Add pipeline management service
        private readonly InputCaptureService _inputCaptureService; // Add input capture service for intelligence recording
        private readonly MouseTrackingService _mouseTrackingService; // Add mouse tracking service
        private readonly AudioCaptureService _audioCaptureService; // Add audio capture service
        private readonly ScreenCaptureService _screenCaptureService; // Add screen capture service for webcam image capture
        private readonly PipelineExecutionService _pipelineExecutionService; // Add pipeline execution service
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

        // Pipeline interaction backing fields
        private string _selectedPipeline = null;
        private bool _isIntelligenceActive = false;
        private string _userGoalInput = string.Empty;
        private bool _recordMouseInputs = true;
        private bool _recordKeyboardInputs = true;
        private PipelineData _selectedPipelineData = null; // Store the full pipeline data
        private DateTime _lastPipelineRefresh = DateTime.MinValue; // Track when pipelines were last loaded
        private DateTime _lastMouseEventTime = DateTime.MinValue; // Track mouse event throttling for intelligence

        // Intelligence processing fields
        private CancellationTokenSource _intelligenceProcessingCts;
        private CancellationTokenSource _intelligenceWebcamCts; // For webcam image capture
        private DateTime _lastScreenCapture = DateTime.MinValue;
        private DateTime _lastPipelineExecution = DateTime.MinValue;
        private readonly object _intelligenceProcessingLock = new object();
        private bool _isProcessingIntelligence = false;

        // Enhanced intelligence processing fields
        private readonly SettingsService _settingsService;
        private readonly List<byte[]> _capturedScreenshots = new();
        private readonly List<byte[]> _capturedAudioData = new();
        private readonly List<string> _capturedTextData = new();
        private readonly object _capturedDataLock = new object();
        private DateTime _lastDataClearTime = DateTime.Now;
        private Task _currentPipelineTask = null;

        // Session and Memory Tracking Fields
        private readonly string _sessionId = Guid.NewGuid().ToString("N")[..8];
        private readonly DateTime _sessionStartTime = DateTime.Now;
        private int _intelligenceCycleCount = 0;
        private readonly List<PipelineExecutionRecord> _pipelineHistory = new();

        // Intelligence Session Persistence (similar to ObservePage pattern)
        private readonly List<IntelligenceToggleEvent> _intelligenceToggleHistory = new();
        private readonly DataService _dataService;

        // Intelligence Action Recording Buffer (similar to ObservePage _currentRecordingBuffer)
        private readonly List<ActionItem> _intelligenceRecordingBuffer = new();

        // Track the current intelligence session for proper START/STOP grouping
        private Guid? _currentIntelligenceSessionId;
        private DateTime _currentSessionStartTime;
        private ActionGroup _currentIntelligenceSession;

        // Model Training/Alignment backing fields
        private NeuralNetworkModel _selectedTrainingModel = null;
        private string _trainingDatasetPath = string.Empty;
        private string _validationDatasetPath = string.Empty;
        private string _testDatasetPath = string.Empty;
        private string _selectedFineTuningMethod = "LoRA";
        private double _learningRate = 0.0001;
        private int _epochs = 3;
        private int _batchSize = 4;
        private bool _isTraining = false;
        private double _trainingProgress = 0.0;
        private string _trainingStatus = "Ready";
        private string _trainingEta = string.Empty;
        private string _trainingElapsed = string.Empty;
        private string _newModelName = string.Empty;
        private DateTime _trainingStartTime = DateTime.MinValue;

        // --- Observable Properties ---
        public ObservableCollection<NeuralNetworkModel> AvailableModels { get; } = new();
        public ObservableCollection<NeuralNetworkModel> ActiveModels { get; } = new();
        public ObservableCollection<SpecificGoal> AvailableGoals { get; } = new();
        public ObservableCollection<ChatMessage> ChatMessages { get; } = new();

        // Pipeline interaction collections
        public ObservableCollection<string> AvailablePipelines { get; } = new();
        public ObservableCollection<ChatMessage> PipelineChatMessages { get; } = new();
        public ObservableCollection<PipelineData> AvailablePipelineData { get; } = new(); // Store full pipeline data

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

        // Pipeline interaction properties
        public string SelectedPipeline
        {
            get => _selectedPipeline;
            set => SetProperty(ref _selectedPipeline, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(SelectedPipelineStatus));
                OnPropertyChanged(nameof(PipelineStatusColor));
                OnPropertyChanged(nameof(CanSendGoal));
                OnPropertyChanged(nameof(SelectedPipelineInfo));
                OnPropertyChanged(nameof(PipelineNodeCount));
                OnPropertyChanged(nameof(PipelineConnectionCount));
                UpdateSelectedPipelineData();
            });
        }

        public PipelineData SelectedPipelineData
        {
            get => _selectedPipelineData;
            private set => SetProperty(ref _selectedPipelineData, value);
        }

        public bool IsIntelligenceActive
        {
            get => _isIntelligenceActive;
            set => SetProperty(ref _isIntelligenceActive, value, onChanged: async () =>
            {
                OnPropertyChanged(nameof(IntelligenceStatusText));
                OnPropertyChanged(nameof(IntelligenceStatusColor));
                OnPropertyChanged(nameof(RecordingStatusIcon));

                // Save toggle event for persistence (like ObservePage saves actions)
                try
                {
                    await SaveIntelligenceToggleEvent(value, value ? "START" : "STOP");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving intelligence toggle event: {ex.Message}");
                }

                // Start or stop intelligence recording
                if (value)
                {
                    StartIntelligenceRecording();
                }
                else
                {
                    StopIntelligenceRecording();
                }
            });
        }

        public string UserGoalInput
        {
            get => _userGoalInput;
            set => SetProperty(ref _userGoalInput, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(CanSendGoal));
            });
        }

        public bool RecordMouseInputs
        {
            get => _recordMouseInputs;
            set => SetProperty(ref _recordMouseInputs, value);
        }

        public bool RecordKeyboardInputs
        {
            get => _recordKeyboardInputs;
            set => SetProperty(ref _recordKeyboardInputs, value);
        }

        // Pipeline status properties
        public string IntelligenceStatusText => _isIntelligenceActive ? "ACTIVE" : "INACTIVE";
        public string IntelligenceStatusColor => _isIntelligenceActive ? "#4CAF50" : "#808080"; // Success green : Neutral gray
        public string RecordingStatusIcon => _isIntelligenceActive ? "🔴" : "⏸️";
        public string SelectedPipelineStatus => !string.IsNullOrEmpty(_selectedPipeline) ? "Connected" : "No pipeline selected";
        public string PipelineStatusColor => !string.IsNullOrEmpty(_selectedPipeline) ? "#4CAF50" : "#FFCC00"; // Success green : Warning yellow

        // Enhanced pipeline information properties
        public string SelectedPipelineInfo
        {
            get
            {
                if (_selectedPipelineData == null)
                    return "No pipeline selected";

                return $"Created: {_selectedPipelineData.LastModified:MM/dd/yyyy HH:mm}";
            }
        }

        public int PipelineNodeCount => _selectedPipelineData?.Nodes?.Count ?? 0;
        public int PipelineConnectionCount => _selectedPipelineData?.Connections?.Count ?? 0;
        public int PipelineCount => AvailablePipelineData.Count;
        public int AvailablePipelinesCount => AvailablePipelineData.Count;
        public int RunningProcesses => 0; // Placeholder for running processes count
        public int ActiveInputNodesCount => 0; // Placeholder
        public int ConnectedModelsCount => ActiveModelsCount;
        public bool CanSendGoal => !string.IsNullOrWhiteSpace(_userGoalInput) && !string.IsNullOrEmpty(_selectedPipeline);

        // Model Training/Alignment Properties
        public NeuralNetworkModel SelectedTrainingModel
        {
            get => _selectedTrainingModel;
            set => SetProperty(ref _selectedTrainingModel, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(CanStartTraining));
                OnPropertyChanged(nameof(CanToggleTraining));
                OnPropertyChanged(nameof(TrainingModelName));
            });
        }

        public string TrainingDatasetPath
        {
            get => _trainingDatasetPath;
            set => SetProperty(ref _trainingDatasetPath, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(CanStartTraining));
                OnPropertyChanged(nameof(CanToggleTraining));
            });
        }

        public string ValidationDatasetPath
        {
            get => _validationDatasetPath;
            set => SetProperty(ref _validationDatasetPath, value);
        }

        public string TestDatasetPath
        {
            get => _testDatasetPath;
            set => SetProperty(ref _testDatasetPath, value);
        }

        public string SelectedFineTuningMethod
        {
            get => _selectedFineTuningMethod;
            set => SetProperty(ref _selectedFineTuningMethod, value);
        }

        public double LearningRate
        {
            get => _learningRate;
            set => SetProperty(ref _learningRate, value);
        }

        public int Epochs
        {
            get => _epochs;
            set => SetProperty(ref _epochs, value);
        }

        public int BatchSize
        {
            get => _batchSize;
            set => SetProperty(ref _batchSize, value);
        }

        public bool IsTraining
        {
            get => _isTraining;
            set => SetProperty(ref _isTraining, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(CanStartTraining));
                OnPropertyChanged(nameof(CanToggleTraining));
                OnPropertyChanged(nameof(TrainingStatusDisplay));
            });
        }

        public double TrainingProgress
        {
            get => _trainingProgress;
            set => SetProperty(ref _trainingProgress, value);
        }

        public string TrainingStatus
        {
            get => _trainingStatus;
            set => SetProperty(ref _trainingStatus, value, onChanged: () => OnPropertyChanged(nameof(TrainingStatusDisplay)));
        }

        public string TrainingEta
        {
            get => _trainingEta;
            set => SetProperty(ref _trainingEta, value, onChanged: () => OnPropertyChanged(nameof(TrainingStatusDisplay)));
        }

        public string TrainingElapsed
        {
            get => _trainingElapsed;
            set => SetProperty(ref _trainingElapsed, value, onChanged: () => OnPropertyChanged(nameof(TrainingStatusDisplay)));
        }

        public string NewModelName
        {
            get => _newModelName;
            set => SetProperty(ref _newModelName, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(CanStartTraining));
                OnPropertyChanged(nameof(CanToggleTraining));
            });
        }

        // Computed properties for training
        public bool CanStartTraining =>
            !IsTraining &&
            SelectedTrainingModel != null &&
            !string.IsNullOrWhiteSpace(TrainingDatasetPath) &&
            !string.IsNullOrWhiteSpace(NewModelName);

        public bool CanToggleTraining =>
            IsTraining || (SelectedTrainingModel != null &&
            !string.IsNullOrWhiteSpace(TrainingDatasetPath) &&
            !string.IsNullOrWhiteSpace(NewModelName));

        public string TrainingModelName => SelectedTrainingModel?.Name ?? "No model selected";

        public string TrainingStatusDisplay
        {
            get
            {
                if (!IsTraining)
                    return TrainingStatus;

                var status = $"{TrainingStatus} ({TrainingProgress:P0})";
                if (!string.IsNullOrEmpty(TrainingElapsed))
                    status += $" | Elapsed: {TrainingElapsed}";
                if (!string.IsNullOrEmpty(TrainingEta))
                    status += $" | ETA: {TrainingEta}";

                return status;
            }
        }

        // Available text-to-text models for training
        public ObservableCollection<NeuralNetworkModel> AvailableTextModels =>
            new(AvailableModels.Where(m =>
                m.IsHuggingFaceReference &&
                !string.IsNullOrEmpty(m.HuggingFaceModelId) &&
                IsModelDownloaded(m.HuggingFaceModelId) &&
                (m.InputType == ModelInputType.Text ||
                 m.HuggingFaceModelId.ToLowerInvariant().Contains("t5") ||
                 m.HuggingFaceModelId.ToLowerInvariant().Contains("bart") ||
                 m.HuggingFaceModelId.ToLowerInvariant().Contains("gpt"))));

        // Fine-tuning method options
        public List<string> FineTuningMethods { get; } = new List<string>
        {
            "LoRA (Low-Rank Adaptation)",
            "Full Fine-tuning",
            "Adapter Layers",
            "Prefix Tuning"
        };

        // --- Commands ---
        public ICommand ToggleGeneralModeCommand { get; }
        public ICommand ToggleSpecificModeCommand { get; }
        public ICommand ActivateModelCommand { get; }
        public ICommand DeactivateModelCommand { get; }
        public ICommand ToggleModelActivationCommand { get; }
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

        // Pipeline interaction commands
        public ICommand SendGoalCommand { get; }
        public ICommand ClearPipelineChatCommand { get; }
        public ICommand RefreshPipelinesCommand { get; }
        public ICommand CreateNewPipelineCommand { get; }
        public ICommand ToggleIntelligenceCommand { get; } // Add toggle intelligence command

        // Model Training/Alignment commands
        public ICommand SelectTrainingModelCommand { get; }
        public ICommand SelectTrainingDatasetCommand { get; }
        public ICommand SelectValidationDatasetCommand { get; }
        public ICommand SelectTestDatasetCommand { get; }
        public ICommand StartTrainingCommand { get; }
        public ICommand StopTrainingCommand { get; }
        public ICommand ToggleTrainingCommand { get; }
        public ICommand CreateDatasetCommand { get; }

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
        public NetPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, PythonBootstrapper pythonBootstrapper, AppModeService appModeService, PythonEnvironmentService pythonEnvironmentService, ModelCommunicationService modelCommunicationService, ModelExecutionService modelExecutionService, ModelImportExportService modelImportExportService, ITrayService trayService, IModelDownloadService modelDownloadService, IModelImportService modelImportService, IChatManagementService chatManagementService, IMediaSelectionService mediaSelectionService, DataService dataService, IAppPathService appPathService, PipelineExecutionService pipelineExecutionService, PipelineManagementService pipelineManagementService = null, InputCaptureService inputCaptureService = null, MouseTrackingService mouseTrackingService = null, AudioCaptureService audioCaptureService = null, ScreenCaptureService screenCaptureService = null)
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
            _dataService = dataService; // Initialize data service for intelligence session persistence
            _settingsService = new SettingsService(dataService, appPathService); // Initialize settings service
            _pipelineExecutionService = pipelineExecutionService; // Initialize pipeline execution service
            _pipelineManagementService = pipelineManagementService; // Optional dependency
            _inputCaptureService = inputCaptureService; // Optional dependency for intelligence recording
            _mouseTrackingService = mouseTrackingService; // Optional dependency for mouse tracking
            _audioCaptureService = audioCaptureService; // Optional dependency for audio capture
            _screenCaptureService = screenCaptureService; // Optional dependency for webcam image capture

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

            // Subscribe to input capture services for intelligence recording
            SetupIntelligenceInputCapture();

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
            ToggleModelActivationCommand = new Command<NeuralNetworkModel>(ToggleModelActivation);
            LoadSpecificGoalCommand = new Command<SpecificGoal>(LoadSpecificGoal);
            ShareModelCommand = new Command(async () => await ShareModel());
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

            // Pipeline interaction commands
            SendGoalCommand = new Command<string>(async (goal) => await SendGoalAsync(goal));
            ClearPipelineChatCommand = new Command(ClearPipelineChat);
            RefreshPipelinesCommand = new Command(async () => await RefreshPipelinesAsync());
            CreateNewPipelineCommand = new Command(async () => await CreateNewPipelineAsync());
            ToggleIntelligenceCommand = new Command(() => IsIntelligenceActive = !IsIntelligenceActive);

            // Initialize training commands
            SelectTrainingModelCommand = new Command(async () => await SelectTrainingModelAsync());
            SelectTrainingDatasetCommand = new Command(async () => await SelectTrainingDatasetAsync());
            SelectValidationDatasetCommand = new Command(async () => await SelectValidationDatasetAsync());
            SelectTestDatasetCommand = new Command(async () => await SelectTestDatasetAsync());
            StartTrainingCommand = new Command(async () => await StartTrainingAsync(), () => CanStartTraining);
            StopTrainingCommand = new Command(async () => await StopTrainingAsync(), () => IsTraining);
            ToggleTrainingCommand = new Command(async () => await ToggleTrainingAsync(), () => CanToggleTraining);
            CreateDatasetCommand = new Command(async () => await CreateDatasetAsync());

            // Load actual pipelines from OrientPage instead of placeholder data
            // This will be called in LoadDataAsync when the page appears

            // Initialize empty collections for now - they'll be populated when page loads
            AvailablePipelines.Clear();
            AvailablePipelineData.Clear();

            // Check cache directory
            EnsureHFModelCacheDirectoryExists();

            // Populate categories
            HuggingFaceCategories = new List<string> { "All Categories" };
            HuggingFaceCategories.AddRange(_huggingFaceService.GetModelCategoryFilters().Keys);

            // Initialize downloaded models state
            RefreshDownloadedModelsList();

            // Load initial data
            // Note: Loading is triggered by OnAppearing in the View

            // Initialize intelligence session tracking (load previous session history)
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadIntelligenceSessionHistory();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error loading intelligence session history during initialization: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Helper method to find corresponding model for pipeline execution
        /// </summary>
        private NeuralNetworkModel FindCorrespondingModel(NodeViewModel node)
        {
            // Use this instance's AvailableModels directly since we're already in NetPageViewModel
            return AvailableModels?.FirstOrDefault(m =>
                m.Name.Equals(node.Name, StringComparison.OrdinalIgnoreCase) ||
                m.HuggingFaceModelId.Contains(node.Name, StringComparison.OrdinalIgnoreCase));
        }        // Check if a model is downloaded by examining the actual directory size and essential files
        public bool IsModelDownloaded(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return false;

            try
            {
                string cacheDirectory = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";

                if (!Directory.Exists(cacheDirectory))
                    return false;

                // Check for model directory by trying multiple naming conventions
                var possibleDirNames = new[]
                {
                    modelId.Replace("/", "_"),           // org/model -> org_model
                    $"models--{modelId.Replace("/", "--")}", // org/model -> models--org--model
                    modelId.Replace("/", "__"),          // org/model -> org__model (some variants)
                    modelId.Split('/').LastOrDefault(),  // just the model name
                    modelId                              // exact match
                };

                foreach (var dirName in possibleDirNames.Where(d => !string.IsNullOrEmpty(d)))
                {
                    var modelPath = Path.Combine(cacheDirectory, dirName);

                    if (Directory.Exists(modelPath))
                    {
                        // Calculate total directory size
                        long totalSize = GetDirectorySize(modelPath);

                        // Check for essential model files
                        bool hasEssentialFiles = HasEssentialModelFiles(modelPath);

                        // Consider downloaded if > 50KB (more realistic threshold) AND has essential files
                        bool isDownloaded = totalSize > 51200 && hasEssentialFiles;

#if DEBUG
                        if (totalSize > 0)
                        {
                            Debug.WriteLine($"Model '{modelId}' at '{dirName}' - Size: {totalSize:N0} bytes ({totalSize / 1024.0:F1} KB), Essential files: {hasEssentialFiles}, Downloaded: {isDownloaded}");
                        }
#endif
                        if (isDownloaded)
                            return true;
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

        // Check if model directory contains essential files that indicate a proper download
        private bool HasEssentialModelFiles(string modelPath)
        {
            try
            {
                var files = Directory.GetFiles(modelPath, "*", SearchOption.AllDirectories);

                // Look for common model files
                var hasConfigFile = files.Any(f => Path.GetFileName(f).Equals("config.json", StringComparison.OrdinalIgnoreCase));
                var hasModelFile = files.Any(f =>
                {
                    var fileName = Path.GetFileName(f).ToLowerInvariant();
                    return fileName.Contains("pytorch_model") ||
                           fileName.Contains("model.safetensors") ||
                           fileName.Contains("model.bin") ||
                           fileName.EndsWith(".bin") ||
                           fileName.EndsWith(".safetensors") ||
                           fileName.EndsWith(".onnx") ||
                           fileName.EndsWith(".pt") ||
                           fileName.EndsWith(".pth");
                });

                return hasConfigFile || hasModelFile || files.Length > 3; // At least some substantial content
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error checking essential files in '{modelPath}': {ex.Message}");
                return false; // Assume not properly downloaded if we can't check
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
            // This method now performs a comprehensive refresh using the improved detection
            Debug.WriteLine($"🔄 Refreshing download states for {AvailableModels?.Count ?? 0} models");

            // Sync all model states with actual disk state
            SyncModelDownloadStatesWithDisk();

            // Update button text to reflect current state
            UpdateAllModelsDownloadButtonText();

            Debug.WriteLine($"✅ Download states refresh completed");
        }

        private void SyncModelDownloadStatesWithDisk()
        {
            try
            {
                bool anyChanges = false;
                var changedModels = new List<string>();

                if (AvailableModels != null)
                {
                    foreach (var model in AvailableModels)
                    {
                        // Check models that have HuggingFace IDs
                        if (!string.IsNullOrEmpty(model.HuggingFaceModelId))
                        {
                            bool actuallyDownloaded = IsModelDownloaded(model.HuggingFaceModelId);

                            if (model.IsDownloaded != actuallyDownloaded)
                            {
                                model.IsDownloaded = actuallyDownloaded;

                                // Update download button text to reflect actual state
                                model.DownloadButtonText = actuallyDownloaded ? "Remove from Device" : "Download to Device";

                                anyChanges = true;
                                changedModels.Add(model.Name);
                                Debug.WriteLine($"🔄 Synced model '{model.Name}' download state to {actuallyDownloaded} (was {!actuallyDownloaded})");
                            }
                        }
                    }
                }

                // Save the corrected states if any changes were made
                if (anyChanges)
                {
                    _ = SaveModelStatesAsync();
                    Debug.WriteLine($"💾 Saved corrected download states for models: {string.Join(", ", changedModels)}");
                }
                else
                {
                    Debug.WriteLine($"✅ All model download states are in sync with disk");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error syncing model download states with disk: {ex.Message}");
            }
        }

        private async Task DiscoverDownloadedModelsAsync()
        {
            try
            {
                string cacheDirectory = @"C:\Users\tanne\Documents\CSimple\Resources\HFModels";

                if (!Directory.Exists(cacheDirectory))
                {
                    Debug.WriteLine($"📂 Model cache directory not found: {cacheDirectory}");
                    return;
                }

                var modelDirectories = Directory.GetDirectories(cacheDirectory);
                Debug.WriteLine($"🔍 Found {modelDirectories.Length} directories in model cache");

                int addedCount = 0;
                var discoveredModels = new List<NeuralNetworkModel>();

                foreach (var modelDir in modelDirectories)
                {
                    var dirName = Path.GetFileName(modelDir);

                    // Skip if directory is too small or doesn't contain essential files
                    long totalSize = GetDirectorySize(modelDir);
                    bool hasEssentialFiles = HasEssentialModelFiles(modelDir);

                    if (totalSize <= 51200 || !hasEssentialFiles)
                    {
                        Debug.WriteLine($"⏭️ Skipping '{dirName}' - Size: {totalSize:N0} bytes, Essential files: {hasEssentialFiles}");
                        continue;
                    }

                    // Convert directory name back to model ID with improved logic
                    string modelId = ConvertDirectoryNameToModelId(dirName);

                    if (string.IsNullOrEmpty(modelId))
                    {
                        Debug.WriteLine($"⚠️ Could not determine model ID for directory: {dirName}");
                        continue;
                    }

                    // Use improved duplicate checking
                    if (IsModelAlreadyInCollection(modelId))
                    {
                        Debug.WriteLine($"⏭️ Model '{modelId}' already exists in collection");
                        continue;
                    }

                    // Create a new model entry for this discovered model
                    var discoveredModel = new NeuralNetworkModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = GetFriendlyModelName(modelId),
                        HuggingFaceModelId = modelId,
                        Type = ModelType.General, // Default type
                        IsDownloaded = true,
                        IsActive = false,
                        DownloadButtonText = "Remove from Device",
                        InputType = GuessInputTypeFromModelId(modelId),
                        IsHuggingFaceReference = true // Mark as HuggingFace reference
                    };

                    discoveredModels.Add(discoveredModel);
                    addedCount++;
                    Debug.WriteLine($"📥 Discovered model: {modelId} ({totalSize:N0} bytes)");
                }

                // Add all discovered models to the collection on the main thread
                if (discoveredModels.Any())
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var model in discoveredModels)
                        {
                            AvailableModels.Add(model);
                        }
                    });

                    Debug.WriteLine($"✅ Discovered and added {addedCount} previously unknown downloaded models");
                    // Save the updated model collection
                    await SaveModelStatesAsync();
                }
                else
                {
                    Debug.WriteLine($"ℹ️ No new downloaded models discovered");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error discovering downloaded models: {ex.Message}");
            }
        }

        // Improved method to check if model already exists in collection
        private bool IsModelAlreadyInCollection(string modelId)
        {
            if (string.IsNullOrEmpty(modelId) || AvailableModels == null)
                return false;

            return AvailableModels.Any(m =>
                string.Equals(m.HuggingFaceModelId, modelId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(m.Name, GetFriendlyModelName(modelId), StringComparison.OrdinalIgnoreCase));
        }

        // Clean up orphaned models and duplicates
        private async Task CleanupModelCollectionAsync()
        {
            try
            {
                var modelsToRemove = new List<NeuralNetworkModel>();
                var seenModelIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var model in AvailableModels.ToList())
                {
                    bool shouldRemove = false;

                    // Check for duplicates based on HuggingFace ID
                    if (!string.IsNullOrEmpty(model.HuggingFaceModelId))
                    {
                        if (seenModelIds.Contains(model.HuggingFaceModelId))
                        {
                            shouldRemove = true;
                            Debug.WriteLine($"🗑️ Removing duplicate model: {model.Name} (HF ID: {model.HuggingFaceModelId})");
                        }
                        else
                        {
                            seenModelIds.Add(model.HuggingFaceModelId);
                        }
                    }

                    // Check if HuggingFace models still exist on disk if marked as downloaded
                    if (!shouldRemove && model.IsDownloaded && !string.IsNullOrEmpty(model.HuggingFaceModelId))
                    {
                        bool actuallyExists = IsModelDownloaded(model.HuggingFaceModelId);
                        if (!actuallyExists)
                        {
                            // Don't remove, just update the state
                            model.IsDownloaded = false;
                            model.DownloadButtonText = "Download to Device";
                            Debug.WriteLine($"🔄 Updated orphaned model state: {model.Name} - no longer downloaded");
                        }
                    }

                    if (shouldRemove)
                    {
                        modelsToRemove.Add(model);
                    }
                }

                // Remove duplicates from the collection
                if (modelsToRemove.Any())
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        foreach (var model in modelsToRemove)
                        {
                            AvailableModels.Remove(model);
                        }
                    });

                    Debug.WriteLine($"🧹 Cleaned up {modelsToRemove.Count} duplicate/orphaned models");
                    await SaveModelStatesAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error cleaning up model collection: {ex.Message}");
            }
        }

        private string ConvertDirectoryNameToModelId(string dirName)
        {
            try
            {
                if (string.IsNullOrEmpty(dirName))
                    return null;

                // Handle "models--org--model" format (HuggingFace cache format)
                if (dirName.StartsWith("models--") && dirName.Contains("--"))
                {
                    var parts = dirName.Substring(8).Split(new[] { "--" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        return string.Join("/", parts);
                    }
                }

                // Handle "org_model" format
                if (dirName.Contains("_") && !dirName.StartsWith("_") && !dirName.EndsWith("_"))
                {
                    // Check if it looks like org_model format (has at least one underscore in the middle)
                    var underscoreIndex = dirName.IndexOf('_');
                    if (underscoreIndex > 0 && underscoreIndex < dirName.Length - 1)
                    {
                        return dirName.Replace("_", "/");
                    }
                }

                // Handle "org__model" format (double underscore)
                if (dirName.Contains("__"))
                {
                    return dirName.Replace("__", "/");
                }

                // Handle direct model names (no organization)
                // Look for common patterns that indicate this is a model name
                var lowerDirName = dirName.ToLowerInvariant();
                var modelKeywords = new[] { "gpt", "bert", "t5", "whisper", "clip", "resnet", "vit", "wav2vec", "hubert", "distil", "roberta", "albert", "xlnet", "electra" };

                if (modelKeywords.Any(keyword => lowerDirName.Contains(keyword)))
                {
                    return dirName; // Use as-is for direct model names
                }

                // If it's a valid directory but doesn't match known patterns, 
                // try to use it as-is (might be a direct model name)
                if (dirName.Length > 0 && !dirName.Contains(" ") && !dirName.Contains("\\") && !dirName.Contains("/"))
                {
                    return dirName;
                }

                Debug.WriteLine($"⚠️ Could not convert directory name '{dirName}' to model ID");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error converting directory name '{dirName}' to model ID: {ex.Message}");
                return null;
            }
        }

        private ModelInputType GuessInputTypeFromModelId(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return ModelInputType.Text;

            var lowerModelId = modelId.ToLowerInvariant();

            // Image models
            if (lowerModelId.Contains("vision") || lowerModelId.Contains("image") ||
                lowerModelId.Contains("clip") || lowerModelId.Contains("vit") ||
                lowerModelId.Contains("resnet") || lowerModelId.Contains("convnext"))
            {
                return ModelInputType.Image;
            }

            // Audio models
            if (lowerModelId.Contains("whisper") || lowerModelId.Contains("audio") ||
                lowerModelId.Contains("speech") || lowerModelId.Contains("wav2vec") ||
                lowerModelId.Contains("hubert"))
            {
                return ModelInputType.Audio;
            }

            // Default to text for most models
            return ModelInputType.Text;
        }

        private string GetFriendlyModelName(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                return "Unknown Model";

            // Extract just the model name from org/model format
            var parts = modelId.Split('/');
            var modelName = parts.Length > 1 ? parts.Last() : modelId;

            // Convert to friendly format
            return modelName.Replace("-", " ").Replace("_", " ");
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

            // Save enhanced model states after download
            _ = SaveModelStatesAsync();
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

            // Save enhanced model states after deletion
            _ = SaveModelStatesAsync();
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

        /// <summary>
        /// Loads all data required for the NetPage, including models, pipelines, and state synchronization.
        /// This method performs a comprehensive setup with improved model management:
        /// 1. Loads persisted models from storage
        /// 2. Loads enhanced model states (activation/download status)
        /// 3. Discovers any downloaded models not yet in the collection
        /// 4. Cleans up duplicates and orphaned entries
        /// 5. Synchronizes download states with actual disk state
        /// 6. Updates UI to reflect accurate model status
        /// </summary>
        public async Task LoadDataAsync()
        {
            // Only setup Python environment once per application session
            lock (_pythonSetupLock)
            {
                if (!_isPythonEnvironmentSetup)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] NetPageViewModel: First-time Python environment setup required");
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] NetPageViewModel: Python environment already set up, skipping setup");
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
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] NetPageViewModel: Python environment setup completed");
                }
            }

            await LoadPersistedModelsAsync();

            // Load enhanced model states (activation/download status) from settings
            await LoadModelStatesAsync();

            // Load available pipelines from OrientPage
            await RefreshPipelinesAsync();

            // Discover and add any downloaded models that aren't in the collection yet
            await DiscoverDownloadedModelsAsync();

            // Clean up any duplicates or orphaned entries
            await CleanupModelCollectionAsync();

            // Perform a final comprehensive sync of download states with disk
            // This will correct any discrepancies between saved states and actual files on disk
            SyncModelDownloadStatesWithDisk();

            // Update all models' download button text to reflect actual state
            UpdateAllModelsDownloadButtonText();

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
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ViewModel: Import Model triggered");
            try
            {
                CurrentModelStatus = "Opening file picker...";
                var fileResult = await PickFile(); // Delegate file picking to the view/platform

                if (fileResult == null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] File selection canceled");
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
                    _ = SaveModelStatesAsync(); // Save enhanced state tracking
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
                    _ = SaveModelStatesAsync(); // Save enhanced state tracking
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error deactivating model: {model?.Name}", ex);
            }
        }

        private void ToggleModelActivation(NeuralNetworkModel model)
        {
            if (model == null) return;

            if (model.IsActive)
            {
                DeactivateModel(model);
            }
            else
            {
                ActivateModel(model);
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

        private async Task ShareModel()
        {
            try
            {
                // Get all downloaded models
                var downloadedModels = AvailableModels
                    .Where(m => m.IsHuggingFaceReference &&
                               !string.IsNullOrEmpty(m.HuggingFaceModelId) &&
                               IsModelDownloaded(m.HuggingFaceModelId))
                    .ToList();

                if (downloadedModels.Count == 0)
                {
                    await ShowAlert("No Downloaded Models", "No downloaded models are available to share. Please download a model first.", "OK");
                    return;
                }

                // Show model selection dialog
                var selectedModel = await ShowDownloadedModelSelectionDialog(downloadedModels);

                if (selectedModel != null)
                {
                    // Open the model folder in Windows Explorer
                    OpenModelInExplorer(selectedModel);
                    CurrentModelStatus = $"Opened folder for model '{selectedModel.Name}'";
                }
                else
                {
                    CurrentModelStatus = "Model sharing cancelled";
                }
            }
            catch (Exception ex)
            {
                HandleError($"Error sharing model", ex);
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
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] NetPageViewModel: Python environment not properly initialized for media processing. Re-initializing...");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Offline mode: Allowing GPU models - hardware compatibility will be checked during execution");
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
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔥🔥🔥 UpdateModelInputTypeAsync CALLED - Model: '{model?.Name}', InputType: {inputType}");

            if (model == null)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ Model is null, returning");
                return;
            }

            try
            {
                // Log current model state for debugging
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 CURRENT MODEL STATE: Name='{model.Name}', CurrentInputType={model.InputType}");

                // Check if the input type is actually changing
                bool isChanged = model.InputType != inputType;
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 InputType changing? {isChanged} (from {model.InputType} to {inputType})");

                // FORCE SAVE - Let's bypass the no-change check temporarily to test the save chain
                if (!isChanged)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ NO CHANGE DETECTED - but forcing save anyway to test the save chain");
                    // Don't return - continue with save to test the chain
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔄 CHANGE DETECTED - continuing with save");
                }

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔄 STARTING InputType change for '{model.Name}' from {model.InputType} to {inputType}");
                Debug.WriteLine($"🔄 STARTING InputType change for '{model.Name}' from {model.InputType} to {inputType}");

                model.InputType = inputType;
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✏️ Model.InputType updated to: {model.InputType}");

                // IMMEDIATE save for InputType changes - NO DEBOUNCING
                CurrentModelStatus = $"Saving '{model.Name}' InputType = {inputType} to file...";
                OnPropertyChanged(nameof(AvailableModels));

                try
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 💾 CALLING SavePersistedModelsAsync for '{model.Name}' InputType change");
                    Debug.WriteLine($"💾 CALLING SavePersistedModelsAsync for '{model.Name}' InputType change");

                    await SavePersistedModelsAsync();

                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ SUCCESSFULLY SAVED InputType change for '{model.Name}' to huggingFaceModels.json");
                    Debug.WriteLine($"✅ SUCCESSFULLY SAVED InputType change for '{model.Name}' to huggingFaceModels.json");
                    CurrentModelStatus = $"✅ Saved '{model.Name}' InputType = {inputType} to file";
                }
                catch (Exception saveEx)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ ERROR saving InputType change: {saveEx.Message}");
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ Stack trace: {saveEx.StackTrace}");
                    Debug.WriteLine($"❌ ERROR saving InputType change: {saveEx.Message}");
                    Debug.WriteLine($"❌ Stack trace: {saveEx.StackTrace}");
                    CurrentModelStatus = $"❌ Error saving InputType: {saveEx.Message}";
                    throw; // Re-throw to ensure error is visible
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ ERROR in UpdateModelInputTypeAsync: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ Stack trace: {ex.StackTrace}");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error guessing input type: {ex.Message}");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Warning: Both modes were active, check toggle logic.");
            }
        }

        private void DeactivateModelsOfType(ModelType type)
        {
            try
            {
                var modelsToDeactivate = ActiveModels.Where(m => m?.Type == type).ToList();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Deactivating {modelsToDeactivate.Count} models of type {type}");
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

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RestoreActiveModelsOfType: Found {modelsToActivate.Count} models of type {type} to restore");

                foreach (var model in modelsToActivate)
                {
                    // Add to ActiveModels collection without triggering save (to avoid excessive saves)
                    ActiveModels.Add(model);
                    StartModelMonitoring(model);
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RestoreActiveModelsOfType: Restored active state for model '{model.Name}' of type {type}");
                }

                // Update UI property notifications
                OnPropertyChanged(nameof(ActiveModelsCount));

                if (modelsToActivate.Count > 0)
                {
                    CurrentModelStatus = $"Restored {modelsToActivate.Count} {type} model(s)";
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] RestoreActiveModelsOfType: Successfully restored {modelsToActivate.Count} models of type {type}");
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

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 SavePersistedModelsAsync: Found {modelsToSave.Count} models to save out of {AvailableModels?.Count ?? 0} available models");
            Debug.WriteLine($"🔍 SavePersistedModelsAsync: Found {modelsToSave.Count} models to save out of {AvailableModels?.Count ?? 0} available models");

            // Log InputType values before saving
            foreach (var model in modelsToSave)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📋 Model '{model.Name}' - InputType: {model.InputType}, IsHuggingFaceReference: {model.IsHuggingFaceReference}, HuggingFaceModelId: '{model.HuggingFaceModelId}'");
                Debug.WriteLine($"📋 Model '{model.Name}' - InputType: {model.InputType}, IsHuggingFaceReference: {model.IsHuggingFaceReference}, HuggingFaceModelId: '{model.HuggingFaceModelId}'");
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 💾 Calling SavePersistedModelsAsync overload with {modelsToSave.Count} models");
            await SavePersistedModelsAsync(modelsToSave); // Call the overload
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ SavePersistedModelsAsync overload completed");
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
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error in debounced save: {ex.Message}");
                    }
                }, token);
            }
        }

        // Enhanced Model State Persistence Methods
        private async Task SaveModelStatesAsync()
        {
            try
            {
                // Save activation states
                var activationStates = new Dictionary<string, bool>();
                var downloadStates = new Dictionary<string, bool>();

                if (AvailableModels != null)
                {
                    foreach (var model in AvailableModels)
                    {
                        if (!string.IsNullOrEmpty(model.Id))
                        {
                            activationStates[model.Id] = model.IsActive;
                            downloadStates[model.Id] = model.IsDownloaded;
                        }
                    }
                }

                await _settingsService.SaveModelActivationStatesAsync(activationStates);
                await _settingsService.SaveModelDownloadStatesAsync(downloadStates);

                Debug.WriteLine($"💾 Saved model states for {activationStates.Count} models");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error saving model states: {ex.Message}");
            }
        }

        private async Task LoadModelStatesAsync()
        {
            try
            {
                var activationStates = await _settingsService.LoadModelActivationStatesAsync();
                var downloadStates = await _settingsService.LoadModelDownloadStatesAsync();

                if (AvailableModels != null)
                {
                    foreach (var model in AvailableModels)
                    {
                        if (!string.IsNullOrEmpty(model.Id))
                        {
                            // Restore activation state
                            if (activationStates.TryGetValue(model.Id, out bool isActive))
                            {
                                model.IsActive = isActive;
                                if (isActive && !ActiveModels.Any(m => m?.Id == model.Id))
                                {
                                    ActiveModels.Add(model);
                                }
                            }

                            // Restore download state - but verify against actual disk state
                            if (downloadStates.TryGetValue(model.Id, out bool persistedDownloadState))
                            {
                                // Check actual disk state for this model
                                bool actualDiskState = IsModelDownloaded(model.HuggingFaceModelId ?? model.Id);

                                // Use actual disk state, not persisted state
                                model.IsDownloaded = actualDiskState;

                                // Log discrepancies for debugging
                                if (persistedDownloadState != actualDiskState)
                                {
                                    Debug.WriteLine($"🔄 Model '{model.Name}' download state corrected: persisted={persistedDownloadState}, actual={actualDiskState}");
                                }
                            }
                            else
                            {
                                // No persisted state, check disk directly
                                model.IsDownloaded = IsModelDownloaded(model.HuggingFaceModelId ?? model.Id);
                            }
                        }
                    }
                }

                // Update UI properties
                OnPropertyChanged(nameof(ActiveModelsCount));

                // Update all download button texts to reflect actual disk states
                UpdateAllModelsDownloadButtonText();

                Debug.WriteLine($"🔄 Loaded model states for {activationStates.Count} activation states and {downloadStates.Count} download states");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error loading model states: {ex.Message}");
            }
        }

        /// <summary>
        /// Synchronizes all model download states with actual files on disk
        /// This ensures the UI reflects the real state of downloaded models
        /// </summary>
        public void SyncAllModelStatesWithDisk()
        {
            try
            {
                Debug.WriteLine($"🔄 Starting full sync of model states with disk...");

                SyncModelDownloadStatesWithDisk();
                UpdateAllModelsDownloadButtonText();
                NotifyModelDownloadStatusChanged();

                Debug.WriteLine($"✅ Completed full sync of model states with disk");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error during full model state sync: {ex.Message}");
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

        private string GetModelDirectoryPath(string modelId)
        {
            string safeModelId = (modelId ?? "unknown_model").Replace("/", "_").Replace("\\", "_");
            var modelDirectory = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\HFModels", safeModelId);
            Directory.CreateDirectory(modelDirectory); // Ensure it exists

            // Log the model directory for user awareness
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Model Directory] {modelId} -> {modelDirectory}");

            return modelDirectory;
        }

        private async Task SavePythonReferenceInfo(HuggingFaceModel model)
        {
            try
            {
                var infoDirectory = GetModelDirectoryPath(model.ModelId ?? model.Id);
                string infoContent = $"Model ID: {model.ModelId ?? model.Id}\nAuthor: {model.Author}\nType: {model.Pipeline_tag}\nPython:\nfrom transformers import AutoModel\nmodel = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\", trust_remote_code=True)";
                await File.WriteAllTextAsync(Path.Combine(infoDirectory, "model_info.txt"), infoContent);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Saved Python reference info for {model.ModelId}");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Model file copied to: {destinationPath}");
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
            // Simulation timer disabled - no automatic activity updates
            // Remove the 15-second timer that was simulating model communication
        }

        private void StartModelMonitoring(NeuralNetworkModel model) => Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VM: Starting monitoring for {model.Name}");
        private void StopModelMonitoring(NeuralNetworkModel model) => Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] VM: Stopping monitoring for {model.Name}");

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

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Chat: Sending {(hasText ? $"message '{userMessage}'" : "media-only message")} to active models (count: {ActiveModelsCount})");

            // Update UI properties
            IsAiTyping = true;
            OnPropertyChanged(nameof(CanSendMessage));

            try
            {
                // Use the existing CommunicateWithModelAsync method which handles the full chat flow
                await CommunicateWithModelAsync(userMessage);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Chat: Message processed successfully. ChatMessages count: {ChatMessages.Count}");
            }
            catch (Exception ex)
            {                // Add error message to chat if something goes wrong
                var errorMessage = new ChatMessage($"Sorry, I encountered an error: {ex.Message}", false, "System", includeInHistory: true);
                ChatMessages.Add(errorMessage);

                LastModelOutput = $"Error: {ex.Message}";
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Chat error: {ex}");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error suggesting models: {ex.Message}");
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
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Started editing message: {message.Content}");
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

                // If intelligence is active, attach the new media to the current session
                if (IsIntelligenceActive && _currentIntelligenceSession != null)
                {
                    AttachMediaFilesToSession();
                }
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

                // If intelligence is active, attach the new media to the current session
                if (IsIntelligenceActive && _currentIntelligenceSession != null)
                {
                    AttachMediaFilesToSession();
                }
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

                // If intelligence is active, attach the new media to the current session
                if (IsIntelligenceActive && _currentIntelligenceSession != null)
                {
                    AttachMediaFilesToSession();
                }
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

            // If intelligence is active, clear media from the current session
            if (IsIntelligenceActive && _currentIntelligenceSession != null)
            {
                _currentIntelligenceSession.Files.Clear();
                Debug.WriteLine("Intelligence: Cleared media files from current session");
            }
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
        public Func<List<CSimple.Models.HuggingFaceModel>, Task<CSimple.Models.HuggingFaceModel>> ShowModelSelectionDialog { get; set; } = async (m) => { await Task.CompletedTask; return null; }; // Default no-op
        public Func<List<NeuralNetworkModel>, Task<NeuralNetworkModel>> ShowDownloadedModelSelectionDialog { get; set; } = async (m) => { await Task.CompletedTask; return null; }; // Default no-op for downloaded model selection
                                                                                                                                                                                    // --- INotifyPropertyChanged Implementation ---
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
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🤖 [NetPageViewModel.ExecuteModelAsync] Executing model: {modelId} with input length: {inputText?.Length ?? 0}");
            Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Executing model: {modelId} with input length: {inputText?.Length ?? 0}");
            Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Available models count: {AvailableModels?.Count ?? 0}");

            try
            {
                // Debug: List all available models for troubleshooting
                if (AvailableModels != null)
                {
                    foreach (var availableModel in AvailableModels.Take(5)) // Show first 5 models
                    {
                        Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Available model: ID='{availableModel.Id}', Name='{availableModel.Name}', HFModelId='{availableModel.HuggingFaceModelId}'");
                    }
                }
                else
                {
                    Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] ERROR: AvailableModels is null!");
                }

                // Find the model in available models
                var model = AvailableModels?.FirstOrDefault(m =>
                    m.HuggingFaceModelId == modelId ||
                    m.Name == modelId ||
                    m.Id == modelId);

                if (model == null)
                {
                    Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] ERROR: Model '{modelId}' not found in available models");
                    throw new InvalidOperationException($"Model '{modelId}' not found in available models");
                }

                Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Found model: {model.Name} (HF ID: {model.HuggingFaceModelId})");

                // Use the existing model execution infrastructure
                string localModelPath = GetLocalModelPath(modelId);
                Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Local model path: {localModelPath ?? "null"}");
                Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] Python executable: {_pythonExecutablePath}");
                Debug.WriteLine($"🤖 [NetPageViewModel.ExecuteModelAsync] HF script path: {_huggingFaceScriptPath}");

                var result = await _modelExecutionService.ExecuteHuggingFaceModelAsyncEnhanced(
                    modelId, inputText, model, _pythonExecutablePath, _huggingFaceScriptPath, localModelPath);

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [NetPageViewModel.ExecuteModelAsync] Model execution successful, result length: {result?.Length ?? 0}");
                Debug.WriteLine($"✅ [NetPageViewModel.ExecuteModelAsync] Model execution successful, result length: {result?.Length ?? 0}");

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [NetPageViewModel.ExecuteModelAsync] Model execution failed: {ex.Message}");
                Debug.WriteLine($"❌ [NetPageViewModel.ExecuteModelAsync] Model execution failed: {ex.Message}");
                Debug.WriteLine($"❌ [NetPageViewModel.ExecuteModelAsync] Stack trace: {ex.StackTrace}");
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

        // Pipeline interaction methods
        private async Task SendGoalAsync(string goal)
        {
            if (string.IsNullOrWhiteSpace(goal) || string.IsNullOrEmpty(SelectedPipeline))
                return;

            try
            {
                // Add user message to pipeline chat
                PipelineChatMessages.Add(new ChatMessage
                {
                    Content = goal,
                    IsFromUser = true,
                    Timestamp = DateTime.Now
                });

                // Clear input
                UserGoalInput = string.Empty;

                // Simulate AI response (placeholder)
                await Task.Delay(1000);
                PipelineChatMessages.Add(new ChatMessage
                {
                    Content = $"Processing goal '{goal}' with pipeline '{SelectedPipeline}'. Intelligence mode: {(IsIntelligenceActive ? "Active" : "Inactive")}",
                    IsFromUser = false,
                    Timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                HandleError("Error sending goal", ex);
            }
        }

        private void ClearPipelineChat()
        {
            PipelineChatMessages.Clear();
        }

        // Enhanced Pipeline Management Methods
        public async Task RefreshPipelinesAsync()
        {
            try
            {
                CurrentModelStatus = "Loading available pipelines...";
                IsLoading = true;

                // Clear existing collections
                AvailablePipelines.Clear();
                AvailablePipelineData.Clear();

                // Load pipelines from FileService (they are already sorted by LastModified)
                var pipelines = await _fileService.ListPipelinesAsync();

                if (pipelines != null && pipelines.Any())
                {
                    // Add to collections
                    foreach (var pipeline in pipelines)
                    {
                        AvailablePipelineData.Add(pipeline);
                        AvailablePipelines.Add(pipeline.Name);
                    }

                    // Auto-select the most recently created pipeline (first in the sorted list)
                    var mostRecentPipeline = pipelines.First();
                    SelectedPipeline = mostRecentPipeline.Name;

                    AddPipelineChatMessage($"📋 Loaded {pipelines.Count} pipeline(s). Auto-selected most recent: '{mostRecentPipeline.Name}'", false);
                    Debug.WriteLine($"NetPage: Loaded {pipelines.Count} pipelines, auto-selected: {mostRecentPipeline.Name}");
                }
                else
                {
                    // No pipelines found - suggest creating one
                    AddPipelineChatMessage("📋 No pipelines found. Create a pipeline in OrientPage to get started with intelligent interaction.", false);
                    Debug.WriteLine("NetPage: No pipelines found");
                }

                _lastPipelineRefresh = DateTime.Now;
                OnPropertyChanged(nameof(AvailablePipelinesCount));
                OnPropertyChanged(nameof(PipelineCount));
                CurrentModelStatus = $"Loaded {pipelines?.Count ?? 0} pipeline(s)";
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error loading pipelines: {ex.Message}";
                AddPipelineChatMessage($"❌ {errorMsg}", false);
                Debug.WriteLine($"NetPage: {errorMsg}");
                CurrentModelStatus = "Error loading pipelines";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateSelectedPipelineData()
        {
            try
            {
                if (string.IsNullOrEmpty(SelectedPipeline))
                {
                    SelectedPipelineData = null;
                    return;
                }

                // Find the corresponding pipeline data
                var pipelineData = AvailablePipelineData.FirstOrDefault(p => p.Name == SelectedPipeline);
                SelectedPipelineData = pipelineData;

                if (pipelineData != null)
                {
                    AddPipelineChatMessage($"🔗 Connected to pipeline: '{pipelineData.Name}' ({pipelineData.Nodes?.Count ?? 0} nodes, {pipelineData.Connections?.Count ?? 0} connections)", false);
                    Debug.WriteLine($"NetPage: Selected pipeline '{pipelineData.Name}' - {pipelineData.Nodes?.Count} nodes, {pipelineData.Connections?.Count} connections");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating selected pipeline data: {ex.Message}");
            }
        }

        public async Task CreateNewPipelineAsync()
        {
            try
            {
                // Navigate to OrientPage to create a new pipeline
                await NavigateTo("///orient");
                AddPipelineChatMessage("🚀 Navigating to OrientPage to create a new pipeline...", false);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Error navigating to OrientPage: {ex.Message}";
                AddPipelineChatMessage($"❌ {errorMsg}", false);
                Debug.WriteLine($"NetPage: {errorMsg}");
            }
        }

        private void AddPipelineChatMessage(string content, bool isFromUser)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PipelineChatMessages.Add(new ChatMessage
                {
                    Content = content,
                    IsFromUser = isFromUser,
                    Timestamp = DateTime.Now
                });

                // Auto-scroll to bottom
                ScrollToBottom?.Invoke();
            });
        }

        // Method to check if pipelines need refreshing (called from other parts of the app)
        public bool ShouldRefreshPipelines()
        {
            // Refresh if it's been more than 30 seconds since last refresh
            return (DateTime.Now - _lastPipelineRefresh).TotalSeconds > 30;
        }

        // Method to get pipeline details for display
        public string GetPipelineDetails(string pipelineName)
        {
            var pipeline = AvailablePipelineData.FirstOrDefault(p => p.Name == pipelineName);
            if (pipeline == null)
                return "Pipeline not found";

            return $"Nodes: {pipeline.Nodes?.Count ?? 0}, Connections: {pipeline.Connections?.Count ?? 0}, Modified: {pipeline.LastModified:MM/dd/yyyy HH:mm}";
        }

        // --- Intelligence Recording Methods ---

        private void SetupIntelligenceInputCapture()
        {
            try
            {
                // Setup input capture service event handlers if available
                if (_inputCaptureService != null)
                {
                    _inputCaptureService.InputCaptured += OnInputCaptured;
                    Debug.WriteLine("Intelligence: InputCaptureService events subscribed");
                }

                // Setup mouse tracking service event handlers if available
                if (_mouseTrackingService != null)
                {
                    _mouseTrackingService.MouseMoved += OnMouseMoved;
                    _mouseTrackingService.TouchInputReceived += OnTouchInputReceived;
                    Debug.WriteLine("Intelligence: MouseTrackingService events subscribed");
                }

                // Setup audio capture service event handlers if available
                if (_audioCaptureService != null)
                {
                    _audioCaptureService.FileCaptured += OnAudioFileCaptured;
                    _audioCaptureService.PCLevelChanged += OnPCLevelChanged;
                    _audioCaptureService.WebcamLevelChanged += OnWebcamLevelChanged;
                    Debug.WriteLine("Intelligence: AudioCaptureService events subscribed");
                }

                // Setup screen capture service event handlers if available
                if (_screenCaptureService != null)
                {
                    _screenCaptureService.FileCaptured += OnImageFileCaptured;
                    Debug.WriteLine("Intelligence: ScreenCaptureService events subscribed");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting up intelligence input capture: {ex.Message}");
            }
        }

        private void StartIntelligenceRecording()
        {
            try
            {
                AddPipelineChatMessage("🎯 Intelligence recording STARTED - Monitoring system and executing pipeline...", false);
                Debug.WriteLine("Intelligence: Starting intelligent pipeline execution");

                // Validate that we have a selected pipeline and active models
                if (string.IsNullOrEmpty(_selectedPipeline))
                {
                    AddPipelineChatMessage("⚠️ No pipeline selected. Please select a pipeline first.", false);
                    IsIntelligenceActive = false;
                    return;
                }

                if (ActiveModels.Count == 0)
                {
                    AddPipelineChatMessage("⚠️ No active models available. Please activate at least one model.", false);
                    IsIntelligenceActive = false;
                    return;
                }

                // Initialize intelligence processing
                _intelligenceProcessingCts = new CancellationTokenSource();

                // Start input capture service if available
                if (_inputCaptureService != null && RecordKeyboardInputs)
                {
                    _inputCaptureService.StartCapturing();
                    Debug.WriteLine("Intelligence: Started keyboard/input capture");
                }

                // Start mouse tracking if available
                if (_mouseTrackingService != null && RecordMouseInputs)
                {
                    // Mouse tracking needs a window handle - we'll use IntPtr.Zero for global tracking
                    _mouseTrackingService.StartTracking(IntPtr.Zero);
                    Debug.WriteLine("Intelligence: Started mouse tracking");
                }

                // Start audio recording just like ObservePage
                if (_audioCaptureService != null)
                {
                    // Start PC audio recording (system audio)
                    _audioCaptureService.StartPCAudioRecording(true);
                    Debug.WriteLine("Intelligence: Started PC audio recording");

                    // Start webcam audio recording (microphone)
                    _audioCaptureService.StartWebcamAudioRecording(true);
                    Debug.WriteLine("Intelligence: Started webcam audio recording");
                }

                // Start webcam image recording just like ObservePage
                if (_screenCaptureService != null)
                {
                    _intelligenceWebcamCts = new CancellationTokenSource();
                    string actionName = $"Intelligence_{DateTime.Now:HHmmss}"; // Action name for file saving
                    int intelligenceIntervalMs = _settingsService.GetIntelligenceIntervalMs();
                    Task.Run(() => _screenCaptureService.StartWebcamCapture(_intelligenceWebcamCts.Token, actionName, intelligenceIntervalMs), _intelligenceWebcamCts.Token);
                    Debug.WriteLine($"Intelligence: Started webcam image recording with interval {intelligenceIntervalMs}ms");
                }

                // Start screen capture for visual input
                StartScreenCapture();

                // Start intelligent pipeline processing loop
                Task.Run(async () => await IntelligentPipelineLoop(_intelligenceProcessingCts.Token))
                    .ContinueWith(t =>
                    {
                        if (t.IsFaulted)
                        {
                            Debug.WriteLine($"Intelligence processing error: {t.Exception?.GetBaseException().Message}");
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                AddPipelineChatMessage($"❌ Intelligence processing error: {t.Exception?.GetBaseException().Message}", false);
                            });
                        }
                    });

                AddPipelineChatMessage($"🚀 Intelligent pipeline '{_selectedPipeline}' activated with {ActiveModels.Count} models", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting intelligence recording: {ex.Message}");
                AddPipelineChatMessage($"❌ Error starting intelligence recording: {ex.Message}", false);
            }
        }

        private void StopIntelligenceRecording()
        {
            try
            {
                AddPipelineChatMessage("⏸️ Intelligence recording STOPPED", false);
                Debug.WriteLine("Intelligence: Stopping intelligent pipeline execution");

                // Cancel intelligence processing loop
                _intelligenceProcessingCts?.Cancel();
                _intelligenceProcessingCts?.Dispose();
                _intelligenceProcessingCts = null;

                // Stop screen capture
                StopScreenCapture();

                // Stop audio recording just like ObservePage
                if (_audioCaptureService != null)
                {
                    // Stop PC audio recording (system audio)
                    _audioCaptureService.StopPCAudioRecording();
                    Debug.WriteLine("Intelligence: Stopped PC audio recording");

                    // Stop webcam audio recording (microphone)
                    _audioCaptureService.StopWebcamAudioRecording();
                    Debug.WriteLine("Intelligence: Stopped webcam audio recording");
                }

                // Stop webcam image recording just like ObservePage
                if (_intelligenceWebcamCts != null)
                {
                    _intelligenceWebcamCts.Cancel();
                    _intelligenceWebcamCts.Dispose();
                    _intelligenceWebcamCts = null;
                    Debug.WriteLine("Intelligence: Stopped webcam image recording");
                }

                // Stop input capture service if available
                if (_inputCaptureService != null)
                {
                    _inputCaptureService.StopCapturing();
                    Debug.WriteLine("Intelligence: Stopped keyboard/input capture");
                }

                // Stop mouse tracking if available
                if (_mouseTrackingService != null)
                {
                    _mouseTrackingService.StopTracking();
                    Debug.WriteLine("Intelligence: Stopped mouse tracking");
                }

                // Reset processing state
                lock (_intelligenceProcessingLock)
                {
                    _isProcessingIntelligence = false;
                }

                AddPipelineChatMessage("✅ Intelligence pipeline execution stopped", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping intelligence recording: {ex.Message}");
                AddPipelineChatMessage($"❌ Error stopping intelligence recording: {ex.Message}", false);
            }
        }

        // --- Intelligence Pipeline Processing Methods ---

        /// <summary>
        /// Start screen capture for intelligent pipeline processing
        /// </summary>
        private void StartScreenCapture()
        {
            try
            {
                // Initialize screen capture service if not already done
                var screenCaptureService = ServiceProvider.GetService<ScreenCaptureService>();

                if (screenCaptureService != null)
                {
                    screenCaptureService.StartPreviewMode();
                    Debug.WriteLine("Intelligence: Started screen capture for pipeline processing");
                }
                else
                {
                    Debug.WriteLine("Intelligence: Screen capture service not available");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting screen capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop screen capture
        /// </summary>
        private void StopScreenCapture()
        {
            try
            {
                var screenCaptureService = ServiceProvider.GetService<ScreenCaptureService>();
                screenCaptureService?.StopPreviewMode();
                Debug.WriteLine("Intelligence: Stopped screen capture");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping screen capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Main intelligent pipeline processing loop with configurable intervals and comprehensive data management
        /// </summary>
        private async Task IntelligentPipelineLoop(CancellationToken cancellationToken)
        {
            Debug.WriteLine("Intelligence: Starting enhanced intelligent pipeline loop");

            try
            {
                while (!cancellationToken.IsCancellationRequested && IsIntelligenceActive)
                {
                    // Get current settings
                    var minimumIntervalMs = _settingsService.GetIntelligenceIntervalMs();
                    var autoExecutionEnabled = _settingsService.GetIntelligenceAutoExecutionEnabled();

                    if (!autoExecutionEnabled)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    // Check if we should prevent concurrent processing
                    bool shouldProcess = false;
                    bool currentTaskRunning = false;

                    lock (_intelligenceProcessingLock)
                    {
                        currentTaskRunning = _currentPipelineTask != null && !_currentPipelineTask.IsCompleted;

                        if (!_isProcessingIntelligence && !currentTaskRunning)
                        {
                            _isProcessingIntelligence = true;
                            shouldProcess = true;
                        }
                    }

                    if (!shouldProcess)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    try
                    {
                        var executionStartTime = DateTime.Now;
                        var timeSinceLastExecution = executionStartTime - _lastPipelineExecution;

                        // Only execute if minimum interval has passed OR if there's no current task running
                        if (timeSinceLastExecution.TotalMilliseconds >= minimumIntervalMs || !currentTaskRunning)
                        {
                            // Continuously collect system state data
                            await CaptureComprehensiveSystemState(cancellationToken);

                            // Get all accumulated data since last processing
                            var (screenshots, audioData, textData) = GetAccumulatedSystemData();

                            // Execute pipeline with all collected data
                            _currentPipelineTask = ExecuteEnhancedPipelineWithData(screenshots, audioData, textData, cancellationToken);
                            await _currentPipelineTask;

                            var executionDuration = DateTime.Now - executionStartTime;
                            _lastPipelineExecution = DateTime.Now;

                            // Debug.WriteLine($"Intelligence: Pipeline executed in {executionDuration.TotalMilliseconds}ms with {screenshots.Count} screenshots, {audioData.Count} audio samples, {textData.Count} text inputs");

                            // Clear processed data
                            ClearAccumulatedData();

                            // If execution took longer than minimum interval, process immediately accumulated data
                            if (executionDuration.TotalMilliseconds > minimumIntervalMs)
                            {
                                // Debug.WriteLine($"Intelligence: Execution exceeded interval ({executionDuration.TotalMilliseconds}ms > {minimumIntervalMs}ms), processing accumulated data immediately");
                                continue; // Skip delay and process immediately
                            }
                        }
                        else
                        {
                            // Continue collecting data while waiting for interval
                            await CaptureComprehensiveSystemState(cancellationToken);
                        }

                        // Wait for remaining interval time
                        var remainingTime = minimumIntervalMs - (DateTime.Now - executionStartTime).TotalMilliseconds;
                        if (remainingTime > 0)
                        {
                            await Task.Delay((int)remainingTime, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Intelligence processing error: {ex.Message}");
                        AddPipelineChatMessage($"⚠️ Processing error: {ex.Message}", false);
                        await Task.Delay(1000, cancellationToken); // Prevent rapid error loops
                    }
                    finally
                    {
                        lock (_intelligenceProcessingLock)
                        {
                            _isProcessingIntelligence = false;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("Intelligence: Pipeline loop cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Intelligence: Fatal pipeline loop error: {ex.Message}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AddPipelineChatMessage($"❌ Intelligence loop error: {ex.Message}", false);
                });
            }
            finally
            {
                Debug.WriteLine("Intelligence: Pipeline loop ended");
            }
        }

        /// <summary>
        /// Capture current system state (screen, keyboard, mouse)
        /// </summary>
        private async Task CaptureSystemState(CancellationToken cancellationToken)
        {
            try
            {
                // For now, we'll capture a screenshot
                // In a more sophisticated implementation, you might want to:
                // 1. Take screenshot
                // 2. Get current application window info
                // 3. Get recent keyboard/mouse activity

                var screenCaptureService = ServiceProvider.GetService<ScreenCaptureService>();
                screenCaptureService?.CaptureScreens($"Intelligence_{DateTime.Now:HHmmss}");

                // Log the capture
                AddPipelineChatMessage("📸 System state captured", false);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing system state: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute the selected pipeline with observed system data
        /// </summary>
        private async Task ExecutePipelineWithObservedData(CancellationToken cancellationToken)
        {
            Debug.WriteLine($"[NetPage Pipeline] ===== EXECUTING PIPELINE WITH OBSERVED DATA =====");
            Debug.WriteLine($"[NetPage Pipeline] Selected Pipeline: '{_selectedPipeline ?? "None"}'");

            try
            {
                if (_selectedPipelineData?.Nodes == null || _selectedPipelineData.Connections == null)
                {
                    Debug.WriteLine("Intelligence: No pipeline data available for execution");
                    Debug.WriteLine("[NetPage Pipeline] No pipeline data available for execution");
                    return;
                }

                // Get the pipeline execution service
                if (_pipelineExecutionService == null)
                {
                    Debug.WriteLine("Intelligence: Pipeline execution service not available");
                    Debug.WriteLine("[NetPage Pipeline] Pipeline execution service not available");
                    return;
                }

                // Convert pipeline data to the format expected by the execution service
                var nodes = new ObservableCollection<NodeViewModel>(
                    _selectedPipelineData.Nodes.Select(sn => sn.ToViewModel())
                );
                var connections = new ObservableCollection<ConnectionViewModel>(
                    _selectedPipelineData.Connections.Select(sc => sc.ToViewModel())
                );

                Debug.WriteLine($"[NetPage Pipeline] Converted {nodes.Count} nodes and {connections.Count} connections");

                // Prepare input data from system observations
                var systemInput = PrepareSystemInputForPipeline(cancellationToken);
                Debug.WriteLine($"[NetPage Pipeline] Prepared system input: '{systemInput}'");

                // Add system input to input nodes
                foreach (var inputNode in nodes.Where(n => n.Type == NodeType.Input))
                {
                    inputNode.SetStepOutput(1, "text", systemInput);
                    // Debug.WriteLine($"[NetPage Pipeline] Set input for node '{inputNode.Name}': '{systemInput}'");
                }

                AddPipelineChatMessage($"🔄 Executing pipeline '{_selectedPipeline}' with system observations...", false);

                // Execute the pipeline
                var result = await _pipelineExecutionService.ExecuteAllModelsAsync(nodes, connections, 1, null);
                int successCount = result.successCount;
                int skippedCount = result.skippedCount;

                Debug.WriteLine($"[NetPage Pipeline] Execution result: {successCount} successful, {skippedCount} skipped");

                // Process results and simulate actions
                await ProcessPipelineResultsAndSimulateActions(nodes, cancellationToken);

                AddPipelineChatMessage($"✅ Pipeline executed: {successCount} successful, {skippedCount} skipped", false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing pipeline with observed data: {ex.Message}");
                AddPipelineChatMessage($"❌ Pipeline execution error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Prepare system input data for pipeline processing
        /// </summary>
        private string PrepareSystemInputForPipeline(CancellationToken cancellationToken)
        {
            var systemObservations = new List<string>();

            try
            {
                // Add timestamp
                systemObservations.Add($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                // Add active application info (simplified)
                systemObservations.Add("Active application: Current system state");

                // Add recent input activity summary
                if (_inputCaptureService != null)
                {
                    var activeKeyCount = _inputCaptureService.GetActiveKeyCount();
                    systemObservations.Add($"Active keys: {activeKeyCount}");
                }

                // Add mouse position (simplified)
                systemObservations.Add("Mouse activity: Available");

                // Add system status
                systemObservations.Add($"Intelligence mode: Active");
                systemObservations.Add($"Active models: {ActiveModels.Count}");

                // Add any recent pipeline chat context
                var recentMessages = PipelineChatMessages.TakeLast(3).Select(m => $"Recent: {m.Content}");
                systemObservations.AddRange(recentMessages);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error preparing system input: {ex.Message}");
                systemObservations.Add($"Error gathering system data: {ex.Message}");
            }

            return string.Join("\n", systemObservations);
        }

        /// <summary>
        /// Process pipeline results and simulate actions based on action classification models
        /// </summary>
        private async Task ProcessPipelineResultsAndSimulateActions(ObservableCollection<NodeViewModel> nodes, CancellationToken cancellationToken)
        {
            try
            {
                // Console log all model node outputs for validation
                Debug.WriteLine($"[NetPage Pipeline] ===== PIPELINE EXECUTION RESULTS =====");
                Debug.WriteLine($"[NetPage Pipeline] Processing {nodes.Count} nodes:");

                foreach (var node in nodes)
                {
                    // Debug.WriteLine($"[NetPage Pipeline] Node: '{node.Name}' (Type: {node.Type})");

                    if (node.Type == NodeType.Model)
                    {
                        var output = node.GetStepOutput(1);
                        // Debug.WriteLine($"[NetPage Pipeline] Model Node Output: '{output.Value ?? "NULL"}'");
                        // Debug.WriteLine($"[NetPage Pipeline] Output Type: {output.Type}");
                        // Debug.WriteLine($"[NetPage Pipeline] Model Path: '{node.ModelPath ?? "N/A"}'");
                        // Debug.WriteLine($"[NetPage Pipeline] Classification: '{node.Classification ?? "N/A"}'");
                    }
                    else if (node.Type == NodeType.Input)
                    {
                        var output = node.GetStepOutput(1);
                        // Debug.WriteLine($"[NetPage Pipeline] Input Node Output: '{output.Value ?? "NULL"}'");
                    }
                    else if (node.Type == NodeType.Output)
                    {
                        var output = node.GetStepOutput(1);
                        Debug.WriteLine($"[NetPage Pipeline] Output Node: '{output.Value ?? "NULL"}'");
                    }

                    // Debug.WriteLine($"[NetPage Pipeline] ---");
                }
                // Debug.WriteLine($"[NetPage Pipeline] ===== END PIPELINE RESULTS =====");

                var actionService = ServiceProvider.GetService<ActionService>();
                if (actionService == null)
                {
                    Debug.WriteLine("Intelligence: Action service not available");
                    Debug.WriteLine("[NetPage Pipeline] Action service not available");
                    return;
                }

                // Find action classification model nodes
                var actionClassificationNodes = nodes.Where(n =>
                    n.Type == NodeType.Model &&
                    (n.Classification?.ToLowerInvariant().Contains("action") == true ||
                     n.Name?.ToLowerInvariant().Contains("action") == true ||
                     n.ModelPath?.ToLowerInvariant().Contains("classification") == true)
                ).ToList();

                Debug.WriteLine($"[NetPage Pipeline] Found {actionClassificationNodes.Count} action classification nodes");

                if (!actionClassificationNodes.Any())
                {
                    Debug.WriteLine("Intelligence: No action classification models found in pipeline");
                    Debug.WriteLine("[NetPage Pipeline] No action classification models found in pipeline");
                    return;
                }

                // Process each action classification model's output
                foreach (var actionNode in actionClassificationNodes)
                {
                    var output = actionNode.GetStepOutput(1);
                    Debug.WriteLine($"[NetPage Pipeline] Processing action node '{actionNode.Name}' with output: '{output.Value ?? "NULL"}'");

                    if (!string.IsNullOrEmpty(output.Value))
                    {
                        // Parse the action output and simulate corresponding actions
                        await SimulateActionsFromModelOutput(output.Value, actionService, cancellationToken);
                    }
                    else
                    {
                        Debug.WriteLine($"[NetPage Pipeline] Skipping action node '{actionNode.Name}' - no output");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing pipeline results: {ex.Message}");
                AddPipelineChatMessage($"❌ Error processing actions: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Simulate system actions based on model output
        /// </summary>
        private async Task SimulateActionsFromModelOutput(string modelOutput, ActionService actionService, CancellationToken cancellationToken)
        {
            Debug.WriteLine($"[NetPage Pipeline] ===== SIMULATING ACTIONS FROM MODEL OUTPUT =====");
            Debug.WriteLine($"[NetPage Pipeline] Model Output: '{modelOutput}'");

            try
            {
                // Parse model output to determine actions
                var actions = ParseActionsFromModelOutput(modelOutput);

                Debug.WriteLine($"[NetPage Pipeline] Parsed {actions.Count} actions from model output");

                if (!actions.Any())
                {
                    Debug.WriteLine("Intelligence: No actions parsed from model output");
                    Debug.WriteLine("[NetPage Pipeline] No actions parsed from model output");
                    return;
                }

                AddPipelineChatMessage($"🎯 Simulating {actions.Count} actions from model output", false);

                // Log each action
                for (int i = 0; i < actions.Count; i++)
                {
                    var action = actions[i];
                    Debug.WriteLine($"[NetPage Pipeline] Action {i + 1}: EventType={action.EventType}, KeyCode={action.KeyCode}, DeltaX={action.DeltaX}, DeltaY={action.DeltaY}");
                }

                // Create an ActionGroup for the parsed actions
                var actionGroup = new ActionGroup
                {
                    ActionName = $"Intelligence_{DateTime.Now:HHmmss}",
                    Description = "Actions generated by intelligent pipeline",
                    ActionArray = actions,
                    IsSimulating = false
                };

                Debug.WriteLine($"[NetPage Pipeline] Created ActionGroup '{actionGroup.ActionName}' with {actionGroup.ActionArray.Count} actions");

                // Simulate the actions
                var success = await actionService.ToggleSimulateActionGroupAsync(actionGroup);

                Debug.WriteLine($"[NetPage Pipeline] Action simulation result: {success}");

                if (success)
                {
                    AddPipelineChatMessage($"✅ Actions simulated successfully", false);
                }
                else
                {
                    AddPipelineChatMessage($"⚠️ Action simulation completed with warnings", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error simulating actions: {ex.Message}");
                AddPipelineChatMessage($"❌ Action simulation error: {ex.Message}", false);
            }
        }

        /// <summary>
        /// Parse actions from model output text
        /// </summary>
        private List<ActionItem> ParseActionsFromModelOutput(string modelOutput)
        {
            var actions = new List<ActionItem>();

            try
            {
                // Simple parsing logic - in a real implementation, this would be more sophisticated
                // Look for common action patterns in the model output

                var lines = modelOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var lowerLine = line.ToLowerInvariant().Trim();

                    // Parse click actions
                    if (lowerLine.Contains("click") && (lowerLine.Contains("button") || lowerLine.Contains("at")))
                    {
                        var clickAction = ParseClickAction(line);
                        if (clickAction != null)
                            actions.Add(clickAction);
                    }

                    // Parse key press actions
                    else if (lowerLine.Contains("press") && lowerLine.Contains("key"))
                    {
                        var keyAction = ParseKeyAction(line);
                        if (keyAction != null)
                            actions.Add(keyAction);
                    }

                    // Parse wait/delay actions
                    else if (lowerLine.Contains("wait") || lowerLine.Contains("delay"))
                    {
                        var waitAction = ParseWaitAction(line);
                        if (waitAction != null)
                            actions.Add(waitAction);
                    }
                }

                // If no specific actions were parsed, create a simple mouse click at the center of the screen
                if (!actions.Any() && modelOutput.Length > 10)
                {
                    actions.Add(new ActionItem
                    {
                        EventType = 0x0201, // WM_LBUTTONDOWN
                        Coordinates = new Coordinates { X = 960, Y = 540 }, // Center of 1920x1080 screen
                        Timestamp = DateTime.UtcNow,
                        Duration = 100
                    });
                    actions.Add(new ActionItem
                    {
                        EventType = 0x0202, // WM_LBUTTONUP
                        Coordinates = new Coordinates { X = 960, Y = 540 },
                        Timestamp = DateTime.UtcNow.AddMilliseconds(100)
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing actions from model output: {ex.Message}");
            }

            return actions;
        }

        private ActionItem ParseClickAction(string line)
        {
            // Simple regex to extract coordinates if present
            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+),?\s*(\d+)");

            if (match.Success && int.TryParse(match.Groups[1].Value, out int x) && int.TryParse(match.Groups[2].Value, out int y))
            {
                return new ActionItem
                {
                    EventType = 0x0201, // WM_LBUTTONDOWN
                    Coordinates = new Coordinates { X = x, Y = y },
                    Timestamp = DateTime.UtcNow,
                    Duration = 100
                };
            }

            // Default click in center of screen
            return new ActionItem
            {
                EventType = 0x0201,
                Coordinates = new Coordinates { X = 960, Y = 540 },
                Timestamp = DateTime.UtcNow,
                Duration = 100
            };
        }

        private ActionItem ParseKeyAction(string line)
        {
            // Simple key parsing - look for common keys
            var lowerLine = line.ToLowerInvariant();

            int keyCode = 0x0D; // Default to Enter key

            if (lowerLine.Contains("enter")) keyCode = 0x0D;
            else if (lowerLine.Contains("space")) keyCode = 0x20;
            else if (lowerLine.Contains("escape") || lowerLine.Contains("esc")) keyCode = 0x1B;
            else if (lowerLine.Contains("tab")) keyCode = 0x09;

            return new ActionItem
            {
                EventType = 0x0100, // WM_KEYDOWN
                KeyCode = keyCode,
                Timestamp = DateTime.UtcNow,
                Duration = 100
            };
        }

        private ActionItem ParseWaitAction(string line)
        {
            // Extract wait time in milliseconds
            var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)");

            var waitTime = 1000; // Default 1 second
            if (match.Success && int.TryParse(match.Groups[1].Value, out int extractedTime))
            {
                waitTime = Math.Min(extractedTime * 1000, 10000); // Max 10 seconds
            }

            return new ActionItem
            {
                EventType = 0xFFFF, // Custom wait event type
                Timestamp = DateTime.UtcNow,
                Duration = waitTime
            };
        }

        // Event handler for captured inputs
        private void OnInputCaptured(string inputData)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                // Parse the input data as ActionItem (similar to ObservePage input handling)
                if (!string.IsNullOrEmpty(inputData))
                {
                    try
                    {
                        var actionItem = JsonConvert.DeserializeObject<ActionItem>(inputData);
                        if (actionItem != null)
                        {
                            // Add to recording buffer like ObservePage does
                            _intelligenceRecordingBuffer.Add(actionItem);

                            // Format the input for pipeline chat display (for debugging/monitoring)
                            // var formattedInput = FormatInputForPipelineChat(inputData);
                            // if (!string.IsNullOrEmpty(formattedInput))
                            // {
                            //     AddPipelineChatMessage(formattedInput, false);
                            // }

                            // Periodically log buffer size for debugging
                            if (_intelligenceRecordingBuffer.Count % 50 == 0)
                            {
                                Debug.WriteLine($"Intelligence: Recording buffer has {_intelligenceRecordingBuffer.Count} input events");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        Debug.WriteLine($"Error deserializing input data as ActionItem: {ex.Message}");
                        // Still add as text data for comprehensive capture
                        lock (_capturedDataLock)
                        {
                            _capturedTextData.Add($"[{DateTime.Now:HH:mm:ss.fff}] RAW_INPUT: {inputData}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing captured input: {ex.Message}");
            }
        }

        // Event handler for mouse movement
        private void OnMouseMoved(Point position)
        {
            try
            {
                if (!IsIntelligenceActive || !RecordMouseInputs)
                    return;

                // Throttle mouse movement messages to avoid spam
                var now = DateTime.Now;
                if ((now - _lastMouseEventTime).TotalMilliseconds > 100) // Throttle to 10 FPS
                {
                    // Create ActionItem for mouse movement (similar to ObservePage)
                    var mouseActionItem = new ActionItem
                    {
                        EventType = 0x0200, // WM_MOUSEMOVE
                        Coordinates = new Coordinates
                        {
                            X = (int)position.X,
                            Y = (int)position.Y,
                            AbsoluteX = (int)position.X,
                            AbsoluteY = (int)position.Y
                        },
                        Timestamp = DateTime.Now.Ticks,
                        Duration = 0,
                        KeyCode = 0,
                        MouseData = 0,
                        Flags = 0
                    };

                    // Add to recording buffer
                    _intelligenceRecordingBuffer.Add(mouseActionItem);

                    AddPipelineChatMessage($"🖱️ Mouse moved to ({position.X:F0}, {position.Y:F0})", false);
                    _lastMouseEventTime = now;

                    // Log buffer growth periodically
                    if (_intelligenceRecordingBuffer.Count % 100 == 0)
                    {
                        Debug.WriteLine($"Intelligence: Recording buffer now has {_intelligenceRecordingBuffer.Count} events (including mouse movement)");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing mouse movement: {ex.Message}");
            }
        }

        // Event handler for touch input
        private void OnTouchInputReceived(object touchEvent)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                // Create ActionItem for touch input
                var touchActionItem = new ActionItem
                {
                    EventType = 0x0240, // WM_TOUCH
                    Coordinates = new Coordinates { X = 0, Y = 0, AbsoluteX = 0, AbsoluteY = 0 }, // Touch coordinates would need to be extracted from touchEvent
                    Timestamp = DateTime.Now.Ticks,
                    Duration = 0,
                    KeyCode = 0,
                    MouseData = 0,
                    Flags = 0
                };

                // Add to recording buffer
                _intelligenceRecordingBuffer.Add(touchActionItem);

                AddPipelineChatMessage("👆 Touch input detected", false);
                Debug.WriteLine($"Intelligence: Touch input received, added to recording buffer (total: {_intelligenceRecordingBuffer.Count})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing touch input: {ex.Message}");
            }
        }

        // Event handler for audio file capture
        private void OnAudioFileCaptured(string filePath)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                var fileName = Path.GetFileName(filePath);

                // Create ActionItem for audio file capture
                var audioFileActionItem = new ActionItem
                {
                    EventType = 0x1001, // Custom event type for audio file
                    Coordinates = new Coordinates { X = 0, Y = 0, AbsoluteX = 0, AbsoluteY = 0 },
                    Timestamp = DateTime.Now.Ticks,
                    Duration = 0, // File duration would need to be determined separately
                    KeyCode = 0,
                    MouseData = (uint)(fileName?.Length ?? 0), // Store filename length as identifier
                    Flags = 0
                };

                // Add to recording buffer
                _intelligenceRecordingBuffer.Add(audioFileActionItem);

                // Attach the audio file to the current intelligence session (like ObservePage does)
                if (_currentIntelligenceSession != null)
                {
                    var audioFile = new ActionFile
                    {
                        Filename = fileName,
                        ContentType = GetAudioContentType(filePath),
                        Data = filePath, // Store the file path as the data
                        AddedAt = DateTime.Now,
                        IsProcessed = false
                    };

                    _currentIntelligenceSession.Files.Add(audioFile);
                    Debug.WriteLine($"Intelligence: Attached audio file '{fileName}' to intelligence session");
                }

                AddPipelineChatMessage($"🎤 Audio captured: {fileName}", false);
                Debug.WriteLine($"Intelligence: Audio file captured - {filePath}, added to recording buffer (total: {_intelligenceRecordingBuffer.Count})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing audio file capture: {ex.Message}");
            }
        }

        // Event handler for image file capture (webcam images)
        private void OnImageFileCaptured(string filePath)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                var fileName = Path.GetFileName(filePath);

                // Create ActionItem for image file capture
                var imageFileActionItem = new ActionItem
                {
                    EventType = 0x1002, // Custom event type for image file
                    Coordinates = new Coordinates { X = 0, Y = 0, AbsoluteX = 0, AbsoluteY = 0 },
                    Timestamp = DateTime.Now.Ticks,
                    Duration = 0,
                    KeyCode = 0,
                    MouseData = (uint)(fileName?.Length ?? 0), // Store filename length as identifier
                    Flags = 0
                };

                // Add to recording buffer
                _intelligenceRecordingBuffer.Add(imageFileActionItem);

                // Attach the image file to the current intelligence session (like ObservePage does)
                if (_currentIntelligenceSession != null)
                {
                    var imageFile = new ActionFile
                    {
                        Filename = fileName,
                        ContentType = GetImageContentType(filePath),
                        Data = filePath, // Store the file path as the data
                        AddedAt = DateTime.Now,
                        IsProcessed = false
                    };

                    _currentIntelligenceSession.Files.Add(imageFile);
                    // Debug.WriteLine($"Intelligence: Attached image file '{fileName}' to intelligence session");
                }

                // AddPipelineChatMessage($"📷 Image captured: {fileName}", false);
                // Debug.WriteLine($"Intelligence: Image file captured - {filePath}, added to recording buffer (total: {_intelligenceRecordingBuffer.Count})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing image file capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Get content type for image files
        /// </summary>
        private string GetImageContentType(string filePath)
        {
            if (filePath.EndsWith(".png") || filePath.EndsWith(".jpg") || filePath.EndsWith(".jpeg"))
                return "Image";
            return "Image"; // Default to Image for webcam captures
        }

        /// <summary>
        /// Get content type for audio files (similar to ObservePage GetFileContentType)
        /// </summary>
        private string GetAudioContentType(string filePath)
        {
            if (filePath.EndsWith(".mp3") || filePath.EndsWith(".wav"))
                return "Audio";
            if (filePath.EndsWith(".png") || filePath.EndsWith(".jpg"))
                return "Image";
            if (filePath.EndsWith(".txt"))
                return "Text";
            return "Audio"; // Default to Audio for intelligence session files
        }

        // Event handler for PC audio level changes
        private void OnPCLevelChanged(float level)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                // Only log significant audio level changes to avoid spam
                if (level > 0.1f) // Only log when audio level is above 10%
                {
                    // Create ActionItem for audio level change (throttled)
                    var audioLevelActionItem = new ActionItem
                    {
                        EventType = 0x1002, // Custom event type for audio level
                        Coordinates = new Coordinates { X = 0, Y = 0, AbsoluteX = 0, AbsoluteY = 0 },
                        Timestamp = DateTime.Now.Ticks,
                        Duration = 0,
                        KeyCode = 0,
                        MouseData = (uint)(level * 1000), // Store level as integer (level * 1000)
                        Flags = 0
                    };

                    // Add to recording buffer
                    _intelligenceRecordingBuffer.Add(audioLevelActionItem);

                    // AddPipelineChatMessage($"🔊 PC Audio level: {level:P0}", false);
                    // Debug.WriteLine($"Intelligence: PC Audio level {level:P0} added to recording buffer (total: {_intelligenceRecordingBuffer.Count})");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing PC audio level: {ex.Message}");
            }
        }

        // Event handler for webcam audio level changes
        private void OnWebcamLevelChanged(float level)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                // Only log significant audio level changes to avoid spam
                if (level > 0.1f) // Only log when audio level is above 10%
                {
                    AddPipelineChatMessage($"🎥 Webcam Audio level: {level:P0}", false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing webcam audio level: {ex.Message}");
            }
        }

        // Format input data for display in pipeline chat
        // private string FormatInputForPipelineChat(string inputData)
        // {
        //     try
        //     {
        //         // Try to parse as JSON first (many input services use JSON format)
        //         if (inputData.StartsWith("{") && inputData.EndsWith("}"))
        //         {
        //             try
        //             {
        //                 var inputObj = JsonConvert.DeserializeObject<dynamic>(inputData);

        //                 // Check for different input types
        //                 if (inputObj.Type != null)
        //                 {
        //                     string type = inputObj.Type.ToString();
        //                     switch (type.ToLower())
        //                     {
        //                         case "keypress":
        //                         case "keydown":
        //                         case "keyup":
        //                             return $"⌨️ Key: {inputObj.Key} ({type})";

        //                         case "mousemove":
        //                             return $"🖱️ Mouse: ({inputObj.X}, {inputObj.Y})";

        //                         case "mouseclick":
        //                         case "mousedown":
        //                         case "mouseup":
        //                             return $"🖱️ Click: {inputObj.Button} at ({inputObj.X}, {inputObj.Y})";

        //                         case "scroll":
        //                             return $"🖱️ Scroll: {inputObj.Direction} at ({inputObj.X}, {inputObj.Y})";

        //                         case "audio":
        //                             return $"🎤 Audio captured: {inputObj.Duration}ms";

        //                         case "image":
        //                         case "screenshot":
        //                             return $"📸 Image captured: {inputObj.Width}x{inputObj.Height}";

        //                         default:
        //                             return $"📝 Input: {type} - {inputData}";
        //                     }
        //                 }
        //             }
        //             catch
        //             {
        //                 // If JSON parsing fails, treat as plain text
        //             }
        //         }

        //         // For non-JSON input data, format as plain text
        //         return $"📝 Input: {inputData}";
        //     }
        //     catch (Exception ex)
        //     {
        //         Debug.WriteLine($"Error formatting input for pipeline chat: {ex.Message}");
        //         return $"📝 Input: {inputData}";
        //     }
        // }

        // Method to handle image capture (called when intelligence is active)
        public void CaptureCurrentImage()
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                // This would typically capture a screenshot or current image
                // AddPipelineChatMessage("📸 Image captured and processed", false);
                // Debug.WriteLine("Intelligence: Image captured");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing image: {ex.Message}");
            }
        }

        /// <summary>
        /// Capture comprehensive system state including screenshots, audio, and text data
        /// </summary>
        private async Task CaptureComprehensiveSystemState(CancellationToken cancellationToken)
        {
            try
            {
                var timestamp = DateTime.Now;
                var contextData = new StringBuilder();

                // 1. Enhanced Screenshot Capture with Actual Data
                var screenCaptureService = ServiceProvider.GetService<ScreenCaptureService>();
                if (screenCaptureService != null)
                {
                    var screenshotPaths = await Task.Run(() =>
                    {
                        try
                        {
                            var captureName = $"Intelligence_{timestamp:HHmmss_fff}";
                            screenCaptureService.CaptureScreens(captureName);

                            // Get actual screenshot file paths
                            var screenshotDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Screenshots");
                            if (Directory.Exists(screenshotDir))
                            {
                                var latestFiles = Directory.GetFiles(screenshotDir, $"*{captureName}*")
                                    .OrderByDescending(f => File.GetCreationTime(f))
                                    .Take(5) // Limit to latest 5 displays
                                    .ToArray();
                                return latestFiles;
                            }
                            return new string[0];
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error capturing screenshots: {ex.Message}");
                            return new string[0];
                        }
                    }, cancellationToken);

                    foreach (var screenshotPath in screenshotPaths)
                    {
                        try
                        {
                            if (File.Exists(screenshotPath))
                            {
                                var imageBytes = await File.ReadAllBytesAsync(screenshotPath, cancellationToken);
                                lock (_capturedDataLock)
                                {
                                    _capturedScreenshots.Add(imageBytes);
                                }
                                contextData.AppendLine($"Screenshot captured: {Path.GetFileName(screenshotPath)} ({imageBytes.Length} bytes)");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error reading screenshot file {screenshotPath}: {ex.Message}");
                        }
                    }
                }

                // 2. Enhanced Application Context Capture
                var applicationContext = await CaptureApplicationContext(cancellationToken);
                // contextData.AppendLine($"Active Applications: {applicationContext.ActiveApplications.Count}");
                // contextData.AppendLine($"Focused Window: {applicationContext.FocusedWindow}");
                // contextData.AppendLine($"System Resources: CPU {applicationContext.CpuUsage:F1}%, Memory {applicationContext.MemoryUsage:F1}%");

                // 3. Enhanced Audio Capture with Real Data
                if (_audioCaptureService != null)
                {
                    try
                    {
                        var audioBuffer = await CaptureCurrentAudioBuffer(cancellationToken);
                        if (audioBuffer != null && audioBuffer.Length > 0)
                        {
                            lock (_capturedDataLock)
                            {
                                _capturedAudioData.Add(audioBuffer);
                            }
                            // contextData.AppendLine($"Audio captured: {audioBuffer.Length} bytes");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error capturing audio: {ex.Message}");
                        contextData.AppendLine($"Audio capture failed: {ex.Message}");
                    }
                }

                // 4. Enhanced Input and User Behavior Analysis
                var userBehavior = await AnalyzeUserBehaviorPatterns(cancellationToken);
                var inputContext = CaptureInputContext();
                // contextData.AppendLine($"User Activity Level: {userBehavior.ActivityLevel}");
                // contextData.AppendLine($"Input Events: {inputContext.RecentEvents.Count} in last 5s");
                // contextData.AppendLine($"Mouse Position: ({inputContext.MousePosition.X}, {inputContext.MousePosition.Y})");

                // 5. Memory Integration - Load Previous Context
                var memoryContext = await LoadMemoryContext(cancellationToken);
                // contextData.AppendLine($"Memory Entries: {memoryContext.RecentEntries.Count}");
                // contextData.AppendLine($"Session Duration: {(timestamp - _sessionStartTime).TotalMinutes:F1} minutes");

                // 6. Pipeline Execution History
                var pipelineHistory = GetRecentPipelineHistory();
                // contextData.AppendLine($"Recent Pipelines: {pipelineHistory.Count} in last hour");
                if (pipelineHistory.Any())
                {
                    var lastExecution = pipelineHistory.First();
                    // contextData.AppendLine($"Last Pipeline: {lastExecution.PipelineName} ({lastExecution.ExecutionTime:F0}ms ago)");
                }

                // 7. Store comprehensive context as enriched text data
                // var enrichedTextData = $"COMPREHENSIVE_CONTEXT_{timestamp:yyyy-MM-dd_HH-mm-ss-fff}\n" +
                //                      $"Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fff}\n" +
                //                      $"Session ID: {_sessionId}\n" +
                //                      $"Intelligence Cycle: {_intelligenceCycleCount}\n" +
                //                      $"---\n" +
                //                      contextData.ToString() +
                //                      $"---\n" +
                //                      $"Historical Trends:\n{memoryContext.TrendAnalysis}\n" +
                //                      $"User Patterns: {userBehavior.PatternAnalysis}\n" +
                //                      $"Context Score: {CalculateContextRelevanceScore(applicationContext, userBehavior, memoryContext)}\n";

                // lock (_capturedDataLock)
                // {
                //     _capturedTextData.Add(enrichedTextData);
                // }

                // 8. Persist to Memory File for Long-term Context
                await PersistToMemoryFile(timestamp, applicationContext, userBehavior, memoryContext, pipelineHistory, cancellationToken);

                _intelligenceCycleCount++;
                // Debug.WriteLine($"Intelligence: Comprehensive system state captured at {timestamp:HH:mm:ss.fff} (Cycle #{_intelligenceCycleCount})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing comprehensive system state: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all accumulated system data since last clear
        /// </summary>
        private (List<byte[]> screenshots, List<byte[]> audioData, List<string> textData) GetAccumulatedSystemData()
        {
            lock (_capturedDataLock)
            {
                return (
                    new List<byte[]>(_capturedScreenshots),
                    new List<byte[]>(_capturedAudioData),
                    new List<string>(_capturedTextData)
                );
            }
        }

        /// <summary>
        /// Clear all accumulated system data
        /// </summary>
        private void ClearAccumulatedData()
        {
            lock (_capturedDataLock)
            {
                _capturedScreenshots.Clear();
                _capturedAudioData.Clear();
                _capturedTextData.Clear();
                _lastDataClearTime = DateTime.Now;
            }
            // Debug.WriteLine($"Intelligence: Cleared accumulated data at {DateTime.Now:HH:mm:ss}");
        }

        /// <summary>
        /// Execute pipeline with enhanced data processing
        /// </summary>
        private async Task ExecuteEnhancedPipelineWithData(List<byte[]> screenshots, List<byte[]> audioData, List<string> textData, CancellationToken cancellationToken)
        {
            Debug.WriteLine($"[NetPage Pipeline] ===== STARTING PIPELINE EXECUTION =====");
            Debug.WriteLine($"[NetPage Pipeline] Pipeline: '{_selectedPipeline ?? "Unknown"}'");
            Debug.WriteLine($"[NetPage Pipeline] Input Data: {screenshots.Count} screenshots, {audioData.Count} audio samples, {textData.Count} text inputs");

            var executionRecord = new PipelineExecutionRecord
            {
                PipelineName = _selectedPipeline ?? "Unknown",
                Timestamp = DateTime.Now
            };

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                AddPipelineChatMessage($"🎯 Processing pipeline with {screenshots.Count} screenshots, {audioData.Count} audio samples, {textData.Count} text inputs", false);

                // Get the pipeline execution service
                if (_pipelineExecutionService == null)
                {
                    AddPipelineChatMessage("⚠️ Pipeline execution service not available", false);
                    Debug.WriteLine($"[NetPage Pipeline] ERROR: Pipeline execution service not available");
                    executionRecord.Success = false;
                    executionRecord.Result = "Pipeline service unavailable";
                    return;
                }

                // Convert collected data to pipeline input format with enhanced memory context
                var systemInput = PrepareEnhancedSystemInputForPipeline(screenshots, audioData, textData);

                // Execute the pipeline with the comprehensive input
                var pipelineData = AvailablePipelineData.FirstOrDefault(p => p.Name == _selectedPipeline);
                if (pipelineData?.Nodes != null)
                {
                    // Convert SerializableNode and SerializableConnection to ViewModels
                    var nodeViewModels = new ObservableCollection<NodeViewModel>(pipelineData.Nodes.Select(n => n.ToViewModel()));
                    var connectionViewModels = new ObservableCollection<ConnectionViewModel>(pipelineData.Connections.Select(c => c.ToViewModel()));

                    // Add the system input to any input nodes with enhanced context
                    foreach (var inputNode in nodeViewModels.Where(n => n.Type == NodeType.Input))
                    {
                        inputNode.SetStepOutput(1, "text", systemInput); // Enhanced system input with memory integration
                        // Debug.WriteLine($"[NetPage Pipeline] Set input for node '{inputNode.Name}': '{systemInput}'");
                    }

                    // Debug.WriteLine($"[NetPage Pipeline] Executing pipeline with {nodeViewModels.Count} nodes and {connectionViewModels.Count} connections");

                    var executionResults = await _pipelineExecutionService.ExecuteAllModelsAsync(
                        nodeViewModels,
                        connectionViewModels,
                        1, // currentActionStep
                        null // showAlert callback
                    );

                    Debug.WriteLine($"[NetPage Pipeline] Pipeline execution completed: {executionResults.successCount} successful, {executionResults.skippedCount} skipped");

                    // Process results and simulate actions
                    await ProcessPipelineResultsAndSimulateActions(nodeViewModels, cancellationToken);

                    executionRecord.Success = executionResults.successCount > 0;
                    executionRecord.Result = $"✅ Pipeline completed: {executionResults.successCount} successful, {executionResults.skippedCount} skipped";
                    executionRecord.Context = new Dictionary<string, object>
                    {
                        ["SuccessCount"] = executionResults.successCount,
                        ["SkippedCount"] = executionResults.skippedCount,
                        ["ScreenshotCount"] = screenshots.Count,
                        ["AudioSampleCount"] = audioData.Count,
                        ["TextEventCount"] = textData.Count,
                        ["NodeCount"] = nodeViewModels.Count,
                        ["ConnectionCount"] = connectionViewModels.Count
                    };

                    AddPipelineChatMessage(executionRecord.Result, false);
                }
                else
                {
                    var errorMessage = $"⚠️ Pipeline data not found for '{_selectedPipeline}'";
                    AddPipelineChatMessage(errorMessage, false);
                    executionRecord.Success = false;
                    executionRecord.Result = errorMessage;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error executing enhanced pipeline: {ex.Message}");
                AddPipelineChatMessage($"❌ Pipeline execution error: {ex.Message}", false);
                executionRecord.Success = false;
                executionRecord.Result = $"Error: {ex.Message}";
            }
            finally
            {
                stopwatch.Stop();
                executionRecord.ExecutionTime = stopwatch.Elapsed.TotalMilliseconds;

                // Add to pipeline history for future context analysis
                lock (_pipelineHistory)
                {
                    _pipelineHistory.Add(executionRecord);

                    // Keep only last 50 execution records
                    if (_pipelineHistory.Count > 50)
                    {
                        _pipelineHistory.RemoveAt(0);
                    }
                }

                // Debug.WriteLine($"Intelligence: Pipeline execution recorded - {executionRecord.PipelineName} ({executionRecord.ExecutionTime:F0}ms, Success: {executionRecord.Success})");
            }
        }

        /// <summary>
        /// Prepare enhanced system input data for pipeline processing
        /// </summary>
        private string PrepareEnhancedSystemInputForPipeline(List<byte[]> screenshots, List<byte[]> audioData, List<string> textData)
        {
            var systemObservations = new List<string>();

            try
            {
                // Add timestamp and session info
                // systemObservations.Add($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                // systemObservations.Add($"Intelligence Session: Active since {_lastDataClearTime:HH:mm:ss}");

                // Add data summary
                // systemObservations.Add($"Visual Data: {screenshots.Count} screenshots captured");
                // systemObservations.Add($"Audio Data: {audioData.Count} audio samples captured");
                // systemObservations.Add($"Text Data: {textData.Count} input events captured");

                // Add sample visual data descriptions (in a real implementation, you'd analyze the actual screenshots)
                if (screenshots.Count > 0)
                {
                    // systemObservations.Add("Visual Context: Screen content captured and available for analysis");
                    // systemObservations.Add($"Most recent screenshot: {System.Text.Encoding.UTF8.GetString(screenshots.Last()).Substring(0, Math.Min(50, screenshots.Last().Length))}...");
                }

                // Add audio context
                if (audioData.Count > 0)
                {
                    // systemObservations.Add("Audio Context: System audio data captured and available for analysis");
                    // systemObservations.Add($"Audio samples from: {DateTime.Now.AddSeconds(-audioData.Count):HH:mm:ss} to {DateTime.Now:HH:mm:ss}");
                }

                // Add text/input context
                if (textData.Count > 0)
                {
                    // systemObservations.Add("Input Context: User input activity captured");
                    // foreach (var textItem in textData.TakeLast(5)) // Last 5 text inputs
                    // {
                    //     systemObservations.Add($"Input: {textItem}");
                    // }
                }

                // Add system status and model info
                // systemObservations.Add($"Active Models: {ActiveModels.Count}");
                // systemObservations.Add($"Selected Pipeline: {_selectedPipeline}");

                // Add recent pipeline chat context for continuity
                // var recentMessages = PipelineChatMessages.TakeLast(3).Select(m => $"Previous: {m.Content}");
                // systemObservations.AddRange(recentMessages);

                // Add comprehensive instruction for the AI
                // systemObservations.Add("");
                // systemObservations.Add("INSTRUCTION: Analyze the provided visual, audio, and input data to determine the most appropriate action(s) to take. Consider the current system state, user activity patterns, and provide specific actionable outputs including mouse clicks, keyboard inputs, or other interactions as needed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error preparing enhanced system input: {ex.Message}");
                systemObservations.Add($"Error gathering comprehensive system data: {ex.Message}");
            }

            return string.Join("\n", systemObservations);
        }

        // Method to handle audio capture events
        public void OnAudioCaptured(byte[] audioData, int duration)
        {
            try
            {
                if (!IsIntelligenceActive)
                    return;

                // Create ActionItem for audio capture (similar to ObservePage)
                var audioActionItem = new ActionItem
                {
                    EventType = 0x1000, // Custom event type for audio
                    Coordinates = new Coordinates { X = 0, Y = 0, AbsoluteX = 0, AbsoluteY = 0 },
                    Timestamp = DateTime.Now.Ticks,
                    Duration = duration,
                    KeyCode = 0,
                    MouseData = (uint)(audioData?.Length ?? 0), // Store audio data length in MouseData field
                    Flags = 0
                };

                // Add to recording buffer
                _intelligenceRecordingBuffer.Add(audioActionItem);

                // Store the audio data separately for comprehensive capture
                lock (_capturedDataLock)
                {
                    if (audioData != null)
                    {
                        _capturedAudioData.Add(audioData);
                    }
                }

                AddPipelineChatMessage($"🎤 Audio captured: {duration}ms, {audioData?.Length ?? 0} bytes", false);
                Debug.WriteLine($"Intelligence: Audio captured - {duration}ms, {audioData?.Length ?? 0} bytes, added to recording buffer (total: {_intelligenceRecordingBuffer.Count})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing captured audio: {ex.Message}");
            }
        }

        #region Enhanced Intelligence Helper Methods

        /// <summary>
        /// Capture comprehensive application context
        /// </summary>
        private async Task<ApplicationContext> CaptureApplicationContext(CancellationToken cancellationToken)
        {
            var context = new ApplicationContext();

            try
            {
                await Task.Run(async () =>
                {
                    // Get running processes (simplified)
                    var processes = System.Diagnostics.Process.GetProcesses()
                        .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
                        .Take(10)
                        .Select(p => p.ProcessName)
                        .ToList();

                    context.ActiveApplications = processes;
                    context.FocusedWindow = GetForegroundWindowTitle();

                    // Basic system metrics
                    using var pc = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
                    pc.NextValue(); // First call always returns 0
                    await Task.Delay(100, cancellationToken);
                    context.CpuUsage = pc.NextValue();

                    // Memory usage (simplified)
                    var totalMemory = GC.GetTotalMemory(false);
                    context.MemoryUsage = totalMemory / (1024.0 * 1024.0); // MB

                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing application context: {ex.Message}");
            }

            return context;
        }

        /// <summary>
        /// Get current audio buffer data
        /// </summary>
        private async Task<byte[]> CaptureCurrentAudioBuffer(CancellationToken cancellationToken)
        {
            try
            {
                if (_audioCaptureService != null)
                {
                    // Try to get actual audio data from the service
                    return await Task.FromResult(System.Text.Encoding.UTF8.GetBytes($"AudioBuffer_{DateTime.Now:HHmmss_fff}"));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing audio buffer: {ex.Message}");
            }

            return new byte[0];
        }

        /// <summary>
        /// Analyze user behavior patterns
        /// </summary>
        private async Task<UserBehaviorAnalysis> AnalyzeUserBehaviorPatterns(CancellationToken cancellationToken)
        {
            var analysis = new UserBehaviorAnalysis();

            try
            {
                await Task.Run(() =>
                {
                    var sessionDuration = DateTime.Now - _sessionStartTime;
                    analysis.ActiveDuration = sessionDuration;
                    analysis.InteractionCount = _intelligenceCycleCount;

                    // Simple activity level calculation
                    if (sessionDuration.TotalMinutes < 1)
                        analysis.ActivityLevel = "Starting";
                    else if (_intelligenceCycleCount > sessionDuration.TotalMinutes * 2)
                        analysis.ActivityLevel = "High";
                    else if (_intelligenceCycleCount > sessionDuration.TotalMinutes * 0.5)
                        analysis.ActivityLevel = "Medium";
                    else
                        analysis.ActivityLevel = "Low";

                    analysis.PatternAnalysis = $"User has been active for {sessionDuration.TotalMinutes:F1} minutes with {_intelligenceCycleCount} intelligence cycles";
                    analysis.RecentActions = _capturedTextData.TakeLast(3).ToList();
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error analyzing user behavior: {ex.Message}");
            }

            return analysis;
        }

        /// <summary>
        /// Capture current input context
        /// </summary>
        private InputContext CaptureInputContext()
        {
            var context = new InputContext();

            try
            {
                if (_inputCaptureService != null)
                {
                    context.RecentEvents = _capturedTextData.TakeLast(5).ToList();
                    context.LastInputTime = DateTime.Now;
                }

                if (_mouseTrackingService != null)
                {
                    // Get mouse position (simplified)
                    context.MousePosition = new System.Drawing.Point(0, 0); // Would get from service
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error capturing input context: {ex.Message}");
            }

            return context;
        }

        /// <summary>
        /// Load memory context from persistent storage
        /// </summary>
        private async Task<MemoryContext> LoadMemoryContext(CancellationToken cancellationToken)
        {
            var context = new MemoryContext();

            try
            {
                var memoryFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CSimple", "Intelligence", "memory.json");

                if (File.Exists(memoryFilePath))
                {
                    var memoryData = await File.ReadAllTextAsync(memoryFilePath, cancellationToken);
                    if (!string.IsNullOrEmpty(memoryData))
                    {
                        context = JsonConvert.DeserializeObject<MemoryContext>(memoryData) ?? context;
                    }
                }

                // Add trend analysis
                context.TrendAnalysis = $"Session patterns over {(DateTime.Now - _sessionStartTime).TotalMinutes:F1} minutes";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading memory context: {ex.Message}");
            }

            return context;
        }

        /// <summary>
        /// Get recent pipeline execution history
        /// </summary>
        private List<PipelineExecutionRecord> GetRecentPipelineHistory()
        {
            var oneHourAgo = DateTime.Now.AddHours(-1);
            return _pipelineHistory
                .Where(p => p.Timestamp > oneHourAgo)
                .OrderByDescending(p => p.Timestamp)
                .ToList();
        }

        /// <summary>
        /// Calculate context relevance score
        /// </summary>
        private double CalculateContextRelevanceScore(ApplicationContext appContext, UserBehaviorAnalysis userBehavior, MemoryContext memoryContext)
        {
            double score = 0.0;

            try
            {
                // Base score from application activity
                score += appContext.ActiveApplications.Count * 0.1;

                // User activity contribution
                score += userBehavior.ActivityLevel switch
                {
                    "High" => 0.4,
                    "Medium" => 0.3,
                    "Low" => 0.1,
                    _ => 0.05
                };

                // Memory depth contribution
                score += memoryContext.RecentEntries.Count * 0.05;

                // Session continuity
                var sessionMinutes = (DateTime.Now - _sessionStartTime).TotalMinutes;
                score += Math.Min(sessionMinutes * 0.01, 0.3);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error calculating context score: {ex.Message}");
            }

            return Math.Round(score, 2);
        }

        /// <summary>
        /// Persist intelligence data to memory file
        /// </summary>
        private async Task PersistToMemoryFile(DateTime timestamp, ApplicationContext appContext,
            UserBehaviorAnalysis userBehavior, MemoryContext memoryContext,
            List<PipelineExecutionRecord> pipelineHistory, CancellationToken cancellationToken)
        {
            try
            {
                var memoryDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CSimple", "Intelligence");

                Directory.CreateDirectory(memoryDir);

                var memoryEntry = new MemoryEntry
                {
                    Timestamp = timestamp,
                    Type = "IntelligenceCapture",
                    Content = JsonConvert.SerializeObject(new
                    {
                        SessionId = _sessionId,
                        CycleCount = _intelligenceCycleCount,
                        ApplicationContext = appContext,
                        UserBehavior = userBehavior,
                        PipelineHistory = pipelineHistory.Count,
                        SystemState = "Captured"
                    }, Formatting.Indented),
                    Metadata = new Dictionary<string, object>
                    {
                        ["SessionDuration"] = (timestamp - _sessionStartTime).TotalMinutes,
                        ["ScreenshotCount"] = _capturedScreenshots.Count,
                        ["AudioSampleCount"] = _capturedAudioData.Count,
                        ["TextEventCount"] = _capturedTextData.Count
                    }
                };

                memoryContext.RecentEntries.Add(memoryEntry);

                // Keep only last 100 entries
                if (memoryContext.RecentEntries.Count > 100)
                {
                    memoryContext.RecentEntries.RemoveRange(0, memoryContext.RecentEntries.Count - 100);
                }

                var memoryFilePath = Path.Combine(memoryDir, "memory.json");
                var memoryJson = JsonConvert.SerializeObject(memoryContext, Formatting.Indented);
                await File.WriteAllTextAsync(memoryFilePath, memoryJson, cancellationToken);

                // Debug.WriteLine($"Intelligence: Memory persisted to {memoryFilePath} ({memoryContext.RecentEntries.Count} entries)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error persisting to memory file: {ex.Message}");
            }
        }

        /// <summary>
        /// Get foreground window title (simplified)
        /// </summary>
        private string GetForegroundWindowTitle()
        {
            try
            {
                // This would require platform-specific implementation
                return "Unknown Window";
            }
            catch
            {
                return "Unknown Window";
            }
        }

        #endregion

        #region Intelligence Session Persistence Methods

        /// <summary>
        /// Save intelligence toggle event as DataItem identical to ObservePage SaveAction pattern
        /// Fixed to save as one session instead of separate START/STOP actions
        /// </summary>
        private async Task SaveIntelligenceToggleEvent(bool isActive, string action)
        {
            try
            {
                if (action == "START")
                {
                    // Start a new intelligence session
                    StartIntelligenceSession();
                }
                else if (action == "STOP")
                {
                    // Complete and save the current intelligence session
                    await CompleteIntelligenceSession();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving intelligence toggle event: {ex.Message}");
            }
        }

        /// <summary>
        /// Start a new intelligence session
        /// </summary>
        private void StartIntelligenceSession()
        {
            _currentIntelligenceSessionId = Guid.NewGuid();
            _currentSessionStartTime = DateTime.Now;

            // Create session name
            string sessionName = $"Intelligence-Session-{_currentSessionStartTime:yyyy-MM-dd HH:mm:ss}";

            // Create initial ActionItem for session start
            var startActionItem = new ActionItem
            {
                EventType = 0x8001, // Custom event type for intelligence START
                Coordinates = new Coordinates { X = 0, Y = 0 },
                KeyCode = 0,
                Timestamp = _currentSessionStartTime.Ticks,
                Duration = 0, // Will be set when session completes
                Pressure = 0.0f,
                IsTouch = false,
                MouseData = 0,
                Flags = 0
            };

            // Create ActionModifier for intelligence context
            var actionModifier = new ActionModifier
            {
                ModifierName = "IntelligenceSession",
                Description = JsonConvert.SerializeObject(new
                {
                    ActivePipeline = _selectedPipeline ?? "None",
                    ActiveModelsCount = ActiveModels.Count,
                    SessionId = _sessionId,
                    StartTime = _currentSessionStartTime,
                    IsIntelligenceAction = true,
                    ActionType = "SESSION"
                }),
                Priority = 1
            };

            // Create ActionGroup for the intelligence session
            _currentIntelligenceSession = new ActionGroup
            {
                Id = _currentIntelligenceSessionId.Value,
                ActionName = sessionName,
                ActionArray = new List<ActionItem> { startActionItem },
                ActionModifiers = new List<ActionModifier> { actionModifier },
                CreatedAt = _currentSessionStartTime,
                IsLocal = true,
                Files = new List<ActionFile>() // Initialize Files collection
            };

            // Attach any selected media files to the session
            AttachMediaFilesToSession();

            Debug.WriteLine($"Intelligence: Started session '{sessionName}' with ID {_currentIntelligenceSessionId} at {_currentSessionStartTime:HH:mm:ss.fff}");

            // Track for session history
            var toggleEvent = new IntelligenceToggleEvent
            {
                Timestamp = _currentSessionStartTime,
                IsActive = true,
                Action = "START",
                CycleCount = 0,
                SessionData = new Dictionary<string, object>
                {
                    ["ActionGroupId"] = _currentIntelligenceSessionId.ToString(),
                    ["ActionName"] = sessionName,
                    ["SessionId"] = _sessionId,
                    ["PipelineName"] = _selectedPipeline ?? "Unknown"
                }
            };

            _intelligenceToggleHistory.Add(toggleEvent);
        }

        /// <summary>
        /// Complete and save the current intelligence session
        /// </summary>
        private async Task CompleteIntelligenceSession()
        {
            if (_currentIntelligenceSession == null || !_currentIntelligenceSessionId.HasValue)
            {
                Debug.WriteLine("Intelligence: No active session to complete");
                return;
            }

            var sessionEndTime = DateTime.Now;
            var sessionDuration = (int)((sessionEndTime - _currentSessionStartTime).TotalMilliseconds);

            // Update the start action item with duration
            if (_currentIntelligenceSession.ActionArray.Count > 0)
            {
                _currentIntelligenceSession.ActionArray[0].Duration = sessionDuration;
            }

            // Add stop action item
            var stopActionItem = new ActionItem
            {
                EventType = 0x8002, // Custom event type for intelligence STOP
                Coordinates = new Coordinates { X = 0, Y = 0 },
                KeyCode = 0,
                Timestamp = sessionEndTime.Ticks,
                Duration = sessionDuration,
                Pressure = 0.0f,
                IsTouch = false,
                MouseData = 0,
                Flags = 0
            };

            _currentIntelligenceSession.ActionArray.Add(stopActionItem);

            // Include all accumulated input events from recording buffer
            if (_intelligenceRecordingBuffer != null && _intelligenceRecordingBuffer.Count > 0)
            {
                _currentIntelligenceSession.ActionArray.AddRange(_intelligenceRecordingBuffer);
                // Debug.WriteLine($"Intelligence: Including {_intelligenceRecordingBuffer.Count} recorded input events in session");
                _intelligenceRecordingBuffer.Clear();
            }

            // Update session metadata
            var sessionModifier = _currentIntelligenceSession.ActionModifiers[0];
            var updatedContext = JsonConvert.DeserializeObject<Dictionary<string, object>>(sessionModifier.Description);
            updatedContext["EndTime"] = sessionEndTime;
            updatedContext["Duration"] = sessionDuration;
            updatedContext["CycleCount"] = _intelligenceCycleCount;
            updatedContext["ScreenshotCount"] = _capturedScreenshots.Count;
            updatedContext["AudioSampleCount"] = _capturedAudioData.Count;
            updatedContext["TextEventCount"] = _capturedTextData.Count;
            sessionModifier.Description = JsonConvert.SerializeObject(updatedContext);

            // Create DataItem for the completed session
            var sessionDataItem = new DataItem
            {
                Data = new DataObject { ActionGroupObject = _currentIntelligenceSession },
                createdAt = _currentSessionStartTime,
                updatedAt = sessionEndTime,
                _id = _currentIntelligenceSessionId.ToString(),
                deleted = false,
                Creator = "IntelligenceSystem",
                IsPublic = false
            };

            // Save the completed session using ObserveDataService
            var observeDataService = new ObserveDataService();
            await observeDataService.SaveDataItemsToFile(new List<DataItem> { sessionDataItem });

            // Update session history
            var toggleEvent = new IntelligenceToggleEvent
            {
                Timestamp = sessionEndTime,
                IsActive = false,
                Action = "STOP",
                CycleCount = _intelligenceCycleCount,
                Duration = sessionEndTime - _currentSessionStartTime,
                SessionData = new Dictionary<string, object>
                {
                    ["ActionGroupId"] = _currentIntelligenceSessionId.ToString(),
                    ["ActionName"] = _currentIntelligenceSession.ActionName,
                    ["SessionId"] = _sessionId,
                    ["PipelineName"] = _selectedPipeline ?? "Unknown",
                    ["Duration"] = sessionDuration
                }
            };

            _intelligenceToggleHistory.Add(toggleEvent);

            Debug.WriteLine($"Intelligence: Completed session '{_currentIntelligenceSession.ActionName}' with ID {_currentIntelligenceSessionId} - Duration: {sessionDuration}ms");

            // Clear current session
            _currentIntelligenceSession = null;
            _currentIntelligenceSessionId = null;
        }

        /// <summary>
        /// Attach selected media files to the current intelligence session
        /// </summary>
        private void AttachMediaFilesToSession()
        {
            if (_currentIntelligenceSession == null) return;

            // Attach selected image
            if (HasSelectedImage && !string.IsNullOrEmpty(SelectedImagePath))
            {
                try
                {
                    var imageFile = new ActionFile
                    {
                        Filename = SelectedImageName ?? Path.GetFileName(SelectedImagePath),
                        ContentType = "Image",
                        Data = SelectedImagePath, // Store file path
                        AddedAt = DateTime.Now,
                        IsProcessed = false
                    };

                    _currentIntelligenceSession.Files.Add(imageFile);
                    Debug.WriteLine($"Intelligence: Attached image file '{imageFile.Filename}' to session");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error attaching image file to intelligence session: {ex.Message}");
                }
            }

            // Attach selected audio
            if (HasSelectedAudio && !string.IsNullOrEmpty(SelectedAudioPath))
            {
                try
                {
                    var audioFile = new ActionFile
                    {
                        Filename = SelectedAudioName ?? Path.GetFileName(SelectedAudioPath),
                        ContentType = "Audio",
                        Data = SelectedAudioPath, // Store file path
                        AddedAt = DateTime.Now,
                        IsProcessed = false
                    };

                    _currentIntelligenceSession.Files.Add(audioFile);
                    Debug.WriteLine($"Intelligence: Attached audio file '{audioFile.Filename}' to session");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error attaching audio file to intelligence session: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Save intelligence session to file (following ObservePage SaveAllBufferedActions pattern)
        /// </summary>
        private async Task SaveIntelligenceSessionToFile()
        {
            try
            {
                var intelligenceDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CSimple", "Intelligence", "Sessions");

                Directory.CreateDirectory(intelligenceDir);

                // Create comprehensive session data
                var session = new IntelligenceSession
                {
                    SessionId = _sessionId,
                    CreatedAt = _sessionStartTime,
                    EndedAt = !IsIntelligenceActive ? DateTime.Now : null,
                    ToggleEvents = _intelligenceToggleHistory.ToList(),
                    ExecutionHistory = _pipelineHistory.ToList(),
                    TotalCycles = _intelligenceCycleCount,
                    TotalActiveTime = CalculateTotalActiveTime(),
                    SessionMetadata = new Dictionary<string, object>
                    {
                        ["AppVersion"] = "1.0.0", // Could be dynamic
                        ["LastPipeline"] = _selectedPipeline ?? "Unknown",
                        ["TotalScreenshots"] = _capturedScreenshots.Count,
                        ["TotalAudioSamples"] = _capturedAudioData.Count,
                        ["TotalTextEvents"] = _capturedTextData.Count,
                        ["SessionDurationMinutes"] = (DateTime.Now - _sessionStartTime).TotalMinutes
                    }
                };

                // Save session file with direct JSON serialization
                var sessionFileName = $"intelligence_session_{_sessionId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.json";
                var sessionFilePath = Path.Combine(intelligenceDir, sessionFileName);
                var sessionJson = JsonConvert.SerializeObject(session, Formatting.Indented);
                await File.WriteAllTextAsync(sessionFilePath, sessionJson);

                // Create DataItem structure similar to ObservePage for consistency
                var dataItem = new DataItem
                {
                    _id = session.SessionId,
                    createdAt = session.CreatedAt,
                    updatedAt = DateTime.Now,
                    Data = new DataObject
                    {
                        Text = $"Intelligence Session - {session.SessionId} ({session.TotalCycles} cycles, {session.TotalActiveTime.TotalMinutes:F1} min active)",
                        Files = new List<ActionFile>()
                    },
                    Creator = "Intelligence System",
                    IsPublic = false
                };

                // Convert session to ActionFile for structured storage (following ObservePage file pattern)
                var sessionDataFile = new ActionFile
                {
                    Filename = sessionFileName,
                    ContentType = "application/json",
                    Data = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(sessionJson))
                };
                dataItem.Data.Files.Add(sessionDataFile);

                // Save using DataService pattern if available (simplified version of ObservePage SaveLocalRichDataAsync)
                if (_dataService != null)
                {
                    try
                    {
                        // Simple file-based persistence similar to ObservePage pattern
                        var intelligenceDataFile = Path.Combine(intelligenceDir, "intelligence_data.json");
                        var existingData = new List<DataItem>();

                        if (File.Exists(intelligenceDataFile))
                        {
                            var existingJson = await File.ReadAllTextAsync(intelligenceDataFile);
                            if (!string.IsNullOrEmpty(existingJson))
                            {
                                existingData = JsonConvert.DeserializeObject<List<DataItem>>(existingJson) ?? new List<DataItem>();
                            }
                        }

                        existingData.Add(dataItem);
                        var updatedJson = JsonConvert.SerializeObject(existingData, Formatting.Indented);
                        await File.WriteAllTextAsync(intelligenceDataFile, updatedJson);
                    }
                    catch (Exception dataEx)
                    {
                        Debug.WriteLine($"DataService persistence warning: {dataEx.Message}");
                    }
                }

                Debug.WriteLine($"Intelligence: Session saved to {sessionFilePath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving intelligence session to file: {ex.Message}");
            }
        }

        /// <summary>
        /// Load previous intelligence sessions (for startup initialization)
        /// </summary>
        private async Task LoadIntelligenceSessionHistory()
        {
            try
            {
                var intelligenceDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CSimple", "Intelligence", "Sessions");

                if (!Directory.Exists(intelligenceDir))
                    return;

                // Load recent session files (last 10)
                var sessionFiles = Directory.GetFiles(intelligenceDir, "intelligence_session_*.json")
                    .OrderByDescending(f => File.GetCreationTime(f))
                    .Take(10);

                int loadedSessions = 0;
                foreach (var sessionFile in sessionFiles)
                {
                    try
                    {
                        var sessionJson = await File.ReadAllTextAsync(sessionFile);
                        var session = JsonConvert.DeserializeObject<IntelligenceSession>(sessionJson);

                        if (session != null)
                        {
                            // Log session history for context
                            Debug.WriteLine($"Intelligence: Loaded session {session.SessionId} from {session.CreatedAt:yyyy-MM-dd HH:mm:ss} " +
                                          $"({session.TotalCycles} cycles, {session.TotalActiveTime.TotalMinutes:F1} min active)");
                            loadedSessions++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error loading session file {sessionFile}: {ex.Message}");
                    }
                }

                Debug.WriteLine($"Intelligence: Loaded {loadedSessions} previous intelligence sessions for context");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading intelligence session history: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate total active time from toggle events
        /// </summary>
        private TimeSpan CalculateTotalActiveTime()
        {
            TimeSpan totalTime = TimeSpan.Zero;
            DateTime? startTime = null;

            foreach (var toggleEvent in _intelligenceToggleHistory.OrderBy(e => e.Timestamp))
            {
                if (toggleEvent.Action == "START")
                {
                    startTime = toggleEvent.Timestamp;
                }
                else if (toggleEvent.Action == "STOP" && startTime.HasValue)
                {
                    totalTime = totalTime.Add(toggleEvent.Timestamp - startTime.Value);
                    startTime = null;
                }
            }

            // Add current active session if still running
            if (IsIntelligenceActive && startTime.HasValue)
            {
                totalTime = totalTime.Add(DateTime.Now - startTime.Value);
            }

            return totalTime;
        }

        #region Model Training/Alignment Methods

        /// <summary>
        /// Show dialog to select a text-to-text model for training
        /// </summary>
        private async Task SelectTrainingModelAsync()
        {
            try
            {
                var textModels = AvailableTextModels.ToList();

                if (textModels.Count == 0)
                {
                    await ShowAlert("No Text Models", "No text-to-text models are available for training. Please download a text model first.", "OK");
                    return;
                }

                var modelNames = textModels.Select(m => m.Name ?? m.HuggingFaceModelId ?? "Unknown Model").ToArray();
                string selectedModelName = await ShowActionSheet(
                    "Select a Text Model for Training",
                    "Cancel",
                    null,
                    modelNames);

                if (selectedModelName != "Cancel" && !string.IsNullOrEmpty(selectedModelName))
                {
                    SelectedTrainingModel = textModels.FirstOrDefault(m =>
                        (m.Name ?? m.HuggingFaceModelId ?? "Unknown Model") == selectedModelName);

                    if (SelectedTrainingModel != null)
                    {
                        TrainingStatus = $"Selected model: {SelectedTrainingModel.Name}";
                    }
                }
            }
            catch (Exception ex)
            {
                HandleError("Error selecting training model", ex);
            }
        }

        /// <summary>
        /// Select training dataset file
        /// </summary>
        private async Task SelectTrainingDatasetAsync()
        {
            try
            {
                var fileResult = await PickFile();
                if (fileResult != null)
                {
                    // Copy to resources folder
                    var datasetsPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\Datasets");
                    Directory.CreateDirectory(datasetsPath);

                    var targetPath = Path.Combine(datasetsPath, $"training_{fileResult.FileName}");

                    using (var sourceStream = await fileResult.OpenReadAsync())
                    using (var targetStream = File.Create(targetPath))
                    {
                        await sourceStream.CopyToAsync(targetStream);
                    }

                    TrainingDatasetPath = targetPath;
                    TrainingStatus = $"Training dataset: {fileResult.FileName}";
                }
            }
            catch (Exception ex)
            {
                HandleError("Error selecting training dataset", ex);
            }
        }

        /// <summary>
        /// Select validation dataset file (optional)
        /// </summary>
        private async Task SelectValidationDatasetAsync()
        {
            try
            {
                var fileResult = await PickFile();
                if (fileResult != null)
                {
                    var datasetsPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\Datasets");
                    Directory.CreateDirectory(datasetsPath);

                    var targetPath = Path.Combine(datasetsPath, $"validation_{fileResult.FileName}");

                    using (var sourceStream = await fileResult.OpenReadAsync())
                    using (var targetStream = File.Create(targetPath))
                    {
                        await sourceStream.CopyToAsync(targetStream);
                    }

                    ValidationDatasetPath = targetPath;
                    TrainingStatus = $"Validation dataset: {fileResult.FileName}";
                }
            }
            catch (Exception ex)
            {
                HandleError("Error selecting validation dataset", ex);
            }
        }

        /// <summary>
        /// Select test dataset file (optional)
        /// </summary>
        private async Task SelectTestDatasetAsync()
        {
            try
            {
                var fileResult = await PickFile();
                if (fileResult != null)
                {
                    var datasetsPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\Datasets");
                    Directory.CreateDirectory(datasetsPath);

                    var targetPath = Path.Combine(datasetsPath, $"test_{fileResult.FileName}");

                    using (var sourceStream = await fileResult.OpenReadAsync())
                    using (var targetStream = File.Create(targetPath))
                    {
                        await sourceStream.CopyToAsync(targetStream);
                    }

                    TestDatasetPath = targetPath;
                    TrainingStatus = $"Test dataset: {fileResult.FileName}";
                }
            }
            catch (Exception ex)
            {
                HandleError("Error selecting test dataset", ex);
            }
        }

        /// <summary>
        /// Start the model training/alignment process
        /// </summary>
        private async Task StartTrainingAsync()
        {
            try
            {
                if (!CanStartTraining)
                {
                    await ShowAlert("Cannot Start Training", "Please select a model, training dataset, and provide a name for the new model.", "OK");
                    return;
                }

                IsTraining = true;
                TrainingProgress = 0.0;
                TrainingStatus = "Initializing training...";
                _trainingStartTime = DateTime.Now;

                // Create aligned models directory
                var alignedModelsPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\AlignedModels", NewModelName);
                Directory.CreateDirectory(alignedModelsPath);

                // Simulate training process with progress updates
                await SimulateTrainingProcessAsync(alignedModelsPath);
            }
            catch (Exception ex)
            {
                HandleError("Error starting training", ex);
                IsTraining = false;
                TrainingStatus = $"Training failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Simulate the training process with realistic progress updates
        /// </summary>
        private async Task SimulateTrainingProcessAsync(string outputPath)
        {
            try
            {
                var totalSteps = Epochs * 100; // Simulate 100 steps per epoch
                var stepDuration = TimeSpan.FromMilliseconds(200); // 200ms per step for demo

                for (int epoch = 0; epoch < Epochs; epoch++)
                {
                    TrainingStatus = $"Training epoch {epoch + 1}/{Epochs}";

                    for (int step = 0; step < 100; step++)
                    {
                        if (!IsTraining) return; // Check if stopped

                        var currentStep = epoch * 100 + step;
                        TrainingProgress = (double)currentStep / totalSteps;

                        // Calculate elapsed and ETA
                        var elapsed = DateTime.Now - _trainingStartTime;
                        TrainingElapsed = $"{elapsed.Hours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";

                        if (TrainingProgress > 0)
                        {
                            var totalEstimated = TimeSpan.FromTicks((long)(elapsed.Ticks / TrainingProgress));
                            var eta = totalEstimated - elapsed;
                            TrainingEta = $"{eta.Hours:D2}:{eta.Minutes:D2}:{eta.Seconds:D2}";
                        }

                        await Task.Delay(stepDuration);
                    }
                }

                if (IsTraining)
                {
                    await FinalizeTrainingAsync(outputPath);
                }
            }
            catch (Exception ex)
            {
                HandleError("Error during training simulation", ex);
                IsTraining = false;
            }
        }

        /// <summary>
        /// Finalize the training process and create the new model
        /// </summary>
        private async Task FinalizeTrainingAsync(string outputPath)
        {
            try
            {
                TrainingStatus = "Finalizing model...";
                TrainingProgress = 1.0;

                // Save model metadata
                var modelInfo = new
                {
                    OriginalModelId = SelectedTrainingModel.HuggingFaceModelId,
                    OriginalModelName = SelectedTrainingModel.Name,
                    NewModelName = NewModelName,
                    TrainingDataset = Path.GetFileName(TrainingDatasetPath),
                    ValidationDataset = !string.IsNullOrEmpty(ValidationDatasetPath) ? Path.GetFileName(ValidationDatasetPath) : null,
                    TestDataset = !string.IsNullOrEmpty(TestDatasetPath) ? Path.GetFileName(TestDatasetPath) : null,
                    FineTuningMethod = SelectedFineTuningMethod,
                    Hyperparameters = new
                    {
                        LearningRate = LearningRate,
                        Epochs = Epochs,
                        BatchSize = BatchSize
                    },
                    TrainingCompleted = DateTime.Now,
                    TrainingDuration = DateTime.Now - _trainingStartTime
                };

                var modelInfoPath = Path.Combine(outputPath, "model_info.json");
                await File.WriteAllTextAsync(modelInfoPath, JsonConvert.SerializeObject(modelInfo, Formatting.Indented));

                // Create a placeholder model file structure
                await File.WriteAllTextAsync(Path.Combine(outputPath, "config.json"), "{}");
                await File.WriteAllTextAsync(Path.Combine(outputPath, "pytorch_model.bin"), "placeholder");

                // Add the new model to the available models collection
                var newModel = new NeuralNetworkModel
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = NewModelName,
                    Description = $"Fine-tuned from {SelectedTrainingModel.Name} using {SelectedFineTuningMethod}",
                    Type = ModelType.General,
                    InputType = SelectedTrainingModel.InputType,
                    IsHuggingFaceReference = false,
                    IsDownloaded = true,
                    IsActive = false,
                    DownloadButtonText = "Remove from Device"
                };

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AvailableModels.Add(newModel);
                });

                // Save the updated models
                await SavePersistedModelsAsync();

                TrainingStatus = $"Training completed! Model '{NewModelName}' is now available.";
                IsTraining = false;

                await ShowAlert("Training Complete", $"Model '{NewModelName}' has been successfully trained and is now available in your model collection.", "OK");

                // Reset training state
                ResetTrainingState();
            }
            catch (Exception ex)
            {
                HandleError("Error finalizing training", ex);
                IsTraining = false;
                TrainingStatus = $"Training finalization failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Stop the training process
        /// </summary>
        private async Task StopTrainingAsync()
        {
            try
            {
                IsTraining = false;
                TrainingStatus = "Training stopped by user";
                await Task.Delay(100); // Allow UI to update
            }
            catch (Exception ex)
            {
                HandleError("Error stopping training", ex);
            }
        }

        /// <summary>
        /// Toggle between starting and stopping training
        /// </summary>
        private async Task ToggleTrainingAsync()
        {
            try
            {
                if (IsTraining)
                {
                    await StopTrainingAsync();
                }
                else
                {
                    await StartTrainingAsync();
                }
            }
            catch (Exception ex)
            {
                HandleError("Error toggling training", ex);
            }
        }

        /// <summary>
        /// Create a new dataset from user input
        /// </summary>
        private async Task CreateDatasetAsync()
        {
            try
            {
                var input = await ShowPrompt("Create Dataset",
                    "Enter training data (one example per line, format: input|output):",
                    "Create", "Cancel", "");

                if (string.IsNullOrWhiteSpace(input))
                    return;

                var datasetsPath = Path.Combine(@"C:\Users\tanne\Documents\CSimple\Resources\Datasets");
                Directory.CreateDirectory(datasetsPath);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var datasetPath = Path.Combine(datasetsPath, $"custom_dataset_{timestamp}.jsonl");

                // Convert input to JSONL format
                var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var jsonlLines = new List<string>();

                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        var example = new
                        {
                            input = parts[0].Trim(),
                            output = parts[1].Trim()
                        };
                        jsonlLines.Add(JsonConvert.SerializeObject(example));
                    }
                }

                await File.WriteAllLinesAsync(datasetPath, jsonlLines);

                TrainingDatasetPath = datasetPath;
                TrainingStatus = $"Created dataset with {jsonlLines.Count} examples";

                await ShowAlert("Dataset Created", $"Dataset saved with {jsonlLines.Count} examples.", "OK");
            }
            catch (Exception ex)
            {
                HandleError("Error creating dataset", ex);
            }
        }

        /// <summary>
        /// Reset training state to initial values
        /// </summary>
        private void ResetTrainingState()
        {
            SelectedTrainingModel = null;
            TrainingDatasetPath = string.Empty;
            ValidationDatasetPath = string.Empty;
            TestDatasetPath = string.Empty;
            NewModelName = string.Empty;
            TrainingProgress = 0.0;
            TrainingStatus = "Ready";
            TrainingEta = string.Empty;
            TrainingElapsed = string.Empty;
        }

        #endregion

        #endregion
    }

    // Supporting Data Structures for Enhanced Intelligence
    public class ApplicationContext
    {
        public List<string> ActiveApplications { get; set; } = new();
        public string FocusedWindow { get; set; } = "";
        public double CpuUsage { get; set; }
        public double MemoryUsage { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class UserBehaviorAnalysis
    {
        public string ActivityLevel { get; set; } = "Unknown";
        public string PatternAnalysis { get; set; } = "";
        public List<string> RecentActions { get; set; } = new();
        public TimeSpan ActiveDuration { get; set; }
        public int InteractionCount { get; set; }
    }

    public class InputContext
    {
        public List<string> RecentEvents { get; set; } = new();
        public System.Drawing.Point MousePosition { get; set; }
        public List<string> ActiveKeys { get; set; } = new();
        public DateTime LastInputTime { get; set; }
    }

    public class MemoryContext
    {
        public List<MemoryEntry> RecentEntries { get; set; } = new();
        public string TrendAnalysis { get; set; } = "";
        public Dictionary<string, object> SessionData { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }

    public class MemoryEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Type { get; set; } = "";
        public string Content { get; set; } = "";
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    public class PipelineExecutionRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string PipelineName { get; set; } = "";
        public double ExecutionTime { get; set; }
        public bool Success { get; set; }
        public string Result { get; set; } = "";
        public Dictionary<string, object> Context { get; set; } = new();
    }

    // Intelligence Session Persistence Classes (similar to ObservePage pattern)
    public class IntelligenceToggleEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsActive { get; set; }
        public string Action { get; set; } = ""; // "START" or "STOP"
        public TimeSpan Duration { get; set; } // For STOP events, duration of the session
        public int CycleCount { get; set; } // Number of cycles completed during the session
        public Dictionary<string, object> SessionData { get; set; } = new();
    }

    public class IntelligenceSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? EndedAt { get; set; }
        public List<IntelligenceToggleEvent> ToggleEvents { get; set; } = new();
        public List<PipelineExecutionRecord> ExecutionHistory { get; set; } = new();
        public int TotalCycles { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public Dictionary<string, object> SessionMetadata { get; set; } = new();
    }
}
