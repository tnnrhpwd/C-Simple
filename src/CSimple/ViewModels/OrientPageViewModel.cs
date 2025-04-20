using CSimple.Models; // Keep if needed for model selection
using CSimple.Services; // Keep if needed for loading available models
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSimple.ViewModels
{
    // QueryProperty might not be needed anymore unless used to pre-populate a model node
    // [QueryProperty(nameof(ModelId), "modelId")]
    public class OrientPageViewModel : INotifyPropertyChanged
    {
        // --- Services (Keep if needed for listing available models) ---
        private readonly FileService _fileService;

        // --- Node Editor State ---
        public ObservableCollection<NodeViewModel> Nodes { get; } = new ObservableCollection<NodeViewModel>();
        public ObservableCollection<ConnectionViewModel> Connections { get; } = new ObservableCollection<ConnectionViewModel>();
        private NodeViewModel _selectedNode;
        internal object _temporaryConnectionState; // Made internal for access from View's Draw method

        // --- Available Models (To add new nodes) ---
        public ObservableCollection<NeuralNetworkModel> AvailableModels { get; } = new ObservableCollection<NeuralNetworkModel>();

        // --- Commands ---
        public ICommand AddModelNodeCommand { get; }
        public ICommand DeleteSelectedNodeCommand { get; }
        // Add commands for starting/ending connection drawing if needed

        // --- UI Interaction Abstractions ---
        public Func<string, string, string, Task> ShowAlert { get; set; } = async (t, m, c) => { await Task.CompletedTask; };
        // Corrected signature: title, cancel, destruction, buttons
        public Func<string, string, string, string[], Task<string>> ShowActionSheet { get; set; } = async (t, c, d, b) => await Task.FromResult<string>(null);


        // --- Constructor ---
        public OrientPageViewModel(FileService fileService /* Inject other services if needed */)
        {
            _fileService = fileService;

            // Initialize Input Nodes
            InitializeInputNodes();

            // Load available models for adding nodes
            LoadAvailableModelsAsync();

            // Initialize Commands
            AddModelNodeCommand = new Command<NeuralNetworkModel>(AddModelNode);
            DeleteSelectedNodeCommand = new Command(DeleteSelectedNode, () => SelectedNode != null && SelectedNode.Type == NodeType.Model); // Can only delete model nodes
        }

        // --- Properties ---
        public NodeViewModel SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    if (_selectedNode != null) _selectedNode.IsSelected = false;
                    _selectedNode = value;
                    if (_selectedNode != null) _selectedNode.IsSelected = true;
                    OnPropertyChanged();
                    (DeleteSelectedNodeCommand as Command)?.ChangeCanExecute();
                }
            }
        }

        // --- Initialization ---
        private void InitializeInputNodes()
        {
            Nodes.Add(new NodeViewModel("input_keyboard", "Keyboard Input", NodeType.Input, new PointF(50, 50)));
            Nodes.Add(new NodeViewModel("input_mouse", "Mouse Input", NodeType.Input, new PointF(200, 50)));
            Nodes.Add(new NodeViewModel("input_camera", "Camera Input", NodeType.Input, new PointF(350, 50)));
            Nodes.Add(new NodeViewModel("input_audio", "Audio Input", NodeType.Input, new PointF(500, 50)));
        }

        private async Task LoadAvailableModelsAsync()
        {
            try
            {
                // Use LoadNeuralNetworkModelsAsync (assuming this is the correct name in FileService)
                var models = await _fileService.LoadHuggingFaceModelsAsync() ?? new List<NeuralNetworkModel>();
                AvailableModels.Clear();
                foreach (var model in models)
                {
                    AvailableModels.Add(model);
                }
                Debug.WriteLine($"Loaded {AvailableModels.Count} available models.");
            }
            catch (Exception ex)
            {
                HandleError("Error loading available models", ex);
            }
        }

        // --- Command Implementations ---

        private void AddModelNode(NeuralNetworkModel modelToAdd)
        {
            if (modelToAdd == null) return;

            // Add a new node to the canvas, position TBD (e.g., center or next available slot)
            var newNode = new NodeViewModel(
                $"model_{Guid.NewGuid().ToString().Substring(0, 8)}",
                modelToAdd.Name,
                NodeType.Model,
                new PointF(100, 200 + Nodes.Count(n => n.Type == NodeType.Model) * 80) // Simple positioning
            );
            Nodes.Add(newNode);
            Debug.WriteLine($"Added model node: {newNode.Name}");
        }

        private void DeleteSelectedNode()
        {
            if (SelectedNode != null && SelectedNode.Type == NodeType.Model)
            {
                // Remove connections associated with this node
                var connectionsToRemove = Connections.Where(c => c.SourceNodeId == SelectedNode.Id || c.TargetNodeId == SelectedNode.Id).ToList();
                foreach (var conn in connectionsToRemove)
                {
                    Connections.Remove(conn);
                }

                // Remove the node itself
                Nodes.Remove(SelectedNode);
                SelectedNode = null; // Deselect
                Debug.WriteLine($"Deleted model node and associated connections.");
            }
        }

        // --- Public Methods (Called from View Interaction Logic) ---

        public NodeViewModel GetNodeAtPoint(PointF point)
        {
            // Find the node whose bounds contain the point (iterate in reverse for top-most node)
            for (int i = Nodes.Count - 1; i >= 0; i--)
            {
                var node = Nodes[i];
                var rect = new RectF(node.Position, node.Size);
                if (rect.Contains(point))
                {
                    return node;
                }
            }
            return null;
        }

        public void UpdateNodePosition(NodeViewModel node, PointF newPosition)
        {
            if (node != null)
            {
                node.Position = newPosition;
                // The GraphicsView needs to be invalidated externally to redraw
            }
        }

        public void StartConnection(NodeViewModel sourceNode /*, PointF startPoint */)
        {
            // Store state indicating a connection is being drawn from sourceNode
            _temporaryConnectionState = sourceNode; // Simple state, could be more complex
            Debug.WriteLine($"Starting connection from node: {sourceNode?.Name}");
        }

        public void UpdatePotentialConnection(PointF currentPoint)
        {
            // Update visual feedback for the line being drawn
            // Requires GraphicsView invalidation
        }

        public void CompleteConnection(NodeViewModel targetNode)
        {
            if (_temporaryConnectionState is NodeViewModel sourceNode && targetNode != null && sourceNode != targetNode)
            {
                // Prevent connecting Input -> Input or Model -> Input (basic validation)
                if (sourceNode.Type == NodeType.Input && targetNode.Type == NodeType.Input) return;
                if (targetNode.Type == NodeType.Input) return; // Cannot connect TO an input node

                // Check if connection already exists
                if (!Connections.Any(c => c.SourceNodeId == sourceNode.Id && c.TargetNodeId == targetNode.Id))
                {
                    var newConnection = new ConnectionViewModel(
                        $"conn_{Guid.NewGuid().ToString().Substring(0, 8)}",
                        sourceNode.Id,
                        targetNode.Id
                    );
                    Connections.Add(newConnection);
                    Debug.WriteLine($"Created connection from {sourceNode.Name} to {targetNode.Name}");
                }
            }
            CancelConnection();
        }

        public void CancelConnection()
        {
            _temporaryConnectionState = null;
            // Requires GraphicsView invalidation to remove temporary line
        }


        // --- Error Handling ---
        private void HandleError(string context, Exception ex)
        {
            Debug.WriteLine($"OrientPageViewModel Error - {context}: {ex.Message}\n{ex.StackTrace}");
            ShowAlert?.Invoke("Error", $"An error occurred: {ex.Message}", "OK");
        }

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
