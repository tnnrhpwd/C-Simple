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
using System.Threading.Tasks;
using System.Windows.Input; // Required for ICommand

namespace CSimple.ViewModels
{
    public class OrientPageViewModel : INotifyPropertyChanged
    {
        // --- Services ---
        private readonly FileService _fileService; // Inject FileService

        // --- Properties ---
        public ObservableCollection<NodeViewModel> Nodes { get; } = new ObservableCollection<NodeViewModel>();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new ObservableCollection<ConnectionViewModel>();
        public ObservableCollection<CSimple.Models.HuggingFaceModel> AvailableModels { get; } = new ObservableCollection<CSimple.Models.HuggingFaceModel>(); // Keep for adding models

        private NodeViewModel _selectedNode;
        public NodeViewModel SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

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
        public OrientPageViewModel(FileService fileService) // Inject FileService
        {
            _fileService = fileService;

            // Initialize Commands
            AddModelNodeCommand = new Command<HuggingFaceModel>(async (model) => await AddModelNode(model));
            DeleteSelectedNodeCommand = new Command(async () => await DeleteSelectedNode());
            CreateNewPipelineCommand = new Command(async () => await CreateNewPipeline());
            RenamePipelineCommand = new Command(async () => await RenameCurrentPipeline());
            DeletePipelineCommand = new Command(async () => await DeleteCurrentPipeline());


            // Load initial data (pipelines and default/last pipeline)
            // Moved to OnAppearing in code-behind to ensure services are ready
        }

        // --- Public Methods (Called from View or Commands) ---

        public async Task InitializeAsync()
        {
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
                // No pipelines exist, create a new one
                Debug.WriteLine("No existing pipelines found. Creating a new one.");
                // 1. Create the pipeline in memory (sets CurrentPipelineName, adds to list)
                await CreateNewPipeline();
                // 2. Save the newly created empty pipeline immediately so the file exists
                await SaveCurrentPipelineAsync();
                // 3. Now select it in the picker. This will trigger LoadPipelineAsync via the setter,
                //    but the file should exist now.
                SelectedPipelineName = CurrentPipelineName;
                Debug.WriteLine($"Created, saved, and selected new pipeline: {CurrentPipelineName}");
            }
            await LoadAvailableModelsAsync(); // Load models for the picker
        }


        public async Task LoadAvailableModelsAsync()
        {
            // Simulate loading models (replace with actual logic)
            await Task.Delay(100); // Simulate async work
            if (!AvailableModels.Any()) // Avoid reloading if already populated
            {
                // Assuming HuggingFaceModel has 'Id' and 'ModelId' properties
                AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "gpt2", ModelId = "Text Generator (GPT-2)" });
                AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "resnet-50", ModelId = "Image Classifier (ResNet)" });
                AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "openai/whisper-base", ModelId = "Audio Recognizer (Whisper)" });
                AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "deepseek-ai/deepseek-coder-1.3b-instruct", ModelId = "DeepSeek Coder" });
                AvailableModels.Add(new CSimple.Models.HuggingFaceModel { Id = "meta-llama/Meta-Llama-3-8B-Instruct", ModelId = "Llama 3 8B Instruct" });
            }
            Debug.WriteLine($"Loaded {AvailableModels.Count} available models for picker.");
        }

        public async Task AddModelNode(CSimple.Models.HuggingFaceModel model)
        {
            if (model == null)
            {
                await ShowAlert?.Invoke("Error", "No model selected.", "OK");
                return;
            }

            // Use the NodeViewModel constructor
            var newNode = new NodeViewModel(
                Guid.NewGuid().ToString(), // Generate string ID
                model.ModelId ?? model.Id, // Use ModelId or Id
                InferNodeTypeFromName(model.ModelId ?? model.Id), // Infer type
                new PointF(100, 100) // Default position
            )
            {
                // Set properties not in constructor
                Size = new SizeF(150, 60), // Default size
                ModelPath = model.Id // Store the HuggingFace ID (or ModelId)
            };

            Nodes.Add(newNode);
            Debug.WriteLine($"Added node: {newNode.Name}");
            await SaveCurrentPipelineAsync(); // Save after adding
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
                    Connections.Remove(conn);
                }

                // Remove the node itself
                Nodes.Remove(SelectedNode);
                Debug.WriteLine($"Deleted node: {SelectedNode.Name}");
                SelectedNode = null; // Deselect
                await SaveCurrentPipelineAsync(); // Save after deleting
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
                // await SaveCurrentPipelineAsync(); // Consider moving this call
            }
        }

        // Call this from EndInteraction in the view after a drag completes
        public async Task FinalizeNodeMove()
        {
            await SaveCurrentPipelineAsync();
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
        }

        public void CancelConnection()
        {
            _temporaryConnectionState = null;
            Debug.WriteLine("Connection cancelled.");
        }

        // --- Pipeline Management Methods ---

        private async Task LoadAvailablePipelinesAsync()
        {
            Debug.WriteLine("Loading available pipelines...");
            var pipelines = await _fileService.ListPipelinesAsync();
            AvailablePipelineNames.Clear();
            foreach (var p in pipelines) // Assuming ListPipelinesAsync returns PipelineData with Name
            {
                AvailablePipelineNames.Add(p.Name);
            }
            Debug.WriteLine($"Found {AvailablePipelineNames.Count} pipelines.");
            OnPropertyChanged(nameof(AvailablePipelineNames)); // Notify UI
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
                    Connections.Add(connData.ToViewModel());
                }
                CurrentPipelineName = pipelineData.Name;
                // Manually update the picker selection if needed, though binding should handle it
                if (_selectedPipelineName != pipelineName)
                {
                    _selectedPipelineName = pipelineName;
                    OnPropertyChanged(nameof(SelectedPipelineName));
                }
                Debug.WriteLine($"Successfully loaded pipeline: {pipelineName}");
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

        private async Task SaveCurrentPipelineAsync()
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
                // DO NOT set SelectedPipelineName here. It will be set after saving.
            }
            // No need for await here as it's synchronous now
            await Task.CompletedTask; // Keep async signature if needed elsewhere, otherwise make sync
        }

        private void AddDefaultInputNodes()
        {
            float startX = 50;
            float startY = 50;
            float spacingY = 80; // Vertical spacing between nodes
            SizeF defaultSize = new SizeF(150, 50); // Slightly smaller default size for inputs

            var keyboardNode = new NodeViewModel(Guid.NewGuid().ToString(), "Keyboard Input", NodeType.Input, new PointF(startX, startY)) { Size = defaultSize };
            var mouseNode = new NodeViewModel(Guid.NewGuid().ToString(), "Mouse Input", NodeType.Input, new PointF(startX, startY + spacingY)) { Size = defaultSize };
            var cameraNode = new NodeViewModel(Guid.NewGuid().ToString(), "Camera Input", NodeType.Input, new PointF(startX, startY + 2 * spacingY)) { Size = defaultSize };
            var audioNode = new NodeViewModel(Guid.NewGuid().ToString(), "Audio Input", NodeType.Input, new PointF(startX, startY + 3 * spacingY)) { Size = defaultSize };

            Nodes.Add(keyboardNode);
            Nodes.Add(mouseNode);
            Nodes.Add(cameraNode);
            Nodes.Add(audioNode);

            Debug.WriteLine($"Added default input nodes to '{CurrentPipelineName}'.");
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

                // Load the next available pipeline or create a new one
                if (AvailablePipelineNames.Any())
                {
                    SelectedPipelineName = AvailablePipelineNames.First(); // Load the most recent remaining
                }
                else
                {
                    await CreateNewPipeline(); // Create new if list is empty
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
            // Use Contains (uppercase C)
            if (lowerName.Contains("input") || lowerName.Contains("keyboard") || lowerName.Contains("mouse") || lowerName.Contains("camera") || lowerName.Contains("audio"))
                return NodeType.Input;
            if (lowerName.Contains("output") || lowerName.Contains("display") || lowerName.Contains("speaker"))
                return NodeType.Output; // Use Output type
            // Add more specific checks if needed
            return NodeType.Model; // Default to Model
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
            // Debug.WriteLine($"Property Changed: {propertyName}"); // Optional: Log property changes
        }
    }
}
