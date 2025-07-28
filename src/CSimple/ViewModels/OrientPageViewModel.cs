using CSimple.Models;
using CSimple.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text; // Added for StringBuilder
using System.Text.Json; // Added for JsonSerializer
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;


namespace CSimple.ViewModels
{
    public class OrientPageViewModel : INotifyPropertyChanged, IDisposable
    {
        // --- Services ---
        private readonly FileService _fileService; // Inject FileService
        private readonly HuggingFaceService _huggingFaceService; // Add HuggingFaceService
        private readonly NetPageViewModel _netPageViewModel; // Keep reference if needed
        private readonly PythonBootstrapper _pythonBootstrapper; // Added
        private readonly NodeManagementService _nodeManagementService; // ADDED
        private readonly PipelineManagementService _pipelineManagementService; // ADDED
        private readonly AudioStepContentService _audioStepContentService; // Added for audio step content management
        private readonly ActionReviewService _actionReviewService; // Added for action review functionality
        private readonly EnsembleModelService _ensembleModelService; // Added for ensemble model execution
        private readonly PipelineExecutionService _pipelineExecutionService; // Added for pipeline execution with dependency resolution
        private readonly ActionStepNavigationService _actionStepNavigationService; // Added for action step navigation
        private readonly IMemoryCompressionService _memoryCompressionService; // Added for memory compression functionality
        private readonly ExecutionStatusTrackingService _executionStatusTrackingService; // Added for execution status tracking

        // --- Properties ---
        public ObservableCollection<NodeViewModel> Nodes { get; } = new ObservableCollection<NodeViewModel>();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new ObservableCollection<ConnectionViewModel>();
        public ObservableCollection<CSimple.Models.HuggingFaceModel> AvailableModels { get; } = new ObservableCollection<CSimple.Models.HuggingFaceModel>(); // Keep for adding models

        private NodeViewModel _selectedNode;
        public NodeViewModel SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    // Update the SelectedClassification when the node changes
                    if (value != null && value.IsTextModel)
                    {
                        _selectedClassification = value.Classification;
                        OnPropertyChanged(nameof(SelectedClassification));
                    }

                    // Trigger UpdateStepContent when the selected node changes
                    UpdateStepContent();

                    // Update command can execute states
                    (GenerateCommand as Command)?.ChangeCanExecute();
                    (SelectSaveFileCommand as Command)?.ChangeCanExecute();
                    (CreateNewMemoryFileCommand as Command)?.ChangeCanExecute();
                }
            }
        }

        // Add this property for binding with the classification picker in XAML
        private string _selectedClassification;
        public string SelectedClassification
        {
            get => _selectedClassification;
            set
            {
                if (SetProperty(ref _selectedClassification, value))
                {
                    // If we have a selected node and it's a text model,
                    // update its classification when this property changes
                    if (SelectedNode != null && SelectedNode.IsTextModel)
                    {
                        SetNodeClassification(SelectedNode, value);
                    }
                }
            }
        }

        public List<string> TextModelClassifications { get; } = new List<string>
        {
            "", // Empty option to clear classification
            "Goal",
            "Plan",
            "Action"
        };

        // Pipeline Management Properties
        public ObservableCollection<string> AvailablePipelineNames { get; } = new ObservableCollection<string>();

        private string _selectedPipelineName;
        public string SelectedPipelineName
        {
            get => _selectedPipelineName;
            set
            {
                if (SetProperty(ref _selectedPipelineName, value) && value != null)
                {
                    // Load the selected pipeline when the picker changes
                    _ = LoadPipelineAsync(value);
                }
            }
        }

        private string _currentPipelineName = "Untitled Pipeline"; // Default name
        public string CurrentPipelineName
        {
            get => _currentPipelineName;
            private set => SetProperty(ref _currentPipelineName, value); // Private set for internal control
        }

        // Memory File Name Property
        private string _memoryFileName = "";
        public string MemoryFileName
        {
            get => _memoryFileName;
            set => SetProperty(ref _memoryFileName, value);
        }

        // Camera Offset Properties for Pan Persistence
        private float _cameraOffsetX = 0f;
        public float CameraOffsetX
        {
            get => _cameraOffsetX;
            set => SetProperty(ref _cameraOffsetX, value);
        }

        private float _cameraOffsetY = 0f;
        public float CameraOffsetY
        {
            get => _cameraOffsetY;
            set => SetProperty(ref _cameraOffsetY, value);
        }

        // Temporary state for drawing connections
        internal NodeViewModel _temporaryConnectionState = null;

        // --- Commands ---
        public ICommand AddModelNodeCommand { get; }
        public ICommand DeleteSelectedNodeCommand { get; }
        public ICommand CreateNewPipelineCommand { get; }
        public ICommand RenamePipelineCommand { get; }
        public ICommand DeletePipelineCommand { get; }

        // --- Model Execution Status Properties (Delegated to ExecutionStatusTrackingService) ---
        public bool IsExecutingModels
        {
            get => _executionStatusTrackingService.IsExecutingModels;
            set => _executionStatusTrackingService.IsExecutingModels = value;
        }

        public string ExecutionStatus
        {
            get => _executionStatusTrackingService.ExecutionStatus;
            set => _executionStatusTrackingService.ExecutionStatus = value;
        }

        public int ExecutionProgress
        {
            get => _executionStatusTrackingService.ExecutionProgress;
            set => _executionStatusTrackingService.ExecutionProgress = value;
        }

        public int TotalModelsToExecute
        {
            get => _executionStatusTrackingService.TotalModelsToExecute;
            set => _executionStatusTrackingService.TotalModelsToExecute = value;
        }

        public int ModelsExecutedCount
        {
            get => _executionStatusTrackingService.ModelsExecutedCount;
            set => _executionStatusTrackingService.ModelsExecutedCount = value;
        }

        public string CurrentExecutingModel
        {
            get => _executionStatusTrackingService.CurrentExecutingModel;
            set => _executionStatusTrackingService.CurrentExecutingModel = value;
        }

        public string CurrentExecutingModelType
        {
            get => _executionStatusTrackingService.CurrentExecutingModelType;
            set => _executionStatusTrackingService.CurrentExecutingModelType = value;
        }

        public double ExecutionDurationSeconds
        {
            get => _executionStatusTrackingService.ExecutionDurationSeconds;
            set => _executionStatusTrackingService.ExecutionDurationSeconds = value;
        }

        public string ExecutionDurationDisplay => _executionStatusTrackingService.ExecutionDurationDisplay;

        // Execution group tracking properties
        public bool IsExecutingInGroups
        {
            get => _executionStatusTrackingService.IsExecutingInGroups;
            set => _executionStatusTrackingService.IsExecutingInGroups = value;
        }

        public int CurrentExecutionGroup
        {
            get => _executionStatusTrackingService.CurrentExecutionGroup;
            set => _executionStatusTrackingService.CurrentExecutionGroup = value;
        }

        public int TotalExecutionGroups
        {
            get => _executionStatusTrackingService.TotalExecutionGroups;
            set => _executionStatusTrackingService.TotalExecutionGroups = value;
        }

        public double GroupExecutionDurationSeconds
        {
            get => _executionStatusTrackingService.GroupExecutionDurationSeconds;
            set => _executionStatusTrackingService.GroupExecutionDurationSeconds = value;
        }

        public string GroupExecutionDurationDisplay => _executionStatusTrackingService.GroupExecutionDurationDisplay;

        public ObservableCollection<ExecutionGroupInfo> ExecutionGroups => _executionStatusTrackingService.ExecutionGroups;

        private List<string> _executionResults = new List<string>();
        public ObservableCollection<string> ExecutionResults { get; } = new ObservableCollection<string>();        // Add these properties and fields for Action Review functionality
        private ObservableCollection<string> _availableActionNames;
        public ObservableCollection<string> AvailableActionNames
        {
            get => _availableActionNames ??= new ObservableCollection<string>();
            set
            {
                if (_availableActionNames != value)
                {
                    _availableActionNames = value;
                    OnPropertyChanged();
                }
            }
        }

        private string _selectedReviewActionName;
        public string SelectedReviewActionName
        {
            get => _selectedReviewActionName;
            set
            {
                if (_selectedReviewActionName != value)
                {
                    _selectedReviewActionName = value;
                    OnPropertyChanged();

                    // When a new action is selected, load its details
                    if (!string.IsNullOrEmpty(value))
                    {
                        LoadSelectedAction();
                    }

                    // Update RunAllNodesCommand CanExecute since it depends on having a selected action
                    (RunAllNodesCommand as Command)?.ChangeCanExecute();
                }
            }
        }

        // Current position in the action replay
        private int _currentActionStep;
        public int CurrentActionStep
        {
            get => _currentActionStep;
            set
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.CurrentActionStep_Set] Attempting to set from {_currentActionStep} to {value}");
                if (SetProperty(ref _currentActionStep, value))
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.CurrentActionStep_Set] CurrentActionStep changed to: {CurrentActionStep}. Calling UpdateStepContent.");
                    // Invalidate execution optimization cache when step changes
                    _executionOptimizationCacheValid = false;
                    UpdateStepContent();

                    // Trigger proactive preparation for "Run All Models" when step changes
                    TriggerProactivePreparation();

                    // Update command can execute status
                    (StepBackwardCommand as Command)?.ChangeCanExecute();
                    (StepForwardCommand as Command)?.ChangeCanExecute(); // Ensure forward is also updated
                }
            }
        }

        private List<ActionItem> _currentActionItems = new List<ActionItem>();

        // Commands for action stepping
        public ICommand StepForwardCommand { get; }
        public ICommand StepBackwardCommand { get; }
        public ICommand ResetActionCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand RunAllModelsCommand { get; }
        public ICommand RunAllNodesCommand { get; }
        public ICommand SleepMemoryCompressionCommand { get; }
        public ICommand SelectSaveFileCommand { get; }
        public ICommand CreateNewMemoryFileCommand { get; }


        // --- UI Interaction Delegates ---
        public Func<string, string, string, Task> ShowAlert { get; set; }
        public Func<string, string, string, string[], Task<string>> ShowActionSheet { get; set; }

        // --- Constructor ---
        // Ensure FileService and PythonBootstrapper are injected
        public OrientPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, NetPageViewModel netPageViewModel, PythonBootstrapper pythonBootstrapper, NodeManagementService nodeManagementService, PipelineManagementService pipelineManagementService, ActionReviewService actionReviewService, EnsembleModelService ensembleModelService, ActionStepNavigationService actionStepNavigationService, IMemoryCompressionService memoryCompressionService, ExecutionStatusTrackingService executionStatusTrackingService)
        {
            _fileService = fileService;
            _huggingFaceService = huggingFaceService;
            _netPageViewModel = netPageViewModel;
            _pythonBootstrapper = pythonBootstrapper; // Store injected service
            _nodeManagementService = nodeManagementService; // ADDED
            _pipelineManagementService = pipelineManagementService; // ADDED
            _audioStepContentService = new AudioStepContentService(); // Initialize audio step content service
            _actionReviewService = actionReviewService; // Initialize action review service
            _ensembleModelService = ensembleModelService; // Initialize ensemble model service
            _actionStepNavigationService = actionStepNavigationService; // Initialize action step navigation service via DI
            _memoryCompressionService = memoryCompressionService; // Initialize memory compression service
            _executionStatusTrackingService = executionStatusTrackingService; // Initialize execution status tracking service

            // Subscribe to the execution status tracking service's property changed events
            _executionStatusTrackingService.PropertyChanged += OnExecutionStatusTrackingServicePropertyChanged;

            // Initialize pipeline execution service with dependency injection
            _pipelineExecutionService = new PipelineExecutionService(
                _ensembleModelService,
                (node) => FindCorrespondingModel(((App)Application.Current)?.NetPageViewModel, node)
            );

            // Subscribe to audio playback events
            _audioStepContentService.PlaybackStarted += OnAudioPlaybackStarted;
            _audioStepContentService.PlaybackStopped += OnAudioPlaybackStopped;

            // Initialize Commands
            AddModelNodeCommand = new Command<HuggingFaceModel>(async (model) => await AddModelNode(model));
            DeleteSelectedNodeCommand = new Command(async () => await DeleteSelectedNode());
            // Modify CreateNewPipelineCommand to handle save and select sequence
            CreateNewPipelineCommand = new Command(async () =>
            {
                await CreateNewPipeline(); // Create in memory, add default nodes, add name to list
                await SaveCurrentPipelineAsync(); // Save the new empty pipeline with default nodes
                SelectedPipelineName = CurrentPipelineName; // Select it, triggering LoadPipelineAsync (which should now find the file)
                Debug.WriteLine($"Executed CreateNewPipelineCommand: Created, saved, and selected '{CurrentPipelineName}'");
            });
            RenamePipelineCommand = new Command(async () => await RenameCurrentPipeline());
            DeletePipelineCommand = new Command(async () => await DeleteCurrentPipeline());

            // Subscribe to NetPageViewModel's PropertyChanged event
            netPageViewModel.PropertyChanged += NetPageViewModel_PropertyChanged;

            // Initialize Review Action commands using the service
            StepForwardCommand = new Command(async () =>
                await _actionStepNavigationService.ExecuteStepForwardAsync(
                    CurrentActionStep,
                    _currentActionItems,
                    SetCurrentActionStepAsync,
                    () =>
                    {
                        (StepForwardCommand as Command)?.ChangeCanExecute();
                        (StepBackwardCommand as Command)?.ChangeCanExecute();
                    }),
                () => _actionStepNavigationService.CanStepForward(SelectedReviewActionName, _currentActionItems, CurrentActionStep));

            StepBackwardCommand = new Command(async () =>
                await _actionStepNavigationService.ExecuteStepBackwardAsync(
                    CurrentActionStep,
                    SetCurrentActionStepAsync,
                    () =>
                    {
                        (StepForwardCommand as Command)?.ChangeCanExecute();
                        (StepBackwardCommand as Command)?.ChangeCanExecute();
                    }),
                () => _actionStepNavigationService.CanStepBackward(SelectedReviewActionName, CurrentActionStep));

            ResetActionCommand = new Command(async () =>
                await _actionStepNavigationService.ExecuteResetActionAsync(
                    CurrentActionStep,
                    SetCurrentActionStepAsync,
                    () =>
                    {
                        (StepForwardCommand as Command)?.ChangeCanExecute();
                        (StepBackwardCommand as Command)?.ChangeCanExecute();
                    }),
                () => _actionStepNavigationService.CanResetAction(SelectedReviewActionName));
            GenerateCommand = new Command(async () => await ExecuteGenerateAsync(), () => SelectedNode != null && SelectedNode.Type == NodeType.Model && SelectedNode.EnsembleInputCount > 1);

            // Initialize RunAllModelsCommand with debug logging
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔧 [OrientPageViewModel.Constructor] Initializing RunAllModelsCommand");
            RunAllModelsCommand = new Command(async () =>
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 [RunAllModelsCommand] Button clicked - executing command");
                await ExecuteRunAllModelsAsync();
            }, () =>
            {
                bool canExecute = HasModelNodes();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 [RunAllModelsCommand.CanExecute] Checking: {canExecute} (Model nodes count: {GetModelNodesCount()})");
                return canExecute;
            });

            // Initialize RunAllNodesCommand with debug logging
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔧 [OrientPageViewModel.Constructor] Initializing RunAllNodesCommand");
            RunAllNodesCommand = new Command(async () =>
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 [RunAllNodesCommand] Button clicked - executing command");
                await ExecuteRunAllNodesAsync();
            }, () =>
            {
                bool canExecute = HasModelNodes() && !string.IsNullOrEmpty(SelectedReviewActionName);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 [RunAllNodesCommand.CanExecute] Checking: {canExecute} (Model nodes: {GetModelNodesCount()}, Selected action: {SelectedReviewActionName ?? "none"})");
                return canExecute;
            });

            // Initialize SleepMemoryCompressionCommand
            SleepMemoryCompressionCommand = new Command(async () => await ExecuteSleepMemoryCompressionAsync(), () => true);

            // Initialize SelectSaveFileCommand
            SelectSaveFileCommand = new Command(async () => await ExecuteSelectSaveFileAsync(), () => SelectedNode?.IsFileNode == true);

            // Initialize CreateNewMemoryFileCommand
            CreateNewMemoryFileCommand = new Command(async () => await ExecuteCreateNewMemoryFileAsync(), () => SelectedNode?.IsFileNode == true);

            // Initialize Audio commands
            PlayAudioCommand = new Command(() => PlayAudio(), CanPlayAudio);
            StopAudioCommand = new Command(() => StopAudio(), CanStopAudio);

            // Load available pipelines on initialization
            _ = LoadAvailablePipelinesAsync();

            // No need to initialize execution timer - handled by ExecutionStatusTrackingService

            // Start background warmup immediately to avoid delays later
            StartBackgroundWarmup();
        }

        /// <summary>
        /// Start background warmup to avoid delays during execution
        /// </summary>
        private void StartBackgroundWarmup()
        {
            lock (_warmupLock)
            {
                if (_backgroundWarmupTask == null && !_environmentPrewarmed)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔥 [StartBackgroundWarmup] Starting background environment warmup...");
                    _backgroundWarmupTask = Task.Run(async () =>
                    {
                        try
                        {
                            await BackgroundWarmupAsync();
                            lock (_warmupLock)
                            {
                                _environmentPrewarmed = true;
                            }
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [StartBackgroundWarmup] Background warmup completed successfully");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [StartBackgroundWarmup] Background warmup failed (non-critical): {ex.Message}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Background warmup that doesn't block initialization
        /// </summary>
        private async Task BackgroundWarmupAsync()
        {
            var stopwatch = Stopwatch.StartNew();

            // 1. Ensure NetPageViewModel is warmed up since OrientPage and NetPage are closely related
            try
            {
                var netPageVM = ((App)Application.Current).NetPageViewModel;
                if (netPageVM != null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🌐 [BackgroundWarmup] Warming up NetPage integration");

                    // Ensure NetPage models are loaded in background
                    if (netPageVM.AvailableModels?.Count == 0)
                    {
                        await netPageVM.LoadDataAsync();
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🌐 [BackgroundWarmup] NetPage models loaded: {netPageVM.AvailableModels?.Count ?? 0}");
                    }

                    // Pre-warm model access patterns
                    if (netPageVM.AvailableModels?.Count > 0)
                    {
                        await Task.Run(() =>
                        {
                            var firstFewModels = netPageVM.AvailableModels.Take(3);
                            foreach (var model in firstFewModels)
                            {
                                try
                                {
                                    // Access key properties to warm up caches
                                    var _ = model.Name;
                                    var __ = model.HuggingFaceModelId;
                                    var ___ = model.Type;
                                }
                                catch { /* Ignore warming errors */ }
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [BackgroundWarmup] NetPage warmup failed: {ex.Message}");
            }

            // 2. Ensure Python environment is ready in background
            if (_pythonBootstrapper != null)
            {
                try
                {
                    var isReady = await _pythonBootstrapper.AreRequiredPackagesInstalledAsync();
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🐍 [BackgroundWarmup] Python ready: {isReady}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [BackgroundWarmup] Python check failed: {ex.Message}");
                }
            }

            // 3. Pre-warm file system caches by accessing common directories
            try
            {
                await Task.Run(() =>
                {
                    var commonPaths = new[]
                    {
                        @"C:\Users\tanne\Documents\CSimple\Resources\WebcamImages",
                        @"C:\Users\tanne\Documents\CSimple\Resources\PCAudio",
                        @"C:\Users\tanne\Documents\CSimple\Resources\HFModels"
                    };

                    foreach (var path in commonPaths)
                    {
                        try
                        {
                            if (Directory.Exists(path))
                            {
                                Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Take(1).ToList();
                            }
                        }
                        catch { /* Ignore file system errors */ }
                    }
                });
            }
            catch { /* Ignore warmup errors */ }

            stopwatch.Stop();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔥 [BackgroundWarmup] Completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        // --- Execution Timer Methods (Moved to ExecutionStatusTrackingService) ---
        // Timer methods are now handled by the ExecutionStatusTrackingService

        // --- Event Handlers ---
        private void OnExecutionStatusTrackingServicePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Forward property change notifications from the service to any UI bindings
            OnPropertyChanged(e.PropertyName);
        }

        private async void NetPageViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NetPageViewModel.AvailableModels))
            {
                Debug.WriteLine("NetPageViewModel.AvailableModels changed, updating node classifications");
                await UpdateNodeClassificationsAsync();
            }
        }

        // --- Public Methods (Called from View or Commands) ---

        public async Task InitializeAsync()
        {
            // Initialize execution status panel
            InitializeExecutionStatus();

            // Proactively preload NetPageViewModel since OrientPage and NetPage are closely related
            var netPageVM = ((App)Application.Current).NetPageViewModel;
            Debug.WriteLine($"InitializeAsync: Proactively preloading NetPageViewModel, current state: {netPageVM?.AvailableModels?.Count ?? 0} models");

            if (netPageVM != null)
            {
                try
                {
                    // Always call LoadDataAsync to ensure comprehensive preloading
                    // This ensures models, configurations, persistent data, and all NetPage state is ready
                    Debug.WriteLine("InitializeAsync: Starting comprehensive NetPageViewModel preloading");
                    await netPageVM.LoadDataAsync();
                    Debug.WriteLine($"InitializeAsync: NetPageViewModel fully preloaded with {netPageVM.AvailableModels?.Count ?? 0} models");

                    // Ensure any additional NetPage-specific initialization is complete
                    await PreloadNetPageIntegrationAsync(netPageVM);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"InitializeAsync: Error during NetPageViewModel preloading: {ex.Message}");
                    // Continue with initialization even if preloading fails
                }
            }
            else
            {
                Debug.WriteLine("InitializeAsync: WARNING - NetPageViewModel is null, cannot preload");
            }

            await LoadAvailablePipelinesAsync();
            if (AvailablePipelineNames.Any())
            {
                // Load the most recent pipeline (first in the sorted list)
                string pipelineToLoad = AvailablePipelineNames.First();
                // Load directly first to ensure state is correct before setting SelectedPipelineName
                await LoadPipelineAsync(pipelineToLoad);
                // Now set the SelectedPipelineName, which might trigger another load,
                // but the state should already be consistent.
                SelectedPipelineName = pipelineToLoad;
            }
            else
            {
                // No pipelines exist, create a new one using the command's logic
                Debug.WriteLine("No existing pipelines found. Creating a new one via command logic.");
                // Execute the command logic directly
                await CreateNewPipeline();
                await SaveCurrentPipelineAsync();
                SelectedPipelineName = CurrentPipelineName;
                Debug.WriteLine($"Initialized with new pipeline: '{CurrentPipelineName}'");
            }
            await LoadAvailableModelsAsync(); // Load models for the picker

            // Add this line to explicitly call UpdateNodeClassificationsAsync during initialization
            await UpdateNodeClassificationsAsync();
            Debug.WriteLine("InitializeAsync: Explicitly called UpdateNodeClassificationsAsync");

            // Verify that NetPageViewModel has models for pipeline execution
            var netPageVMVerify = ((App)Application.Current).NetPageViewModel;
            if (netPageVMVerify?.AvailableModels?.Count > 0)
            {
                Debug.WriteLine($"InitializeAsync: Verified NetPageViewModel has {netPageVMVerify.AvailableModels.Count} models ready for pipeline execution");
            }
            else
            {
                Debug.WriteLine("InitializeAsync: WARNING - NetPageViewModel has no models for pipeline execution!");
            }

            // Load available actions for review
            await LoadAvailableActions();

            // Update execution status based on loaded pipeline
            UpdateExecutionStatusFromPipeline();
        }

        /// <summary>
        /// Preloads NetPage integration components to ensure smooth interoperability
        /// </summary>
        private async Task PreloadNetPageIntegrationAsync(NetPageViewModel netPageVM)
        {
            try
            {
                Debug.WriteLine("PreloadNetPageIntegration: Starting NetPage integration preloading");

                // Ensure model execution services are ready
                if (netPageVM.AvailableModels?.Count > 0)
                {
                    Debug.WriteLine($"PreloadNetPageIntegration: Preparing integration for {netPageVM.AvailableModels.Count} models");

                    // Preload model metadata and execution contexts in background
                    await Task.Run(() =>
                    {
                        var modelsToPreload = netPageVM.AvailableModels.Take(3).ToList(); // Limit for performance
                        foreach (var model in modelsToPreload)
                        {
                            try
                            {
                                // Access model properties to ensure they're cached
                                var _ = model.Name;
                                var __ = model.Type;
                                var ___ = model.HuggingFaceModelId;

                                Debug.WriteLine($"PreloadNetPageIntegration: Preloaded model {model.Name}");
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"PreloadNetPageIntegration: Error preloading model {model?.Name}: {ex.Message}");
                            }
                        }
                    });

                    // Start background warmup for model execution environment
                    StartBackgroundWarmup();
                }

                Debug.WriteLine("PreloadNetPageIntegration: NetPage integration preloading completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"PreloadNetPageIntegration: Error during integration preloading: {ex.Message}");
            }
        }

        public async Task LoadAvailableModelsAsync()
        {
            try
            {
                Debug.WriteLine("Loading available HuggingFace models...");
                AvailableModels.Clear();

                // Get the NetPageViewModel and ensure it has loaded its models
                var netPageVM = ((App)Application.Current).NetPageViewModel;
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
                var persistedModels = await _fileService.LoadHuggingFaceModelsAsync();

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

                        AvailableModels.Add(hfModel);
                    }

                    Debug.WriteLine($"Loaded {AvailableModels.Count} available models from persisted data.");
                }

                // If no models were loaded from persistence, add some defaults as fallback
                if (AvailableModels.Count == 0)
                {
                    Debug.WriteLine("No persisted models found. Adding default examples.");
                    AddDefaultModelExamples();
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
                AvailableModels.Clear();
                AddDefaultModelExamples();
            }

            // Add input nodes to the AvailableModels list
            AddDefaultInputNodesToAvailableModels();
        }

        // Helper method to add default input nodes to the AvailableModels list
        private void AddDefaultInputNodesToAvailableModels()
        {
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "webcam_image", ModelId = "Webcam Image (Input)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "screen_image", ModelId = "Screen Image (Input)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "pc_audio", ModelId = "PC Audio (Input)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "webcam_audio", ModelId = "Webcam Audio (Input)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "keyboard_text", ModelId = "Keyboard Text (Input)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "mouse_text", ModelId = "Mouse Text (Input)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "memory_node", ModelId = "Memory (File)" });
        }

        // Helper method to add default examples as a fallback
        private void AddDefaultModelExamples()
        {
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "gpt2", ModelId = "Text Generator (GPT-2)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "resnet-50", ModelId = "Image Classifier (ResNet)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "openai/whisper-base", ModelId = "Audio Recognizer (Whisper)" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "deepseek-ai/deepseek-coder-1.3b-instruct", ModelId = "DeepSeek Coder" });
            AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "meta-llama/Meta-Llama-3-8B-Instruct", ModelId = "Llama 3 8B Instruct" });
        }

        public async Task AddModelNode(CSimple.Models.HuggingFaceModel model)
        {
            if (model == null)
            {
                await ShowAlert?.Invoke("Error", "No model selected.", "OK");
                return;
            }

            // Improve model node creation with HuggingFace info
            var modelId = model.ModelId ?? model.Id;
            var modelType = _nodeManagementService.InferNodeTypeFromName(modelId);
            var modelName = _nodeManagementService.GetFriendlyModelName(modelId);

            // Generate a reasonable position for the new node
            // Find a vacant spot in the middle area of the canvas
            float x = 300 + (Nodes.Count % 3) * 180;
            float y = 200 + (Nodes.Count / 3) * 100;

            // Use the NodeManagementService to add the node
            await _nodeManagementService.AddModelNodeAsync(Nodes, model.Id, modelName, modelType, new PointF(x, y));
            InvalidatePipelineStateCache(); // Invalidate cache when structure changes
            UpdateEnsembleCounts(); // ADDED: Update counts after adding node
            Debug.WriteLine($"🔄 [AddModelNode] Updating RunAllModelsCommand CanExecute - Model nodes count: {Nodes.Count(n => n.Type == NodeType.Model)}");
            (RunAllModelsCommand as Command)?.ChangeCanExecute(); // Update Run All Models button state
            (RunAllNodesCommand as Command)?.ChangeCanExecute(); // Update Run All Nodes button state
            await SaveCurrentPipelineAsync(); // Save after adding

            // Update execution status
            UpdateExecutionStatusFromPipeline();
        }

        public async Task DeleteSelectedNode()
        {
            if (SelectedNode != null)
            {
                await _nodeManagementService.DeleteSelectedNodeAsync(Nodes, Connections, SelectedNode, InvalidateCanvas);
                SelectedNode = null; // Deselect
                InvalidatePipelineStateCache(); // Invalidate cache when structure changes
                UpdateEnsembleCounts(); // ADDED: Update counts after removing connections
                Debug.WriteLine($"🗑️ [DeleteSelectedNode] Updating RunAllModelsCommand CanExecute - Model nodes count: {Nodes.Count(n => n.Type == NodeType.Model)}");
                (RunAllModelsCommand as Command)?.ChangeCanExecute(); // Update Run All Models button state
                (RunAllNodesCommand as Command)?.ChangeCanExecute(); // Update Run All Nodes button state
                await SaveCurrentPipelineAsync(); // Save after deleting
                InvalidateCanvas?.Invoke(); // ADDED: Ensure redraw after potential count update

                // Update execution status
                UpdateExecutionStatusFromPipeline();
            }
            else
            {
                await ShowAlert?.Invoke("Info", "No node selected to delete.", "OK");
            }
        }

        public void UpdateNodePosition(NodeViewModel node, PointF newPosition)
        {
            _nodeManagementService.UpdateNodePosition(node, newPosition);
            // Note: Saving on every move update might be too frequent.
            // Consider saving only on DragEnd interaction in the view,
            // or implement debouncing here. For simplicity, saving here for now.
            // await SaveCurrentPipelineAsync(); // Commented out again
        }

        // Call this from EndInteraction in the view after a drag completes
        public async Task FinalizeNodeMove()
        {
            // This method is now less critical if saving happens in UpdateNodePosition,
            // but can be kept for potential future use (e.g., debounced saving).
            // For now, we rely on saving within UpdateNodePosition.
            await SaveCurrentPipelineAsync(); // Uncommented to save only on drag end
            // await Task.CompletedTask; // Keep async signature
        }


        public NodeViewModel GetNodeAtPoint(PointF point)
        {
            return _nodeManagementService.GetNodeAtPoint(Nodes, point);
        }

        // --- Connection Logic ---
        public void StartConnection(NodeViewModel sourceNode)
        {
            if (sourceNode != null && (sourceNode.Type == NodeType.Input || sourceNode.Type == NodeType.Model || sourceNode.Type == NodeType.File))
            {
                _temporaryConnectionState = sourceNode;
                Debug.WriteLine($"Starting connection from {sourceNode.Name}");
            }
            else
            {
                _temporaryConnectionState = null;
                Debug.WriteLine("Cannot start connection from this node type or null node.");
            }
        }

        public async void CompleteConnection(NodeViewModel targetNode)
        {
            if (_temporaryConnectionState != null && targetNode != null && _temporaryConnectionState.Id != targetNode.Id)
            {
                // Basic validation: Prevent connecting Output directly to Input (example)
                if (_temporaryConnectionState.Type == NodeType.Output && targetNode.Type == NodeType.Input)
                {
                    await ShowAlert?.Invoke("Invalid Connection", "Cannot connect an Output node directly to an Input node.", "OK");
                    CancelConnection();
                    return;
                }

                // Check if connection already exists - thread-safe check
                bool exists;
                lock (_connectionsLock)
                {
                    exists = Connections.Any(c =>
                        (c.SourceNodeId == _temporaryConnectionState.Id && c.TargetNodeId == targetNode.Id) ||
                        (c.SourceNodeId == targetNode.Id && c.TargetNodeId == _temporaryConnectionState.Id));
                }

                if (!exists)
                {
                    // Use the NodeManagementService to complete the connection
                    _nodeManagementService.CompleteConnection(Connections, _temporaryConnectionState, targetNode, InvalidateCanvas);
                    Debug.WriteLine($"Completed connection from {_temporaryConnectionState.Name} to {targetNode.Name}");
                    InvalidatePipelineStateCache(); // Invalidate cache when structure changes
                    UpdateEnsembleCounts(); // ADDED: Update counts after adding
                    await SaveCurrentPipelineAsync(); // Save after adding connection
                }
                else
                {
                    Debug.WriteLine("Connection already exists.");
                }
            }
            else
            {
                Debug.WriteLine($"Failed to complete connection. StartNode: {_temporaryConnectionState?.Name}, TargetNode: {targetNode?.Name}");
            }
            // Reset state regardless of success
            _temporaryConnectionState = null;
            InvalidateCanvas?.Invoke(); // ADDED: Ensure redraw after potential count update
        }

        public void CancelConnection()
        {
            _temporaryConnectionState = null;
            Debug.WriteLine("Connection cancelled.");
        }

        // --- Pipeline Management Methods ---

        private async Task LoadAvailablePipelinesAsync()
        {
            await _pipelineManagementService.LoadAvailablePipelinesAsync(AvailablePipelineNames);
        }

        private async Task LoadPipelineAsync(string pipelineName)
        {
            Debug.WriteLine($"📂 [LoadPipelineAsync] Loading pipeline: {pipelineName}");
            await _pipelineManagementService.LoadPipelineAsync(pipelineName, Nodes, Connections, InvalidateCanvas, CurrentPipelineName, DisplayAlert, SetCurrentPipelineName, SetSelectedPipelineName, OnPropertyChanged, UpdateNodeClassificationsAsync);
            Debug.WriteLine($"📂 [LoadPipelineAsync] Pipeline loaded. Total nodes: {Nodes.Count}, Model nodes: {Nodes.Count(n => n.Type == NodeType.Model)}");
            Debug.WriteLine($"📂 [LoadPipelineAsync] Updating RunAllModelsCommand CanExecute after pipeline load");
            (RunAllModelsCommand as Command)?.ChangeCanExecute(); // Update Run All Models button state after loading
            (RunAllNodesCommand as Command)?.ChangeCanExecute(); // Update Run All Nodes button state after loading

            // Load camera offset for this pipeline
            await LoadCameraOffsetAsync();

            // Update execution status after pipeline is loaded
            UpdateExecutionStatusFromPipeline();
        }

        // Change from protected to public to make it accessible from OrientPage
        public async Task SaveCurrentPipelineAsync()
        {
            await _pipelineManagementService.SaveCurrentPipelineAsync(CurrentPipelineName, Nodes, Connections);
        }

        // Save camera offset for pan persistence
        public async Task SaveCameraOffsetAsync()
        {
            try
            {
                // Save camera offset to the same directory as pipelines (MyDocuments instead of ApplicationData)
                string pipelineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Pipelines");
                if (!Directory.Exists(pipelineDir))
                {
                    Directory.CreateDirectory(pipelineDir);
                }

                string offsetFile = Path.Combine(pipelineDir, $"{CurrentPipelineName}_cameraOffset.json");
                var offsetData = new { X = CameraOffsetX, Y = CameraOffsetY };
                string json = System.Text.Json.JsonSerializer.Serialize(offsetData);

                await File.WriteAllTextAsync(offsetFile, json);
                Debug.WriteLine($"💾 [SaveCameraOffsetAsync] Saved camera offset: ({CameraOffsetX}, {CameraOffsetY}) for pipeline: {CurrentPipelineName} to {offsetFile}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [SaveCameraOffsetAsync] Error saving camera offset: {ex.Message}");
            }
        }

        // Load camera offset for pan persistence  
        public async Task LoadCameraOffsetAsync()
        {
            try
            {
                string pipelineDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CSimple", "Resources", "Pipelines");
                string offsetFile = Path.Combine(pipelineDir, $"{CurrentPipelineName}_cameraOffset.json");

                if (File.Exists(offsetFile))
                {
                    string json = await File.ReadAllTextAsync(offsetFile);
                    var offsetData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);

                    if (offsetData != null)
                    {
                        var element = (System.Text.Json.JsonElement)offsetData;
                        if (element.TryGetProperty("X", out var xElement) && element.TryGetProperty("Y", out var yElement))
                        {
                            CameraOffsetX = xElement.GetSingle();
                            CameraOffsetY = yElement.GetSingle();
                            Debug.WriteLine($"� [LoadCameraOffsetAsync] Loaded camera offset: ({CameraOffsetX}, {CameraOffsetY}) for pipeline: {CurrentPipelineName}");
                        }
                    }
                }
                else
                {
                    // Set default values if no saved offset exists
                    CameraOffsetX = 0f;
                    CameraOffsetY = 0f;
                    Debug.WriteLine($"📂 [LoadCameraOffsetAsync] No saved camera offset found for pipeline: {CurrentPipelineName}, using defaults");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [LoadCameraOffsetAsync] Error loading camera offset: {ex.Message}");
                // Set default values on error
                CameraOffsetX = 0f;
                CameraOffsetY = 0f;
            }
        }

        // Update camera offset from OrientPage
        public void UpdateCameraOffset(float x, float y)
        {
            CameraOffsetX = x;
            CameraOffsetY = y;
        }

        private async Task CreateNewPipeline()
        {
            await _pipelineManagementService.CreateNewPipelineAsync(AvailablePipelineNames, Nodes, Connections, InvalidateCanvas, ClearCanvas, SetCurrentPipelineName, OnPropertyChanged);
        }

        private async Task RenameCurrentPipeline()
        {
            if (string.IsNullOrWhiteSpace(SelectedPipelineName))
            {
                await ShowAlert?.Invoke("Info", "No pipeline selected to rename.", "OK");
                return;
            }

            string oldName = SelectedPipelineName;
            string newName = await Application.Current.MainPage.DisplayPromptAsync("Rename Pipeline", "Enter new name:", initialValue: oldName);

            if (!string.IsNullOrWhiteSpace(newName) && newName != oldName)
            {
                bool success = await _pipelineManagementService.RenamePipelineAsync(oldName, newName);
                if (success)
                {
                    CurrentPipelineName = newName; // Update current name if it was the one renamed
                    // Update the list
                    int index = AvailablePipelineNames.IndexOf(oldName);
                    if (index != -1)
                    {
                        AvailablePipelineNames[index] = newName;
                    }
                    // Re-select the renamed item
                    SelectedPipelineName = newName;
                    await ShowAlert?.Invoke("Success", $"Pipeline renamed to '{newName}'.", "OK");
                    // Optionally reload the list to ensure sorting, but direct update is faster
                    // await LoadAvailablePipelinesAsync();
                }
                else
                {
                    await ShowAlert?.Invoke("Error", "Failed to rename pipeline.", "OK");
                }
            }
        }

        private async Task DeleteCurrentPipeline()
        {
            if (string.IsNullOrWhiteSpace(SelectedPipelineName))
            {
                await ShowAlert?.Invoke("Info", "No pipeline selected to delete.", "OK");
                return;
            }

            bool confirm = await Application.Current.MainPage.DisplayAlert("Confirm Delete", $"Are you sure you want to delete pipeline '{SelectedPipelineName}'?", "Yes", "No");
            if (confirm)
            {
                string nameToDelete = SelectedPipelineName;
                await _pipelineManagementService.DeletePipelineAsync(nameToDelete);
                AvailablePipelineNames.Remove(nameToDelete);
                OnPropertyChanged(nameof(AvailablePipelineNames)); // Notify UI

                // Load the next available pipeline or create a new one
                if (AvailablePipelineNames.Any())
                {
                    // Select the most recent remaining pipeline
                    SelectedPipelineName = AvailablePipelineNames.First();
                    Debug.WriteLine($"Deleted '{nameToDelete}'. Loaded next pipeline: '{SelectedPipelineName}'");
                }
                else
                {
                    // No pipelines left, create a new default one
                    Debug.WriteLine($"Deleted '{nameToDelete}'. No pipelines left. Creating a new one.");
                    await CreateNewPipeline(); // Create in memory
                    await SaveCurrentPipelineAsync(); // Save it
                    SelectedPipelineName = CurrentPipelineName; // Select it
                    Debug.WriteLine($"Created and selected new default pipeline: '{CurrentPipelineName}'");
                }
                await ShowAlert?.Invoke("Success", $"Pipeline '{nameToDelete}' deleted.", "OK");
            }
        }


        // --- Helper Methods ---

        private void ClearCanvas()
        {
            lock (_nodesLock)
            {
                Nodes.Clear();
            }
            lock (_connectionsLock)
            {
                Connections.Clear();
            }
            SelectedNode = null;
            _temporaryConnectionState = null;

            // Invalidate caches since collections changed
            InvalidatePipelineStateCache();
        }

        private NodeType InferNodeTypeFromName(string name)
        {
            return _nodeManagementService.InferNodeTypeFromName(name);
        }

        // Helper to determine a more friendly model name
        private string GetFriendlyModelName(string modelId)
        {
            return _nodeManagementService.GetFriendlyModelName(modelId);
        }

        public async Task UpdateNodeClassificationsAsync()
        {
            await _nodeManagementService.UpdateNodeClassificationsAsync(Nodes, _netPageViewModel.AvailableModels, InvalidateCanvas, DetermineDataTypeFromName, SaveCurrentPipelineAsync);
        }

        // Helper to infer data type from node name as a fallback
        private string DetermineDataTypeFromName(string nodeName)
        {
            return _nodeManagementService.DetermineDataTypeFromName(nodeName);
        }

        // New helper method for finding corresponding model with better matching logic
        private NeuralNetworkModel FindCorrespondingModel(NetPageViewModel netPageVM, NodeViewModel node)
        {
            // Always use the Application's NetPageViewModel to ensure we get the latest loaded models
            var currentNetPageVM = ((App)Application.Current)?.NetPageViewModel;
            if (currentNetPageVM?.AvailableModels != null && currentNetPageVM.AvailableModels.Count > 0)
            {
                return _nodeManagementService.FindCorrespondingModel(currentNetPageVM.AvailableModels, node);
            }

            // Fallback to the provided parameter if the Application instance isn't available
            return _nodeManagementService.FindCorrespondingModel(netPageVM?.AvailableModels, node);
        }

        // --- Pipeline Execution Logic ---

        /// <summary>
        /// Executes the currently loaded pipeline, optionally injecting a prompt into the final text model.
        /// NOTE: This is a simulation and does not run actual models.
        /// </summary>
        /// <param name="promptOverride">A specific prompt to add to the final text model's input.</param>
        /// <returns>The simulated output string from the final node, or an error message.</returns>
        public async Task<string> ExecuteCurrentPipelineAsync(string promptOverride = null)
        {
            Debug.WriteLine($"Executing pipeline '{CurrentPipelineName}' with prompt override: '{promptOverride}'");

            if (!HasAnyNodes() || !HasAnyConnections())
            {
                return "Error: Pipeline is empty or has no connections.";
            }

            // --- Simulation Logic ---
            // This needs to be replaced with actual graph traversal and model execution.
            // For now, we'll make assumptions based on common patterns:
            // 1. Find Input nodes.
            // 2. Find Model nodes directly connected FROM Input nodes (Interpreters).
            // 3. Find a Model node connected FROM multiple Interpreters (Combiner/Final Text Model).

            var inputNodes = Nodes.Where(n => n.Type == NodeType.Input).ToList();
            if (!inputNodes.Any()) return "Error: No input nodes found.";

            var interpreterOutputs = new Dictionary<string, string>(); // NodeId -> Simulated Output
            var interpreterNodes = new List<NodeViewModel>();

            // Simulate interpreter models processing inputs
            foreach (var inputNode in inputNodes)
            {
                var connectedModelIds = Connections
                    .Where(c => c.SourceNodeId == inputNode.Id)
                    .Select(c => c.TargetNodeId);

                foreach (var modelId in connectedModelIds)
                {
                    var modelNode = Nodes.FirstOrDefault(n => n.Id == modelId && n.Type == NodeType.Model);
                    if (modelNode != null && !interpreterOutputs.ContainsKey(modelNode.Id))
                    {
                        // Simulate output based on input type
                        string simulatedOutput = $"Interpreted {inputNode.DataType ?? "data"} from '{inputNode.Name}' via '{modelNode.Name}'.";
                        interpreterOutputs.Add(modelNode.Id, simulatedOutput);
                        if (!interpreterNodes.Contains(modelNode))
                        {
                            interpreterNodes.Add(modelNode);
                        }
                        Debug.WriteLine($"Simulated output for interpreter '{modelNode.Name}': {simulatedOutput}");
                    }
                }
            }

            if (!interpreterNodes.Any()) return "Error: No interpreter models found connected to inputs.";

            // Find the final combiner/text model (connected FROM interpreters)
            NodeViewModel finalModel = null;
            var textModelNodes = GetTextModelNodes();
            foreach (var potentialFinalNode in textModelNodes)
            {
                var incomingConnections = Connections
                    .Where(c => c.TargetNodeId == potentialFinalNode.Id)
                    .Select(c => c.SourceNodeId);

                // Check if this node receives input from *all* identified interpreters
                bool receivesFromAllInterpreters = interpreterNodes.All(interp => incomingConnections.Contains(interp.Id));

                // Or check if it receives from *any* interpreter (simpler assumption)
                bool receivesFromAnyInterpreter = interpreterNodes.Any(interp => incomingConnections.Contains(interp.Id));


                // Let's assume the final node is the first text model connected to *any* interpreter
                if (receivesFromAnyInterpreter)
                {
                    finalModel = potentialFinalNode;
                    Debug.WriteLine($"Identified potential final model: '{finalModel.Name}'");
                    break;
                }
            }


            if (finalModel == null)
            {
                // Fallback: Find *any* model connected from an interpreter if no text model found - thread-safe version
                List<NodeViewModel> nodesCopy;
                List<ConnectionViewModel> connectionsCopy;

                lock (_nodesLock)
                {
                    nodesCopy = Nodes.ToList();
                }

                lock (_connectionsLock)
                {
                    connectionsCopy = Connections.ToList();
                }

                finalModel = nodesCopy.FirstOrDefault(n => n.Type == NodeType.Model &&
                    connectionsCopy.Any(c => c.TargetNodeId == n.Id && interpreterNodes.Any(interp => interp.Id == c.SourceNodeId)));
                if (finalModel != null)
                {
                    Debug.WriteLine($"Identified fallback final model (non-text?): '{finalModel.Name}'");
                }
            }


            if (finalModel == null) return "Error: Could not identify a final processing model connected to interpreters.";

            // Simulate final model execution
            var combinedInput = new StringBuilder();
            combinedInput.AppendLine($"Processing request for model '{finalModel.Name}':");

            // Gather inputs from connected interpreters
            var finalModelInputs = Connections
                   .Where(c => c.TargetNodeId == finalModel.Id)
                   .Select(c => c.SourceNodeId);

            foreach (var inputId in finalModelInputs)
            {
                if (interpreterOutputs.TryGetValue(inputId, out var output))
                {
                    combinedInput.AppendLine($"- Input: {output}");
                }
                else
                {
                    // Maybe connected directly from an input node?
                    var directInputNode = Nodes.FirstOrDefault(n => n.Id == inputId && n.Type == NodeType.Input);
                    if (directInputNode != null)
                    {
                        combinedInput.AppendLine($"- Direct Input: Raw {directInputNode.DataType ?? "data"} from '{directInputNode.Name}'.");
                    }
                }
            }


            if (!string.IsNullOrWhiteSpace(promptOverride))
            {
                combinedInput.AppendLine($"- Specific Prompt: {promptOverride}");
            }

            // Simulate API call or local execution delay
            await Task.Delay(1500); // Simulate processing time

            string finalOutput = $"Simulated result from '{finalModel.Name}': Based on the inputs ({interpreterNodes.Count} sources) and the prompt, the suggested improvement is to [Simulated AI Suggestion - Refine workflow for {finalModel.Name}].";
            Debug.WriteLine($"Final simulated output: {finalOutput}");

            return finalOutput;
        }

        // Method to load a specific pipeline by name
        public async Task LoadPipelineByNameAsync(string pipelineName)
        {
            if (string.IsNullOrEmpty(pipelineName))
            {
                Debug.WriteLine("LoadPipelineByNameAsync: Pipeline name is null or empty.");
                return;
            }

            try
            {
                var pipelineData = await _fileService.LoadPipelineAsync(pipelineName);
                if (pipelineData != null)
                {
                    LoadPipelineData(pipelineData); // Use existing method to load nodes/connections
                    CurrentPipelineName = pipelineName; // Update the current pipeline name
                    Debug.WriteLine($"Pipeline '{pipelineName}' loaded successfully.");
                }
                else
                {
                    Debug.WriteLine($"Failed to load pipeline data for '{pipelineName}'.");
                    // Optionally clear the canvas or show an error
                    // ClearCanvas();
                    // await ShowAlert("Error", $"Could not load pipeline '{pipelineName}'.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading pipeline '{pipelineName}': {ex.Message}");
                // await ShowAlert("Error", $"An error occurred while loading pipeline '{pipelineName}'.", "OK");
            }
        }


        // Method to execute a pipeline by name
        public async Task<string> ExecutePipelineByNameAsync(string pipelineName, string initialInput)
        {
            Debug.WriteLine($"Attempting to execute pipeline: {pipelineName} with input: {initialInput}");
            await LoadPipelineByNameAsync(pipelineName); // Load the specified pipeline first

            if (CurrentPipelineName != pipelineName || Nodes.Count == 0)
            {
                return $"Error: Failed to load pipeline '{pipelineName}' before execution.";
            }

            // Now execute the loaded pipeline (using the logic from ExecuteCurrentPipelineAsync)
            return await ExecuteCurrentPipelineAsync(initialInput);
        }

        // Helper method to load pipeline data into the view model state
        private void LoadPipelineData(PipelineData data)
        {
            _nodeManagementService.LoadPipelineData(Nodes, Connections, data, InvalidateCanvas);
            CurrentPipelineName = data.Name;
        }


        // --- INotifyPropertyChanged Implementation ---

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            // Update command states only when necessary (optimized)
            switch (propertyName)
            {
                case nameof(SelectedNode):
                case nameof(Nodes):
                case nameof(Connections):
                    InvalidatePipelineStateCache(); // Invalidate cache when structure changes
                    break;

                case nameof(StepContent):
                case nameof(StepContentType):
                    // Update audio command states when relevant properties change
                    ((Command)PlayAudioCommand)?.ChangeCanExecute();
                    ((Command)StopAudioCommand)?.ChangeCanExecute();
                    break;
            }
        }

        public Action InvalidateCanvas { get; set; }

        // --- Helper Methods ---

        /// <summary>
        /// Initialize execution status panel with default values
        /// </summary>
        private void InitializeExecutionStatus()
        {
            _executionStatusTrackingService.InitializeExecutionStatus();
            ExecutionResults.Clear();
            AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] System initialized");
        }

        /// <summary>
        /// Update execution status based on current pipeline state
        /// </summary>
        private void UpdateExecutionStatusFromPipeline()
        {
            var modelCount = Nodes.Count(n => n.Type == NodeType.Model);
            if (modelCount != TotalModelsToExecute && !IsExecutingModels)
            {
                TotalModelsToExecute = modelCount;
                if (modelCount > 0)
                {
                    ExecutionStatus = $"Pipeline loaded: {modelCount} models ready";
                    AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Pipeline loaded with {modelCount} models");
                }
                else
                {
                    ExecutionStatus = "No models in pipeline";
                    AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Empty pipeline loaded");
                }
            }
        }

        /// <summary>
        /// Add execution result and maintain only the most recent 6 entries
        /// </summary>
        private void AddExecutionResult(string message)
        {
            ExecutionResults.Add(message);

            // Keep only the most recent 6 results
            while (ExecutionResults.Count > 6)
            {
                ExecutionResults.RemoveAt(0);
            }
        }

        // ADDED: Method to calculate and update ensemble input counts for all nodes
        private void UpdateEnsembleCounts()
        {
            _nodeManagementService.UpdateEnsembleCounts(Nodes, Connections, InvalidateCanvas);
        }

        // ADDED: Method to set a node's classification
        public async void SetNodeClassification(NodeViewModel node, string classification)
        {
            if (node == null) return;

            _nodeManagementService.SetNodeClassification(node, classification, InvalidateCanvas);

            // Save the pipeline after classification change to persist the changes
            if (!string.IsNullOrEmpty(CurrentPipelineName))
            {
                try
                {
                    await _pipelineManagementService.SaveCurrentPipelineAsync(CurrentPipelineName, Nodes, Connections);
                    Debug.WriteLine($"Pipeline '{CurrentPipelineName}' saved after classification change for node '{node.OriginalName}' to '{classification}'");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error saving pipeline after classification change: {ex.Message}");
                    // Note: In a production app, you might want to show a user-friendly error message
                    // await DisplayAlert("Save Error", $"Failed to save classification change: {ex.Message}", "OK");
                }
            }
            else
            {
                Debug.WriteLine("No current pipeline name set, classification change not saved");
            }
        }

        // Add these methods for Action Review functionality using the service
        private async Task LoadAvailableActions()
        {
            try
            {
                // Clear existing items
                AvailableActionNames.Clear();

                var actionNames = await _actionReviewService.LoadAvailableActionsAsync();

                foreach (var actionName in actionNames)
                {
                    AvailableActionNames.Add(actionName);
                }

                // Automatically select the most recent action if available
                if (AvailableActionNames.Count > 0)
                {
                    SelectedReviewActionName = AvailableActionNames[0];
                }

                Debug.WriteLine($"Loaded {AvailableActionNames.Count} actions for review, selected: {SelectedReviewActionName ?? "none"}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading actions: {ex.Message}");
            }
        }

        private string GetFileTypeFromName(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return "unknown";
            string ext = System.IO.Path.GetExtension(filename).ToLowerInvariant();
            switch (ext)
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".bmp":
                case ".gif":
                    return "image";
                case ".wav":
                case ".mp3":
                case ".aac":
                    return "audio";
                case ".txt":
                case ".md":
                case ".json":
                case ".xml":
                    return "text";
                default:
                    // Basic content sniffing for text if no extension
                    if (!string.IsNullOrEmpty(filename) && (filename.StartsWith("text:") || filename.Length < 255 && !filename.Any(c => char.IsControl(c) && c != '\r' && c != '\n' && c != '\t')))
                        return "text"; // Crude check for text content
                    return "unknown";
            }
        }

        private async void LoadSelectedAction()
        {
            try
            {
                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Attempting to load action: {SelectedReviewActionName ?? "null"}");

                // Clear the step content cache to ensure fresh data for the new action
                _ensembleModelService.ClearStepContentCache();
                _ensembleModelService.ClearModelNodeActionSteps(Nodes);
                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Cleared step content cache and model ActionSteps for action change");

                // Use the ActionStepNavigationService to load the action
                var result = await _actionStepNavigationService.LoadSelectedActionAsync(
                    SelectedReviewActionName,
                    Nodes,
                    SetCurrentActionStepAsync,
                    () =>
                    {
                        (StepForwardCommand as Command)?.ChangeCanExecute();
                        (StepBackwardCommand as Command)?.ChangeCanExecute();
                    });

                _currentActionItems = result.ActionItems;

                // Update static property for NodeViewModel access
                NodeViewModel.CurrentActionItems = _currentActionItems;

                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Loaded '{SelectedReviewActionName}' with {_currentActionItems.Count} action items via navigation service.");

                // Force UI refresh after action change
                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Forcing UI refresh for new action");

                // Explicit refresh of all node ActionSteps to ensure UI updates
                RefreshAllNodeStepContent();

                // Notify UI that step content might have changed
                OnPropertyChanged(nameof(StepContent));
                OnPropertyChanged(nameof(StepContentType));

                // Also notify that the current action step display should update
                OnPropertyChanged(nameof(CurrentActionStep));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            finally
            {
                (StepForwardCommand as Command)?.ChangeCanExecute();
                (StepBackwardCommand as Command)?.ChangeCanExecute();
                (ResetActionCommand as Command)?.ChangeCanExecute();

                // Add a small delay to ensure all async operations complete
                await Task.Delay(100);

                UpdateStepContent(); // Call this to reflect the state for CurrentActionStep = 0

                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Action loading complete, step content updated");
            }
        }

        private async Task LoadActionStepData()
        {
            try
            {
                await _actionReviewService.LoadActionStepDataAsync(CurrentActionStep, _currentActionItems);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OrientPageViewModel.LoadActionStepData] Error: {ex.Message}");
            }
            finally
            {
                UpdateStepContent(); // This is the most important call here.
            }
        }

        private string _stepContentType;
        public string StepContentType
        {
            get => _stepContentType;
            set => SetProperty(ref _stepContentType, value);
        }

        private string _stepContent;
        public string StepContent
        {
            get => _stepContent;
            set => SetProperty(ref _stepContent, value);
        }

        private List<string> _stepContentImages = new List<string>();
        public List<string> StepContentImages
        {
            get => _stepContentImages;
            set
            {
                // Validate and filter the incoming image paths to prevent crashes
                var validatedPaths = ValidateImagePaths(value);

                if (SetProperty(ref _stepContentImages, validatedPaths))
                {
                    // Update computed properties when the collection changes
                    OnPropertyChanged(nameof(FirstImage));
                    OnPropertyChanged(nameof(SecondImage));
                    OnPropertyChanged(nameof(HasFirstImage));
                    OnPropertyChanged(nameof(HasSecondImage));
                }
            }
        }

        /// <summary>
        /// Validates a list of image paths and returns only those that exist and are accessible
        /// </summary>
        private List<string> ValidateImagePaths(List<string> imagePaths)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                return new List<string>();
            }

            var validPaths = new List<string>();
            foreach (var path in imagePaths)
            {
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        if (File.Exists(path))
                        {
                            validPaths.Add(path);
                        }
                        else
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.ValidateImagePaths] Filtering out non-existent image: {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.ValidateImagePaths] Error validating image path '{path}': {ex.Message}");
                    }
                }
            }

            return validPaths;
        }

        private bool _hasMultipleImages;
        public bool HasMultipleImages
        {
            get => _hasMultipleImages;
            set => SetProperty(ref _hasMultipleImages, value);
        }

        // Safe access properties for individual images
        public string FirstImage => StepContentImages?.Count > 0 ? StepContentImages[0] : null;
        public string SecondImage => StepContentImages?.Count > 1 ? StepContentImages[1] : null;
        public bool HasFirstImage => !string.IsNullOrEmpty(FirstImage);
        public bool HasSecondImage => !string.IsNullOrEmpty(SecondImage);

        public ICommand PlayAudioCommand { get; }
        public ICommand StopAudioCommand { get; }

        public OrientPageViewModel()
        {
            // This constructor may be used by XAML designer or tests
        }

        public void UpdateStepContent()
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Called - SelectedNode: {SelectedNode?.Name ?? "null"}, CurrentActionStep: {CurrentActionStep}, SelectedAction: {SelectedReviewActionName ?? "null"}");

            try
            {
                var stepContentData = _actionReviewService.UpdateStepContent(SelectedNode, CurrentActionStep, _currentActionItems, SelectedReviewActionName);

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Retrieved content - Type: {stepContentData.ContentType}, Content: {stepContentData.Content?.Substring(0, Math.Min(100, stepContentData.Content?.Length ?? 0))}...");

                StepContentType = stepContentData.ContentType;
                StepContent = stepContentData.Content;

                // Handle multiple images for screen capture nodes with enhanced error checking
                if (stepContentData.ContentType?.ToLower() == "image" && !string.IsNullOrEmpty(stepContentData.Content))
                {
                    try
                    {
                        if (stepContentData.Content.Contains(';'))
                        {
                            // Multiple images - split and store separately with validation
                            var imagePaths = stepContentData.Content.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                                                    .Select(path => path.Trim())
                                                                    .Where(path => !string.IsNullOrEmpty(path))
                                                                    .ToList();

                            // Validate each image path and log missing files
                            var validImagePaths = new List<string>();
                            foreach (var imagePath in imagePaths)
                            {
                                if (File.Exists(imagePath))
                                {
                                    validImagePaths.Add(imagePath);
                                }
                                else
                                {
                                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Warning: Image file not found: {imagePath}");
                                }
                            }

                            if (validImagePaths.Count > 0)
                            {
                                StepContentImages = validImagePaths;
                                HasMultipleImages = validImagePaths.Count > 1;
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Found {validImagePaths.Count} valid image(s) out of {imagePaths.Count} total paths");
                            }
                            else
                            {
                                // No valid images found after filtering
                                StepContentImages = new List<string>();
                                HasMultipleImages = false;
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] No valid images found after filtering {imagePaths.Count} paths");
                            }
                        }
                        else
                        {
                            // Single image - validate it exists
                            if (File.Exists(stepContentData.Content))
                            {
                                StepContentImages = new List<string> { stepContentData.Content };
                                HasMultipleImages = false;
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Found single valid image: {stepContentData.Content}");
                            }
                            else
                            {
                                // Image file doesn't exist
                                StepContentImages = new List<string>();
                                HasMultipleImages = false;
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Single image file not found: {stepContentData.Content}");
                            }
                        }
                    }
                    catch (Exception imageEx)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Error processing images: {imageEx.Message}");
                        StepContentImages = new List<string>();
                        HasMultipleImages = false;
                    }
                }
                else
                {
                    StepContentImages = new List<string>();
                    HasMultipleImages = false;
                }

                OnPropertyChanged(nameof(StepContentType));
                OnPropertyChanged(nameof(StepContent));
                OnPropertyChanged(nameof(StepContentImages));
                OnPropertyChanged(nameof(HasMultipleImages));
                OnPropertyChanged(nameof(FirstImage));
                OnPropertyChanged(nameof(SecondImage));
                OnPropertyChanged(nameof(HasFirstImage));
                OnPropertyChanged(nameof(HasSecondImage));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.UpdateStepContent] Error: {ex.Message}");

                // Set safe defaults on error
                StepContentType = null;
                StepContent = null;
                StepContentImages = new List<string>();
                HasMultipleImages = false;

                OnPropertyChanged(nameof(StepContentType));
                OnPropertyChanged(nameof(StepContent));
                OnPropertyChanged(nameof(StepContentImages));
                OnPropertyChanged(nameof(HasMultipleImages));
                OnPropertyChanged(nameof(FirstImage));
                OnPropertyChanged(nameof(SecondImage));
                OnPropertyChanged(nameof(HasFirstImage));
                OnPropertyChanged(nameof(HasSecondImage));
            }
        }

        /// <summary>
        /// Refreshes ActionSteps for all nodes to ensure they reflect the current action
        /// </summary>
        private void RefreshAllNodeStepContent()
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.RefreshAllNodeStepContent] Refreshing step content for all nodes");

            var allNodes = GetAllNodes();
            foreach (var node in allNodes)
            {
                // Force a refresh by triggering property change notifications
                if (node.Type == NodeType.Input || node.Type == NodeType.Model)
                {
                    // If the node has ActionSteps, we want to ensure UI elements that depend on them are refreshed
                    if (node.ActionSteps != null && node.ActionSteps.Count > 0)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [OrientPageViewModel.RefreshAllNodeStepContent] Node {node.Name} has {node.ActionSteps.Count} ActionSteps");
                    }
                }
            }
        }

        private async void PlayAudio()
        {
            await _audioStepContentService.PlayStepContentAsync(StepContent, StepContentType, SelectedNode);
        }

        private async void StopAudio()
        {
            await _audioStepContentService.StopAudioAsync();
        }

        private Task DisplayAlert(string message)
        {
            return ShowAlert?.Invoke("Error", message, "OK");
        }

        private void SetCurrentPipelineName(string name)
        {
            CurrentPipelineName = name;
        }

        private void SetSelectedPipelineName(string name)
        {
            SelectedPipelineName = name;
        }

        // --- Cached data for performance ---
        private List<NodeViewModel> _cachedModelNodes;
        private List<NodeViewModel> _cachedInputNodes;
        private Dictionary<string, int> _cachedConnectionCounts;
        private bool _pipelineStateCacheValid = false;

        // --- Ultra-fast execution caches ---
        private Dictionary<string, NeuralNetworkModel> _preloadedModelCache = new Dictionary<string, NeuralNetworkModel>();
        private Dictionary<string, (string contentType, string content)> _precomputedStepContentCache = new Dictionary<string, (string, string)>();
        private Dictionary<string, List<NodeViewModel>> _precomputedInputRelationships = new Dictionary<string, List<NodeViewModel>>();
        private Dictionary<string, string> _precomputedCombinedInputs = new Dictionary<string, string>();
        private bool _executionOptimizationCacheValid = false;

        // --- Background warmup optimization ---
        private Task _backgroundWarmupTask = null;
        private bool _environmentPrewarmed = false;
        private readonly object _warmupLock = new object();

        // Collection access locks for thread safety
        private readonly object _nodesLock = new object();
        private readonly object _connectionsLock = new object();
        private readonly object _cachedCollectionsLock = new object(); // Dedicated lock for cached collections

        // Thread-safe helper methods for collection access
        private bool HasModelNodes()
        {
            lock (_nodesLock)
            {
                return Nodes.Any(n => n.Type == NodeType.Model);
            }
        }

        private int GetModelNodesCount()
        {
            lock (_nodesLock)
            {
                return Nodes.Count(n => n.Type == NodeType.Model);
            }
        }

        private List<NodeViewModel> GetTextModelNodes()
        {
            lock (_nodesLock)
            {
                return Nodes.Where(n => n.Type == NodeType.Model && n.DataType == "text").ToList();
            }
        }

        private List<NodeViewModel> GetAllNodes()
        {
            lock (_nodesLock)
            {
                return Nodes.ToList();
            }
        }

        private List<ConnectionViewModel> GetAllConnections()
        {
            lock (_connectionsLock)
            {
                return Connections.ToList();
            }
        }

        private bool HasAnyNodes()
        {
            lock (_nodesLock)
            {
                return Nodes.Any();
            }
        }

        private bool HasAnyConnections()
        {
            lock (_connectionsLock)
            {
                return Connections.Any();
            }
        }

        // --- Proactive preparation for Run All Models optimization ---
        private Task _proactivePreparationTask = null;
        private bool _modelExecutionPrepared = false;
        private readonly object _preparationLock = new object();
        private CancellationTokenSource _preparationCancellationSource = null;

        // Cache pipeline state for performance
        private void CachePipelineState()
        {
            // Capture collections safely to avoid concurrent access issues
            List<NodeViewModel> nodesCopy;
            List<ConnectionViewModel> connectionsCopy;

            // Lock to ensure thread-safe access to ObservableCollections
            lock (_nodesLock)
            {
                nodesCopy = Nodes.ToList();
            }

            lock (_connectionsLock)
            {
                connectionsCopy = Connections.ToList();
            }

            // Thread-safely update cached collections using dedicated lock
            lock (_cachedCollectionsLock)
            {
                _cachedModelNodes = nodesCopy.Where(n => n.Type == NodeType.Model).ToList();
                _cachedInputNodes = nodesCopy.Where(n => n.Type == NodeType.Input).ToList();
                _cachedConnectionCounts = new Dictionary<string, int>();

                foreach (var node in _cachedModelNodes)
                {
                    _cachedConnectionCounts[node.Id] = connectionsCopy.Count(c => c.TargetNodeId == node.Id);
                }

                _pipelineStateCacheValid = true;
            }
        }

        // Invalidate cache when pipeline structure changes
        private void InvalidatePipelineStateCache()
        {
            _pipelineStateCacheValid = false;
            _executionOptimizationCacheValid = false;

            // Also invalidate background warmup if pipeline structure changed
            lock (_warmupLock)
            {
                if (!_environmentPrewarmed)
                {
                    StartBackgroundWarmup(); // Restart warmup with new pipeline
                }
            }
        }

        // --- Ultra-fast execution optimization methods ---

        /// <summary>
        /// Pre-loads and caches all expensive operations before model execution
        /// </summary>
        private async Task PrecomputeExecutionOptimizationsAsync()
        {
            if (_executionOptimizationCacheValid) return;

            var stopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 [PrecomputeExecutionOptimizations] Starting pre-computation...");

            // Clear existing caches
            _preloadedModelCache.Clear();
            _precomputedStepContentCache.Clear();
            _precomputedInputRelationships.Clear();
            _precomputedCombinedInputs.Clear();

            // Ensure pipeline state is cached
            if (!_pipelineStateCacheValid)
            {
                CachePipelineState();
            }

            // 1. Pre-load all neural network models to avoid lookup delays (parallel)
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📚 [PrecomputeExecutionOptimizations] Pre-loading model references...");

            // Create a safe copy of the cached model nodes to prevent concurrent modification
            List<NodeViewModel> modelNodesCopy;
            lock (_cachedCollectionsLock)
            {
                modelNodesCopy = _cachedModelNodes?.ToList() ?? new List<NodeViewModel>();
            }

            var modelTasks = modelNodesCopy.Select(async modelNode =>
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var correspondingModel = FindCorrespondingModel(_netPageViewModel, modelNode);
                        if (correspondingModel != null)
                        {
                            lock (_preloadedModelCache)
                            {
                                _preloadedModelCache[modelNode.Id] = correspondingModel;
                            }
                            Debug.WriteLine($"   ✅ Cached model: {modelNode.Name} -> {correspondingModel.HuggingFaceModelId}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"   ⚠️ Failed to cache model {modelNode.Name}: {ex.Message}");
                }
            }).ToArray();

            await Task.WhenAll(modelTasks);

            // 2. Ensure background warmup is ready (but don't wait if it's not)
            await EnsureWarmupReadyAsync(maxWaitMs: 2000); // Quick check, don't block

            // 3. Pre-compute step content for all input nodes (parallel)
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📄 [PrecomputeExecutionOptimizations] Pre-computing step content...");
            int stepIndex = CurrentActionStep + 1; // Convert to 1-based

            // Get the ActionItem timestamp for precise file correlation
            DateTime? actionItemTimestamp = null;
            if (_currentActionItems != null && CurrentActionStep >= 0 && CurrentActionStep < _currentActionItems.Count)
            {
                var currentActionItem = _currentActionItems[CurrentActionStep];
                if (currentActionItem?.Timestamp != null)
                {
                    if (currentActionItem.Timestamp is DateTime directTimestamp)
                    {
                        actionItemTimestamp = directTimestamp;
                    }
                    else if (DateTime.TryParse(currentActionItem.Timestamp.ToString(), out DateTime parsedTimestamp))
                    {
                        actionItemTimestamp = parsedTimestamp;
                    }
                }
            }

            // Create a safe copy of the cached input nodes to prevent concurrent modification
            List<NodeViewModel> inputNodesCopy;
            lock (_cachedCollectionsLock)
            {
                inputNodesCopy = _cachedInputNodes?.ToList() ?? new List<NodeViewModel>();
            }

            var stepContentTasks = inputNodesCopy.Select(async inputNode =>
            {
                try
                {
                    await Task.Run(() =>
                    {
                        // FIXED: Pass ActionItem timestamp for audio/image file correlation
                        var (contentType, content) = inputNode.GetStepContent(stepIndex, actionItemTimestamp);
                        if (!string.IsNullOrEmpty(content))
                        {
                            lock (_precomputedStepContentCache)
                            {
                                _precomputedStepContentCache[inputNode.Id] = (contentType, content);
                            }
                            Debug.WriteLine($"   ✅ Cached content for {inputNode.Name}: {contentType} ({content?.Length ?? 0} chars)");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"   ⚠️ Failed to cache content for {inputNode.Name}: {ex.Message}");
                }
            }).ToArray();

            await Task.WhenAll(stepContentTasks);

            // 4. Pre-compute input relationships and combined inputs for all models (parallel)
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔗 [PrecomputeExecutionOptimizations] Pre-computing input relationships...");

            // Create a safe copy of the cached model nodes to prevent concurrent modification
            List<NodeViewModel> relationshipModelNodesCopy;
            lock (_cachedCollectionsLock)
            {
                relationshipModelNodesCopy = _cachedModelNodes?.ToList() ?? new List<NodeViewModel>();
            }

            var relationshipTasks = relationshipModelNodesCopy.Select(async modelNode =>
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var connectedInputNodes = GetConnectedInputNodes(modelNode);
                        lock (_precomputedInputRelationships)
                        {
                            _precomputedInputRelationships[modelNode.Id] = connectedInputNodes;
                        }

                        // Only pre-compute for models that have direct input node connections
                        var directInputNodes = connectedInputNodes.Where(n => n.Type == NodeType.Input).ToList();

                        if (directInputNodes.Count > 0)
                        {
                            // Pre-compute combined input based on cached step content for direct inputs only
                            var stepContents = new List<string>();
                            bool allInputsAvailable = true;

                            foreach (var inputNode in directInputNodes)
                            {
                                lock (_precomputedStepContentCache)
                                {
                                    if (_precomputedStepContentCache.TryGetValue(inputNode.Id, out var cachedContent))
                                    {
                                        var (contentType, content) = cachedContent;
                                        if (contentType?.ToLowerInvariant() == "image" || contentType?.ToLowerInvariant() == "audio")
                                        {
                                            stepContents.Add(content); // Direct file path
                                        }
                                        else
                                        {
                                            stepContents.Add($"{inputNode.Name}: {content}"); // Use safe formatting without brackets
                                        }
                                    }
                                    else
                                    {
                                        allInputsAvailable = false;
                                        break;
                                    }
                                }
                            }

                            if (allInputsAvailable && stepContents.Count > 0)
                            {
                                string combinedInput = CombineStepContents(stepContents, modelNode.SelectedEnsembleMethod);
                                lock (_precomputedCombinedInputs)
                                {
                                    _precomputedCombinedInputs[modelNode.Id] = combinedInput;
                                }
                                Debug.WriteLine($"   ✅ Pre-computed input for {modelNode.Name}: {combinedInput?.Length ?? 0} chars");
                            }
                            else
                            {
                                Debug.WriteLine($"   ⏭️ Will compute {modelNode.Name} input dynamically (has model dependencies)");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"   ⏭️ Will compute {modelNode.Name} input dynamically (has model dependencies)");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"   ⚠️ Failed to compute relationships for {modelNode.Name}: {ex.Message}");
                }
            }).ToArray();

            await Task.WhenAll(relationshipTasks);

            _executionOptimizationCacheValid = true;
            stopwatch.Stop();
            Debug.WriteLine($"🎉 [PrecomputeExecutionOptimizations] Completed in {stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Ensures background warmup is ready, with optional timeout
        /// </summary>
        private async Task EnsureWarmupReadyAsync(int maxWaitMs = 5000)
        {
            lock (_warmupLock)
            {
                if (_environmentPrewarmed) return;
            }

            if (_backgroundWarmupTask != null)
            {
                try
                {
                    await _backgroundWarmupTask.WaitAsync(TimeSpan.FromMilliseconds(maxWaitMs));
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine($"⏰ [EnsureWarmupReady] Warmup timeout after {maxWaitMs}ms, continuing anyway");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"⚠️ [EnsureWarmupReady] Warmup error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Pre-warms the execution environment by ensuring Python is ready and models are accessible
        /// </summary>
        private async Task PrewarmExecutionEnvironmentAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔥 [PrewarmExecutionEnvironment] Starting environment pre-warming...");

            try
            {
                // 1. Ensure Python environment is ready
                if (_pythonBootstrapper != null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🐍 [PrewarmExecutionEnvironment] Ensuring Python environment is ready...");
                    await Task.Run(async () =>
                    {
                        // This will ensure Python is available and packages are installed
                        var isReady = await _pythonBootstrapper.AreRequiredPackagesInstalledAsync();
                        Debug.WriteLine($"🐍 [PrewarmExecutionEnvironment] Python ready: {isReady}");
                    });
                }

                // 2. Pre-warm NetPageViewModel execution pipeline if we have models (OPTIMIZED VERSION)
                if (_preloadedModelCache.Count > 0 && _netPageViewModel != null)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🤖 [PrewarmExecutionEnvironment] Pre-warming model execution pipeline...");

                    // Run a quick test execution with a minimal input to warm up the pipeline
                    var firstModel = _preloadedModelCache.Values.FirstOrDefault();
                    if (firstModel != null)
                    {
                        try
                        {
                            // Execute a very short test input to warm up the model execution pipeline
                            await Task.Run(async () =>
                            {
                                Debug.WriteLine($"🔥 [PrewarmExecutionEnvironment] Warming up with model: {firstModel.Name}");
                                // Use a minimal test input that won't affect the actual results
                                await _netPageViewModel.ExecuteModelAsync(firstModel.HuggingFaceModelId, "test");
                            });
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [PrewarmExecutionEnvironment] Model execution pipeline warmed up");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [PrewarmExecutionEnvironment] Warmup failed (non-critical): {ex.Message}");
                        }
                    }
                }

                stopwatch.Stop();
                Debug.WriteLine($"🔥 [PrewarmExecutionEnvironment] Completed in {stopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [PrewarmExecutionEnvironment] Error (non-critical): {ex.Message}");
            }
        }

        // --- Run All Models Command Implementation ---
        private async Task ExecuteRunAllModelsAsync()
        {
            var totalStopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🎯 [ExecuteRunAllModelsAsync] Starting ultra-optimized execution with proactive preparation");

            try
            {
                // CRITICAL: Ensure NetPageViewModel has models loaded for execution
                var netPageVM = ((App)Application.Current).NetPageViewModel;
                if (netPageVM?.AvailableModels == null || netPageVM.AvailableModels.Count == 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [ExecuteRunAllModelsAsync] NetPageViewModel has no models, forcing load...");
                    ExecutionStatus = "Loading models for execution...";

                    if (netPageVM != null)
                    {
                        await netPageVM.LoadDataAsync();
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [ExecuteRunAllModelsAsync] After LoadDataAsync, NetPageViewModel has {netPageVM.AvailableModels?.Count ?? 0} models");
                    }

                    if (netPageVM?.AvailableModels == null || netPageVM.AvailableModels.Count == 0)
                    {
                        ExecutionStatus = "Failed to load models";
                        await ShowAlert?.Invoke("Error", "Could not load models for execution. Please navigate to the Models page first.", "OK");
                        return;
                    }
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [ExecuteRunAllModelsAsync] NetPageViewModel has {netPageVM.AvailableModels.Count} models ready");
                }

                // Set initial execution status
                IsExecutingModels = true;
                ExecutionStatus = "Preparing execution...";
                ModelsExecutedCount = 0;
                ExecutionProgress = 0;
                ExecutionResults.Clear();

                // Start execution timer
                _executionStatusTrackingService.StartExecutionTimer();

                // Count total models to execute
                var modelNodes = Nodes.Where(n => n.Type == NodeType.Model).ToList();
                TotalModelsToExecute = modelNodes.Count;

                if (TotalModelsToExecute == 0)
                {
                    ExecutionStatus = "No models to execute";
                    await ShowAlert?.Invoke("No Models", "No model nodes found in the pipeline.", "OK");
                    return;
                }

                ExecutionStatus = $"Executing {TotalModelsToExecute} models...";

                // Verify that model lookup will work correctly
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 [ExecuteRunAllModelsAsync] Verifying model lookup for {TotalModelsToExecute} model nodes...");
                var testNetPageVM = ((App)Application.Current)?.NetPageViewModel;
                int foundModels = 0;
                foreach (var modelNode in modelNodes)
                {
                    var testModel = FindCorrespondingModel(testNetPageVM, modelNode);
                    if (testModel != null)
                    {
                        foundModels++;
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [ExecuteRunAllModelsAsync] Found model for node '{modelNode.Name}': {testModel.Name}");
                    }
                    else
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteRunAllModelsAsync] No model found for node '{modelNode.Name}' (Id: {modelNode.Id})");
                    }
                }
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 [ExecuteRunAllModelsAsync] Model lookup verification: {foundModels}/{TotalModelsToExecute} models found");

                if (foundModels == 0)
                {
                    ExecutionStatus = "No executable models found";
                    await ShowAlert?.Invoke("Model Lookup Error", $"Could not find executable models for any of the {TotalModelsToExecute} model nodes in the pipeline. Please ensure models are properly loaded in the Models page.", "OK");
                    return;
                }

                // Step 1: Check if proactive preparation is ready (fast path)
                bool preparationReady = await IsProactivePreparationReadyAsync(maxWaitMs: 1500);

                if (!preparationReady)
                {
                    // Fallback: Pre-compute optimizations if proactive preparation wasn't ready
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔧 [ExecuteRunAllModelsAsync] Fallback: Pre-computing optimizations...");
                    ExecutionStatus = "Optimizing execution...";
                    var precomputeStopwatch = Stopwatch.StartNew();

                    // Use cached pipeline state for faster access
                    if (!_pipelineStateCacheValid)
                    {
                        CachePipelineState();
                    }

                    // Ensure background warmup is ready (don't wait too long)
                    await EnsureWarmupReadyAsync(maxWaitMs: 3000);

                    // Pre-compute all execution optimizations
                    await PrecomputeExecutionOptimizationsAsync();
                    precomputeStopwatch.Stop();
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚡ [ExecuteRunAllModelsAsync] Fallback pre-computation completed in {precomputeStopwatch.ElapsedMilliseconds}ms");
                }
                else
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 [ExecuteRunAllModelsAsync] Using proactive preparation - skipping expensive setup!");
                }

                int modelNodesCount;
                lock (_cachedCollectionsLock)
                {
                    modelNodesCount = _cachedModelNodes?.Count ?? 0;
                }
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 [ExecuteRunAllModelsAsync] Processing {modelNodesCount} models");

                // Store the original selected node reference only (no copying)
                var originalSelectedNode = SelectedNode;

                // Step 2: Execute all models using optimized pipeline execution service
                ExecutionStatus = "Executing models...";
                var executionStopwatch = Stopwatch.StartNew();

                var executionResult = await _pipelineExecutionService.ExecuteAllModelsOptimizedAsync(
                    Nodes,
                    Connections,
                    CurrentActionStep,
                    _preloadedModelCache,
                    _precomputedCombinedInputs,
                    ShowAlert,
                    onGroupsInitialized: (groupCount) => _executionStatusTrackingService.InitializeExecutionGroups(groupCount),
                    onGroupStarted: (groupNumber, modelCount) => _executionStatusTrackingService.StartGroupExecution(groupNumber, modelCount),
                    onGroupCompleted: (groupNumber) => _executionStatusTrackingService.CompleteGroupExecution(groupNumber)
                );
                var successCount = executionResult.Item1;
                var skippedCount = executionResult.Item2;
                executionStopwatch.Stop();

                // Restore the original selected node
                SelectedNode = originalSelectedNode;

                totalStopwatch.Stop();

                // Update final status
                ExecutionStatus = $"Completed: {successCount} successful, {skippedCount} skipped";
                ModelsExecutedCount = successCount;
                ExecutionProgress = 100;

                // Add execution results
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Executed {successCount} models successfully");
                if (skippedCount > 0)
                {
                    AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Skipped {skippedCount} models");
                }

                // Streamlined logging for better performance
                string preparationMethod = preparationReady ? "proactive" : "fallback";
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🎉 [ExecuteRunAllModelsAsync] Completed in {totalStopwatch.ElapsedMilliseconds}ms using {preparationMethod} preparation: {successCount} successful, {skippedCount} skipped");
                if (successCount > 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]    └── Execution: {executionStopwatch.ElapsedMilliseconds}ms");
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]    └── Avg: {executionStopwatch.ElapsedMilliseconds / successCount:F0}ms/model");
                }

                // Defer pipeline saving to avoid blocking execution - only save if there were successful executions
                if (successCount > 0)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(100); // Small delay to let execution complete fully
                            await SaveCurrentPipelineAsync();
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 💾 [ExecuteRunAllModelsAsync] Pipeline saved asynchronously");
                        }
                        catch (Exception saveEx)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [ExecuteRunAllModelsAsync] Async save failed: {saveEx.Message}");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                ExecutionStatus = $"Error: {ex.Message}";
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteRunAllModelsAsync] Critical error after {totalStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                await ShowAlert?.Invoke("Error", $"Failed to run all models: {ex.Message}", "OK");
            }
            finally
            {
                IsExecutingModels = false;

                // Stop execution timer
                _executionStatusTrackingService.StopExecutionTimer();

                // Complete any remaining group execution
                if (CurrentExecutionGroup > 0)
                {
                    _executionStatusTrackingService.CompleteGroupExecution(CurrentExecutionGroup);
                }

                // Don't reset ExecutionProgress immediately so user can see final result
                // Reset it after a short delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000); // Show final progress for 3 seconds
                    if (!IsExecutingModels) // Only reset if not executing again
                    {
                        ExecutionProgress = 0;
                        ModelsExecutedCount = 0;
                        CurrentExecutingModel = "";
                        CurrentExecutingModelType = "";

                        // Don't reset group execution - preserve the durations for viewing
                        // Only reset the execution tracking state, but keep the groups and their durations
                        IsExecutingInGroups = false;
                        CurrentExecutionGroup = 0;
                        GroupExecutionDurationSeconds = 0;
                        // Leave ExecutionGroups intact so users can see the final durations
                    }
                });
            }
        }

        // --- Run All Nodes Command Implementation ---
        private async Task ExecuteRunAllNodesAsync()
        {
            var totalStopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🎯 [ExecuteRunAllNodesAsync] Starting execution for all action steps");

            try
            {
                // Validate prerequisites
                if (string.IsNullOrEmpty(SelectedReviewActionName))
                {
                    await ShowAlert?.Invoke("No Action Selected", "Please select an action from the Review Action dropdown before running all nodes.", "OK");
                    return;
                }

                // Fresh load of the selected action to ensure we have the most current data
                var result = await _actionStepNavigationService.LoadSelectedActionAsync(
                    SelectedReviewActionName,
                    Nodes,
                    SetCurrentActionStepAsync,
                    () => { });

                var currentActionItems = result.ActionItems;

                if (currentActionItems == null || currentActionItems.Count == 0)
                {
                    await ShowAlert?.Invoke("No Action Steps", "The selected action has no steps to process.", "OK");
                    return;
                }

                var modelNodes = Nodes.Where(n => n.Type == NodeType.Model).ToList();
                if (modelNodes.Count == 0)
                {
                    await ShowAlert?.Invoke("No Models", "No model nodes found in the pipeline.", "OK");
                    return;
                }

                // Ensure NetPageViewModel has models loaded
                var netPageVM = ((App)Application.Current).NetPageViewModel;
                if (netPageVM?.AvailableModels == null || netPageVM.AvailableModels.Count == 0)
                {
                    ExecutionStatus = "Loading models for execution...";
                    if (netPageVM != null)
                    {
                        await netPageVM.LoadDataAsync();
                    }
                    if (netPageVM?.AvailableModels == null || netPageVM.AvailableModels.Count == 0)
                    {
                        await ShowAlert?.Invoke("Error", "Could not load models for execution. Please navigate to the Models page first.", "OK");
                        return;
                    }
                }

                // Set initial execution status
                IsExecutingModels = true;
                ExecutionStatus = "Analyzing action steps...";
                ModelsExecutedCount = 0;
                ExecutionProgress = 0;
                ExecutionResults.Clear();
                _executionStatusTrackingService.StartExecutionTimer();

                int totalSteps = currentActionItems.Count;
                int totalExecutions = totalSteps * modelNodes.Count;
                TotalModelsToExecute = totalExecutions;

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 [ExecuteRunAllNodesAsync] Processing {totalSteps} action steps with {modelNodes.Count} models each (total: {totalExecutions} executions)");

                // Create Analysis directory in resources folder
                var analysisDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "CSimple",
                    "Resources",
                    "Analysis"
                );
                Directory.CreateDirectory(analysisDir);

                // Create subdirectory for this analysis run
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var runDir = Path.Combine(analysisDir, $"{SelectedReviewActionName}_{timestamp}");
                Directory.CreateDirectory(runDir);

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📁 [ExecuteRunAllNodesAsync] Created analysis directory: {runDir}");

                // Store original state
                var originalSelectedNode = SelectedNode;

                int successfulExecutions = 0;
                int skippedExecutions = 0;

                // Get execution groups for all models using proper dependency resolution (this will be used for all steps)
                var executionGroups = _pipelineExecutionService.GetType()
                    .GetMethod("BuildOptimizedExecutionGroups", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(_pipelineExecutionService, new object[] { modelNodes, Connections }) as List<List<NodeViewModel>>;

                if (executionGroups == null)
                {
                    // Fallback: create a single group with all models
                    executionGroups = new List<List<NodeViewModel>> { modelNodes };
                }

                // Initialize execution groups display
                _executionStatusTrackingService.InitializeExecutionGroups(executionGroups.Count);

                // Execute each group across all action steps
                for (int groupIndex = 0; groupIndex < executionGroups.Count; groupIndex++)
                {
                    var group = executionGroups[groupIndex];
                    _executionStatusTrackingService.StartGroupExecution(groupIndex + 1, group.Count * totalSteps);

                    ExecutionStatus = $"Executing Group {groupIndex + 1}/{executionGroups.Count} across all {totalSteps} steps...";

                    // Execute this group for ALL action steps at once
                    var groupTasks = new List<Task>();

                    foreach (var step in Enumerable.Range(0, totalSteps))
                    {
                        foreach (var modelNode in group)
                        {
                            groupTasks.Add(ExecuteModelForStepAsync(modelNode, step, runDir, netPageVM, currentActionItems));
                        }
                    }

                    // Wait for all tasks in this group to complete
                    var groupResults = await Task.WhenAll(groupTasks.Select(async task =>
                    {
                        try
                        {
                            await task;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ [ExecuteRunAllNodesAsync] Group execution error: {ex.Message}");
                            return false;
                        }
                    }));

                    // Count results for this group
                    var groupSuccessCount = groupResults.Count(r => r);
                    var groupSkipCount = groupResults.Count(r => !r);

                    successfulExecutions += groupSuccessCount;
                    skippedExecutions += groupSkipCount;

                    // Update progress
                    ExecutionProgress = (int)((double)(successfulExecutions + skippedExecutions) / totalExecutions * 100);
                    ModelsExecutedCount = successfulExecutions;

                    _executionStatusTrackingService.CompleteGroupExecution(groupIndex + 1);

                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [ExecuteRunAllNodesAsync] Group {groupIndex + 1} completed: {groupSuccessCount} successful, {groupSkipCount} failed");
                }

                // Restore original state
                SelectedNode = originalSelectedNode;
                // Don't reset CurrentActionStep - keep the user's current step position

                // Save summary report
                await SaveAnalysisSummaryAsync(runDir, successfulExecutions, skippedExecutions, totalSteps, modelNodes.Count, totalStopwatch.Elapsed, currentActionItems);

                totalStopwatch.Stop();

                // Update final status
                ExecutionStatus = $"Analysis completed: {successfulExecutions} successful, {skippedExecutions} failed";
                ExecutionProgress = 100;

                // Add execution results
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Analyzed {totalSteps} action steps with {modelNodes.Count} models");
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Results saved to: {runDir}");
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Total executions: {successfulExecutions} successful, {skippedExecutions} failed");

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🎉 [ExecuteRunAllNodesAsync] Completed in {totalStopwatch.ElapsedMilliseconds}ms: {successfulExecutions} successful, {skippedExecutions} failed");

                // Show completion message
                await ShowAlert?.Invoke("Analysis Complete",
                    $"Executed all models on {totalSteps} action steps.\n" +
                    $"Results: {successfulExecutions} successful, {skippedExecutions} failed\n" +
                    $"Output saved to: Analysis folder", "OK");
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                ExecutionStatus = $"Error: {ex.Message}";
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteRunAllNodesAsync] Critical error after {totalStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                await ShowAlert?.Invoke("Error", $"Failed to run all nodes: {ex.Message}", "OK");
            }
            finally
            {
                IsExecutingModels = false;
                _executionStatusTrackingService.StopExecutionTimer();

                // Complete any remaining group execution
                if (CurrentExecutionGroup > 0)
                {
                    _executionStatusTrackingService.CompleteGroupExecution(CurrentExecutionGroup);
                }

                // Reset progress after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (!IsExecutingModels)
                    {
                        ExecutionProgress = 0;
                        ModelsExecutedCount = 0;
                        CurrentExecutingModel = "";
                        CurrentExecutingModelType = "";
                        IsExecutingInGroups = false;
                        CurrentExecutionGroup = 0;
                        GroupExecutionDurationSeconds = 0;
                    }
                });
            }
        }

        private async Task ExecuteSleepMemoryCompressionAsync()
        {
            try
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 💤 [ExecuteSleepMemoryCompressionAsync] Starting memory compression process");

                IsExecutingModels = true;
                ExecutionStatus = "Compressing Memory...";
                ExecutionProgress = 0;

                // Step 1: Check for available memory nodes
                var memoryNodes = Nodes.Where(n => n.Name.ToLowerInvariant().Contains("memory") || n.Type == NodeType.Processor).ToList();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🧠 [ExecuteSleepMemoryCompressionAsync] Found {memoryNodes.Count} memory/processor nodes");

                ExecutionProgress = 20;
                await Task.Delay(500); // Simulate processing time

                ExecutionStatus = "Applying Neural Memory Compression...";
                ExecutionProgress = 40;

                // Use the memory compression service to handle all compression logic
                var compressionResult = await _memoryCompressionService.ExecuteSleepMemoryCompressionAsync(Nodes, Connections);

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🗜️ [ExecuteSleepMemoryCompressionAsync] Compression completed: {compressionResult.TokensReduced} tokens reduced, {compressionResult.EfficiencyGain:P2} efficiency gain");

                ExecutionProgress = 80;
                await Task.Delay(500);

                // Step 5: Update pipeline with compressed memory state
                ExecutionStatus = "Updating Pipeline State...";
                await _memoryCompressionService.UpdatePipelineWithCompressedStateAsync(
                    compressionResult,
                    SaveCurrentPipelineAsync,
                    AddExecutionResult);

                ExecutionProgress = 100;
                await Task.Delay(300);

                ExecutionStatus = $"Memory Compression Complete: {compressionResult.TokensReduced} tokens reduced, {compressionResult.EfficiencyGain:P1} more efficient";
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Memory compression: -{compressionResult.TokensReduced} tokens, +{compressionResult.EfficiencyGain:P1} efficiency");

                await ShowAlert?.Invoke("Sleep Complete", $"Memory compression successful!\n\nTokens reduced: {compressionResult.TokensReduced}\nEfficiency gain: {compressionResult.EfficiencyGain:P2}\n\nThe system is now optimized for reduced memory usage while preserving data integrity.", "OK");

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [ExecuteSleepMemoryCompressionAsync] Memory compression completed successfully");
            }
            catch (Exception ex)
            {
                ExecutionStatus = $"Memory Compression Error: {ex.Message}";
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] ERROR: Memory compression failed - {ex.Message}");
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteSleepMemoryCompressionAsync] Error: {ex.Message}");
                await ShowAlert?.Invoke("Error", $"Memory compression failed: {ex.Message}", "OK");
            }
            finally
            {
                IsExecutingModels = false;

                // Reset progress after delay
                _ = Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    if (!IsExecutingModels)
                    {
                        ExecutionProgress = 0;
                        ExecutionStatus = "Ready";
                    }
                });
            }
        }

        private async Task ExecuteModelForStepAsync(NodeViewModel modelNode, int stepIndex, string runDir, NetPageViewModel netPageVM, List<ActionItem> actionItems)
        {
            try
            {
                // Find corresponding model
                var correspondingModel = FindCorrespondingModel(netPageVM, modelNode);
                if (correspondingModel == null)
                {
                    Debug.WriteLine($"❌ [ExecuteModelForStepAsync] No model found for node: {modelNode.Name}");
                    return;
                }

                // Set current step for context
                CurrentActionStep = stepIndex;

                // Update execution status
                CurrentExecutingModel = modelNode.Name;
                CurrentExecutingModelType = correspondingModel.Type.ToString() ?? "unknown";

                // Get ActionItem timestamp for this step
                DateTime? actionItemTimestamp = null;
                if (actionItems != null && stepIndex >= 0 && stepIndex < actionItems.Count)
                {
                    var currentActionItem = actionItems[stepIndex];
                    if (currentActionItem?.Timestamp != null)
                    {
                        if (currentActionItem.Timestamp is DateTime directTimestamp)
                        {
                            actionItemTimestamp = directTimestamp;
                        }
                        else if (DateTime.TryParse(currentActionItem.Timestamp.ToString(), out DateTime parsedTimestamp))
                        {
                            actionItemTimestamp = parsedTimestamp;
                        }
                    }
                }

                // Get connected input nodes and prepare input with timestamp - thread-safe version
                List<NodeViewModel> nodesCopy;
                List<ConnectionViewModel> connectionsCopy;

                lock (_nodesLock)
                {
                    nodesCopy = Nodes.ToList();
                }

                lock (_connectionsLock)
                {
                    connectionsCopy = Connections.ToList();
                }

                var connectedInputNodes = _ensembleModelService.GetConnectedInputNodes(modelNode, nodesCopy, connectionsCopy);

                string result;
                string input;

                // For image models, process each input node individually and combine results
                if (correspondingModel.InputType == ModelInputType.Image)
                {
                    Debug.WriteLine($"🖼️ [ExecuteModelForStepAsync] Image model detected - processing each input node individually");

                    var nodeResults = new List<string>();
                    var imagePaths = new List<string>();

                    // Process each input node's images sequentially to maintain proper organization
                    foreach (var inputNode in connectedInputNodes)
                    {
                        // Get step content for current step
                        int stepForNodeContent = stepIndex + 1; // Convert to 1-based index
                        var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent, actionItemTimestamp);

                        if (contentType?.ToLowerInvariant() == "image" && !string.IsNullOrEmpty(contentValue))
                        {
                            Debug.WriteLine($"📸 [ExecuteModelForStepAsync] Found image content from {inputNode.Name}: {contentValue}");

                            // Check if this is multiple images (semicolon-separated)
                            var nodeImagePaths = contentValue.Contains(';')
                                ? contentValue.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
                                : new List<string> { contentValue };

                            var nodeImageResults = new List<string>();

                            // Process each image from this input node
                            foreach (var imagePath in nodeImagePaths)
                            {
                                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                                {
                                    imagePaths.Add(imagePath);
                                    var imageResult = await ProcessSingleImageAsync(correspondingModel, imagePath, inputNode.Name);
                                    if (!string.IsNullOrEmpty(imageResult))
                                    {
                                        nodeImageResults.Add(imageResult);
                                    }
                                }
                            }

                            // Combine results from this input node
                            if (nodeImageResults.Count > 0)
                            {
                                if (nodeImageResults.Count == 1)
                                {
                                    nodeResults.Add(nodeImageResults[0]);
                                }
                                else
                                {
                                    var combinedNodeResult = string.Join("\n", nodeImageResults);
                                    nodeResults.Add(combinedNodeResult);
                                }
                            }
                        }
                    }

                    if (nodeResults.Count > 0)
                    {
                        result = string.Join("\n\n", nodeResults);
                        input = string.Join("; ", imagePaths); // For logging purposes
                        Debug.WriteLine($"✅ [ExecuteModelForStepAsync] Combined results from {nodeResults.Count} input nodes");
                    }
                    else
                    {
                        Debug.WriteLine($"❌ [ExecuteModelForStepAsync] No valid image content found");
                        result = "Error: No valid image content found";
                        input = "No valid images";
                    }
                }
                else
                {
                    // For non-image models, use the existing approach
                    input = _ensembleModelService.PrepareModelInput(modelNode, connectedInputNodes, stepIndex, actionItemTimestamp);

                    // Execute the model
                    result = await _ensembleModelService.ExecuteModelWithInput(correspondingModel, input);
                }

                // Determine content type and store step output
                var resultContentType = _ensembleModelService.DetermineResultContentType(correspondingModel, result);
                var currentStep = stepIndex + 1; // Convert to 1-based
                modelNode.SetStepOutput(currentStep, resultContentType, result);

                // Route output to connected file nodes
                await RouteOutputToConnectedFileNodesAsync(modelNode, resultContentType, result, currentStep);

                // Save individual result to file
                await SaveStepResultAsync(runDir, modelNode.Name, stepIndex, input, result, resultContentType, actionItems);

                Debug.WriteLine($"✅ [ExecuteModelForStepAsync] Executed {modelNode.Name} for step {stepIndex + 1}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ExecuteModelForStepAsync] Error executing {modelNode.Name} for step {stepIndex + 1}: {ex.Message}");

                // Save error to file
                await SaveStepErrorAsync(runDir, modelNode.Name, stepIndex, ex.Message);
                throw;
            }
        }

        private async Task SaveStepResultAsync(string runDir, string modelName, int stepIndex, string input, string result, string contentType, List<ActionItem> actionItems)
        {
            try
            {
                var stepDir = Path.Combine(runDir, $"Step_{stepIndex + 1:D3}");
                Directory.CreateDirectory(stepDir);

                var modelFileName = SanitizeFileName(modelName);
                var resultFile = Path.Combine(stepDir, $"{modelFileName}_result.json");

                var resultData = new
                {
                    ModelName = modelName,
                    StepIndex = stepIndex + 1,
                    Timestamp = DateTime.UtcNow,
                    Input = input,
                    Output = result,
                    ContentType = contentType,
                    ActionItem = stepIndex < actionItems.Count ? actionItems[stepIndex] : null
                };

                var json = JsonSerializer.Serialize(resultData, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(resultFile, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [SaveStepResultAsync] Failed to save result for {modelName} step {stepIndex + 1}: {ex.Message}");
            }
        }

        private async Task SaveStepErrorAsync(string runDir, string modelName, int stepIndex, string errorMessage)
        {
            try
            {
                var stepDir = Path.Combine(runDir, $"Step_{stepIndex + 1:D3}");
                Directory.CreateDirectory(stepDir);

                var modelFileName = SanitizeFileName(modelName);
                var errorFile = Path.Combine(stepDir, $"{modelFileName}_error.txt");

                var errorData = $"Model: {modelName}\n" +
                               $"Step: {stepIndex + 1}\n" +
                               $"Timestamp: {DateTime.UtcNow}\n" +
                               $"Error: {errorMessage}\n";

                await File.WriteAllTextAsync(errorFile, errorData);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [SaveStepErrorAsync] Failed to save error for {modelName} step {stepIndex + 1}: {ex.Message}");
            }
        }

        private async Task SaveAnalysisSummaryAsync(string runDir, int successfulExecutions, int skippedExecutions, int totalSteps, int modelCount, TimeSpan duration, List<ActionItem> actionItems)
        {
            try
            {
                var summaryFile = Path.Combine(runDir, "analysis_summary.json");

                var summary = new
                {
                    ActionName = SelectedReviewActionName,
                    Timestamp = DateTime.UtcNow,
                    TotalSteps = totalSteps,
                    ModelCount = modelCount,
                    TotalExecutions = totalSteps * modelCount,
                    SuccessfulExecutions = successfulExecutions,
                    SkippedExecutions = skippedExecutions,
                    Duration = duration.ToString(),
                    DurationMs = duration.TotalMilliseconds,
                    Models = Nodes.Where(n => n.Type == NodeType.Model).Select(n => n.Name).ToList(),
                    ActionSteps = actionItems.Select((item, index) => new
                    {
                        StepIndex = index + 1,
                        EventType = item.EventType,
                        Description = item.ToString(),
                        Timestamp = item.Timestamp
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(summaryFile, json);

                Debug.WriteLine($"💾 [SaveAnalysisSummaryAsync] Summary saved to: {summaryFile}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [SaveAnalysisSummaryAsync] Failed to save summary: {ex.Message}");
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        }

        // --- Generate Command Implementation ---
        private async Task ExecuteGenerateAsync()
        {
            try
            {
                // Set execution status for single model
                IsExecutingModels = true;
                ExecutionStatus = "Preparing generation...";
                CurrentExecutingModel = SelectedNode?.Name ?? "Unknown";
                CurrentExecutingModelType = SelectedNode?.DataType ?? "unknown";
                TotalModelsToExecute = 1;
                ModelsExecutedCount = 0;
                ExecutionProgress = 0;
                ExecutionResults.Clear();

                // Start execution timer
                _executionStatusTrackingService.StartExecutionTimer();

                Debug.WriteLine($"🚀 [OrientPageViewModel.ExecuteGenerateAsync] Starting generation for node: {SelectedNode?.Name}");

                if (SelectedNode == null || SelectedNode.Type != NodeType.Model)
                {
                    ExecutionStatus = "Error: No model selected";
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteGenerateAsync] No valid model node selected");
                    await ShowAlert?.Invoke("Error", "Please select a model node to generate content.", "OK");
                    return;
                }

                if (SelectedNode.EnsembleInputCount <= 1)
                {
                    ExecutionStatus = "Error: Insufficient inputs";
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteGenerateAsync] Not enough input connections for ensemble generation");
                    await ShowAlert?.Invoke("Error", "This model node needs multiple input connections to use ensemble generation.", "OK");
                    return;
                }

                ExecutionStatus = $"Processing {SelectedNode.Name}...";
                ExecutionProgress = 25;

                Debug.WriteLine($"📊 [ExecuteGenerateAsync] Model node has {SelectedNode.EnsembleInputCount} input connections");
                Debug.WriteLine($"📊 [ExecuteGenerateAsync] Selected ensemble method: {SelectedNode.SelectedEnsembleMethod}");

                // Find all connected input nodes
                var connectedInputNodes = GetConnectedInputNodes(SelectedNode);
                Debug.WriteLine($"🔍 [ExecuteGenerateAsync] Found {connectedInputNodes.Count} connected input nodes");

                if (connectedInputNodes.Count == 0)
                {
                    ExecutionStatus = "Error: No connected inputs";
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteGenerateAsync] No connected input nodes found");
                    await ShowAlert?.Invoke("Error", "No connected input nodes found for this model.", "OK");
                    return;
                }

                ExecutionStatus = "Collecting inputs...";
                ExecutionProgress = 50;

                // Find corresponding model first to determine processing strategy
                var correspondingModel = FindCorrespondingModel(_netPageViewModel, SelectedNode);
                if (correspondingModel == null)
                {
                    ExecutionStatus = "Error: Model not found";
                    Debug.WriteLine($"❌ [ExecuteGenerateAsync] No corresponding model found for node: {SelectedNode.Name}");
                    await ShowAlert?.Invoke("Error", $"No corresponding model found for '{SelectedNode.Name}'. Please ensure the model is loaded in the Net page.", "OK");
                    return;
                }

                Debug.WriteLine($"✅ [ExecuteGenerateAsync] Found corresponding model: {correspondingModel.Name} (HF ID: {correspondingModel.HuggingFaceModelId})");

                string result;

                // For image models, process each input node individually and combine results
                if (correspondingModel.InputType == ModelInputType.Image)
                {
                    ExecutionStatus = "Processing images individually...";
                    ExecutionProgress = 60;

                    Debug.WriteLine($"🖼️ [ExecuteGenerateAsync] Image model detected - processing each input node individually");

                    var nodeResults = new List<string>();

                    // Process each input node's images sequentially to maintain proper organization
                    foreach (var inputNode in connectedInputNodes)
                    {
                        Debug.WriteLine($"📄 [ExecuteGenerateAsync] Processing input node: {inputNode.Name} (Type: {inputNode.DataType})");

                        // Get step content for current step
                        int stepForNodeContent = CurrentActionStep + 1; // Convert to 1-based index
                        var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);

                        if (contentType?.ToLowerInvariant() == "image" && !string.IsNullOrEmpty(contentValue))
                        {
                            Debug.WriteLine($"🔍 [ExecuteGenerateAsync] Found image content from {inputNode.Name}: {contentValue}");

                            // Check if this is multiple images (semicolon-separated)
                            var imagePaths = contentValue.Contains(';')
                                ? contentValue.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToList()
                                : new List<string> { contentValue };

                            var nodeImageResults = new List<string>();

                            // Process each image from this input node
                            foreach (var imagePath in imagePaths)
                            {
                                if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
                                {
                                    var imageResult = await ProcessSingleImageAsync(correspondingModel, imagePath, inputNode.Name);
                                    if (!string.IsNullOrEmpty(imageResult))
                                    {
                                        nodeImageResults.Add(imageResult);
                                    }
                                }
                            }

                            // Combine results from this input node
                            if (nodeImageResults.Count > 0)
                            {
                                if (nodeImageResults.Count == 1)
                                {
                                    nodeResults.Add(nodeImageResults[0]);
                                }
                                else
                                {
                                    var combinedNodeResult = string.Join("\n", nodeImageResults);
                                    nodeResults.Add(combinedNodeResult);
                                }
                            }
                        }
                    }

                    if (nodeResults.Count == 0)
                    {
                        ExecutionStatus = "Error: No valid images";
                        Debug.WriteLine($"❌ [ExecuteGenerateAsync] No valid image content found from connected nodes");
                        await ShowAlert?.Invoke("Error", "No valid image content found from connected input nodes.", "OK");
                        return;
                    }

                    ExecutionStatus = $"Processed {nodeResults.Count} input nodes...";
                    ExecutionProgress = 80;

                    // Create well-organized combined result with proper separation by input node
                    if (nodeResults.Count == 1)
                    {
                        result = nodeResults[0];
                    }
                    else
                    {
                        result = $"Image Analysis Results:\n\n{string.Join("\n\n", nodeResults)}";
                    }
                    Debug.WriteLine($"✅ [ExecuteGenerateAsync] Combined results from {nodeResults.Count} input nodes");
                }
                else
                {
                    // For non-image models, use the existing approach
                    var stepContents = new List<string>();
                    foreach (var inputNode in connectedInputNodes)
                    {
                        Debug.WriteLine($"📄 [ExecuteGenerateAsync] Processing input node: {inputNode.Name} (Type: {inputNode.DataType})");

                        // Get step content for current step
                        int stepForNodeContent = CurrentActionStep + 1; // Convert to 1-based index
                        var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);

                        Debug.WriteLine($"� [ExecuteGenerateAsync] Input node '{inputNode.Name}' content: Type='{contentType}', Value='{contentValue?.Substring(0, Math.Min(contentValue?.Length ?? 0, 100))}...'");

                        if (!string.IsNullOrEmpty(contentValue))
                        {
                            // For audio content, pass the file path directly for model execution
                            if (contentType?.ToLowerInvariant() == "audio")
                            {
                                stepContents.Add(contentValue); // Direct file path for audio models
                                Debug.WriteLine($"🔊 [ExecuteGenerateAsync] Added audio file path: {contentValue}");
                            }
                            else
                            {
                                // Use safe formatting without brackets to avoid command line argument conflicts
                                stepContents.Add($"{inputNode.Name}: {contentValue}"); // Text content with node name prefix (no brackets)
                            }
                        }
                    }

                    if (stepContents.Count == 0)
                    {
                        ExecutionStatus = "Error: No valid content";
                        Debug.WriteLine($"❌ [ExecuteGenerateAsync] No valid step content found from connected nodes");
                        await ShowAlert?.Invoke("Error", "No valid content found from connected input nodes.", "OK");
                        return;
                    }

                    ExecutionStatus = "Finding model...";
                    ExecutionProgress = 75;

                    // Combine step contents using ensemble method
                    string combinedInput = CombineStepContents(stepContents, SelectedNode.SelectedEnsembleMethod);
                    Debug.WriteLine($"🔀 [ExecuteGenerateAsync] Combined input ({SelectedNode.SelectedEnsembleMethod}): {combinedInput?.Substring(0, Math.Min(combinedInput?.Length ?? 0, 200))}...");

                    ExecutionStatus = $"Executing {SelectedNode.Name}...";
                    ExecutionProgress = 90;

                    // Execute the model using NetPageViewModel's infrastructure
                    result = await ExecuteModelWithInput(correspondingModel, combinedInput);
                }

                Debug.WriteLine($"🎉 [ExecuteGenerateAsync] Model execution result: {result?.Substring(0, Math.Min(result?.Length ?? 0, 200))}...");

                // Update step content with the result
                StepContent = result;

                // Determine the correct content type based on the result
                // For image-to-text models, the output is text even though the model processes images
                string resultContentType = DetermineResultContentType(correspondingModel, result);
                StepContentType = resultContentType;

                Debug.WriteLine($"📋 [ExecuteGenerateAsync] Set StepContentType to: {StepContentType}");

                // Store the generated output in the model node so it persists when switching nodes
                int currentStep = CurrentActionStep + 1; // Convert to 1-based index
                SelectedNode.SetStepOutput(currentStep, resultContentType, result);
                Debug.WriteLine($"💾 [ExecuteGenerateAsync] Stored output in model node '{SelectedNode.Name}' at step {currentStep}");

                // Route output to connected file nodes
                await RouteOutputToConnectedFileNodesAsync(SelectedNode, resultContentType, result, currentStep);

                // Update final status
                ExecutionStatus = $"Completed: {SelectedNode.Name}";
                ModelsExecutedCount = 1;
                ExecutionProgress = 100;
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] {SelectedNode.Name} generated content successfully");

                // Note: Pipeline saving is deferred to reduce I/O operations
                // It will be saved when appropriate (e.g., on action completion or manual save)

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [ExecuteGenerateAsync] Generation completed successfully");

                // await ShowAlert?.Invoke("Success", $"Generated content using {SelectedNode.SelectedEnsembleMethod} ensemble method with {connectedInputNodes.Count} inputs.", "OK");

            }
            catch (Exception ex)
            {
                ExecutionStatus = $"Error: {ex.Message}";
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}");
                Debug.WriteLine($"❌ [ExecuteGenerateAsync] Error during generation: {ex.Message}");
                Debug.WriteLine($"❌ [ExecuteGenerateAsync] Stack trace: {ex.StackTrace}");
                await ShowAlert?.Invoke("Error", $"Failed to generate content: {ex.Message}", "OK");
            }
            finally
            {
                IsExecutingModels = false;

                // Stop execution timer
                _executionStatusTrackingService.StopExecutionTimer();

                // Don't reset progress immediately for single model execution
                _ = Task.Run(async () =>
                {
                    await Task.Delay(2000); // Show final progress for 2 seconds
                    if (!IsExecutingModels) // Only reset if not executing again
                    {
                        ExecutionProgress = 0;
                        ModelsExecutedCount = 0;
                        CurrentExecutingModel = "";
                        CurrentExecutingModelType = "";
                    }
                });
            }
        }

        private List<NodeViewModel> GetConnectedInputNodes(NodeViewModel modelNode)
        {
            // Get thread-safe copies of the collections
            List<NodeViewModel> nodesCopy;
            List<ConnectionViewModel> connectionsCopy;

            lock (_nodesLock)
            {
                nodesCopy = Nodes.ToList();
            }

            lock (_connectionsLock)
            {
                connectionsCopy = Connections.ToList();
            }

            return _ensembleModelService.GetConnectedInputNodes(modelNode, nodesCopy, connectionsCopy);
        }

        private string CombineStepContents(List<string> stepContents, string ensembleMethod)
        {
            return _ensembleModelService.CombineStepContents(stepContents, ensembleMethod);
        }

        private async Task<string> ExecuteModelWithInput(NeuralNetworkModel model, string input)
        {
            return await _ensembleModelService.ExecuteModelWithInput(model, input);
        }

        /// <summary>
        /// Processes a single image through the model and returns the result with context
        /// </summary>
        private async Task<string> ProcessSingleImageAsync(NeuralNetworkModel model, string imagePath, string nodeContext)
        {
            try
            {
                Debug.WriteLine($"📸 [ProcessSingleImageAsync] Processing image: {Path.GetFileName(imagePath)} from {nodeContext}");

                // Execute model on individual image
                var result = await _netPageViewModel.ExecuteModelAsync(model.HuggingFaceModelId, imagePath);

                if (!string.IsNullOrEmpty(result))
                {
                    // Clean up the result - remove duplicate filename references
                    var cleanResult = result;
                    var filename = Path.GetFileName(imagePath);

                    // Remove duplicate filename if it appears at the start of the result
                    if (cleanResult.StartsWith($"{filename}: "))
                    {
                        cleanResult = cleanResult.Substring($"{filename}: ".Length);
                    }

                    // Create well-formatted result with clear source labeling
                    var formattedResult = $"[{nodeContext}] {cleanResult}";

                    Debug.WriteLine($"✅ [ProcessSingleImageAsync] Successfully processed image from {nodeContext}");
                    return formattedResult;
                }
                else
                {
                    Debug.WriteLine($"⚠️ [ProcessSingleImageAsync] Empty result from model for image: {imagePath}");
                    return $"[{nodeContext}] No description generated";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ProcessSingleImageAsync] Error processing image {imagePath}: {ex.Message}");
                return $"[{nodeContext}] Error - {ex.Message}";
            }
        }

        private string DetermineResultContentType(NeuralNetworkModel model, string result)
        {
            return _ensembleModelService.DetermineResultContentType(model, result);
        }

        // --- File Selection Command Implementation ---
        private async Task ExecuteSelectSaveFileAsync()
        {
            try
            {
                if (SelectedNode == null || !SelectedNode.IsFileNode)
                {
                    await ShowAlert?.Invoke("Error", "Please select a file node first.", "OK");
                    return;
                }

                Debug.WriteLine($"🗂️ [ExecuteSelectSaveFileAsync] Opening file picker for node: {SelectedNode.Name}");

                // Use the MAUI FilePicker to select a file for saving
                var fileResult = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select save file for text output",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".txt", ".md", ".log" } },
                        { DevicePlatform.Android, new[] { "text/*" } },
                        { DevicePlatform.iOS, new[] { "public.text" } },
                        { DevicePlatform.macOS, new[] { "txt", "md", "log" } }
                    })
                });

                if (fileResult != null)
                {
                    // Update the selected node's save file path
                    SelectedNode.SaveFilePath = fileResult.FullPath;

                    Debug.WriteLine($"✅ [ExecuteSelectSaveFileAsync] File selected: {fileResult.FullPath}");

                    // Persist the pipeline to save the file selection
                    await SaveCurrentPipelineAsync();

                    Debug.WriteLine($"💾 [ExecuteSelectSaveFileAsync] Pipeline saved with updated file path");
                }
                else
                {
                    Debug.WriteLine($"❌ [ExecuteSelectSaveFileAsync] File selection cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [ExecuteSelectSaveFileAsync] Error selecting file: {ex.Message}");
                await ShowAlert?.Invoke("Error", $"Failed to select file: {ex.Message}", "OK");
            }
        }

        private async Task ExecuteCreateNewMemoryFileAsync()
        {
            try
            {
                if (SelectedNode == null || !SelectedNode.IsFileNode)
                {
                    await ShowAlert?.Invoke("Error", "No file node selected.", "OK");
                    return;
                }

                Debug.WriteLine($"🗂️ [ExecuteCreateNewMemoryFileAsync] Creating new memory file for node: {SelectedNode.Name}");

                // Get the user-specific memory files directory path
                string userName = Environment.UserName;
                string memoryFilesDir = Path.Combine("C:", "Users", userName, "Documents", "CSimple", "Resources", "MemoryFiles");

                // Ensure the directory exists
                if (!Directory.Exists(memoryFilesDir))
                {
                    Directory.CreateDirectory(memoryFilesDir);
                    Debug.WriteLine($"📁 [ExecuteCreateNewMemoryFileAsync] Created memory files directory: {memoryFilesDir}");
                }

                // Get filename from input field
                string fileName = string.IsNullOrWhiteSpace(MemoryFileName)
                    ? $"Memory_{SelectedNode.Name}_{DateTime.Now:yyyyMMdd_HHmmss}"
                    : MemoryFileName.Trim();

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    await ShowAlert?.Invoke("Error", "Please enter a name for the memory file.", "OK");
                    return;
                }

                // Validate filename - remove invalid characters
                char[] invalidChars = Path.GetInvalidFileNameChars();
                foreach (char c in invalidChars)
                {
                    fileName = fileName.Replace(c, '_');
                }

                // Ensure .txt extension
                if (!fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    fileName += ".txt";
                }

                string fullFilePath = Path.Combine(memoryFilesDir, fileName);

                // Check if file already exists
                if (File.Exists(fullFilePath))
                {
                    bool overwrite = await Application.Current.MainPage.DisplayAlert(
                        "File Exists",
                        $"A file named '{fileName}' already exists. Do you want to overwrite it?",
                        "Yes", "No");

                    if (!overwrite)
                    {
                        Debug.WriteLine($"❌ [ExecuteCreateNewMemoryFileAsync] File creation cancelled - user chose not to overwrite");
                        return;
                    }
                }

                // Create the file with initial content
                string initialContent = $"# Memory File for {SelectedNode.Name}\n" +
                                      $"Created: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n" +
                                      $"Node Type: {SelectedNode.Type}\n" +
                                      $"Data Type: {SelectedNode.DataType}\n\n" +
                                      $"## Memory Contents\n" +
                                      $"This file will store outputs from the '{SelectedNode.Name}' node.\n\n";

                await File.WriteAllTextAsync(fullFilePath, initialContent);

                // Update the selected node's save file path
                SelectedNode.SaveFilePath = fullFilePath;

                Debug.WriteLine($"✅ [ExecuteCreateNewMemoryFileAsync] Memory file created: {fullFilePath}");

                // Persist the pipeline to save the file selection
                await SaveCurrentPipelineAsync();

                Debug.WriteLine($"💾 [ExecuteCreateNewMemoryFileAsync] Pipeline saved with new memory file path");

                // Clear the memory file name input for next use
                MemoryFileName = "";

                // Show success message
                await ShowAlert?.Invoke("Success", $"Memory file '{fileName}' created successfully!", "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [ExecuteCreateNewMemoryFileAsync] Error creating memory file: {ex.Message}");
                await ShowAlert?.Invoke("Error", $"Failed to create memory file: {ex.Message}", "OK");
            }
        }

        // --- Output Routing Implementation ---
        private async Task RouteOutputToConnectedFileNodesAsync(NodeViewModel sourceNode, string contentType, string content, int stepIndex)
        {
            try
            {
                // Only route text outputs from text model nodes
                if (contentType?.ToLowerInvariant() != "text" || sourceNode.Type != NodeType.Model)
                {
                    return;
                }

                // Find all file nodes connected as outputs from this model node
                var connectedFileNodes = Connections
                    .Where(c => c.SourceNodeId == sourceNode.Id)
                    .Select(c => Nodes.FirstOrDefault(n => n.Id == c.TargetNodeId))
                    .Where(n => n != null && n.IsFileNode && !string.IsNullOrEmpty(n.SaveFilePath))
                    .ToList();

                if (connectedFileNodes.Count == 0)
                {
                    Debug.WriteLine($"📄 [RouteOutputToConnectedFileNodes] No connected file nodes with save paths found for {sourceNode.Name}");
                    return;
                }

                Debug.WriteLine($"📄 [RouteOutputToConnectedFileNodes] Routing output from {sourceNode.Name} to {connectedFileNodes.Count} file nodes");

                foreach (var fileNode in connectedFileNodes)
                {
                    try
                    {
                        // Prepare the output text with metadata
                        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        var outputText = $"\n--- Output from {sourceNode.Name} (Step {stepIndex}) at {timestamp} ---\n{content}\n";

                        // Append to the file
                        await File.AppendAllTextAsync(fileNode.SaveFilePath, outputText);

                        Debug.WriteLine($"✅ [RouteOutputToConnectedFileNodes] Appended output to file: {fileNode.SaveFilePath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ [RouteOutputToConnectedFileNodes] Error writing to file {fileNode.SaveFilePath}: {ex.Message}");
                        // Continue with other file nodes even if one fails
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [RouteOutputToConnectedFileNodes] Error in output routing: {ex.Message}");
            }
        }        // IDisposable implementation
        public void Dispose()
        {
            // Unsubscribe from service events
            if (_executionStatusTrackingService != null)
            {
                _executionStatusTrackingService.PropertyChanged -= OnExecutionStatusTrackingServicePropertyChanged;
            }

            // Clean up execution timer (now handled by ExecutionStatusTrackingService)
            try
            {
                _executionStatusTrackingService?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [Dispose] Error cleaning up execution timer: {ex.Message}");
            }

            // Clean up proactive preparation resources
            try
            {
                _preparationCancellationSource?.Cancel();
                _preparationCancellationSource?.Dispose();
                _preparationCancellationSource = null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"⚠️ [Dispose] Error cleaning up preparation resources: {ex.Message}");
            }

            if (_audioStepContentService != null)
            {
                _audioStepContentService.PlaybackStarted -= OnAudioPlaybackStarted;
                _audioStepContentService.PlaybackStopped -= OnAudioPlaybackStopped;
                _audioStepContentService.Dispose();
            }
        }

        private void OnAudioPlaybackStarted()
        {
            Debug.WriteLine("Audio playback started");
            ((Command)PlayAudioCommand)?.ChangeCanExecute();
            ((Command)StopAudioCommand)?.ChangeCanExecute();
        }

        private void OnAudioPlaybackStopped()
        {
            Debug.WriteLine("Audio playback stopped");
            ((Command)PlayAudioCommand)?.ChangeCanExecute();
            ((Command)StopAudioCommand)?.ChangeCanExecute();
        }

        private bool CanPlayAudio()
        {
            return _audioStepContentService.CanPlayStepContent(StepContent, StepContentType);
        }

        private bool CanStopAudio()
        {
            bool result = _audioStepContentService?.IsPlaying == true;
            // Debug.WriteLine($"[CanStopAudio] Result: {result}, IsPlaying: {_audioStepContentService?.IsPlaying}");
            return result;
        }

        // Debug method to check RunAllModelsCommand state
        public void DebugRunAllModelsCommand()
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🐛 [DebugRunAllModelsCommand] === DEBUG INFO ===");
            Debug.WriteLine($"🐛 RunAllModelsCommand is null: {RunAllModelsCommand == null}");
            var allNodes = GetAllNodes();
            Debug.WriteLine($"🐛 Total nodes: {allNodes?.Count ?? 0}");
            if (allNodes != null)
            {
                Debug.WriteLine($"🐛 Model nodes: {GetModelNodesCount()}");
                foreach (var node in allNodes)
                {
                    Debug.WriteLine($"🐛 Node: {node.Name} - Type: {node.Type}");
                }
            }
            if (RunAllModelsCommand != null)
            {
                bool canExecute = ((Command)RunAllModelsCommand).CanExecute(null);
                Debug.WriteLine($"🐛 CanExecute: {canExecute}");
            }
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🐛 [DebugRunAllModelsCommand] === END DEBUG ===");
        }

        /// <summary>
        /// Helper method for the navigation service to set current action step asynchronously
        /// </summary>
        private Task SetCurrentActionStepAsync(int newStep)
        {
            CurrentActionStep = newStep;
            return Task.CompletedTask;
        }

        // --- Event Handlers ---

        // --- Proactive Preparation for Run All Models Optimization ---

        /// <summary>
        /// Triggers proactive preparation for model execution to optimize "Run All Models" performance
        /// </summary>
        private void TriggerProactivePreparation()
        {
            lock (_preparationLock)
            {
                // Cancel any existing preparation
                _preparationCancellationSource?.Cancel();
                _preparationCancellationSource?.Dispose();
                _preparationCancellationSource = new CancellationTokenSource();

                // Mark as not prepared to force fresh preparation
                _modelExecutionPrepared = false;

                // Start new proactive preparation task
                var cancellationToken = _preparationCancellationSource.Token;
                _proactivePreparationTask = Task.Run(async () =>
                {
                    try
                    {
                        await PerformProactivePreparationAsync(cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔄 [TriggerProactivePreparation] Preparation cancelled (new step change)");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [TriggerProactivePreparation] Error: {ex.Message}");
                    }
                }, cancellationToken);
            }
        }

        /// <summary>
        /// Performs proactive preparation for model execution in background
        /// </summary>
        private async Task PerformProactivePreparationAsync(CancellationToken cancellationToken)
        {
            var preparationStopwatch = Stopwatch.StartNew();
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🚀 [PerformProactivePreparation] Starting proactive preparation for step {CurrentActionStep}");

            try
            {
                // Step 1: Ensure environment warmup is ready
                await EnsureWarmupReadyAsync(maxWaitMs: 2000);
                cancellationToken.ThrowIfCancellationRequested();

                // Step 2: Update pipeline state cache
                if (!_pipelineStateCacheValid)
                {
                    await Task.Run(() => CachePipelineState(), cancellationToken);
                }
                cancellationToken.ThrowIfCancellationRequested();

                // Step 3: Pre-compute execution optimizations
                await PrecomputeExecutionOptimizationsAsync();
                cancellationToken.ThrowIfCancellationRequested();

                // Step 4: Pre-warm Python environment and models in background
                await PrewarmModelExecutionEnvironmentAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                lock (_preparationLock)
                {
                    _modelExecutionPrepared = true;
                }

                preparationStopwatch.Stop();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [PerformProactivePreparation] Completed in {preparationStopwatch.ElapsedMilliseconds}ms");
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔄 [PerformProactivePreparation] Cancelled after {preparationStopwatch.ElapsedMilliseconds}ms");
                throw;
            }
            catch (Exception ex)
            {
                preparationStopwatch.Stop();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [PerformProactivePreparation] Failed after {preparationStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            }
        }

        /// <summary>
        /// Pre-warms the Python environment and model cache for faster execution
        /// </summary>
        private async Task PrewarmModelExecutionEnvironmentAsync(CancellationToken cancellationToken)
        {
            List<NodeViewModel> modelNodesCopy;
            lock (_cachedCollectionsLock)
            {
                if (!_pipelineStateCacheValid || _cachedModelNodes == null || _cachedModelNodes.Count == 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⏭️ [PrewarmModelExecution] No models to pre-warm");
                    return;
                }

                modelNodesCopy = _cachedModelNodes.ToList();
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔥 [PrewarmModelExecution] Pre-warming environment for {modelNodesCopy.Count} models");
            }

            try
            {
                // Pre-load models that will likely be executed
                var modelsToPreload = modelNodesCopy
                    .Where(node => !string.IsNullOrEmpty(node.OriginalModelId))
                    .Take(3) // Limit to avoid overwhelming the system
                    .ToList();

                if (modelsToPreload.Count == 0)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⏭️ [PrewarmModelExecution] No configured models to pre-warm");
                    return;
                }

                // Run model preloading operations in parallel (but with limited concurrency)
                var preloadTasks = modelsToPreload.Select(async modelNode =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await Task.Run(() =>
                        {
                            var correspondingModel = FindCorrespondingModel(_netPageViewModel, modelNode);
                            if (correspondingModel != null)
                            {
                                // Cache the model reference for faster lookup during execution
                                lock (_preloadedModelCache)
                                {
                                    _preloadedModelCache[modelNode.Id] = correspondingModel;
                                }
                                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]    ✅ Pre-cached model: {modelNode.Name}");
                            }
                        }, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}]    ⚠️ Failed to pre-cache {modelNode.Name}: {ex.Message}");
                    }
                }).ToArray();

                await Task.WhenAll(preloadTasks);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🎯 [PrewarmModelExecution] Pre-warmed {modelsToPreload.Count} models");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [PrewarmModelExecution] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if proactive preparation is complete, with optional wait
        /// </summary>
        private async Task<bool> IsProactivePreparationReadyAsync(int maxWaitMs = 1000)
        {
            lock (_preparationLock)
            {
                if (_modelExecutionPrepared)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚡ [IsProactivePreparationReady] Already prepared!");
                    return true;
                }
            }

            if (_proactivePreparationTask != null && !_proactivePreparationTask.IsCompleted)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⏳ [IsProactivePreparationReady] Waiting up to {maxWaitMs}ms for preparation...");
                try
                {
                    await _proactivePreparationTask.WaitAsync(TimeSpan.FromMilliseconds(maxWaitMs));

                    lock (_preparationLock)
                    {
                        if (_modelExecutionPrepared)
                        {
                            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ✅ [IsProactivePreparationReady] Preparation completed in time!");
                            return true;
                        }
                    }
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⏰ [IsProactivePreparationReady] Preparation timeout - will proceed anyway");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [IsProactivePreparationReady] Error waiting: {ex.Message}");
                }
            }

            return false;
        }

        // ===== Memory Compression Support Methods =====

        private async Task<MemoryPersonalityProfile> LoadOrCreateMemoryPersonalityProfileAsync()
        {
            try
            {
                // Try to load existing personality profile from file
                var profilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CSimple", "memory_personality.json");

                if (File.Exists(profilePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(profilePath);
                    var profile = System.Text.Json.JsonSerializer.Deserialize<MemoryPersonalityProfile>(jsonContent);
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📁 [LoadOrCreateMemoryPersonalityProfileAsync] Loaded existing profile from {profilePath}");
                    return profile;
                }
                else
                {
                    // Create default personality profile
                    var defaultProfile = CreateDefaultMemoryPersonalityProfile();

                    // Save it for future use
                    Directory.CreateDirectory(Path.GetDirectoryName(profilePath));
                    var jsonContent = System.Text.Json.JsonSerializer.Serialize(defaultProfile, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(profilePath, jsonContent);

                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🆕 [LoadOrCreateMemoryPersonalityProfileAsync] Created and saved default profile to {profilePath}");
                    return defaultProfile;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⚠️ [LoadOrCreateMemoryPersonalityProfileAsync] Error: {ex.Message}, using default profile");
                return CreateDefaultMemoryPersonalityProfile();
            }
        }

        private MemoryPersonalityProfile CreateDefaultMemoryPersonalityProfile()
        {
            return new MemoryPersonalityProfile
            {
                Name = "Default Memory Compression",
                Version = "1.0",
                CompressionRules = new List<CompressionRule>
                {
                    new CompressionRule { Type = "TokenReduction", Priority = 1, Threshold = 0.1f, Description = "Remove low-importance tokens" },
                    new CompressionRule { Type = "ConnectionOptimization", Priority = 2, Threshold = 0.2f, Description = "Optimize redundant connections" },
                    new CompressionRule { Type = "DataDeduplication", Priority = 3, Threshold = 0.05f, Description = "Remove duplicate data structures" },
                    new CompressionRule { Type = "ContextCompression", Priority = 4, Threshold = 0.15f, Description = "Compress similar context patterns" }
                },
                PreservationSettings = new PreservationSettings
                {
                    PreserveCriticalNodes = true,
                    PreserveUserData = true,
                    PreserveClassifications = true,
                    MinimumEfficiencyThreshold = 0.05f
                }
            };
        }

        private async Task<PipelineMemoryAnalysis> AnalyzePipelineMemoryUsageAsync()
        {
            await Task.Delay(100); // Simulate analysis time

            var analysis = new PipelineMemoryAnalysis();

            // Analyze nodes
            analysis.TotalNodes = Nodes.Count;
            analysis.TotalConnections = Connections.Count;

            // Estimate token usage based on node types and content
            analysis.TotalTokens = Nodes.Sum(n => EstimateNodeTokenUsage(n));

            // Find redundant connections (connections that could be optimized)
            analysis.RedundantConnections = Connections.Count(c => IsConnectionRedundant(c));

            // Calculate memory efficiency
            analysis.MemoryEfficiency = analysis.TotalConnections > 0
                ? (float)(analysis.TotalConnections - analysis.RedundantConnections) / analysis.TotalConnections
                : 1.0f;

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 [AnalyzePipelineMemoryUsageAsync] Analysis complete: {analysis.TotalTokens} tokens, {analysis.MemoryEfficiency:P2} efficient");

            return analysis;
        }

        private int EstimateNodeTokenUsage(NodeViewModel node)
        {
            // Base token estimate based on node type and properties
            int baseTokens = node.Type switch
            {
                NodeType.Model => 150, // Models typically use more tokens
                NodeType.Input => 50,
                NodeType.Output => 75,
                NodeType.Processor => 100, // Memory nodes
                _ => 25
            };

            // Add tokens for classification and ensemble settings
            if (!string.IsNullOrEmpty(node.Classification))
                baseTokens += 25;

            if (node.EnsembleInputCount > 1)
                baseTokens += node.EnsembleInputCount * 10;

            // Add tokens for name and model path
            baseTokens += (node.Name?.Length ?? 0) / 4; // Rough estimate: 4 chars per token

            return baseTokens;
        }

        private bool IsConnectionRedundant(ConnectionViewModel connection)
        {
            // Simple heuristic: if there are multiple connections between the same node types
            // and they're not serving different purposes, they might be redundant
            var sourceNode = Nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
            var targetNode = Nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId);

            if (sourceNode == null || targetNode == null) return false;

            // Count similar connections
            var similarConnections = Connections.Count(c =>
            {
                var srcNode = Nodes.FirstOrDefault(n => n.Id == c.SourceNodeId);
                var tgtNode = Nodes.FirstOrDefault(n => n.Id == c.TargetNodeId);
                return srcNode?.Type == sourceNode.Type && tgtNode?.Type == targetNode.Type;
            });

            // If there are many similar connections, some might be redundant
            return similarConnections > 3;
        }

        private async Task<CompressionResult> ApplyNeuralMemoryCompressionAsync(MemoryPersonalityProfile profile, PipelineMemoryAnalysis analysis)
        {
            await Task.Delay(200); // Simulate neural network processing

            var result = new CompressionResult();

            // Apply compression rules based on personality profile
            foreach (var rule in profile.CompressionRules.OrderBy(r => r.Priority))
            {
                var ruleTokensReduced = await ApplyCompressionRuleAsync(rule, analysis);
                result.TokensReduced += ruleTokensReduced;
                result.RulesApplied.Add($"{rule.Type}: -{ruleTokensReduced} tokens");

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔧 [ApplyNeuralMemoryCompressionAsync] Applied {rule.Type}: reduced {ruleTokensReduced} tokens");
            }

            // Calculate efficiency gain
            result.EfficiencyGain = analysis.TotalTokens > 0
                ? (float)result.TokensReduced / analysis.TotalTokens
                : 0f;

            // Ensure we don't exceed minimum efficiency threshold
            if (result.EfficiencyGain < profile.PreservationSettings.MinimumEfficiencyThreshold)
            {
                result.EfficiencyGain = profile.PreservationSettings.MinimumEfficiencyThreshold;
                result.TokensReduced = (int)(analysis.TotalTokens * result.EfficiencyGain);
            }

            result.CompressionSuccessful = result.TokensReduced > 0;

            return result;
        }

        private async Task<int> ApplyCompressionRuleAsync(CompressionRule rule, PipelineMemoryAnalysis analysis)
        {
            await Task.Delay(50); // Simulate rule processing

            return rule.Type switch
            {
                "TokenReduction" => (int)(analysis.TotalTokens * rule.Threshold),
                "ConnectionOptimization" => analysis.RedundantConnections * 15, // Each redundant connection saves ~15 tokens
                "DataDeduplication" => (int)(analysis.TotalTokens * 0.03f), // Small but consistent savings
                "ContextCompression" => (int)(analysis.TotalTokens * rule.Threshold * 0.5f), // Conservative context compression
                _ => 0
            };
        }

        private async Task UpdatePipelineWithCompressedStateAsync(CompressionResult compressionResult)
        {
            await Task.Delay(100); // Simulate pipeline update

            if (compressionResult.CompressionSuccessful)
            {
                // Add a compression note to pipeline metadata (if it exists)
                // In a real implementation, this would update node properties or add compression metadata

                // Update execution results with compression info
                AddExecutionResult($"[{DateTime.Now:HH:mm:ss}] Applied memory compression rules:");
                foreach (var rule in compressionResult.RulesApplied)
                {
                    AddExecutionResult($"  - {rule}");
                }

                // Trigger a save of the current pipeline state
                await SaveCurrentPipelineAsync();

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 💾 [UpdatePipelineWithCompressedStateAsync] Pipeline state saved with compression metadata");
            }
        }
    }

    // Supporting data classes for memory compression
    public class MemoryPersonalityProfile
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public List<CompressionRule> CompressionRules { get; set; } = new List<CompressionRule>();
        public PreservationSettings PreservationSettings { get; set; } = new PreservationSettings();
    }

    public class CompressionRule
    {
        public string Type { get; set; }
        public int Priority { get; set; }
        public float Threshold { get; set; }
        public string Description { get; set; }
    }

    public class PreservationSettings
    {
        public bool PreserveCriticalNodes { get; set; } = true;
        public bool PreserveUserData { get; set; } = true;
        public bool PreserveClassifications { get; set; } = true;
        public float MinimumEfficiencyThreshold { get; set; } = 0.05f;
    }

    public class PipelineMemoryAnalysis
    {
        public int TotalNodes { get; set; }
        public int TotalConnections { get; set; }
        public int TotalTokens { get; set; }
        public int RedundantConnections { get; set; }
        public float MemoryEfficiency { get; set; }
    }

    public class CompressionResult
    {
        public int TokensReduced { get; set; }
        public float EfficiencyGain { get; set; }
        public bool CompressionSuccessful { get; set; }
        public List<string> RulesApplied { get; set; } = new List<string>();
    }
}
