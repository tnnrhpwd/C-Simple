using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Add this namespace for Color and Colors
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CSimple.Services.AppModeService;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        // Sample data
        private int activeModels = 0;
        private double averageAccuracy = 0.0;
        private int dataPoints = 0;

        // Model types for ML.NET integration
        public enum ModelType
        {
            GeneralAssistant,
            SpecificTask,
            CommandAutomation,
            ImageRecognition,
            VoiceAssistant
        }

        // Data source tracking
        public class DataSource
        {
            public string Name { get; set; }
            public string Type { get; set; } // Screen, Audio, Text
            public int SampleCount { get; set; }
            public bool IsSelected { get; set; }
            public DateTime LastUpdated { get; set; }
            public string FilePath { get; set; }
            public double QualityScore { get; set; } // 0-100%
        }

        // New properties for ML.NET integration
        public ObservableCollection<DataSource> DataSources { get; set; } = new ObservableCollection<DataSource>();
        public ObservableCollection<string> AvailableModels { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> ModelArchitectures { get; set; } = new ObservableCollection<string>();
        public string SelectedArchitecture { get; set; }

        // New network type selection
        public List<string> NetworkTypes { get; set; } = new List<string>
        {
            "Convolutional Neural Network",
            "Recurrent Neural Network",
            "Transformer",
            "LSTM Network",
            "GAN",
            "Autoencoder"
        };

        // Task type selection for specific models
        public List<string> TaskTypes { get; set; } = new List<string>
        {
            "General Assistance",
            "Document Processing",
            "Image Analysis",
            "Voice Command Processing",
            "Business Process Automation"
        };

        // Training status tracking
        private bool isTraining = false;
        public bool IsTraining
        {
            get => isTraining;
            set
            {
                isTraining = value;
                OnPropertyChanged(nameof(IsTraining));
                OnPropertyChanged(nameof(CanStartTraining));
            }
        }

        public bool CanStartTraining => !IsTraining && SelectedDataSources?.Any() == true;

        // Track selected data sources
        public ObservableCollection<DataSource> SelectedDataSources { get; set; } = new ObservableCollection<DataSource>();

        // ML.NET specific parameters
        public bool UseGpu { get; set; } = true;
        public bool UseTransferLearning { get; set; } = true;
        public bool UseAutoML { get; set; } = false;
        public string MLNetVersion { get; set; } = "2.0";
        public int MaxTrainingTimeMinutes { get; set; } = 60;

        // Current model information
        private ModelConfig currentModelConfig = new ModelConfig();
        public ModelConfig CurrentModelConfig
        {
            get => currentModelConfig;
            set
            {
                currentModelConfig = value;
                OnPropertyChanged(nameof(CurrentModelConfig));
            }
        }

        // Model configuration class
        public class ModelConfig
        {
            public string Name { get; set; } = "New Model";
            public ModelType Type { get; set; } = ModelType.GeneralAssistant;
            public string Architecture { get; set; } = "Transformer";
            public int Epochs { get; set; } = 10;
            public float LearningRate { get; set; } = 0.001f;
            public int BatchSize { get; set; } = 32;
            public float Dropout { get; set; } = 0.2f;
            public bool UseDataAugmentation { get; set; } = true;
            public List<string> DataSourceIds { get; set; } = new List<string>();
            public string TaskType { get; set; } = "General Assistance";
            public string ShareableId { get; set; } = Guid.NewGuid().ToString();
            public bool IsPublic { get; set; } = false;
        }

        // Access to app mode service
        private readonly AppModeService _appModeService;

        public OrientPage()
        {
            InitializeComponent();

            // Get app mode service
            _appModeService = ServiceProvider.GetService<AppModeService>();

            InitializeUIWithSampleData();

            // Initialize collections
            InitializeModelArchitectures();
            InitializeSampleDataSources();

            // Set binding context
            BindingContext = this;
        }

        private void InitializeModelArchitectures()
        {
            ModelArchitectures.Clear();
            ModelArchitectures.Add("Convolutional Neural Network");
            ModelArchitectures.Add("Recurrent Neural Network");
            ModelArchitectures.Add("Transformer");
            ModelArchitectures.Add("LSTM Network");
            ModelArchitectures.Add("GAN");
            ModelArchitectures.Add("Autoencoder");

            SelectedArchitecture = ModelArchitectures.First();
        }

        private void InitializeSampleDataSources()
        {
            DataSources.Clear();

            // Sample data sources for demonstration
            DataSources.Add(new DataSource
            {
                Name = "Screen Captures",
                Type = "Screen",
                SampleCount = 1240,
                IsSelected = true,
                LastUpdated = DateTime.Now.AddDays(-2),
                QualityScore = 85
            });

            DataSources.Add(new DataSource
            {
                Name = "Audio Recordings",
                Type = "Audio",
                SampleCount = 856,
                IsSelected = true,
                LastUpdated = DateTime.Now.AddDays(-1),
                QualityScore = 78
            });

            DataSources.Add(new DataSource
            {
                Name = "Text Inputs",
                Type = "Text",
                SampleCount = 2405,
                IsSelected = true,
                LastUpdated = DateTime.Now.AddHours(-5),
                QualityScore = 92
            });

            // Add sample models
            AvailableModels.Clear();
            AvailableModels.Add("General Assistant v1.0");
            AvailableModels.Add("Image Recognition Model");
            AvailableModels.Add("Sales Report Automation");
            AvailableModels.Add("Email Response Assistant");
        }

        private void InitializeUIWithSampleData()
        {
            // Initialize stats
            UpdateStatistics(2, 82.5, 4501);

            // Initialize slider labels
            UpdateSliderLabels();
        }

        private void UpdateStatistics(int models, double accuracy, int data)
        {
            activeModels = models;
            averageAccuracy = accuracy;
            dataPoints = data;

            // Update UI labels
            ActiveModelsLabel.Text = activeModels.ToString();
            AccuracyLabel.Text = $"{averageAccuracy:F1}%";
            DataPointsLabel.Text = dataPoints.ToString("N0");
        }

        private void UpdateSliderLabels()
        {
            // Update basic tab slider values
            EpochsValueLabel.Text = ((int)EpochsSlider.Value).ToString();

            // Update advanced tab slider values
            LearningRateValueLabel.Text = LearningRateSlider.Value.ToString("F4");
            BatchSizeValueLabel.Text = ((int)BatchSizeSlider.Value).ToString();
            DropoutValueLabel.Text = DropoutSlider.Value.ToString("F2");
        }

        // Tab navigation
        private void OnBasicTabClicked(object sender, EventArgs e)
        {
            BasicTabButton.BackgroundColor = (Color)Application.Current.Resources["Primary"];
            AdvancedTabButton.BackgroundColor = Colors.Gray;
            BasicSettingsPanel.IsVisible = true;
            AdvancedSettingsPanel.IsVisible = false;
        }

        private void OnAdvancedTabClicked(object sender, EventArgs e)
        {
            BasicTabButton.BackgroundColor = Colors.Gray;
            AdvancedTabButton.BackgroundColor = (Color)Application.Current.Resources["Primary"];
            BasicSettingsPanel.IsVisible = false;
            AdvancedSettingsPanel.IsVisible = true;
        }

        // Slider value changed handlers
        private void OnEpochsSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            EpochsValueLabel.Text = ((int)e.NewValue).ToString();
            CurrentModelConfig.Epochs = (int)e.NewValue;
        }

        private void OnLearningRateSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            LearningRateValueLabel.Text = e.NewValue.ToString("F4");
            CurrentModelConfig.LearningRate = (float)e.NewValue;
        }

        private void OnBatchSizeSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            // Ensure batch size is a power of 2
            int value = (int)Math.Pow(2, Math.Round(Math.Log(e.NewValue, 2)));
            BatchSizeValueLabel.Text = value.ToString();
            CurrentModelConfig.BatchSize = value;

            // Update the slider directly (removed IsDragging check which is not available)
            BatchSizeSlider.Value = value;
        }

        private void OnDropoutSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            DropoutValueLabel.Text = e.NewValue.ToString("F2");
            CurrentModelConfig.Dropout = (float)e.NewValue;
        }

        // Button click handlers
        private async void OnTrainModelClicked(object sender, EventArgs e)
        {
            // Don't start if we're already training or don't have data
            if (IsTraining || !DataSources.Any(ds => ds.IsSelected))
            {
                await DisplayAlert("Cannot Start Training",
                    "Please select at least one data source first or wait for current training to complete.",
                    "OK");
                return;
            }

            // Get model type selection
            string modelType = ModelTypePicker.SelectedItem?.ToString() ?? "No model selected";
            CurrentModelConfig.Name = await DisplayPromptAsync("Model Name",
                "Enter a name for this model:",
                initialValue: $"{modelType} - {DateTime.Now:yyyy-MM-dd}");

            if (string.IsNullOrEmpty(CurrentModelConfig.Name))
            {
                await DisplayAlert("Training Cancelled", "A model name is required.", "OK");
                return;
            }

            // Get parameter values from UI
            CurrentModelConfig.Epochs = (int)EpochsSlider.Value;
            CurrentModelConfig.Type = GetModelTypeFromString(modelType);

            // Collect selected data source information
            SelectedDataSources.Clear();
            foreach (var ds in DataSources.Where(d => d.IsSelected))
            {
                SelectedDataSources.Add(ds);
            }

            // Collect advanced parameters if that tab is selected
            if (AdvancedSettingsPanel.IsVisible)
            {
                CurrentModelConfig.LearningRate = (float)LearningRateSlider.Value;
                CurrentModelConfig.BatchSize = int.Parse(BatchSizeValueLabel.Text);
                CurrentModelConfig.Dropout = (float)DropoutSlider.Value;
                CurrentModelConfig.Architecture = ArchitecturePicker.SelectedItem?.ToString() ?? "Default";
                CurrentModelConfig.UseDataAugmentation = DataAugmentationSwitch.IsToggled;
            }

            // Start training process
            IsTraining = true;
            SystemStatusLabel.Text = "Training...";
            SystemStatusLabel.TextColor = Colors.Orange;

            // Display training information
            StringBuilder trainingInfo = new StringBuilder();
            trainingInfo.AppendLine($"Training {CurrentModelConfig.Name} model with:");
            trainingInfo.AppendLine($"Model Type: {modelType}");
            trainingInfo.AppendLine($"Epochs: {CurrentModelConfig.Epochs}");
            trainingInfo.AppendLine($"Selected Data Sources: {string.Join(", ", SelectedDataSources.Select(ds => ds.Name))}");
            trainingInfo.AppendLine($"Total Samples: {SelectedDataSources.Sum(ds => ds.SampleCount):N0}");

            if (AdvancedSettingsPanel.IsVisible)
            {
                trainingInfo.AppendLine($"Learning Rate: {CurrentModelConfig.LearningRate:F4}");
                trainingInfo.AppendLine($"Batch Size: {CurrentModelConfig.BatchSize}");
                trainingInfo.AppendLine($"Dropout: {CurrentModelConfig.Dropout:F2}");
                trainingInfo.AppendLine($"Architecture: {CurrentModelConfig.Architecture}");
                trainingInfo.AppendLine($"Data Augmentation: {(CurrentModelConfig.UseDataAugmentation ? "Enabled" : "Disabled")}");
            }

            await DisplayAlert("Training Started", trainingInfo.ToString(), "OK");

            // Start the training simulation/execution
            await SimulateTraining();
        }

        private async Task SimulateTraining()
        {
            try
            {
                // Simulate a training process with progress updates
                int totalSteps = 5;

                for (int i = 1; i <= totalSteps; i++)
                {
                    if (!IsTraining) break; // Check if cancelled

                    // Update progress based on current step
                    double progress = (double)i / totalSteps;
                    ProcessingPowerBar.Progress = progress;

                    // Update status with metrics
                    SystemStatusLabel.Text = $"Training ({i}/{totalSteps}): " +
                        $"Loss: {0.5 - (0.4 * progress):F3}, " +
                        $"Accuracy: {50 + (40 * progress):F1}%";

                    await Task.Delay(1000); // Wait for 1 second
                }

                // Training complete
                SystemStatusLabel.Text = "Ready";
                SystemStatusLabel.TextColor = Colors.Green;

                if (IsTraining) // Only update if we weren't cancelled
                {
                    // Update statistics with new model
                    UpdateStatistics(
                        activeModels + 1,
                        (averageAccuracy * activeModels + 90) / (activeModels + 1),
                        dataPoints + SelectedDataSources.Sum(ds => ds.SampleCount)
                    );

                    // Add to available models
                    AvailableModels.Add(CurrentModelConfig.Name);

                    await DisplayAlert("Training Complete",
                        $"Model '{CurrentModelConfig.Name}' has been successfully trained and is ready for use.\n\n" +
                        $"Final Accuracy: 90%\nF1-Score: 0.89\nPrecision: 0.91\nRecall: 0.87",
                        "OK");

                    // Offer to export the model
                    bool shouldExport = await DisplayAlert("Export Model",
                        "Would you like to export this model to share with other users?",
                        "Yes", "No");

                    if (shouldExport)
                    {
                        await ExportModel(CurrentModelConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Training Error", $"An error occurred during model training: {ex.Message}", "OK");
                SystemStatusLabel.Text = "Error";
                SystemStatusLabel.TextColor = Colors.Red;
            }
            finally
            {
                IsTraining = false;
                ProcessingPowerBar.Progress = 1.0; // Complete the progress bar
            }
        }

        private async Task ExportModel(ModelConfig model)
        {
            try
            {
                // Show export options
                string exportOption = await DisplayActionSheet(
                    "Export Options",
                    "Cancel",
                    null,
                    "Export as Public Model",
                    "Export as Private Model",
                    "Export for Local Use Only"
                );

                if (exportOption == "Cancel" || string.IsNullOrEmpty(exportOption))
                    return;

                model.IsPublic = exportOption == "Export as Public Model";

                // Show exporting progress
                await DisplayAlert("Model Exported",
                    $"Model '{model.Name}' has been {(model.IsPublic ? "publicly" : "privately")} exported with ID: {model.ShareableId}",
                    "OK");

                // In a real app, you'd upload the model to a storage service here
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Error", $"Could not export model: {ex.Message}", "OK");
            }
        }

        private ModelType GetModelTypeFromString(string modelTypeStr)
        {
            switch (modelTypeStr)
            {
                case "General Assistant": return ModelType.GeneralAssistant;
                case "Specialized Task": return ModelType.SpecificTask;
                case "Command Automation": return ModelType.CommandAutomation;
                case "Image Recognition": return ModelType.ImageRecognition;
                case "Voice Assistant": return ModelType.VoiceAssistant;
                default: return ModelType.GeneralAssistant;
            }
        }

        private void OnValidateModelClicked(object sender, EventArgs e)
        {
            // Get selected model
            var selectedModel = GetSelectedModel();
            if (selectedModel == null)
            {
                DisplayAlert("No Model Selected", "Please select a model from the list to validate.", "OK");
                return;
            }

            // Show validation options dialog
            DisplayAlert("Validate Model",
                $"Validating model '{selectedModel}'\n\n" +
                "Validation Options:\n" +
                "- Cross-validation with 5 folds\n" +
                "- Test split: 20%\n" +
                "- Metrics: Accuracy, Precision, Recall, F1-Score", "Start Validation");
        }

        private string GetSelectedModel()
        {
            // In a real app, you'd have a proper model selection UI
            // This is a simplified placeholder

            // Try to get the name from the ModelTypePicker
            string selectedModel = ModelTypePicker.SelectedItem?.ToString();

            // If nothing selected there, check if we have a current config
            if (string.IsNullOrEmpty(selectedModel) && !string.IsNullOrEmpty(CurrentModelConfig?.Name))
            {
                selectedModel = CurrentModelConfig.Name;
            }

            return selectedModel;
        }

        private async void OnExportModelClicked(object sender, EventArgs e)
        {
            var selectedModel = GetSelectedModel();
            if (string.IsNullOrEmpty(selectedModel))
            {
                await DisplayAlert("No Model Selected", "Please select a model from the list to export.", "OK");
                return;
            }

            // Create a temporary model config for the selected model
            var modelToExport = new ModelConfig
            {
                Name = selectedModel,
                ShareableId = Guid.NewGuid().ToString()
            };

            await ExportModel(modelToExport);
        }

        private async void OnImportModelClicked(object sender, EventArgs e)
        {
            try
            {
                var importType = await DisplayActionSheet(
                    "Import Model",
                    "Cancel",
                    null,
                    "Import from Public Repository",
                    "Import from File",
                    "Import from Shared Link");

                if (string.IsNullOrEmpty(importType) || importType == "Cancel")
                    return;

                // Get model ID/link
                string modelIdentifier = await DisplayPromptAsync(
                    importType == "Import from Public Repository" ? "Public Model ID" :
                    importType == "Import from Shared Link" ? "Shared Link" : "File Path",
                    "Enter the model identifier:",
                    initialValue: importType == "Import from Public Repository" ?
                        "model_12345" : importType == "Import from Shared Link" ?
                        "https://csimple.ai/models/shared/abc123" : "/models/mymodel.mlnet");

                if (string.IsNullOrEmpty(modelIdentifier))
                    return;

                // Simulate import
                await SimulateImportModel(modelIdentifier, importType);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Import Error", $"Error importing model: {ex.Message}", "OK");
            }
        }

        private async Task SimulateImportModel(string modelId, string importType)
        {
            // Show loading indicator
            SystemStatusLabel.Text = "Importing...";
            SystemStatusLabel.TextColor = Colors.Orange;

            // Simulate network/file operation
            await Task.Delay(2000);

            // Create a sample imported model
            string modelName;

            if (importType == "Import from Public Repository")
                modelName = $"Public Model {modelId.Substring(modelId.Length - 5)}";
            else if (importType == "Import from Shared Link")
                modelName = $"Shared Model {Path.GetFileName(modelId)}";
            else
                modelName = Path.GetFileNameWithoutExtension(modelId);

            // Add to available models
            AvailableModels.Add(modelName);

            // Update stats
            UpdateStatistics(activeModels + 1, averageAccuracy, dataPoints + 500);

            // Show success
            SystemStatusLabel.Text = "Ready";
            SystemStatusLabel.TextColor = Colors.Green;

            await DisplayAlert("Import Successful",
                $"Model '{modelName}' has been imported successfully.\n\n" +
                "Model Information:\n" +
                "- Type: Specialized Task\n" +
                "- Architecture: Transformer\n" +
                "- Accuracy: 88.5%\n" +
                "- Parameters: 12.3M",
                "OK");
        }

        private void OnActivateModelClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is string modelId)
            {
                // Toggle the button state
                if (button.Text == "Activate")
                {
                    button.Text = "Deactivate";
                    button.BackgroundColor = (Color)Application.Current.Resources["Primary"];
                    DisplayAlert("Model Activated", $"Model {modelId} has been activated and is now running.", "OK");
                }
                else
                {
                    button.Text = "Activate";
                    button.BackgroundColor = Colors.Gray;
                    DisplayAlert("Model Deactivated", $"Model {modelId} has been deactivated.", "OK");
                }
            }
        }

        private void OnRefreshModelsClicked(object sender, EventArgs e)
        {
            // Refresh models from repository
            InitializeSampleDataSources(); // Re-fetch sample data in a real app
            DisplayAlert("Refresh Models", "Model list has been refreshed from the repository.", "OK");
        }

        private void OnModelSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            // In a real implementation, filter the models list based on search text
            DisplayAlert("Search", $"Searching for models containing: {e.NewTextValue}", "OK");
        }

        private void OnManageDataClicked(object sender, EventArgs e)
        {
            DisplayAlert("Manage Data", "Opening data management interface for reviewing and curating your training data.", "OK");
        }

        private async void OnDataSourceSelected(object sender, EventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.BindingContext is DataSource dataSource)
            {
                dataSource.IsSelected = checkBox.IsChecked;

                // Update UI based on selection
                OnPropertyChanged(nameof(CanStartTraining));

                // If nothing is selected, show warning
                if (!DataSources.Any(ds => ds.IsSelected))
                {
                    await DisplayAlert("No Data Selected",
                        "Please select at least one data source for training.",
                        "OK");
                }
            }
        }

        private void OnCancelTrainingClicked(object sender, EventArgs e)
        {
            if (IsTraining)
            {
                IsTraining = false;
                SystemStatusLabel.Text = "Cancelled";
                SystemStatusLabel.TextColor = Colors.Orange;
                DisplayAlert("Training Cancelled", "Model training has been cancelled.", "OK");
            }
        }

        private async void OnModelComparisionClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Model Comparison",
                "Compare multiple models to evaluate their performance across different metrics:\n\n" +
                "Selected Models:\n" +
                "1. General Assistant v1.0 (Accuracy: 85.2%)\n" +
                "2. General Assistant v2.0 (Accuracy: 87.8%)\n\n" +
                "Metrics:\n" +
                "- Accuracy\n" +
                "- Precision & Recall\n" +
                "- F1-Score\n" +
                "- Inference Speed\n" +
                "- Resource Usage",
                "OK");
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Check app mode and update UI accordingly
            if (_appModeService != null)
            {
                bool isOnline = _appModeService.CurrentMode == AppMode.Online;
                // Could update UI elements based on mode
            }
        }
    }
}
