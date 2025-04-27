using System;
using System.Collections.Generic; // Added for List
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq; // Added for LINQ
using System.Runtime.CompilerServices;
using CSimple.Models; // Ensure Models namespace is included for NodeType
using Microsoft.Maui.Graphics; // For PointF

namespace CSimple.ViewModels
{
    public class NodeViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private NodeType _type;
        private bool _isSelected;
        private PointF _position;
        private SizeF _size;
        private string _dataType;
        private string _selectedEnsembleMethod;
        private ObservableCollection<string> _availableEnsembleMethods = new ObservableCollection<string>();
        private string _classification; // Added for text model classification
        private string _modelPath; // Added for model path storage
        private string _originalName; // Added to store name before classification suffix

        public string Id { get; }
        public string Name { get; set; } // Allow setting name if needed
        public NodeType Type { get; }
        public string OriginalModelId { get; set; } // Store the original HuggingFace ID if applicable
        public string ModelPath // Added Property
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value);
        }
        public string OriginalName // Added Property
        {
            get => _originalName;
            set => SetProperty(ref _originalName, value);
        }

        public PointF Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public SizeF Size
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Property to determine the data type (e.g., "text", "image", "audio")
        public string DataType
        {
            get => _dataType;
            set
            {
                if (SetProperty(ref _dataType, value))
                {
                    UpdateAvailableEnsembleMethods(); // Update methods when data type changes
                    OnPropertyChanged(nameof(IsTextModel)); // Notify IsTextModel change
                }
            }
        }

        // Expose EnsembleInputCount for UI binding (e.g., border thickness)
        public int EnsembleInputCount { get; set; } = 0; // Initialize to 0

        // --- Ensemble Properties --- 
        public bool IsModel => Type == NodeType.Model;

        public ObservableCollection<string> AvailableEnsembleMethods
        {
            get => _availableEnsembleMethods;
            private set => SetProperty(ref _availableEnsembleMethods, value); // Private set for internal control
        }

        public string SelectedEnsembleMethod
        {
            get => _selectedEnsembleMethod;
            set => SetProperty(ref _selectedEnsembleMethod, value);
        }
        // --- End Ensemble Properties ---


        // --- Text Model Classification ---
        public bool IsTextModel => IsModel && (DataType?.Equals("text", StringComparison.OrdinalIgnoreCase) ?? false);

        public string Classification
        {
            get => _classification;
            set
            {
                if (SetProperty(ref _classification, value))
                {
                    // Update the node name when classification changes
                    UpdateNodeNameBasedOnClassification();
                }
            }
        }
        // --- End Text Model Classification ---


        // Modified Constructor to accept ID as string and handle OriginalName
        public NodeViewModel(string id, string name, NodeType type, PointF position, string dataType = "unknown", string originalModelId = null, string modelPath = null, string classification = null, string originalName = null)
        {
            Id = id ?? Guid.NewGuid().ToString(); // Use provided ID or generate new
            Type = type;
            Position = position;
            Size = new SizeF(120, 60); // Default size, adjust as needed
            DataType = dataType; // Set data type on creation
            OriginalModelId = originalModelId;
            ModelPath = modelPath; // Store model path
            _classification = classification; // Store initial classification without triggering update yet
            _originalName = originalName ?? name; // Store original name or use initial name
            Name = name; // Set initial name (might be updated immediately if classification exists)

            // Update name based on initial classification if provided
            UpdateNodeNameBasedOnClassification();

            // Initial population of ensemble methods
            UpdateAvailableEnsembleMethods();
        }

        // Helper method to update the node's display name
        private void UpdateNodeNameBasedOnClassification()
        {
            if (IsTextModel && !string.IsNullOrEmpty(Classification) && Classification != "None")
            {
                // Use OriginalName if available, otherwise use current Name before suffix
                string baseName = !string.IsNullOrEmpty(OriginalName) ? OriginalName : Name.Split(" [")[0];
                Name = $"{baseName} [{Classification}]";
            }
            else
            {
                // Revert to original name if classification is removed or node is not a text model
                Name = OriginalName ?? Name.Split(" [")[0]; // Fallback if OriginalName is somehow null
            }
            OnPropertyChanged(nameof(Name)); // Notify that Name has changed
        }


        private void UpdateAvailableEnsembleMethods()
        {
            AvailableEnsembleMethods.Clear();
            if (!IsModel) return; // Only models have ensemble methods

            var methods = new List<string>();
            string dataTypeLower = DataType?.ToLowerInvariant() ?? "unknown";

            switch (dataTypeLower)
            {
                case "text":
                    methods.Add("Averaging (Logits)");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking (Meta-Learner)");
                    methods.Add("Concatenation (Features)");
                    break;
                case "image":
                    methods.Add("Averaging (Predictions)");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking (Meta-Learner)");
                    methods.Add("Feature Fusion (Late)");
                    methods.Add("Non-Maximum Suppression (Detection)");
                    break;
                case "audio":
                    methods.Add("Averaging (Features)");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking (Meta-Learner)");
                    methods.Add("Feature Fusion (Intermediate)");
                    break;
                default: // Multimodal or Unknown - Offer generic options
                    methods.Add("Averaging");
                    methods.Add("Majority Voting");
                    methods.Add("Weighted Averaging");
                    methods.Add("Stacking");
                    break;
            }

            foreach (var method in methods.OrderBy(m => m))
            {
                AvailableEnsembleMethods.Add(method);
            }

            // Set a default if none is selected or the previous one is invalid
            if (string.IsNullOrEmpty(SelectedEnsembleMethod) || !AvailableEnsembleMethods.Contains(SelectedEnsembleMethod))
            {
                SelectedEnsembleMethod = AvailableEnsembleMethods.FirstOrDefault();
            }
            else
            {
                // Ensure property changed is raised even if the list content changes but selection remains
                OnPropertyChanged(nameof(SelectedEnsembleMethod));
            }
            OnPropertyChanged(nameof(AvailableEnsembleMethods)); // Notify collection changed
        }


        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
