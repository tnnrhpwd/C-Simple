using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content;
        private DateTime _timestamp;
        private bool _isFromUser;
        private bool _isProcessing;
        private string _modelName;

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        public bool IsFromUser
        {
            get => _isFromUser;
            set => SetProperty(ref _isFromUser, value);
        }

        public bool IsFromAssistant => !IsFromUser;

        public bool IsProcessing
        {
            get => _isProcessing;
            set => SetProperty(ref _isProcessing, value);
        }

        public string ModelName
        {
            get => _modelName;
            set => SetProperty(ref _modelName, value);
        }

        public string FormattedTimestamp => Timestamp.ToString("HH:mm");

        public string DisplayName => IsFromUser ? "You" : (string.IsNullOrEmpty(ModelName) ? "Assistant" : ModelName);

        public ChatMessage()
        {
            Timestamp = DateTime.Now;
        }

        public ChatMessage(string content, bool isFromUser, string modelName = null) : this()
        {
            Content = content;
            IsFromUser = isFromUser;
            ModelName = modelName;
        }

        public event PropertyChangedEventHandler PropertyChanged;        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
