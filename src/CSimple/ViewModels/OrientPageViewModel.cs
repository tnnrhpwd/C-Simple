using CSimple.Models;
using CSimple.Services;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text; // Added for StringBuilder
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


        // Temporary state for drawing connections
        internal NodeViewModel _temporaryConnectionState = null;

        // --- Commands ---
        public ICommand AddModelNodeCommand { get; }
        public ICommand DeleteSelectedNodeCommand { get; }
        public ICommand CreateNewPipelineCommand { get; }
        public ICommand RenamePipelineCommand { get; }
        public ICommand DeletePipelineCommand { get; }

        // --- Model Execution Status Properties ---
        private bool _isExecutingModels;
        public bool IsExecutingModels
        {
            get => _isExecutingModels;
            set => SetProperty(ref _isExecutingModels, value);
        }

        private string _executionStatus = "Ready";
        public string ExecutionStatus
        {
            get => _executionStatus;
            set => SetProperty(ref _executionStatus, value);
        }

        private int _executionProgress;
        public int ExecutionProgress
        {
            get => _executionProgress;
            set => SetProperty(ref _executionProgress, value);
        }

        private int _totalModelsToExecute;
        public int TotalModelsToExecute
        {
            get => _totalModelsToExecute;
            set => SetProperty(ref _totalModelsToExecute, value);
        }

        private int _modelsExecutedCount;
        public int ModelsExecutedCount
        {
            get => _modelsExecutedCount;
            set
            {
                if (SetProperty(ref _modelsExecutedCount, value))
                {
                    // Update progress percentage with safer calculation
                    if (TotalModelsToExecute > 0)
                    {
                        ExecutionProgress = Math.Min(100, (int)((double)value / TotalModelsToExecute * 100));
                    }
                    else
                    {
                        ExecutionProgress = value > 0 ? 100 : 0;
                    }
                }
            }
        }

        private string _currentExecutingModel = "";
        public string CurrentExecutingModel
        {
            get => _currentExecutingModel;
            set => SetProperty(ref _currentExecutingModel, value);
        }

        private string _currentExecutingModelType = "";
        public string CurrentExecutingModelType
        {
            get => _currentExecutingModelType;
            set => SetProperty(ref _currentExecutingModelType, value);
        }

        // Execution timing properties
        private DateTime _executionStartTime;
        private System.Timers.Timer _executionTimer;
        private double _executionDurationSeconds;

        public double ExecutionDurationSeconds
        {
            get => _executionDurationSeconds;
            set => SetProperty(ref _executionDurationSeconds, value);
        }

        public string ExecutionDurationDisplay
        {
            get
            {
                if (_executionDurationSeconds <= 0)
                    return "No cycle time";

                var timeSpan = TimeSpan.FromSeconds(_executionDurationSeconds);
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                else
                    return $"{timeSpan.TotalSeconds:F1}s";
            }
        }

        // Execution group tracking properties
        private bool _isExecutingInGroups;
        public bool IsExecutingInGroups
        {
            get => _isExecutingInGroups;
            set => SetProperty(ref _isExecutingInGroups, value);
        }

        private int _currentExecutionGroup;
        public int CurrentExecutionGroup
        {
            get => _currentExecutionGroup;
            set => SetProperty(ref _currentExecutionGroup, value);
        }

        private int _totalExecutionGroups;
        public int TotalExecutionGroups
        {
            get => _totalExecutionGroups;
            set => SetProperty(ref _totalExecutionGroups, value);
        }

        private DateTime _groupExecutionStartTime;
        private double _groupExecutionDurationSeconds;
        public double GroupExecutionDurationSeconds
        {
            get => _groupExecutionDurationSeconds;
            set => SetProperty(ref _groupExecutionDurationSeconds, value);
        }

        public string GroupExecutionDurationDisplay
        {
            get
            {
                if (_groupExecutionDurationSeconds <= 0)
                    return "0.0s";

                var timeSpan = TimeSpan.FromSeconds(_groupExecutionDurationSeconds);
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                else
                    return $"{timeSpan.TotalSeconds:F1}s";
            }
        }

        public ObservableCollection<ExecutionGroupInfo> ExecutionGroups { get; } = new ObservableCollection<ExecutionGroupInfo>();

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


        // --- UI Interaction Delegates ---
        public Func<string, string, string, Task> ShowAlert { get; set; }
        public Func<string, string, string, string[], Task<string>> ShowActionSheet { get; set; }

        // --- Constructor ---
        // Ensure FileService and PythonBootstrapper are injected
        public OrientPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, NetPageViewModel netPageViewModel, PythonBootstrapper pythonBootstrapper, NodeManagementService nodeManagementService, PipelineManagementService pipelineManagementService, ActionReviewService actionReviewService, EnsembleModelService ensembleModelService, ActionStepNavigationService actionStepNavigationService)
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
                bool canExecute = Nodes.Any(n => n.Type == NodeType.Model);
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔍 [RunAllModelsCommand.CanExecute] Checking: {canExecute} (Model nodes count: {Nodes.Count(n => n.Type == NodeType.Model)})");
                return canExecute;
            });

            // Initialize Audio commands
            PlayAudioCommand = new Command(() => PlayAudio(), CanPlayAudio);
            StopAudioCommand = new Command(() => StopAudio(), CanStopAudio);

            // Load available pipelines on initialization
            _ = LoadAvailablePipelinesAsync();

            // Initialize execution timer
            InitializeExecutionTimer();

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

            // 1. Ensure Python environment is ready in background
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

            // 2. Pre-warm file system caches by accessing common directories
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

        // --- Execution Timer Methods ---
        /// <summary>
        /// Initialize the execution timer for tracking model execution duration
        /// </summary>
        private void InitializeExecutionTimer()
        {
            _executionTimer = new System.Timers.Timer(100); // Update every 100ms for smooth display
            _executionTimer.Elapsed += OnExecutionTimerElapsed;
            _executionTimer.AutoReset = true;
            ExecutionDurationSeconds = 0;
        }

        /// <summary>
        /// Start the execution timer
        /// </summary>
        private void StartExecutionTimer()
        {
            _executionStartTime = DateTime.Now;
            ExecutionDurationSeconds = 0;
            _executionTimer?.Start();
            OnPropertyChanged(nameof(ExecutionDurationDisplay));
        }

        /// <summary>
        /// Stop the execution timer
        /// </summary>
        private void StopExecutionTimer()
        {
            _executionTimer?.Stop();
            OnPropertyChanged(nameof(ExecutionDurationDisplay));
        }

        /// <summary>
        /// Timer elapsed event handler to update execution duration
        /// </summary>
        private void OnExecutionTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            ExecutionDurationSeconds = (DateTime.Now - _executionStartTime).TotalSeconds;

            // Update group execution duration if executing in groups
            if (IsExecutingInGroups && CurrentExecutionGroup > 0)
            {
                GroupExecutionDurationSeconds = (DateTime.Now - _groupExecutionStartTime).TotalSeconds;

                // Update the current group's duration in the collection
                var currentGroup = ExecutionGroups.FirstOrDefault(g => g.IsCurrentlyExecuting);
                if (currentGroup != null)
                {
                    currentGroup.ExecutionDurationSeconds = GroupExecutionDurationSeconds;
                }
            }

            Microsoft.Maui.Controls.Application.Current?.Dispatcher.Dispatch(() =>
            {
                OnPropertyChanged(nameof(ExecutionDurationDisplay));
                OnPropertyChanged(nameof(GroupExecutionDurationDisplay));
            });
        }

        /// <summary>
        /// Initialize execution groups for tracking
        /// </summary>
        private void InitializeExecutionGroups(int groupCount)
        {
            ExecutionGroups.Clear();
            for (int i = 1; i <= groupCount; i++)
            {
                ExecutionGroups.Add(new ExecutionGroupInfo
                {
                    GroupNumber = i,
                    ModelCount = 0, // Will be updated when groups are actually processed
                    ExecutionDurationSeconds = 0,
                    IsCurrentlyExecuting = false,
                    IsCompleted = false
                });
            }

            TotalExecutionGroups = groupCount;
            IsExecutingInGroups = groupCount > 1;
            CurrentExecutionGroup = 0;
        }

        /// <summary>
        /// Start execution for a specific group
        /// </summary>
        private void StartGroupExecution(int groupNumber, int modelCount)
        {
            // Complete previous group if any
            if (CurrentExecutionGroup > 0)
            {
                CompleteGroupExecution(CurrentExecutionGroup);
            }

            CurrentExecutionGroup = groupNumber;
            _groupExecutionStartTime = DateTime.Now;
            GroupExecutionDurationSeconds = 0;

            // Update the group info
            var group = ExecutionGroups.FirstOrDefault(g => g.GroupNumber == groupNumber);
            if (group != null)
            {
                group.ModelCount = modelCount;
                group.IsCurrentlyExecuting = true;
                group.IsCompleted = false;
                group.ExecutionDurationSeconds = 0;
            }
        }

        /// <summary>
        /// Complete execution for a specific group
        /// </summary>
        private void CompleteGroupExecution(int groupNumber)
        {
            var group = ExecutionGroups.FirstOrDefault(g => g.GroupNumber == groupNumber);
            if (group != null)
            {
                // Capture the final duration before changing execution state
                if (group.IsCurrentlyExecuting && _groupExecutionStartTime != default)
                {
                    var finalDuration = (DateTime.Now - _groupExecutionStartTime).TotalSeconds;
                    group.ExecutionDurationSeconds = finalDuration;
                }

                group.IsCurrentlyExecuting = false;
                group.IsCompleted = true;
                // The final duration is now preserved in ExecutionDurationSeconds
            }
        }

        /// <summary>
        /// Reset group execution tracking
        /// </summary>
        private void ResetGroupExecution()
        {
            IsExecutingInGroups = false;
            CurrentExecutionGroup = 0;
            TotalExecutionGroups = 0;
            GroupExecutionDurationSeconds = 0;
            ExecutionGroups.Clear();
        }

        // --- Event Handlers ---
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

            // Get the NetPageViewModel and ensure it loads its models
            var netPageVM = ((App)Application.Current).NetPageViewModel;
            Debug.WriteLine($"InitializeAsync: Checking NetPageViewModel, HasModels: {netPageVM?.AvailableModels?.Count > 0}");

            if (netPageVM != null)
            {
                // Always call LoadDataAsync to ensure persistent models are loaded
                // This handles cases where OrientPage initializes before HomePage has loaded the models
                if (netPageVM.AvailableModels == null || netPageVM.AvailableModels.Count == 0)
                {
                    Debug.WriteLine("InitializeAsync: NetPageViewModel has no models yet, loading them first");
                    await netPageVM.LoadDataAsync();
                    Debug.WriteLine($"InitializeAsync: After LoadDataAsync, NetPageViewModel has {netPageVM.AvailableModels?.Count ?? 0} models");
                }
                else
                {
                    Debug.WriteLine("InitializeAsync: NetPageViewModel already has models, but ensuring persistent models are fully loaded");
                    // Even if models exist, ensure the persistent models loading process has completed
                    // This might be necessary if models were added in memory but persistent loading didn't complete
                    try
                    {
                        // Call LoadDataAsync to ensure the full initialization chain has run
                        await netPageVM.LoadDataAsync();
                        Debug.WriteLine($"InitializeAsync: After ensuring LoadDataAsync, NetPageViewModel has {netPageVM.AvailableModels?.Count ?? 0} models");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"InitializeAsync: Error during NetPageViewModel LoadDataAsync: {ex.Message}");
                    }
                }
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
            if (sourceNode != null && (sourceNode.Type == NodeType.Input || sourceNode.Type == NodeType.Model))
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

                // Check if connection already exists
                bool exists = Connections.Any(c =>
                    (c.SourceNodeId == _temporaryConnectionState.Id && c.TargetNodeId == targetNode.Id) ||
                    (c.SourceNodeId == targetNode.Id && c.TargetNodeId == _temporaryConnectionState.Id));

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

            // Update execution status after pipeline is loaded
            UpdateExecutionStatusFromPipeline();
        }

        // Change from protected to public to make it accessible from OrientPage
        public async Task SaveCurrentPipelineAsync()
        {
            await _pipelineManagementService.SaveCurrentPipelineAsync(CurrentPipelineName, Nodes, Connections);
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
            Nodes.Clear();
            Connections.Clear();
            SelectedNode = null;
            _temporaryConnectionState = null;
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

            if (!Nodes.Any() || !Connections.Any())
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
            foreach (var potentialFinalNode in Nodes.Where(n => n.Type == NodeType.Model && n.DataType == "text")) // Assume final is text
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
                // Fallback: Find *any* model connected from an interpreter if no text model found
                finalModel = Nodes.FirstOrDefault(n => n.Type == NodeType.Model && Connections.Any(c => c.TargetNodeId == n.Id && interpreterNodes.Any(interp => interp.Id == c.SourceNodeId)));
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
            IsExecutingModels = false;
            ExecutionStatus = "Ready";
            ExecutionProgress = 0;
            ModelsExecutedCount = 0;
            TotalModelsToExecute = 0;
            CurrentExecutingModel = "";
            CurrentExecutingModelType = "";
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
        public void SetNodeClassification(NodeViewModel node, string classification)
        {
            _nodeManagementService.SetNodeClassification(node, classification, InvalidateCanvas);
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

                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Loaded '{SelectedReviewActionName}' with {_currentActionItems.Count} action items via navigation service.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OrientPageViewModel.LoadSelectedAction] Error: {ex.Message}\nStackTrace: {ex.StackTrace}");
            }
            finally
            {
                (StepForwardCommand as Command)?.ChangeCanExecute();
                (StepBackwardCommand as Command)?.ChangeCanExecute();
                UpdateStepContent(); // Call this to reflect the state for CurrentActionStep = 0
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

        public ICommand PlayAudioCommand { get; }
        public ICommand StopAudioCommand { get; }

        public OrientPageViewModel()
        {
            // This constructor may be used by XAML designer or tests
        }

        public void UpdateStepContent()
        {
            var stepContentData = _actionReviewService.UpdateStepContent(SelectedNode, CurrentActionStep, _currentActionItems, SelectedReviewActionName);

            StepContentType = stepContentData.ContentType;
            StepContent = stepContentData.Content;
            OnPropertyChanged(nameof(StepContentType));
            OnPropertyChanged(nameof(StepContent));
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

        // --- Proactive preparation for Run All Models optimization ---
        private Task _proactivePreparationTask = null;
        private bool _modelExecutionPrepared = false;
        private readonly object _preparationLock = new object();
        private CancellationTokenSource _preparationCancellationSource = null;

        // Cache pipeline state for performance
        private void CachePipelineState()
        {
            _cachedModelNodes = Nodes.Where(n => n.Type == NodeType.Model).ToList();
            _cachedInputNodes = Nodes.Where(n => n.Type == NodeType.Input).ToList();
            _cachedConnectionCounts = new Dictionary<string, int>();

            foreach (var node in _cachedModelNodes)
            {
                _cachedConnectionCounts[node.Id] = Connections.Count(c => c.TargetNodeId == node.Id);
            }

            _pipelineStateCacheValid = true;
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
            var modelTasks = _cachedModelNodes.Select(async modelNode =>
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

            var stepContentTasks = _cachedInputNodes.Select(async inputNode =>
            {
                try
                {
                    await Task.Run(() =>
                    {
                        var (contentType, content) = inputNode.GetStepContent(stepIndex);
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
            var relationshipTasks = _cachedModelNodes.Select(async modelNode =>
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
                                            stepContents.Add($"[{inputNode.Name}]: {content}");
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
                StartExecutionTimer();

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

                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 📊 [ExecuteRunAllModelsAsync] Processing {_cachedModelNodes?.Count ?? 0} models");

                // Store the original selected node reference only (no copying)
                var originalSelectedNode = SelectedNode;

                // Step 2: Execute all models using optimized pipeline execution service
                ExecutionStatus = "Executing models...";
                var executionStopwatch = Stopwatch.StartNew();

                var (successCount, skippedCount) = await _pipelineExecutionService.ExecuteAllModelsOptimizedAsync(
                    Nodes,
                    Connections,
                    CurrentActionStep,
                    _preloadedModelCache,
                    _precomputedCombinedInputs,
                    ShowAlert,
                    onGroupsInitialized: (groupCount) => InitializeExecutionGroups(groupCount),
                    onGroupStarted: (groupNumber, modelCount) => StartGroupExecution(groupNumber, modelCount),
                    onGroupCompleted: (groupNumber) => CompleteGroupExecution(groupNumber)
                );
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
                StopExecutionTimer();

                // Complete any remaining group execution
                if (CurrentExecutionGroup > 0)
                {
                    CompleteGroupExecution(CurrentExecutionGroup);
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
                StartExecutionTimer();

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

                // Collect step content from connected nodes
                var stepContents = new List<string>();
                foreach (var inputNode in connectedInputNodes)
                {
                    Debug.WriteLine($"📄 [ExecuteGenerateAsync] Processing input node: {inputNode.Name} (Type: {inputNode.DataType})");

                    // Get step content for current step (using the same logic as UpdateStepContent)
                    int stepForNodeContent = CurrentActionStep + 1; // Convert to 1-based index
                    var (contentType, contentValue) = inputNode.GetStepContent(stepForNodeContent);

                    Debug.WriteLine($"📝 [ExecuteGenerateAsync] Input node '{inputNode.Name}' content: Type='{contentType}', Value='{contentValue?.Substring(0, Math.Min(contentValue?.Length ?? 0, 100))}...'");

                    if (!string.IsNullOrEmpty(contentValue))
                    {
                        // For image content, pass the file path directly for model execution
                        if (contentType?.ToLowerInvariant() == "image")
                        {
                            stepContents.Add(contentValue); // Direct file path for image models
                            Debug.WriteLine($"📸 [ExecuteGenerateAsync] Added image file path: {contentValue}");
                        }
                        // For audio content, pass the file path directly for model execution
                        else if (contentType?.ToLowerInvariant() == "audio")
                        {
                            stepContents.Add(contentValue); // Direct file path for audio models
                            Debug.WriteLine($"🔊 [ExecuteGenerateAsync] Added audio file path: {contentValue}");
                        }
                        else
                        {
                            stepContents.Add($"[{inputNode.Name}]: {contentValue}"); // Text content with node name prefix
                        }
                    }
                }

                if (stepContents.Count == 0)
                {
                    ExecutionStatus = "Error: No valid content";
                    Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ❌ [ExecuteGenerateAsync] No valid step content found from connected nodes");
                    await ShowAlert?.Invoke("Error", "No valid content found from connected input nodes.", "OK");
                    return;
                }

                ExecutionStatus = "Finding model...";
                ExecutionProgress = 75;

                // Combine step contents using ensemble method
                string combinedInput = CombineStepContents(stepContents, SelectedNode.SelectedEnsembleMethod);
                Debug.WriteLine($"🔀 [ExecuteGenerateAsync] Combined input ({SelectedNode.SelectedEnsembleMethod}): {combinedInput?.Substring(0, Math.Min(combinedInput?.Length ?? 0, 200))}...");

                // Find corresponding model in NetPageViewModel
                var correspondingModel = FindCorrespondingModel(_netPageViewModel, SelectedNode);
                if (correspondingModel == null)
                {
                    ExecutionStatus = "Error: Model not found";
                    Debug.WriteLine($"❌ [ExecuteGenerateAsync] No corresponding model found for node: {SelectedNode.Name}");
                    await ShowAlert?.Invoke("Error", $"No corresponding model found for '{SelectedNode.Name}'. Please ensure the model is loaded in the Net page.", "OK");
                    return;
                }

                Debug.WriteLine($"✅ [ExecuteGenerateAsync] Found corresponding model: {correspondingModel.Name} (HF ID: {correspondingModel.HuggingFaceModelId})");

                ExecutionStatus = $"Executing {SelectedNode.Name}...";
                ExecutionProgress = 90;

                // Execute the model using NetPageViewModel's infrastructure
                string result = await ExecuteModelWithInput(correspondingModel, combinedInput);

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
                StopExecutionTimer();

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
            return _ensembleModelService.GetConnectedInputNodes(modelNode, Nodes, Connections);
        }

        private string CombineStepContents(List<string> stepContents, string ensembleMethod)
        {
            return _ensembleModelService.CombineStepContents(stepContents, ensembleMethod);
        }

        private async Task<string> ExecuteModelWithInput(NeuralNetworkModel model, string input)
        {
            return await _ensembleModelService.ExecuteModelWithInput(model, input);
        }

        private string DetermineResultContentType(NeuralNetworkModel model, string result)
        {
            return _ensembleModelService.DetermineResultContentType(model, result);
        }

        // IDisposable implementation
        public void Dispose()
        {
            // Clean up execution timer
            try
            {
                _executionTimer?.Stop();
                _executionTimer?.Dispose();
                _executionTimer = null;
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
            Debug.WriteLine($"[CanStopAudio] Result: {result}, IsPlaying: {_audioStepContentService?.IsPlaying}");
            return result;
        }

        // Debug method to check RunAllModelsCommand state
        public void DebugRunAllModelsCommand()
        {
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🐛 [DebugRunAllModelsCommand] === DEBUG INFO ===");
            Debug.WriteLine($"🐛 RunAllModelsCommand is null: {RunAllModelsCommand == null}");
            Debug.WriteLine($"🐛 Total nodes: {Nodes?.Count ?? 0}");
            if (Nodes != null)
            {
                Debug.WriteLine($"🐛 Model nodes: {Nodes.Count(n => n.Type == NodeType.Model)}");
                foreach (var node in Nodes)
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
            if (!_pipelineStateCacheValid || _cachedModelNodes == null || _cachedModelNodes.Count == 0)
            {
                Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ⏭️ [PrewarmModelExecution] No models to pre-warm");
                return;
            }

            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] 🔥 [PrewarmModelExecution] Pre-warming environment for {_cachedModelNodes.Count} models");

            try
            {
                // Pre-load models that will likely be executed
                var modelsToPreload = _cachedModelNodes
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
    }
}
