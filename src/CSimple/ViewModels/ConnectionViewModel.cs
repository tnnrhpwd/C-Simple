using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.ViewModels
{
    public class ConnectionViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _sourceNodeId;
        private string _targetNodeId;
        // Add properties for source/target connection points if more specific than node ID

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

        public ConnectionViewModel(string id, string sourceNodeId, string targetNodeId)
        {
            Id = id;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
        }
    }
}
