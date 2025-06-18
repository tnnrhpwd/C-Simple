using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    // Implement INotifyPropertyChanged to support two-way binding
    public class NeuralNetworkModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // Backing fields for properties
        private bool _isActive;
        private ModelInputType _inputType = ModelInputType.Unknown;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }
        public string Description { get; set; }
        public ModelType Type { get; set; }
        public string AssociatedGoalId { get; set; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                }
            }
        }

        public double AccuracyScore { get; set; } = 0.75;
        public DateTime LastTrainedDate { get; set; } = DateTime.Now.AddDays(-10);
        public DateTime LastUsed { get; set; } = DateTime.MinValue;

        // Calculated properties
        public string TrainingStatus => AccuracyScore > 0.9 ? "Excellent" : AccuracyScore > 0.8 ? "Good" : "Needs Training";
        public string AccuracyDisplay => $"{AccuracyScore:P0}";

        // HuggingFace specific properties
        public bool IsHuggingFaceReference { get; set; } = false;
        public string HuggingFaceModelId { get; set; }

        // Full property implementation for InputType with notification
        public ModelInputType InputType
        {
            get => _inputType;
            set
            {
                if (_inputType != value)
                {
                    _inputType = value;
                    OnPropertyChanged();
                }
            }
        }

        // Standard INPC implementation
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum ModelType
    {
        General,
        InputSpecific,
        GoalSpecific
    }

    public enum ModelInputType
    {
        Text,
        Image,
        Audio,
        Unknown
    }
}
