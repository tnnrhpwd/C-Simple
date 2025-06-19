using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
        private string _llmSource; // New: local or api

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

        public string LLMSource
        {
            get => _llmSource;
            set => SetProperty(ref _llmSource, value, onChanged: () =>
            {
                // Notify dependent properties when LLMSource changes
                OnPropertyChanged(nameof(ModelDisplayNameWithSource));
                OnPropertyChanged(nameof(ModelDisplayNameWithSourcePrefixed));
                Debug.WriteLine($"LLMSource updated to: '{value}' for message with ModelName: '{ModelName}'");
            });
        }

        public string FormattedTimestamp => Timestamp.ToString("HH:mm");

        public string DisplayName => IsFromUser ? "You" : (string.IsNullOrEmpty(ModelName) ? "Assistant" : ModelName);

        public string ModelDisplayNameWithSource
        {
            get
            {
                if (IsFromUser) return "You";
                var name = string.IsNullOrWhiteSpace(ModelName) ? "Assistant" : ModelName;
                if (!string.IsNullOrWhiteSpace(LLMSource))
                {
                    // Capitalize first letter of source
                    var src = char.ToUpper(LLMSource[0]) + LLMSource.Substring(1).ToLower();
                    return $"{src} {name}";
                }
                return name;
            }
        }

        public string ModelDisplayNameWithSourcePrefixed
        {
            get
            {
                Debug.WriteLine($"ModelDisplayNameWithSourcePrefixed called: IsFromUser={IsFromUser}, ModelName='{ModelName}', LLMSource='{LLMSource}'");
                if (IsFromUser || string.IsNullOrEmpty(ModelName))
                    return string.Empty;

                if (string.IsNullOrEmpty(LLMSource))
                {
                    Debug.WriteLine($"LLMSource is empty, returning ModelName: '{ModelName}'");
                    return ModelName;
                }

                var prefix = LLMSource.Equals("local", StringComparison.OrdinalIgnoreCase)
                    ? "Local"
                    : LLMSource.Equals("api", StringComparison.OrdinalIgnoreCase)
                        ? "API"
                        : char.ToUpper(LLMSource[0]) + LLMSource.Substring(1).ToLower();

                // Clean up the model name (remove any existing prefixes)
                var cleanModelName = ModelName;
                if (cleanModelName.StartsWith("Local ", StringComparison.OrdinalIgnoreCase) ||
                    cleanModelName.StartsWith("API ", StringComparison.OrdinalIgnoreCase))
                {
                    var spaceIndex = cleanModelName.IndexOf(' ');
                    if (spaceIndex > 0 && spaceIndex < cleanModelName.Length - 1)
                    {
                        cleanModelName = cleanModelName.Substring(spaceIndex + 1);
                    }
                }

                var result = $"{prefix} {cleanModelName}";
                Debug.WriteLine($"ModelDisplayNameWithSourcePrefixed: prefix='{prefix}', cleanModelName='{cleanModelName}', result='{result}'");
                return result;
            }
        }

        public ChatMessage()
        {
            Timestamp = DateTime.Now;
        }

        public ChatMessage(string content, bool isFromUser, string modelName = null, string llmSource = null) : this()
        {
            Content = content;
            IsFromUser = isFromUser;
            ModelName = modelName;
            LLMSource = llmSource;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "", Action onChanged = null)
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
