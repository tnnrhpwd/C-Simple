using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    /// <summary>
    /// Represents information about an execution group during model pipeline execution
    /// </summary>
    public class ExecutionGroupInfo : INotifyPropertyChanged
    {
        private int _groupNumber;
        public int GroupNumber
        {
            get => _groupNumber;
            set
            {
                if (SetProperty(ref _groupNumber, value))
                {
                    OnPropertyChanged(nameof(GroupDisplayName));
                    OnPropertyChanged(nameof(GroupDisplayNameForBinding));
                }
            }
        }

        private int _modelCount;
        public int ModelCount
        {
            get => _modelCount;
            set
            {
                if (SetProperty(ref _modelCount, value))
                {
                    OnPropertyChanged(nameof(GroupDisplayName));
                    OnPropertyChanged(nameof(GroupDisplayNameForBinding));
                }
            }
        }

        private double _executionDurationSeconds;
        public double ExecutionDurationSeconds
        {
            get => _executionDurationSeconds;
            set
            {
                if (SetProperty(ref _executionDurationSeconds, value))
                {
                    OnPropertyChanged(nameof(ExecutionDurationDisplay));
                }
            }
        }

        public string ExecutionDurationDisplay
        {
            get
            {
                if (_executionDurationSeconds <= 0)
                    return "0.0s";

                var timeSpan = System.TimeSpan.FromSeconds(_executionDurationSeconds);
                if (timeSpan.TotalMinutes >= 1)
                    return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                else
                    return $"{timeSpan.TotalSeconds:F1}s";
            }
        }

        private bool _isCurrentlyExecuting;
        public bool IsCurrentlyExecuting
        {
            get => _isCurrentlyExecuting;
            set => SetProperty(ref _isCurrentlyExecuting, value);
        }

        private bool _isCompleted;
        public bool IsCompleted
        {
            get => _isCompleted;
            set => SetProperty(ref _isCompleted, value);
        }

        public string GroupDisplayName => $"Group {GroupNumber} ({ModelCount} models)";

        public string GroupDisplayNameForBinding
        {
            get => $"Group {GroupNumber} ({ModelCount} models)";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(storage, value))
                return false;

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
