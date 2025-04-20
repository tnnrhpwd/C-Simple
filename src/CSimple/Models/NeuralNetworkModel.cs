using System;
using System.ComponentModel; // Required for INotifyPropertyChanged if needed later
using System.Runtime.CompilerServices; // Required for CallerMemberName if needed later

namespace CSimple.Models
{
    // Consider implementing INotifyPropertyChanged if properties change after initial load
    public class NeuralNetworkModel // : INotifyPropertyChanged
    {
        // public event PropertyChangedEventHandler PropertyChanged;

        // Backing fields might be needed if implementing INotifyPropertyChanged
        private bool _isActive;

        public string Id { get; set; } = Guid.NewGuid().ToString(); // Default ID
        public string Name { get; set; }
        public string Description { get; set; }
        public ModelType Type { get; set; }
        public string AssociatedGoalId { get; set; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                // Example using SetProperty pattern if INotifyPropertyChanged is implemented
                // SetProperty(ref _isActive, value);
                _isActive = value;
                // OnPropertyChanged(); // Manually call if not using SetProperty
            }
        }

        public double AccuracyScore { get; set; } = 0.75;
        public DateTime LastTrainedDate { get; set; } = DateTime.Now.AddDays(-10);

        // Calculated properties
        public string TrainingStatus => AccuracyScore > 0.9 ? "Excellent" : AccuracyScore > 0.8 ? "Good" : "Needs Training";
        public string AccuracyDisplay => $"{AccuracyScore:P0}";

        // HuggingFace specific properties
        public bool IsHuggingFaceReference { get; set; } = false;
        public string HuggingFaceModelId { get; set; }

        // --- INotifyPropertyChanged Implementation (Optional) ---
        // protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        // {
        //     if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
        //     backingStore = value;
        //     OnPropertyChanged(propertyName);
        //     return true;
        // }
        // protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
        //     PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum ModelType
    {
        General,
        InputSpecific,
        GoalSpecific
    }
}
