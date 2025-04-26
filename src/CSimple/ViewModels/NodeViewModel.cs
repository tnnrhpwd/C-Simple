using Microsoft.Maui.Graphics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.ViewModels
{
    public enum NodeType
    {
        Input,
        Model,
        Output // Add Output type
        // Add other types as needed
    }

    public class NodeViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private NodeType _type;
        private PointF _position;
        private SizeF _size;
        private bool _isSelected;
        private string _modelPath; // Add ModelPath property
        private string _dataType; // Add DataType property
        private int _ensembleInputCount; // ADDED: Count of incoming connections
        private string _classification; // ADDED: For Goal/Plan/Action classification

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public NodeType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
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

        public string ModelPath
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value);
        }

        public string DataType
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        public int EnsembleInputCount
        {
            get => _ensembleInputCount;
            set => SetProperty(ref _ensembleInputCount, value);
        }

        // ADDED: Property for text model classification (Goal/Plan/Action)
        public string Classification
        {
            get => _classification;
            set
            {
                if (SetProperty(ref _classification, value))
                {
                    // Update the displayed name automatically when classification changes
                    UpdateDisplayNameWithClassification();
                }
            }
        }

        // ADDED: Property to check if node is a text model
        public bool IsTextModel => DataType == "text" && Type == NodeType.Model;

        // ADDED: Original name without classification suffix
        private string _originalName;
        public string OriginalName
        {
            get => _originalName ?? _name;
            set => SetProperty(ref _originalName, value);
        }

        // Constructor matching expected arguments (adjust if needed)
        public NodeViewModel(string id, string name, NodeType type, PointF position)
        {
            Id = id;
            Name = name;
            OriginalName = name; // Store original name
            Type = type;
            Position = position;
            Size = new SizeF(150, 60); // Default size
        }

        // Parameterless constructor might be needed for some frameworks or serialization
        public NodeViewModel() { }

        // ADDED: Method to update the displayed name with classification
        private void UpdateDisplayNameWithClassification()
        {
            // Remove any existing classification suffix first
            string nameWithoutClassification = OriginalName;

            // Add new classification if one is set
            if (!string.IsNullOrEmpty(Classification))
            {
                Name = $"{nameWithoutClassification} ({Classification})";
            }
            else
            {
                // No classification, just use original name
                Name = nameWithoutClassification;
            }
        }

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
        }
    }
}
