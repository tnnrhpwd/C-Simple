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

        // Add these properties and fields for Action Review functionality
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
                Debug.WriteLine($"[OrientPageViewModel.CurrentActionStep_Set] Attempting to set from {_currentActionStep} to {value}");
                if (SetProperty(ref _currentActionStep, value))
                {
                    Debug.WriteLine($"[OrientPageViewModel.CurrentActionStep_Set] CurrentActionStep changed to: {CurrentActionStep}. Calling UpdateStepContent.");
                    UpdateStepContent();
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
                (node) => FindCorrespondingModel(_netPageViewModel, node)
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
            Debug.WriteLine("🔧 [OrientPageViewModel.Constructor] Initializing RunAllModelsCommand");
            RunAllModelsCommand = new Command(async () =>
            {
                Debug.WriteLine("🚀 [RunAllModelsCommand] Button clicked - executing command");
                await ExecuteRunAllModelsAsync();
            }, () =>
            {
                bool canExecute = Nodes.Any(n => n.Type == NodeType.Model);
                Debug.WriteLine($"🔍 [RunAllModelsCommand.CanExecute] Checking: {canExecute} (Model nodes count: {Nodes.Count(n => n.Type == NodeType.Model)})");
                return canExecute;
            });

            // Initialize Audio commands
            PlayAudioCommand = new Command(() => PlayAudio(), CanPlayAudio);
            StopAudioCommand = new Command(() => StopAudio(), CanStopAudio);

            // Load available pipelines on initialization
            _ = LoadAvailablePipelinesAsync();
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
            // Get the NetPageViewModel and ensure it loads its models
            var netPageVM = ((App)Application.Current).NetPageViewModel;
            Debug.WriteLine($"InitializeAsync: Checking NetPageViewModel, HasModels: {netPageVM?.AvailableModels?.Count > 0}");

            if (netPageVM != null && (netPageVM.AvailableModels == null || netPageVM.AvailableModels.Count == 0))
            {
                Debug.WriteLine("InitializeAsync: NetPageViewModel has no models yet, loading them first");
                await netPageVM.LoadDataAsync();
                Debug.WriteLine($"InitializeAsync: After LoadDataAsync, NetPageViewModel has {netPageVM.AvailableModels?.Count ?? 0} models");
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

            // Load available actions for review
            await LoadAvailableActions();
        }

        public async Task LoadAvailableModelsAsync()
        {
            try
            {
                Debug.WriteLine("Loading available HuggingFace models...");
                AvailableModels.Clear();

                // Load models from FileService like NetPageViewModel does
                var persistedModels = await _fileService.LoadHuggingFaceModelsAsync();

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
            UpdateEnsembleCounts(); // ADDED: Update counts after adding node
            Debug.WriteLine($"🔄 [AddModelNode] Updating RunAllModelsCommand CanExecute - Model nodes count: {Nodes.Count(n => n.Type == NodeType.Model)}");
            (RunAllModelsCommand as Command)?.ChangeCanExecute(); // Update Run All Models button state
            await SaveCurrentPipelineAsync(); // Save after adding
        }

        public async Task DeleteSelectedNode()
        {
            if (SelectedNode != null)
            {
                await _nodeManagementService.DeleteSelectedNodeAsync(Nodes, Connections, SelectedNode, InvalidateCanvas);
                SelectedNode = null; // Deselect
                UpdateEnsembleCounts(); // ADDED: Update counts after removing connections
                Debug.WriteLine($"🗑️ [DeleteSelectedNode] Updating RunAllModelsCommand CanExecute - Model nodes count: {Nodes.Count(n => n.Type == NodeType.Model)}");
                (RunAllModelsCommand as Command)?.ChangeCanExecute(); // Update Run All Models button state
                await SaveCurrentPipelineAsync(); // Save after deleting
                InvalidateCanvas?.Invoke(); // ADDED: Ensure redraw after potential count update
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
            return _nodeManagementService.FindCorrespondingModel(netPageVM.AvailableModels, node);
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
            // Update command states if necessary when properties change
            if (propertyName == nameof(SelectedNode) || propertyName == nameof(Nodes) || propertyName == nameof(Connections))
            {
                // Example: ((Command)DeleteSelectedNodeCommand)?.ChangeCanExecute();
            }

            // Update audio command states when relevant properties change
            if (propertyName == nameof(StepContent) || propertyName == nameof(StepContentType))
            {
                ((Command)PlayAudioCommand)?.ChangeCanExecute();
                ((Command)StopAudioCommand)?.ChangeCanExecute();
                Debug.WriteLine($"[OrientPageViewModel.OnPropertyChanged] Updated audio command states for property: {propertyName}");
            }
        }

        public Action InvalidateCanvas { get; set; }

        // --- Helper Methods ---

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

        // --- Run All Models Command Implementation ---
        private async Task ExecuteRunAllModelsAsync()
        {
            var totalStopwatch = Stopwatch.StartNew();
            Debug.WriteLine("🎯 [ExecuteRunAllModelsAsync] Starting execution using PipelineExecutionService");

            // Log pipeline state before execution
            var modelNodes = Nodes.Where(n => n.Type == NodeType.Model).ToList();
            var inputNodes = Nodes.Where(n => n.Type == NodeType.Input).ToList();
            Debug.WriteLine($"📊 [ExecuteRunAllModelsAsync] Pipeline Overview:");
            Debug.WriteLine($"   • Total Nodes: {Nodes.Count} (Models: {modelNodes.Count}, Inputs: {inputNodes.Count})");
            Debug.WriteLine($"   • Total Connections: {Connections.Count}");
            Debug.WriteLine($"   • Current Action Step: {CurrentActionStep}");
            Debug.WriteLine($"   • Pipeline Name: {CurrentPipelineName}");

            try
            {
                // Step 1: Store the original selected node
                var step1Stopwatch = Stopwatch.StartNew();
                var originalSelectedNode = SelectedNode;
                step1Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [ExecuteRunAllModelsAsync] Step 1 - Store selected node: {step1Stopwatch.ElapsedMilliseconds}ms");

                // Step 2: Execute all models using pipeline execution service
                var step2Stopwatch = Stopwatch.StartNew();
                Debug.WriteLine($"🚀 [ExecuteRunAllModelsAsync] Step 2 - Starting pipeline execution with {modelNodes.Count} models...");

                // Log model nodes details before execution
                foreach (var modelNode in modelNodes)
                {
                    var inputCount = Connections.Count(c => c.TargetNodeId == modelNode.Id);
                    Debug.WriteLine($"   🤖 Model: '{modelNode.Name}' | Inputs: {inputCount} | Ensemble: {modelNode.SelectedEnsembleMethod} | Ready: {inputCount > 0}");
                }

                var (successCount, skippedCount) = await _pipelineExecutionService.ExecuteAllModelsAsync(
                    Nodes,
                    Connections,
                    CurrentActionStep,
                    ShowAlert
                );
                step2Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [ExecuteRunAllModelsAsync] Step 2 - Pipeline execution completed: {step2Stopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"📊 [ExecuteRunAllModelsAsync] Pipeline Results: {successCount} successful, {skippedCount} skipped");

                // Step 3: Restore the original selected node
                var step3Stopwatch = Stopwatch.StartNew();
                SelectedNode = originalSelectedNode;
                step3Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [ExecuteRunAllModelsAsync] Step 3 - Restore selected node: {step3Stopwatch.ElapsedMilliseconds}ms");

                // Step 4: Save the pipeline
                var step4Stopwatch = Stopwatch.StartNew();
                Debug.WriteLine($"💾 [ExecuteRunAllModelsAsync] Step 4 - Saving pipeline '{CurrentPipelineName}'...");
                await SaveCurrentPipelineAsync();
                step4Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [ExecuteRunAllModelsAsync] Step 4 - Save pipeline: {step4Stopwatch.ElapsedMilliseconds}ms");

                totalStopwatch.Stop();
                string resultMessage = $"Execution completed!\nSuccessful: {successCount}\nSkipped: {skippedCount}";
                Debug.WriteLine($"🎉 [ExecuteRunAllModelsAsync] {resultMessage}");
                Debug.WriteLine($"⏱️ [ExecuteRunAllModelsAsync] TOTAL UI EXECUTION TIME: {totalStopwatch.ElapsedMilliseconds}ms");

                // Enhanced timing breakdown with more context
                Debug.WriteLine("📊 [ExecuteRunAllModelsAsync] DETAILED TIMING BREAKDOWN:");
                Debug.WriteLine($"   ├── Step 1 (Store selected node): {step1Stopwatch.ElapsedMilliseconds}ms ({(double)step1Stopwatch.ElapsedMilliseconds / totalStopwatch.ElapsedMilliseconds * 100:F1}%)");
                Debug.WriteLine($"   ├── Step 2 (Pipeline execution): {step2Stopwatch.ElapsedMilliseconds}ms ({(double)step2Stopwatch.ElapsedMilliseconds / totalStopwatch.ElapsedMilliseconds * 100:F1}%) ← MAIN EXECUTION");
                Debug.WriteLine($"   │   ├── Models processed: {successCount + skippedCount}");
                Debug.WriteLine($"   │   ├── Success rate: {(successCount > 0 ? (double)successCount / (successCount + skippedCount) * 100 : 0):F1}%");
                Debug.WriteLine($"   │   ├── Avg time per model: {(successCount > 0 ? step2Stopwatch.ElapsedMilliseconds / successCount : 0):F0}ms");
                Debug.WriteLine($"   ├── Step 3 (Restore selected node): {step3Stopwatch.ElapsedMilliseconds}ms ({(double)step3Stopwatch.ElapsedMilliseconds / totalStopwatch.ElapsedMilliseconds * 100:F1}%)");
                Debug.WriteLine($"   └── Step 4 (Save pipeline): {step4Stopwatch.ElapsedMilliseconds}ms ({(double)step4Stopwatch.ElapsedMilliseconds / totalStopwatch.ElapsedMilliseconds * 100:F1}%)");
                Debug.WriteLine($"📊 [ExecuteRunAllModelsAsync] TOTAL EXECUTION TIME: {totalStopwatch.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                Debug.WriteLine($"❌ [ExecuteRunAllModelsAsync] Critical error after {totalStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                await ShowAlert?.Invoke("Error", $"Failed to run all models: {ex.Message}", "OK");
            }
        }

        // --- Generate Command Implementation ---
        private async Task ExecuteGenerateAsync()
        {
            try
            {
                Debug.WriteLine($"🚀 [OrientPageViewModel.ExecuteGenerateAsync] Starting generation for node: {SelectedNode?.Name}");

                if (SelectedNode == null || SelectedNode.Type != NodeType.Model)
                {
                    Debug.WriteLine("❌ [ExecuteGenerateAsync] No valid model node selected");
                    await ShowAlert?.Invoke("Error", "Please select a model node to generate content.", "OK");
                    return;
                }

                if (SelectedNode.EnsembleInputCount <= 1)
                {
                    Debug.WriteLine("❌ [ExecuteGenerateAsync] Not enough input connections for ensemble generation");
                    await ShowAlert?.Invoke("Error", "This model node needs multiple input connections to use ensemble generation.", "OK");
                    return;
                }

                Debug.WriteLine($"📊 [ExecuteGenerateAsync] Model node has {SelectedNode.EnsembleInputCount} input connections");
                Debug.WriteLine($"📊 [ExecuteGenerateAsync] Selected ensemble method: {SelectedNode.SelectedEnsembleMethod}");

                // Find all connected input nodes
                var connectedInputNodes = GetConnectedInputNodes(SelectedNode);
                Debug.WriteLine($"🔍 [ExecuteGenerateAsync] Found {connectedInputNodes.Count} connected input nodes");

                if (connectedInputNodes.Count == 0)
                {
                    Debug.WriteLine("❌ [ExecuteGenerateAsync] No connected input nodes found");
                    await ShowAlert?.Invoke("Error", "No connected input nodes found for this model.", "OK");
                    return;
                }

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
                    Debug.WriteLine("❌ [ExecuteGenerateAsync] No valid step content found from connected nodes");
                    await ShowAlert?.Invoke("Error", "No valid content found from connected input nodes.", "OK");
                    return;
                }

                // Combine step contents using ensemble method
                string combinedInput = CombineStepContents(stepContents, SelectedNode.SelectedEnsembleMethod);
                Debug.WriteLine($"🔀 [ExecuteGenerateAsync] Combined input ({SelectedNode.SelectedEnsembleMethod}): {combinedInput?.Substring(0, Math.Min(combinedInput?.Length ?? 0, 200))}...");

                // Find corresponding model in NetPageViewModel
                var correspondingModel = FindCorrespondingModel(_netPageViewModel, SelectedNode);
                if (correspondingModel == null)
                {
                    Debug.WriteLine($"❌ [ExecuteGenerateAsync] No corresponding model found for node: {SelectedNode.Name}");
                    await ShowAlert?.Invoke("Error", $"No corresponding model found for '{SelectedNode.Name}'. Please ensure the model is loaded in the Net page.", "OK");
                    return;
                }

                Debug.WriteLine($"✅ [ExecuteGenerateAsync] Found corresponding model: {correspondingModel.Name} (HF ID: {correspondingModel.HuggingFaceModelId})");

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

                // Note: Pipeline saving is deferred to reduce I/O operations
                // It will be saved when appropriate (e.g., on action completion or manual save)

                Debug.WriteLine($"✅ [ExecuteGenerateAsync] Generation completed successfully");

                // await ShowAlert?.Invoke("Success", $"Generated content using {SelectedNode.SelectedEnsembleMethod} ensemble method with {connectedInputNodes.Count} inputs.", "OK");

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ExecuteGenerateAsync] Error during generation: {ex.Message}");
                Debug.WriteLine($"❌ [ExecuteGenerateAsync] Stack trace: {ex.StackTrace}");
                await ShowAlert?.Invoke("Error", $"Failed to generate content: {ex.Message}", "OK");
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
            Debug.WriteLine("🐛 [DebugRunAllModelsCommand] === DEBUG INFO ===");
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
            Debug.WriteLine("🐛 [DebugRunAllModelsCommand] === END DEBUG ===");
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
    }
}
