using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics; // Add this namespace for Color and Colors
using System;

namespace CSimple.Pages
{
    public partial class OrientPage : ContentPage
    {
        // Sample data
        private int activeModels = 0;
        private double averageAccuracy = 0.0;
        private int dataPoints = 0;

        public OrientPage()
        {
            InitializeComponent();
            InitializeUIWithSampleData();
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
        }

        private void OnLearningRateSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            LearningRateValueLabel.Text = e.NewValue.ToString("F4");
        }

        private void OnBatchSizeSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            // Ensure batch size is a power of 2
            int value = (int)Math.Pow(2, Math.Round(Math.Log(e.NewValue, 2)));
            BatchSizeValueLabel.Text = value.ToString();

            // Update the slider directly (removed IsDragging check which is not available)
            BatchSizeSlider.Value = value;
        }

        private void OnDropoutSliderValueChanged(object sender, ValueChangedEventArgs e)
        {
            DropoutValueLabel.Text = e.NewValue.ToString("F2");
        }

        // Button click handlers
        private void OnTrainModelClicked(object sender, EventArgs e)
        {
            // Get model type selection
            string modelType = ModelTypePicker.SelectedItem?.ToString() ?? "No model selected";

            // Get parameter values
            int epochs = (int)EpochsSlider.Value;
            bool useScreenData = ScreenDataCheckbox.IsChecked;
            bool useAudioData = AudioDataCheckbox.IsChecked;
            bool useTextData = TextDataCheckbox.IsChecked;

            // Get advanced parameters if that tab is selected
            string advancedParams = "";
            if (AdvancedSettingsPanel.IsVisible)
            {
                double learningRate = LearningRateSlider.Value;
                int batchSize = int.Parse(BatchSizeValueLabel.Text);
                double dropout = DropoutSlider.Value;
                string architecture = ArchitecturePicker.SelectedItem?.ToString() ?? "Default";

                advancedParams = $"\nLearning Rate: {learningRate:F4}\nBatch Size: {batchSize}\nDropout: {dropout:F2}\nArchitecture: {architecture}";
            }

            // Simulate starting a training process
            SystemStatusLabel.Text = "Training...";
            SystemStatusLabel.TextColor = Colors.Orange;

            // Show a message with the selected parameters
            string message = $"Training {modelType} model with:\nEpochs: {epochs}" +
                             $"\nData Sources: {(useScreenData ? "Screen " : "")}{(useAudioData ? "Audio " : "")}{(useTextData ? "Text" : "")}" +
                             advancedParams;

            DisplayAlert("Training Started", message, "OK");

            // In a real app, you would start the actual training process here
            SimulateTraining();
        }

        private async void SimulateTraining()
        {
            // Simulate a training process with progress updates
            for (int i = 1; i <= 5; i++)
            {
                await System.Threading.Tasks.Task.Delay(1000); // Wait for 1 second

                // Update progress
                ProcessingPowerBar.Progress = i / 5.0;
            }

            // Training complete
            SystemStatusLabel.Text = "Ready";
            SystemStatusLabel.TextColor = Colors.Green;

            // Update statistics with new values
            UpdateStatistics(activeModels + 1, (averageAccuracy * activeModels + 90) / (activeModels + 1), dataPoints + 500);

            await DisplayAlert("Training Complete", "Model has been successfully trained and is ready for use.", "OK");
        }

        private void OnValidateModelClicked(object sender, EventArgs e)
        {
            DisplayAlert("Validate Model", "Model validation would analyze your model's performance against test data.", "OK");
        }

        private void OnExportModelClicked(object sender, EventArgs e)
        {
            DisplayAlert("Export Model", "This would export your trained model for sharing or backup.", "OK");
        }

        private void OnImportModelClicked(object sender, EventArgs e)
        {
            DisplayAlert("Import Model", "This would allow you to import a pre-trained model.", "OK");
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
            DisplayAlert("Refresh Models", "This would refresh the list of available models.", "OK");
        }

        private void OnModelSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            // In a real app, you would filter the models list based on the search text
            DisplayAlert("Search", $"Searching for models containing: {e.NewTextValue}", "OK");
        }

        private void OnManageDataClicked(object sender, EventArgs e)
        {
            DisplayAlert("Manage Data", "This would open a data management interface for reviewing and curating your training data.", "OK");
        }
    }
}
