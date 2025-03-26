using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public class ActionStep : INotifyPropertyChanged
    {
        private string _stepNumber;
        private string _stepAction;
        private bool _isSuccess;
        private DateTime _executedAt;
        private string _duration;

        public string StepNumber
        {
            get => _stepNumber;
            set
            {
                if (_stepNumber != value)
                {
                    _stepNumber = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StepAction
        {
            get => _stepAction;
            set
            {
                if (_stepAction != value)
                {
                    _stepAction = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsSuccess
        {
            get => _isSuccess;
            set
            {
                if (_isSuccess != value)
                {
                    _isSuccess = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime ExecutedAt
        {
            get => _executedAt;
            set
            {
                if (_executedAt != value)
                {
                    _executedAt = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Duration
        {
            get => _duration;
            set
            {
                if (_duration != value)
                {
                    _duration = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
