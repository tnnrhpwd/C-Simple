using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace CSimple.ViewModels
{
    public class OrientViewModel : INotifyPropertyChanged
    {
        // Properties for stats
        private int _activeModelsCount;
        public int ActiveModelsCount
        {
            get => _activeModelsCount;
            set
            {
                if (_activeModelsCount != value)
                {
                    _activeModelsCount = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActiveModelsText));
                }
            }
        }

        public string ActiveModelsText => ActiveModelsCount.ToString();

        private double _averageAccuracy;
        public double AverageAccuracy
        {
            get => _averageAccuracy;
            set
            {
                if (_averageAccuracy != value)
                {
                    _averageAccuracy = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AverageAccuracyText));
                }
            }
        }

        public string AverageAccuracyText => $"{AverageAccuracy:F1}%";

        private int _totalDataPoints;
        public int TotalDataPoints
        {
            get => _totalDataPoints;
            set
            {
                if (_totalDataPoints != value)
                {
                    _totalDataPoints = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(TotalDataPointsText));
                }
            }
        }

        public string TotalDataPointsText => TotalDataPoints.ToString("N0");

        // System Status
        private string _systemStatus = "Ready";
        public string SystemStatus
        {
            get => _systemStatus;
            set
            {
                if (_systemStatus != value)
                {
                    _systemStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        private double _processingPower;
        public double ProcessingPower
        {
            get => _processingPower;
            set
            {
                if (_processingPower != value)
                {
                    _processingPower = value;
                    OnPropertyChanged();
                }
            }
        }

        // Training parameters
        private string _selectedModelType;
        public string SelectedModelType
        {
            get => _selectedModelType;
            set
            {
                if (_selectedModelType != value)
                {
                    _selectedModelType = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _trainingEpochs = 10;
        public int TrainingEpochs
        {
            get => _trainingEpochs;
            set
            {
                if (_trainingEpochs != value)
                {
                    _trainingEpochs = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useScreenData = true;
        public bool UseScreenData
        {
            get => _useScreenData;
            set
            {
                if (_useScreenData != value)
                {
                    _useScreenData = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useAudioData = true;
        public bool UseAudioData
        {
            get => _useAudioData;
            set
            {
                if (_useAudioData != value)
                {
                    _useAudioData = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _useTextData = true;
        public bool UseTextData
        {
            get => _useTextData;
            set
            {
                if (_useTextData != value)
                {
                    _useTextData = value;
                    OnPropertyChanged();
                }
            }
        }

        // Commands
        public ICommand TrainModelCommand { get; }
        public ICommand ValidateModelCommand { get; }
        public ICommand ExportModelCommand { get; }
        public ICommand ImportModelCommand { get; }

        public OrientViewModel()
        {
            TrainModelCommand = new Command(async () => await TrainModel());
            ValidateModelCommand = new Command(ValidateModel);
            ExportModelCommand = new Command(ExportModel);
            ImportModelCommand = new Command(ImportModel);

            // Initialize with sample data
            ActiveModelsCount = 2;
            AverageAccuracy = 82.5;
            TotalDataPoints = 4501;
        }

        // Add this method to fix the build error
        public async Task<bool> TrainModelAsync()
        {
            // Call the existing TrainModel method
            await TrainModel();
            return true;
        }

        private async Task TrainModel()
        {
            SystemStatus = "Training...";

            // Simulate progress
            for (int i = 1; i <= 5; i++)
            {
                ProcessingPower = i / 5.0;
                await Task.Delay(1000);
            }

            // Update statistics
            ActiveModelsCount += 1;
            AverageAccuracy = (AverageAccuracy * (ActiveModelsCount - 1) + 90) / ActiveModelsCount;
            TotalDataPoints += 500;

            SystemStatus = "Ready";
        }

        private void ValidateModel()
        {
            // Validation logic
        }

        private void ExportModel()
        {
            // Export logic
        }

        private void ImportModel()
        {
            // Import logic
        }

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
