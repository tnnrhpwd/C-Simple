using CSimple.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CSimple.ViewModels
{
    // Add QueryProperty attribute if receiving data via navigation
    [QueryProperty(nameof(ModelId), "modelId")]
    public class OrientPageViewModel : INotifyPropertyChanged
    {
        // --- Backing Fields ---
        private string _modelId;
        private NeuralNetworkModel _selectedModel;
        private string _trainingStatus = "Ready";
        private double _trainingProgress = 0;
        private string _performanceMetrics = "No data yet.";

        // --- Observable Properties ---
        public string ModelId
        {
            get => _modelId;
            set
            {
                _modelId = value;
                // Load model details when ModelId is set by navigation
                LoadModelDetailsAsync(value);
            }
        }

        public NeuralNetworkModel SelectedModel
        {
            get => _selectedModel;
            set => SetProperty(ref _selectedModel, value);
        }

        public string TrainingStatus
        {
            get => _trainingStatus;
            set => SetProperty(ref _trainingStatus, value);
        }

        public double TrainingProgress
        {
            get => _trainingProgress;
            set => SetProperty(ref _trainingProgress, value);
        }

        public string PerformanceMetrics
        {
            get => _performanceMetrics;
            set => SetProperty(ref _performanceMetrics, value);
        }

        // --- Commands ---
        public ICommand StartTrainingCommand { get; }
        public ICommand ViewPerformanceCommand { get; }
        public ICommand ManageTrainingDataCommand { get; }

        // --- UI Interaction Abstractions (Similar to NetPageViewModel) ---
        public Func<string, string, string, Task> ShowAlert { get; set; } = async (t, m, c) => { await Task.CompletedTask; };
        public Func<string, Task> NavigateTo { get; set; } = async (r) => { await Task.CompletedTask; };

        // --- Constructor ---
        public OrientPageViewModel(/* Inject services if needed, e.g., FileService, TrainingService */)
        {
            StartTrainingCommand = new Command(async () => await StartTrainingAsync(), () => SelectedModel != null);
            ViewPerformanceCommand = new Command(ViewModelPerformance, () => SelectedModel != null); // Renamed from ViewModelPerformanceCommand
            ManageTrainingDataCommand = new Command(async () => await ManageTrainingDataAsync(), () => SelectedModel != null); // Renamed from ManageTrainingCommand
        }

        // --- Command Implementations ---

        private async Task StartTrainingAsync()
        {
            if (SelectedModel == null) return;
            TrainingStatus = $"Training '{SelectedModel.Name}'...";
            TrainingProgress = 0;
            try
            {
                // Simulate training process
                for (int i = 1; i <= 10; i++)
                {
                    await Task.Delay(500); // Simulate work
                    TrainingProgress = i / 10.0;
                    if (i == 5) TrainingStatus = "Halfway through training...";
                }
                TrainingStatus = $"Training for '{SelectedModel.Name}' complete.";
                SelectedModel.LastTrainedDate = DateTime.Now; // Update model property (consider saving)
                SelectedModel.AccuracyScore = Math.Min(0.99, SelectedModel.AccuracyScore + 0.05); // Simulate improvement
                OnPropertyChanged(nameof(SelectedModel)); // Notify UI about model changes
                ViewModelPerformance(); // Update performance display
            }
            catch (Exception ex)
            {
                HandleError($"Error training model {SelectedModel.Name}", ex);
                TrainingStatus = "Training failed.";
                TrainingProgress = 0;
            }
        }

        // Method moved from NetPageViewModel
        private void ViewModelPerformance()
        {
            if (SelectedModel == null)
            {
                PerformanceMetrics = "No model selected.";
                ShowAlert?.Invoke("No Active Models", "Select a model to view performance.", "OK");
                return;
            }
            // Simulate fetching detailed performance data
            PerformanceMetrics = $"Detailed Performance for '{SelectedModel.Name}':\n" +
                                 $"Accuracy: {SelectedModel.AccuracyScore:P1}\n" +
                                 $"Last Trained: {SelectedModel.LastTrainedDate:g}\n" +
                                 $"Training Status: {SelectedModel.TrainingStatus}\n" +
                                 $"CPU Usage (simulated): {new Random().Next(5, 25)}%\n" +
                                 $"Memory Usage (simulated): {new Random().Next(100, 500)} MB";
            ShowAlert?.Invoke("Model Performance", PerformanceMetrics, "OK");
        }

        // Method moved from NetPageViewModel
        private async Task ManageTrainingDataAsync()
        {
            if (SelectedModel == null) return;
            await ShowAlert?.Invoke("Manage Training Data", $"This would open an interface to manage training data for '{SelectedModel.Name}'.", "OK");
            // Potentially navigate to a dedicated data management page or show a modal
            // await NavigateTo("///dataManagement?modelId=" + SelectedModel.Id);
        }

        // --- Private Helper Methods ---

        private async Task LoadModelDetailsAsync(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return;
            Debug.WriteLine($"OrientVM: Loading details for ModelId: {modelId}");
            // In a real app, you'd fetch the model details from a service
            // For now, we simulate finding it or creating a placeholder
            // This requires access to the model list, ideally via a shared service or repository.
            // Placeholder simulation:
            await Task.Delay(100); // Simulate async load
            SelectedModel = new NeuralNetworkModel
            {
                Id = modelId,
                Name = $"Model {modelId.Substring(0, 6)}...",
                Description = "Loaded for orientation/training.",
                AccuracyScore = 0.82, // Sample data
                LastTrainedDate = DateTime.Now.AddDays(-5) // Sample data
                // Load other properties from your data source
            };
            // Update command CanExecute status
            (StartTrainingCommand as Command)?.ChangeCanExecute();
            (ViewPerformanceCommand as Command)?.ChangeCanExecute();
            (ManageTrainingDataCommand as Command)?.ChangeCanExecute();
            ViewModelPerformance(); // Load initial performance metrics
            Debug.WriteLine($"OrientVM: Loaded details for {SelectedModel.Name}");
        }

        private void HandleError(string context, Exception ex)
        {
            Debug.WriteLine($"OrientPageViewModel Error - {context}: {ex.Message}\n{ex.StackTrace}");
            TrainingStatus = $"Error: {context}"; // Update status on error
        }

        // --- INotifyPropertyChanged Implementation ---
        public event PropertyChangedEventHandler PropertyChanged;
        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value)) return false;
            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
