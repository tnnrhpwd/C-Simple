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
        public string ModelPath // Getter/Setter for ModelPath
        {
            get => _modelPath;
            set => SetProperty(ref _modelPath, value);
        }
        public string DataType // Add DataType property for input classification
        {
            get => _dataType;
            set => SetProperty(ref _dataType, value);
        }

        // Constructor matching expected arguments (adjust if needed)
        public NodeViewModel(string id, string name, NodeType type, PointF position)
        {
            Id = id;
            Name = name;
            Type = type;
            Position = position;
            Size = new SizeF(150, 60); // Default size
        }

        // Parameterless constructor might be needed for some frameworks or serialization
        public NodeViewModel() { }


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
