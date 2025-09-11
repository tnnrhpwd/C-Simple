using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public enum ChatMode
    {
        Standard,
        Testing,
        ConsoleLogging,
        ModelTesting
    }

    public enum MessageType
    {
        Standard,
        TestInput,
        TestOutput,
        TestResult,
        ConsoleLog,
        ConsoleError,
        ConsoleWarning,
        ConsoleInfo,
        SystemStatus,
        ModelTest,
        ModelTestResult,
        IntelligenceLog,
        PipelineExecution
    }

    public class ChatMessage : INotifyPropertyChanged
    {
        private string _content;
        private DateTime _timestamp;
        private bool _isFromUser;
        private bool _isProcessing;
        private string _modelName;
        private string _llmSource; // New: local or api
        private bool _isEditing;
        private bool _includeInHistory = true; // New: whether to include this message in chat history for models
        private MessageType _messageType = MessageType.Standard;
        private ChatMode _chatMode = ChatMode.Standard;
        private string _testId;
        private Dictionary<string, object> _metadata = new();

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

        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        public bool IncludeInHistory
        {
            get => _includeInHistory;
            set => SetProperty(ref _includeInHistory, value);
        }

        public MessageType MessageType
        {
            get => _messageType;
            set => SetProperty(ref _messageType, value, onChanged: () =>
            {
                OnPropertyChanged(nameof(MessageTypeIcon));
                OnPropertyChanged(nameof(MessageTypeColor));
                OnPropertyChanged(nameof(MessageTypeDescription));
            });
        }

        public ChatMode ChatMode
        {
            get => _chatMode;
            set => SetProperty(ref _chatMode, value);
        }

        public string TestId
        {
            get => _testId;
            set => SetProperty(ref _testId, value);
        }

        public Dictionary<string, object> Metadata
        {
            get => _metadata;
            set => SetProperty(ref _metadata, value ?? new Dictionary<string, object>());
        }

        public string FormattedTimestamp => Timestamp.ToString("HH:mm");

        public string DisplayName => IsFromUser ? "You" : (string.IsNullOrEmpty(ModelName) ? "Assistant" : ModelName);

        public string MessageTypeIcon
        {
            get
            {
                return MessageType switch
                {
                    MessageType.TestInput => "🧪",
                    MessageType.TestOutput => "📊",
                    MessageType.TestResult => "✅",
                    MessageType.ConsoleLog => "📝",
                    MessageType.ConsoleError => "❌",
                    MessageType.ConsoleWarning => "⚠️",
                    MessageType.ConsoleInfo => "ℹ️",
                    MessageType.SystemStatus => "🔧",
                    MessageType.ModelTest => "🤖",
                    MessageType.ModelTestResult => "🎯",
                    MessageType.IntelligenceLog => "🧠",
                    MessageType.PipelineExecution => "⚙️",
                    _ => "💬"
                };
            }
        }

        public string MessageTypeColor
        {
            get
            {
                return MessageType switch
                {
                    MessageType.TestInput => "#2196F3",
                    MessageType.TestOutput => "#4CAF50",
                    MessageType.TestResult => "#8BC34A",
                    MessageType.ConsoleLog => "#607D8B",
                    MessageType.ConsoleError => "#F44336",
                    MessageType.ConsoleWarning => "#FF9800",
                    MessageType.ConsoleInfo => "#2196F3",
                    MessageType.SystemStatus => "#9C27B0",
                    MessageType.ModelTest => "#FF5722",
                    MessageType.ModelTestResult => "#E91E63",
                    MessageType.IntelligenceLog => "#673AB7",
                    MessageType.PipelineExecution => "#795548",
                    _ => "#757575"
                };
            }
        }

        public string MessageTypeDescription
        {
            get
            {
                return MessageType switch
                {
                    MessageType.TestInput => "Test Input",
                    MessageType.TestOutput => "Test Output",
                    MessageType.TestResult => "Test Result",
                    MessageType.ConsoleLog => "Console Log",
                    MessageType.ConsoleError => "Error",
                    MessageType.ConsoleWarning => "Warning",
                    MessageType.ConsoleInfo => "Info",
                    MessageType.SystemStatus => "System Status",
                    MessageType.ModelTest => "Model Test",
                    MessageType.ModelTestResult => "Model Result",
                    MessageType.IntelligenceLog => "Intelligence Log",
                    MessageType.PipelineExecution => "Pipeline Execution",
                    _ => "Message"
                };
            }
        }

        public bool IsTestMessage => MessageType == MessageType.TestInput || MessageType == MessageType.TestOutput || MessageType == MessageType.TestResult || MessageType == MessageType.ModelTest || MessageType == MessageType.ModelTestResult;
        public bool IsConsoleMessage => MessageType == MessageType.ConsoleLog || MessageType == MessageType.ConsoleError || MessageType == MessageType.ConsoleWarning || MessageType == MessageType.ConsoleInfo || MessageType == MessageType.IntelligenceLog;
        public bool IsSystemMessage => MessageType == MessageType.SystemStatus || MessageType == MessageType.PipelineExecution;

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

        public ChatMessage(string content, bool isFromUser, string modelName = null, string llmSource = null, bool includeInHistory = true) : this()
        {
            Content = content;
            IsFromUser = isFromUser;
            ModelName = modelName;
            LLMSource = llmSource;
            IncludeInHistory = includeInHistory;
        }

        public ChatMessage(string content, bool isFromUser, MessageType messageType, ChatMode chatMode = ChatMode.Standard, string testId = null, string modelName = null, string llmSource = null, bool includeInHistory = true) : this(content, isFromUser, modelName, llmSource, includeInHistory)
        {
            MessageType = messageType;
            ChatMode = chatMode;
            TestId = testId;
        }

        // Helper methods for creating specific message types
        public static ChatMessage CreateTestInput(string content, string testId, string modelName = null)
        {
            return new ChatMessage(content, true, MessageType.TestInput, ChatMode.Testing, testId, modelName);
        }

        public static ChatMessage CreateTestOutput(string content, string testId, string modelName = null)
        {
            return new ChatMessage(content, false, MessageType.TestOutput, ChatMode.Testing, testId, modelName);
        }

        public static ChatMessage CreateTestResult(string content, string testId, bool passed)
        {
            var message = new ChatMessage(content, false, MessageType.TestResult, ChatMode.Testing, testId);
            message.Metadata["TestPassed"] = passed;
            return message;
        }

        public static ChatMessage CreateConsoleLog(string content, MessageType logType = MessageType.ConsoleLog)
        {
            return new ChatMessage(content, false, logType, ChatMode.ConsoleLogging);
        }

        public static ChatMessage CreateSystemStatus(string content, string modelName = null)
        {
            return new ChatMessage(content, false, MessageType.SystemStatus, ChatMode.Standard, null, modelName);
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
