using Microsoft.Maui.Graphics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.ViewModels
{
    public enum NodeType
    {
        Input,
        Model
    }

    public class NodeViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private NodeType _type;
        private PointF _position;
        private SizeF _size = new SizeF(120, 60); // Default size
        private bool _isSelected;

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

        public SizeF Size // Size might vary based on content
        {
            get => _size;
            set => SetProperty(ref _size, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Add properties for input/output connection points if needed

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

        public NodeViewModel(string id, string name, NodeType type, PointF position)
        {
            Id = id;
            Name = name;
            Type = type;
            Position = position;
        }
    }
}
