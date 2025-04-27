using CSimple.Models;
using CSimple.Services;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text; // Added for StringBuilder
using System.Threading.Tasks;
using System.Windows.Input;


namespace CSimple.ViewModels
{
    public class OrientPageViewModel : INotifyPropertyChanged
    {
        // --- Services ---
        private readonly FileService _fileService; // Inject FileService
        private readonly HuggingFaceService _huggingFaceService; // Add HuggingFaceService
        private readonly NetPageViewModel _netPageViewModel; // Keep reference if needed
        private readonly PythonBootstrapper _pythonBootstrapper; // Added

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

                    // Clear node output when selecting a different node
                    NodeOutputText = null;
                }
            }
        }

        // Added property to store node execution output
        private string _nodeOutputText;
        public string NodeOutputText
        {
            get => _nodeOutputText;
            set => SetProperty(ref _nodeOutputText, value);
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


        // --- UI Interaction Delegates ---
        public Func<string, string, string, Task> ShowAlert { get; set; }
        public Func<string, string, string, string[], Task<string>> ShowActionSheet { get; set; }

        // --- Constructor ---
        // Ensure FileService and PythonBootstrapper are injected
        public OrientPageViewModel(FileService fileService, HuggingFaceService huggingFaceService, NetPageViewModel netPageViewModel, PythonBootstrapper pythonBootstrapper)
        {
            _fileService = fileService;
            _huggingFaceService = huggingFaceService;
            _netPageViewModel = netPageViewModel;
            _pythonBootstrapper = pythonBootstrapper; // Store injected service

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
            var modelType = InferNodeTypeFromName(modelId);
            var modelName = GetFriendlyModelName(modelId);

            // Generate a reasonable position for the new node
            // Find a vacant spot in the middle area of the canvas
            float x = 300 + (Nodes.Count % 3) * 180;
            float y = 200 + (Nodes.Count / 3) * 100;

            // Use the NodeViewModel constructor
            var newNode = new NodeViewModel(
                Guid.NewGuid().ToString(), // Generate string ID
                modelName, // Use a friendly name
                modelType, // Infer type
                new PointF(x, y) // Calculated position
            )
            {
                // Set properties not in constructor
                Size = new SizeF(180, 60), // Size based on name length if needed
                ModelPath = model.Id // Store the HuggingFace ID
                // ModelDetails property doesn't exist in NodeViewModel, removing it
            };

            Nodes.Add(newNode);
            Debug.WriteLine($"Added node: {newNode.Name} at position {x},{y}");
            await SaveCurrentPipelineAsync(); // Save after adding
        }

        // Helper to determine a more friendly model name
        private string GetFriendlyModelName(string modelId)
        {
            // Similar to NetPageViewModel implementation
            var name = modelId.Contains('/') ? modelId.Split('/').Last() : modelId;
            name = name.Replace("-", " ").Replace("_", " ");
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name.ToLower());
        }

        public async Task DeleteSelectedNode()
        {
            if (SelectedNode != null)
            {
                // Remove connections associated with the node
                var connectionsToRemove = Connections
                    .Where(c => c.SourceNodeId == SelectedNode.Id || c.TargetNodeId == SelectedNode.Id)
                    .ToList();
                foreach (var conn in connectionsToRemove)
                {
                    Connections.Remove(conn); // Remove connection
                }

                // Remove the node itself
                Nodes.Remove(SelectedNode);
                Debug.WriteLine($"Deleted node: {SelectedNode.Name}");
                SelectedNode = null; // Deselect
                UpdateEnsembleCounts(); // ADDED: Update counts after removing connections
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
            if (node != null)
            {
                node.Position = newPosition;
                // Note: Saving on every move update might be too frequent.
                // Consider saving only on DragEnd interaction in the view,
                // or implement debouncing here. For simplicity, saving here for now.
                // await SaveCurrentPipelineAsync(); // Commented out again
            }
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
            // Check nodes in reverse order so top-most node is selected
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                var node = Nodes[i];
                var nodeRect = new RectF(node.Position, node.Size);
                if (nodeRect.Contains(point))
                {
                    return node;
                }
            }
            return null;
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
                    // Use the ConnectionViewModel constructor
                    var newConnection = new ConnectionViewModel(
                        Guid.NewGuid().ToString(), // Generate string ID
                        _temporaryConnectionState.Id,
                        targetNode.Id
                    );
                    Connections.Add(newConnection);
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
            try
            {
                var pipelines = await _fileService.ListPipelinesAsync();
                AvailablePipelineNames.Clear();
                if (pipelines != null)
                {
                    foreach (var pipeline in pipelines.OrderBy(p => p.Name))
                    {
                        AvailablePipelineNames.Add(pipeline.Name);
                    }
                }
                // Optionally set the SelectedPipelineName if needed within OrientPage itself
                // SelectedPipelineName = AvailablePipelineNames.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading available pipeline names in OrientPageViewModel: {ex.Message}");
            }
        }

        private async Task LoadPipelineAsync(string pipelineName)
        {
            Debug.WriteLine($"Loading pipeline: {pipelineName}");
            var pipelineData = await _fileService.LoadPipelineAsync(pipelineName);
            if (pipelineData != null)
            {
                // Check if we are loading the same pipeline that's already current
                // and if the canvas is already empty (avoids unnecessary clearing/reloading)
                if (CurrentPipelineName == pipelineName && !Nodes.Any() && !Connections.Any() && pipelineData.Nodes.Count == 0 && pipelineData.Connections.Count == 0)
                {
                    Debug.WriteLine($"Pipeline '{pipelineName}' is already the current empty pipeline. Skipping reload.");
                    // Ensure CurrentPipelineName is set correctly even if skipping
                    if (_currentPipelineName != pipelineName)
                    {
                        CurrentPipelineName = pipelineName;
                    }
                    // Ensure picker reflects the name
                    if (_selectedPipelineName != pipelineName)
                    {
                        _selectedPipelineName = pipelineName;
                        OnPropertyChanged(nameof(SelectedPipelineName));
                    }
                    return; // Exit early
                }


                Nodes.Clear();
                Connections.Clear();

                foreach (var nodeData in pipelineData.Nodes)
                {
                    Nodes.Add(nodeData.ToViewModel());
                }
                foreach (var connData in pipelineData.Connections)
                {
                    Connections.Add(connData.ToViewModel()); // Add connection
                }
                CurrentPipelineName = pipelineData.Name;
                // Manually update the picker selection if needed, though binding should handle it
                if (_selectedPipelineName != pipelineName)
                {
                    _selectedPipelineName = pipelineName;
                    OnPropertyChanged(nameof(SelectedPipelineName));
                }
                Debug.WriteLine($"Successfully loaded pipeline: {pipelineName}");
                UpdateEnsembleCounts(); // ADDED: Update counts after loading all connections
                // After loading, update classifications based on the newly loaded nodes
                await UpdateNodeClassificationsAsync();
            }
            else
            {
                // Only show error if it's not the initial creation scenario
                if (!(CurrentPipelineName == pipelineName && !Nodes.Any() && !Connections.Any()))
                {
                    await ShowAlert?.Invoke("Error", $"Failed to load pipeline '{pipelineName}'.", "OK");
                }
                else
                {
                    Debug.WriteLine($"Failed to load pipeline '{pipelineName}', but assuming it's the initial empty one being created.");
                }

                // Fallback logic remains the same
                if (AvailablePipelineNames.Any() && AvailablePipelineNames.First() != pipelineName) // Avoid infinite loop if first fails
                {
                    SelectedPipelineName = AvailablePipelineNames.First(); // Fallback to first available
                }
                else if (!AvailablePipelineNames.Any()) // Only create new if list becomes empty
                {
                    // This case should ideally not be hit if creation logic is sound,
                    // but as a safeguard:
                    Debug.WriteLine($"Load failed for '{pipelineName}', and no other pipelines exist. Attempting to create a new one again.");
                    await CreateNewPipeline();
                    await SaveCurrentPipelineAsync();
                    SelectedPipelineName = CurrentPipelineName;
                }
            }
        }

        // Change from protected to public to make it accessible from OrientPage
        public async Task SaveCurrentPipelineAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPipelineName))
            {
                // This shouldn't happen if CurrentPipelineName is managed correctly
                Debug.WriteLine("Cannot save pipeline with empty name.");
                return;
            }

            Debug.WriteLine($"Saving pipeline: {CurrentPipelineName}");
            var pipelineData = new PipelineData
            {
                Name = CurrentPipelineName,
                Nodes = Nodes.Select(n => new SerializableNode(n)).ToList(),
                Connections = Connections.Select(c => new SerializableConnection(c)).ToList()
            };
            await _fileService.SavePipelineAsync(pipelineData);
            // No need to reload list unless timestamp sorting is critical for immediate UI update
        }

        private async Task CreateNewPipeline()
        {
            ClearCanvas();
            // Find a unique default name
            int counter = 1;
            string baseName = "Untitled Pipeline";
            string newName = baseName;
            while (AvailablePipelineNames.Contains(newName))
            {
                newName = $"{baseName} {counter++}";
            }
            CurrentPipelineName = newName;
            // SelectedPipelineName = null; // Keep deselected initially

            Debug.WriteLine($"Created new pipeline placeholder: {CurrentPipelineName}");

            // Add default input nodes
            AddDefaultInputNodes();

            // Add to list immediately so it can be selected later
            if (!AvailablePipelineNames.Contains(CurrentPipelineName))
            {
                AvailablePipelineNames.Insert(0, CurrentPipelineName); // Add to top
                OnPropertyChanged(nameof(AvailablePipelineNames)); // Notify UI about the change
            }
            // No saving or selecting here - handled by the command or initialization logic
            await Task.CompletedTask;
        }

        // --- Private Helper Methods --- (Moved AddDefaultInputNodes here)
        private void AddDefaultInputNodes()
        {
            // Create more organized default layout
            float startX = 50;
            float startY = 50;
            float spacingY = 80; // Vertical spacing between nodes
            float spacingX = 170; // Horizontal spacing between nodes (Node Width + Gap)
            SizeF defaultSize = new SizeF(150, 50); // Default size for input nodes

            // Group by type: Visual inputs, Audio inputs, Text inputs

            // Visual inputs (top row)
            var webcamImageNode = new NodeViewModel(Guid.NewGuid().ToString(), "Webcam Image", NodeType.Input, new PointF(startX, startY))
            {
                Size = defaultSize,
                DataType = "image" // Add data type for better classification
            };

            var screenImageNode = new NodeViewModel(Guid.NewGuid().ToString(), "Screen Image", NodeType.Input, new PointF(startX + spacingX, startY))
            {
                Size = defaultSize,
                DataType = "image" // Add data type for better classification
            };

            // Audio inputs (middle row)
            var pcAudioNode = new NodeViewModel(Guid.NewGuid().ToString(), "PC Audio", NodeType.Input, new PointF(startX, startY + spacingY))
            {
                Size = defaultSize,
                DataType = "audio" // Add data type for better classification
            };

            var webcamAudioNode = new NodeViewModel(Guid.NewGuid().ToString(), "Webcam Audio", NodeType.Input, new PointF(startX + spacingX, startY + spacingY))
            {
                Size = defaultSize,
                DataType = "audio" // Add data type for better classification
            };

            // Text inputs (bottom row)
            var keyboardTextNode = new NodeViewModel(Guid.NewGuid().ToString(), "Keyboard Text", NodeType.Input, new PointF(startX, startY + 2 * spacingY))
            {
                Size = defaultSize,
                DataType = "text" // Add data type for better classification
            };

            var mouseTextNode = new NodeViewModel(Guid.NewGuid().ToString(), "Mouse Text", NodeType.Input, new PointF(startX + spacingX, startY + 2 * spacingY))
            {
                Size = defaultSize,
                DataType = "text" // Add data type for better classification
            };

            // Add all nodes to the collection
            Nodes.Add(webcamImageNode);
            Nodes.Add(screenImageNode);
            Nodes.Add(pcAudioNode);
            Nodes.Add(webcamAudioNode);
            Nodes.Add(keyboardTextNode);
            Nodes.Add(mouseTextNode);

            Debug.WriteLine($"Added specialized input nodes to '{CurrentPipelineName}'.");
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
                if (AvailablePipelineNames.Contains(newName))
                {
                    await ShowAlert?.Invoke("Error", $"A pipeline named '{newName}' already exists.", "OK");
                    return;
                }

                bool success = await _fileService.RenamePipelineAsync(oldName, newName);
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
                await _fileService.DeletePipelineAsync(nameToDelete);
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
            string lowerName = name.ToLower();

            // Input node detection with more specific categories
            if (lowerName.Contains("webcam") || lowerName.Contains("screen") ||
                lowerName.Contains("keyboard") || lowerName.Contains("mouse") ||
                lowerName.Contains("audio") || lowerName.Contains("input"))
                return NodeType.Input;

            if (lowerName.Contains("output") || lowerName.Contains("display") || lowerName.Contains("speaker"))
                return NodeType.Output;

            return NodeType.Model; // Default to Model
        }

        // Helper to determine data type from node name (can be used for UI styling)
        public string DetermineDataType(string nodeName)
        {
            // Get the NetPageViewModel from App
            var netPageVM = ((App)Application.Current).NetPageViewModel;
            if (netPageVM == null)
            {
                Debug.WriteLine("DetermineDataType: NetPageViewModel is null. Defaulting to 'unknown'.");
                return "unknown";
            }

            // Check if this name exists in any models in NetPageViewModel
            var matchingModel = netPageVM.AvailableModels.FirstOrDefault(m =>
                m.Name.Equals(nodeName, StringComparison.OrdinalIgnoreCase) ||
                (m.HuggingFaceModelId != null && m.HuggingFaceModelId.Contains(nodeName, StringComparison.OrdinalIgnoreCase)));

            if (matchingModel != null)
            {
                // Use the model's actual InputType
                return matchingModel.InputType switch
                {
                    ModelInputType.Text => "text",
                    ModelInputType.Image => "image",
                    ModelInputType.Audio => "audio",
                    _ => "unknown"
                };
            }

            // For input node names that might not be in the models collection
            string lowerName = nodeName.ToLowerInvariant();
            if (lowerName.Contains("image") || lowerName.Contains("webcam") || lowerName.Contains("screen"))
                return "image";
            if (lowerName.Contains("audio") || lowerName.Contains("sound") || lowerName.Contains("speech"))
                return "audio";
            if (lowerName.Contains("text") || lowerName.Contains("keyboard") || lowerName.Contains("mouse"))
                return "text";

            return "unknown";
        }

        public async Task UpdateNodeClassificationsAsync()
        {
            bool pipelineChanged = false;

            // Access NetPageViewModel directly from App class
            var netPageVM = ((App)Application.Current).NetPageViewModel;

            if (netPageVM == null || netPageVM.AvailableModels == null)
            {
                Debug.WriteLine("UpdateNodeClassificationsAsync: NetPageViewModel or AvailableModels is null. Cannot update.");
                return;
            }

            Debug.WriteLine($"UpdateNodeClassificationsAsync: Found {netPageVM.AvailableModels.Count} models in NetPageViewModel.");

            // Iterate through the nodes in the current pipeline
            foreach (var node in Nodes)
            {
                // Only update nodes that represent models (not Input/Output nodes)
                if (node.Type == NodeType.Model)
                {
                    // Improved model matching logic with better debugging
                    var correspondingNetModel = FindCorrespondingModel(netPageVM, node);

                    if (correspondingNetModel != null)
                    {
                        Debug.WriteLine($"Found corresponding NetModel '{correspondingNetModel.Name}' for Node '{node.Name}' (ModelPath: {node.ModelPath})");

                        // Get InputType directly from the model - this is the key part we want to ensure is working
                        var inputType = correspondingNetModel.InputType;
                        Debug.WriteLine($"Model '{correspondingNetModel.Name}' has InputType: {inputType}");

                        // Convert ModelInputType enum to string for DataType
                        string newDataType = inputType switch
                        {
                            ModelInputType.Text => "text",
                            ModelInputType.Image => "image",
                            ModelInputType.Audio => "audio",
                            _ => "unknown" // Default or Unknown
                        };

                        Debug.WriteLine($"Converted InputType {inputType} to DataType '{newDataType}'");

                        // Check if the DataType needs updating
                        if (node.DataType != newDataType)
                        {
                            Debug.WriteLine($"Updating DataType for Node '{node.Name}' from '{node.DataType}' to '{newDataType}' based on NetModel InputType '{correspondingNetModel.InputType}'.");
                            node.DataType = newDataType;
                            pipelineChanged = true; // Mark that a change occurred
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Could not find corresponding NetModel for Node '{node.Name}' with ModelPath '{node.ModelPath}'.");
                        // Try to determine data type based on node name as fallback
                        string inferredDataType = DetermineDataTypeFromName(node.Name);
                        if (inferredDataType != "unknown" && node.DataType != inferredDataType)
                        {
                            Debug.WriteLine($"Using inferred data type '{inferredDataType}' for node '{node.Name}' based on name");
                            node.DataType = inferredDataType;
                            pipelineChanged = true;
                        }
                    }
                }
            }

            // Save the pipeline only if any node's DataType was actually changed
            if (pipelineChanged)
            {
                Debug.WriteLine("UpdateNodeClassificationsAsync: Pipeline data changed, saving...");
                await SaveCurrentPipelineAsync();

                // Force redraw of the canvas to reflect potential color changes
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    InvalidateCanvas?.Invoke();
                    Debug.WriteLine("Requested canvas invalidation via InvalidateCanvas action.");
                });
            }
            else
            {
                Debug.WriteLine("UpdateNodeClassificationsAsync: No pipeline data changed, skipping save.");
            }
        }

        // New helper method for finding corresponding model with better matching logic
        private NeuralNetworkModel FindCorrespondingModel(NetPageViewModel netPageVM, NodeViewModel node)
        {
            // First try exact match by ID (most precise)
            var exactIdMatch = netPageVM.AvailableModels.FirstOrDefault(m =>
                (!string.IsNullOrEmpty(m.Id) && m.Id == node.ModelPath) ||
                (!string.IsNullOrEmpty(m.HuggingFaceModelId) && m.HuggingFaceModelId == node.ModelPath));

            if (exactIdMatch != null)
            {
                Debug.WriteLine($"Found exact ID match for node {node.Name}");
                return exactIdMatch;
            }

            // Try matching by name (second best)
            var nameMatch = netPageVM.AvailableModels.FirstOrDefault(m =>
                string.Equals(m.Name, node.Name, StringComparison.OrdinalIgnoreCase));

            if (nameMatch != null)
            {
                Debug.WriteLine($"Found name match for node {node.Name}");
                return nameMatch;
            }

            // Try fuzzy name matching (as a last resort)
            string nodeName = node.Name.ToLowerInvariant();
            var fuzzyMatch = netPageVM.AvailableModels.FirstOrDefault(m =>
                (m.Name != null && m.Name.ToLowerInvariant().Contains(nodeName)) ||
                (nodeName.Length > 5 && m.Name != null && nodeName.Contains(m.Name.ToLowerInvariant())));

            if (fuzzyMatch != null)
            {
                Debug.WriteLine($"Found fuzzy name match for node {node.Name} -> {fuzzyMatch.Name}");
            }

            return fuzzyMatch; // May be null if no match found
        }

        // Helper to infer data type from node name as a fallback
        private string DetermineDataTypeFromName(string nodeName)
        {
            string lowerName = nodeName.ToLowerInvariant();

            // Text models
            if (lowerName.Contains("text") ||
                lowerName.Contains("gpt") ||
                lowerName.Contains("llama") ||
                lowerName.Contains("llm") ||
                lowerName.Contains("bert") ||
                lowerName.Contains("token") ||
                lowerName.Contains("deepseek") ||
                lowerName.Contains("mistral") ||
                lowerName.Contains("chat"))
                return "text";

            // Image models
            if (lowerName.Contains("image") ||
                lowerName.Contains("vision") ||
                lowerName.Contains("yolo") ||
                lowerName.Contains("resnet") ||
                lowerName.Contains("clip") ||
                lowerName.Contains("diffusion") ||
                lowerName.Contains("stable") ||
                lowerName.Contains("gan"))
                return "image";

            // Audio models
            if (lowerName.Contains("audio") ||
                lowerName.Contains("speech") ||
                lowerName.Contains("whisper") ||
                lowerName.Contains("wav2vec") ||
                lowerName.Contains("sound") ||
                lowerName.Contains("voice"))
                return "audio";

            return "unknown";
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
            if (data == null) return;

            Nodes.Clear();
            Connections.Clear();

            if (data.Nodes != null)
            {
                foreach (var nodeData in data.Nodes)
                {
                    Nodes.Add(nodeData.ToViewModel());
                }
            }

            if (data.Connections != null)
            {
                foreach (var connData in data.Connections)
                {
                    // Ensure source and target nodes exist before adding connection
                    if (Nodes.Any(n => n.Id == connData.SourceNodeId.ToString()) &&
                        Nodes.Any(n => n.Id == connData.TargetNodeId.ToString()))
                    {
                        Connections.Add(connData.ToViewModel());
                    }
                    else
                    {
                        Debug.WriteLine($"Warning: Skipping connection {connData.Id} due to missing source/target node during load.");
                    }
                }
            }

            CurrentPipelineName = data.Name;
            InvalidateCanvas?.Invoke(); // Redraw canvas
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
        }

        public Action InvalidateCanvas { get; set; }

        // --- Helper Methods ---

        // ADDED: Method to calculate and update ensemble input counts for all nodes
        private void UpdateEnsembleCounts()
        {
            Debug.WriteLine("Updating ensemble counts...");
            bool countsChanged = false;
            var inputCounts = Connections
                .GroupBy(c => c.TargetNodeId)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (var node in Nodes)
            {
                int newCount = inputCounts.TryGetValue(node.Id, out var count) ? count : 0;
                if (node.EnsembleInputCount != newCount)
                {
                    node.EnsembleInputCount = newCount;
                    countsChanged = true;
                    Debug.WriteLine($"Node '{node.Name}' input count set to {newCount}");
                }
            }

            if (countsChanged)
            {
                Debug.WriteLine("Ensemble counts changed, invalidating canvas.");
                InvalidateCanvas?.Invoke(); // Trigger redraw if any count changed
            }
            else
            {
                Debug.WriteLine("No ensemble counts changed.");
            }
        }

        // ADDED: Method to set a node's classification
        public void SetNodeClassification(NodeViewModel node, string classification)
        {
            if (node != null && node.IsTextModel)
            {
                // This will automatically update the node's display name via the property setter
                node.Classification = classification;

                // Request redraw to show the updated name
                InvalidateCanvas?.Invoke();

                Debug.WriteLine($"Set node '{node.OriginalName}' classification to '{classification}'");
            }
        }
    }
}
