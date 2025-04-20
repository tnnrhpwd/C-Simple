using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.ViewModels
{
    public class ConnectionViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _sourceNodeId;
        private string _targetNodeId;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        public string SourceNodeId
        {
            get => _sourceNodeId;
            set => SetProperty(ref _sourceNodeId, value);
        }
        public string TargetNodeId
        {
            get => _targetNodeId;
            set => SetProperty(ref _targetNodeId, value);
        }

        // Constructor matching expected arguments
        public ConnectionViewModel(string id, string sourceNodeId, string targetNodeId)
        {
            Id = id;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
        }

        // Parameterless constructor might be needed
        public ConnectionViewModel() { }

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
