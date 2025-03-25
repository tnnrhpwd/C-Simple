using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CSimple.Models
{
    public class NeuralModel : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString();
        private string _name;
        private string _description;
        private string _architecture;
        private double _accuracy;
        private DateTime _createdDate = DateTime.Now;
        private DateTime _lastTrainedDate = DateTime.Now;
        private bool _isActive;
        private int _trainingEpochs;
        private double _learningRate;
        private int _batchSize;
        private double _dropoutRate;
        private bool _usesScreenData;
        private bool _usesAudioData;
        private bool _usesTextData;
        private int _trainingDataPoints;
        private TimeSpan _trainingDuration;

        public string Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Architecture
        {
            get => _architecture;
            set
            {
                if (_architecture != value)
                {
                    _architecture = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Accuracy
        {
            get => _accuracy;
            set
            {
                // Validate accuracy is between 0 and 1
                double validatedValue = Math.Clamp(value, 0, 1);
                if (!_accuracy.Equals(validatedValue))
                {
                    _accuracy = validatedValue;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AccuracyPercentage));
                }
            }
        }

        public string AccuracyPercentage => $"{Accuracy:P1}";

        public DateTime CreatedDate
        {
            get => _createdDate;
            set
            {
                if (_createdDate != value)
                {
                    _createdDate = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastTrainedDate
        {
            get => _lastTrainedDate;
            set
            {
                if (_lastTrainedDate != value)
                {
                    _lastTrainedDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(LastTrainedText));
                }
            }
        }

        public string LastTrainedText => LastTrainedDate.ToString("g");

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanBeActivated));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public bool CanBeActivated => !IsActive;

        public string StatusText => IsActive ? "Active" : "Inactive";

        // Training parameters
        public int TrainingEpochs
        {
            get => _trainingEpochs;
            set
            {
                if (_trainingEpochs != value)
                {
                    _trainingEpochs = Math.Max(1, value); // Minimum of 1 epoch
                    OnPropertyChanged();
                }
            }
        }

        public double LearningRate
        {
            get => _learningRate;
            set
            {
                if (!_learningRate.Equals(value))
                {
                    _learningRate = Math.Clamp(value, 0.0001, 0.1); // Typical range
                    OnPropertyChanged();
                }
            }
        }

        public int BatchSize
        {
            get => _batchSize;
            set
            {
                if (_batchSize != value)
                {
                    // Batch size is typically a power of 2
                    _batchSize = Math.Max(1, value);
                    OnPropertyChanged();
                }
            }
        }

        public double DropoutRate
        {
            get => _dropoutRate;
            set
            {
                if (!_dropoutRate.Equals(value))
                {
                    _dropoutRate = Math.Clamp(value, 0, 0.5); // Typical range
                    OnPropertyChanged();
                }
            }
        }

        // Data sources
        public bool UsesScreenData
        {
            get => _usesScreenData;
            set
            {
                if (_usesScreenData != value)
                {
                    _usesScreenData = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DataSourcesText));
                }
            }
        }

        public bool UsesAudioData
        {
            get => _usesAudioData;
            set
            {
                if (_usesAudioData != value)
                {
                    _usesAudioData = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DataSourcesText));
                }
            }
        }

        public bool UsesTextData
        {
            get => _usesTextData;
            set
            {
                if (_usesTextData != value)
                {
                    _usesTextData = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DataSourcesText));
                }
            }
        }

        public string DataSourcesText
        {
            get
            {
                var sources = new System.Collections.Generic.List<string>();
                if (UsesScreenData) sources.Add("Screen");
                if (UsesAudioData) sources.Add("Audio");
                if (UsesTextData) sources.Add("Text");

                return sources.Count > 0
                    ? string.Join(", ", sources)
                    : "None";
            }
        }

        // Training statistics
        public int TrainingDataPoints
        {
            get => _trainingDataPoints;
            set
            {
                if (_trainingDataPoints != value)
                {
                    _trainingDataPoints = Math.Max(0, value);
                    OnPropertyChanged();
                }
            }
        }

        public TimeSpan TrainingDuration
        {
            get => _trainingDuration;
            set
            {
                if (_trainingDuration != value)
                {
                    _trainingDuration = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TrainingDurationText));
                }
            }
        }

        public string TrainingDurationText
        {
            get
            {
                if (TrainingDuration.TotalHours >= 1)
                    return $"{TrainingDuration.TotalHours:F1} hours";
                if (TrainingDuration.TotalMinutes >= 1)
                    return $"{TrainingDuration.TotalMinutes:F1} minutes";
                return $"{TrainingDuration.TotalSeconds:F1} seconds";
            }
        }

        // INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString() => Name ?? "Unnamed Model";

        // Clone the model
        public NeuralModel Clone()
        {
            return new NeuralModel
            {
                Id = this.Id,
                Name = this.Name,
                Description = this.Description,
                Architecture = this.Architecture,
                Accuracy = this.Accuracy,
                CreatedDate = this.CreatedDate,
                LastTrainedDate = this.LastTrainedDate,
                IsActive = this.IsActive,
                TrainingEpochs = this.TrainingEpochs,
                LearningRate = this.LearningRate,
                BatchSize = this.BatchSize,
                DropoutRate = this.DropoutRate,
                UsesScreenData = this.UsesScreenData,
                UsesAudioData = this.UsesAudioData,
                UsesTextData = this.UsesTextData,
                TrainingDataPoints = this.TrainingDataPoints,
                TrainingDuration = this.TrainingDuration
            };
        }
    }
}
