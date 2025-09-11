using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    // Implement INotifyPropertyChanged to support two-way binding
    public class NeuralNetworkModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;        // Backing fields for properties
        private bool _isActive;
        private ModelInputType _inputType = ModelInputType.Unknown;
        private string _downloadButtonText = "Download to Device";
        private double _downloadProgress = 0.0;
        private bool _isDownloading = false;
        private string _downloadStatus = "";
        private bool _isDownloaded = false;

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

        // Aligned model properties
        public bool IsAlignedModel { get; set; } = false;
        public string ParentModelId { get; set; }
        public string ParentModelName { get; set; }
        public string AlignmentTechnique { get; set; }
        public DateTime? AlignmentDate { get; set; }

        // Computed property for display
        public string ModelOrigin => IsAlignedModel ? $"Aligned from {ParentModelName}" : (IsHuggingFaceReference ? "HuggingFace" : "Custom");
        public string ModelTypeDisplay => IsAlignedModel ? $"Aligned ({AlignmentTechnique})" : Type.ToString();        // Download button text property with change notification
        public string DownloadButtonText
        {
            get => _downloadButtonText;
            set
            {
                if (_downloadButtonText != value)
                {
                    _downloadButtonText = value;
                    OnPropertyChanged();
                }
            }
        }

        // Download progress properties
        public double DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                if (Math.Abs(_downloadProgress - value) > 0.001)
                {
                    _downloadProgress = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DownloadProgressPercentage));
                }
            }
        }

        public bool IsDownloading
        {
            get => _isDownloading;
            set
            {
                if (_isDownloading != value)
                {
                    _isDownloading = value;
                    OnPropertyChanged();
                }
            }
        }

        public string DownloadStatus
        {
            get => _downloadStatus;
            set
            {
                if (_downloadStatus != value)
                {
                    _downloadStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsDownloaded
        {
            get => _isDownloaded;
            set
            {
                if (_isDownloaded != value)
                {
                    _isDownloaded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ShowDeleteButton));
                    OnPropertyChanged(nameof(ShowTrainButton));
                    // Update button text based on download status
                    DownloadButtonText = value ? "Remove from Device" : "Download to Device";
                }
            }
        }

        // Helper property for UI binding (0-100 percentage)
        public string DownloadProgressPercentage => $"{DownloadProgress:P0}";

        // Property to control delete button visibility - show for all models that are downloaded or custom
        public bool ShowDeleteButton => IsDownloaded || !IsHuggingFaceReference;

        // Property to control train button visibility - show for all downloaded models
        public bool ShowTrainButton => IsDownloaded || !IsHuggingFaceReference;

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

        // Static property for input type display items - available to all model instances
        public static List<ModelInputTypeDisplayItem> InputTypeDisplayItems { get; } = new()
        {
            new ModelInputTypeDisplayItem { Value = ModelInputType.Text, DisplayName = "Text" },
            new ModelInputTypeDisplayItem { Value = ModelInputType.Image, DisplayName = "Image" },
            new ModelInputTypeDisplayItem { Value = ModelInputType.Audio, DisplayName = "Audio" },
            new ModelInputTypeDisplayItem { Value = ModelInputType.Unknown, DisplayName = "Unknown" }
        };

        // Helper class for input type display
        public class ModelInputTypeDisplayItem
        {
            public ModelInputType Value { get; set; }
            public string DisplayName { get; set; }
            public override string ToString() => DisplayName;
        }
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
