using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.IO; // Add this namespace for Path, Directory, and File classes
using CSimple.Services;
using System.Collections.Generic;
using System.Globalization; // Add this namespace for CultureInfo

namespace CSimple.Pages;

public partial class NetPage : ContentPage
{
    // Model management properties
    public ObservableCollection<NeuralNetworkModel> AvailableModels { get; private set; } = new();
    public ObservableCollection<NeuralNetworkModel> ActiveModels { get; private set; } = new();

    // Mode properties
    public bool IsGeneralModeActive { get; set; } = true;
    public bool IsSpecificModeActive { get; set; } = false;
    public ObservableCollection<SpecificGoal> AvailableGoals { get; private set; } = new();

    // Current state
    public string CurrentModelStatus { get; set; } = "Idle";
    public string LastModelOutput { get; set; } = "No recent outputs";
    public int ActiveModelsCount => ActiveModels.Count;
    public bool IsLoading { get; set; } = false;

    // Enhanced properties for model visualization
    public string GeneralModeDescription => "General mode monitors all inputs and provides assistance based on learned patterns";
    public string SpecificModeDescription => "Specific mode executes predefined actions for particular goals";
    public string ModelStatusColor => ActiveModelsCount > 0 ? "#00aa00" : "#aa0000";
    public string ModelOutputHistory { get; set; } = "No recent outputs";
    public bool IsModelCommunicating { get; set; }

    // Commands
    public ICommand ToggleGeneralModeCommand { get; private set; }
    public ICommand ToggleSpecificModeCommand { get; private set; }
    public ICommand ActivateModelCommand { get; private set; }
    public ICommand DeactivateModelCommand { get; private set; }
    public ICommand LoadSpecificGoalCommand { get; private set; }
    public ICommand ShareModelCommand { get; private set; }
    public ICommand CommunicateWithModelCommand { get; private set; }
    public ICommand ExportModelCommand { get; private set; }
    public ICommand ImportModelCommand { get; private set; }
    public ICommand ManageTrainingCommand { get; private set; }
    public ICommand ViewModelPerformanceCommand { get; private set; }

    // Add these properties to the class
    private readonly HuggingFaceService _huggingFaceService;
    private List<HuggingFaceModel> _searchResults = new List<HuggingFaceModel>();

    public NetPage()
    {
        InitializeComponent();
        // Bind the context
        BindingContext = this;

        // Initialize commands
        ToggleGeneralModeCommand = new Command(ToggleGeneralMode);
        ToggleSpecificModeCommand = new Command(ToggleSpecificMode);
        ActivateModelCommand = new Command<NeuralNetworkModel>(ActivateModel);
        DeactivateModelCommand = new Command<NeuralNetworkModel>(DeactivateModel);
        LoadSpecificGoalCommand = new Command<SpecificGoal>(LoadSpecificGoal);
        ShareModelCommand = new Command<NeuralNetworkModel>(ShareModel);
        CommunicateWithModelCommand = new Command<string>(CommunicateWithModel);
        ExportModelCommand = new Command<NeuralNetworkModel>(ExportModel);
        ImportModelCommand = new Command(ImportModel);
        ManageTrainingCommand = new Command(ManageTraining);
        ViewModelPerformanceCommand = new Command(ViewModelPerformance);

        // Initialize the HuggingFace service
        _huggingFaceService = new HuggingFaceService();

        CheckUserLoggedIn();

        // Load sample models for demo
        LoadSampleModels();

        // Subscribe to global input notifications
        SubscribeToInputNotifications();
    }

    private async void CheckUserLoggedIn()
    {
        if (!await IsUserLoggedInAsync())
        {
            Debug.WriteLine("User is not logged in, navigating to login...");
            NavigateLogin();
        }
        else
        {
            Debug.WriteLine("User is logged in.");
        }
    }

    async void NavigateLogin()
    {
        try
        {
            await Shell.Current.GoToAsync($"///login");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error navigating to login: {ex.Message}");
        }
    }

    private async Task<bool> IsUserLoggedInAsync()
    {
        try
        {
            // Retrieve stored token
            var userToken = await SecureStorage.GetAsync("userToken");

            // Check if token exists and is not empty
            if (!string.IsNullOrEmpty(userToken))
            {
                Debug.WriteLine("User token found: " + userToken);
                return true; // User is logged in
            }
            else
            {
                Debug.WriteLine("No user token found.");
                return false; // User is not logged in
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error retrieving user token: {ex.Message}");
            return false;
        }
    }

    private void LoadSampleModels()
    {
        // Clear existing models
        AvailableModels.Clear();

        // Add general-purpose models
        AvailableModels.Add(new NeuralNetworkModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "General Assistant",
            Description = "Monitors all inputs and suggests actions based on learned patterns",
            Type = ModelType.General,
            AccuracyScore = 0.92,
            LastTrainedDate = DateTime.Now.AddDays(-5)
        });

        AvailableModels.Add(new NeuralNetworkModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Text Analyzer",
            Description = "Analyzes text inputs and automates text-related tasks",
            Type = ModelType.InputSpecific,
            AccuracyScore = 0.89,
            LastTrainedDate = DateTime.Now.AddDays(-12)
        });

        AvailableModels.Add(new NeuralNetworkModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Audio Assistant",
            Description = "Processes audio inputs for voice commands and responses",
            Type = ModelType.InputSpecific,
            AccuracyScore = 0.85,
            LastTrainedDate = DateTime.Now.AddDays(-8)
        });

        // Add specific goal models
        AvailableGoals.Add(new SpecificGoal
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Create Weekly Sales Report",
            Description = "Automatically generates sales reports from CSV data files",
            Category = "Business",
            DownloadCount = 1240
        });

        AvailableGoals.Add(new SpecificGoal
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Email Processor",
            Description = "Organizes inbox, drafts responses, and sets follow-up reminders",
            Category = "Productivity",
            DownloadCount = 875
        });

        AvailableGoals.Add(new SpecificGoal
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Meeting Scheduler",
            Description = "Handles meeting scheduling across teams with conflicting calendars",
            Category = "Collaboration",
            DownloadCount = 653
        });
    }

    private void ToggleGeneralMode()
    {
        try
        {
            Debug.WriteLine("ToggleGeneralMode: Beginning to toggle general mode");
            // Store original state to restore if something fails
            bool originalState = IsGeneralModeActive;

            try
            {
                // Check AvailableModels initialization
                if (AvailableModels == null)
                {
                    Debug.WriteLine("ToggleGeneralMode: AvailableModels was null, initializing");
                    AvailableModels = new ObservableCollection<NeuralNetworkModel>();
                    OnPropertyChanged(nameof(AvailableModels));
                }

                // Check ActiveModels initialization
                if (ActiveModels == null)
                {
                    Debug.WriteLine("ToggleGeneralMode: ActiveModels was null, initializing");
                    ActiveModels = new ObservableCollection<NeuralNetworkModel>();
                    OnPropertyChanged(nameof(ActiveModels));
                }

                IsGeneralModeActive = !IsGeneralModeActive;
                Debug.WriteLine($"ToggleGeneralMode: Set IsGeneralModeActive to {IsGeneralModeActive}");
                OnPropertyChanged(nameof(IsGeneralModeActive));

                // Ensure consistent state when enabling general mode
                if (IsGeneralModeActive && IsSpecificModeActive)
                {
                    Debug.WriteLine("ToggleGeneralMode: Also need to turn off specific mode");
                    IsSpecificModeActive = false;
                    OnPropertyChanged(nameof(IsSpecificModeActive));
                }

                if (IsGeneralModeActive)
                {
                    CurrentModelStatus = "General mode activated";
                    Debug.WriteLine("ToggleGeneralMode: General mode activated");
                }
                else
                {
                    CurrentModelStatus = "General mode deactivated";
                    Debug.WriteLine("ToggleGeneralMode: General mode deactivated");

                    // Safely deactivate models using a try-catch for each model
                    try
                    {
                        var generalModels = ActiveModels
                            ?.Where(m => m?.Type == ModelType.General)
                            ?.ToList() ?? new List<NeuralNetworkModel>();

                        Debug.WriteLine($"ToggleGeneralMode: Found {generalModels.Count} general models to deactivate");

                        foreach (var model in generalModels)
                        {
                            try
                            {
                                if (model != null)
                                {
                                    Debug.WriteLine($"ToggleGeneralMode: Deactivating model {model.Name}");
                                    DeactivateModel(model);
                                }
                            }
                            catch (Exception modelEx)
                            {
                                Debug.WriteLine($"Error deactivating model {model?.Name}: {modelEx.Message}");
                            }
                        }
                    }
                    catch (Exception modelsEx)
                    {
                        Debug.WriteLine($"Error processing general models: {modelsEx.Message}");
                    }
                }

                OnPropertyChanged(nameof(CurrentModelStatus));
                Debug.WriteLine("ToggleGeneralMode: Method completed successfully");
            }
            catch (Exception innerEx)
            {
                // Restore original state if the toggle failed
                Debug.WriteLine($"ToggleGeneralMode inner exception: {innerEx.GetType().Name}: {innerEx.Message}");
                Debug.WriteLine($"Stack trace: {innerEx.StackTrace}");

                try
                {
                    IsGeneralModeActive = originalState;
                    OnPropertyChanged(nameof(IsGeneralModeActive));

                    CurrentModelStatus = "Error toggling general mode";
                    OnPropertyChanged(nameof(CurrentModelStatus));
                }
                catch (Exception restoreEx)
                {
                    Debug.WriteLine($"Error restoring state: {restoreEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ToggleGeneralMode outer exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            HandleError("Error toggling general mode", ex);
        }
    }

    private void ToggleSpecificMode()
    {
        try
        {
            // Store original state to restore if something fails
            bool originalState = IsSpecificModeActive;
            Debug.WriteLine($"ToggleSpecificMode: Starting toggle. Current state: {originalState}");

            try
            {
                // Set the new state value
                IsSpecificModeActive = !IsSpecificModeActive;
                OnPropertyChanged(nameof(IsSpecificModeActive));

                // Ensure consistent state - when enabling specific mode, make sure general mode is off
                if (IsSpecificModeActive && IsGeneralModeActive)
                {
                    Debug.WriteLine("ToggleSpecificMode: Turning off general mode");
                    IsGeneralModeActive = false;
                    OnPropertyChanged(nameof(IsGeneralModeActive));
                }

                if (IsSpecificModeActive)
                {
                    // Extra safety - ensure AvailableGoals is initialized
                    if (AvailableGoals == null)
                    {
                        Debug.WriteLine("ToggleSpecificMode: AvailableGoals was null, initializing");
                        AvailableGoals = new ObservableCollection<SpecificGoal>();
                        OnPropertyChanged(nameof(AvailableGoals));
                    }

                    // Double-check goals have been loaded
                    if (AvailableGoals.Count == 0)
                    {
                        Debug.WriteLine("ToggleSpecificMode: AvailableGoals was empty, adding sample goal");
                        // Add a default goal if empty to prevent UI issues
                        AvailableGoals.Add(new SpecificGoal
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = "Default Goal",
                            Description = "Sample goal"
                        });
                    }

                    CurrentModelStatus = "Specific mode activated";
                    Debug.WriteLine("ToggleSpecificMode: Activated specific mode");
                }
                else
                {
                    CurrentModelStatus = "Specific mode deactivated";
                    Debug.WriteLine("ToggleSpecificMode: Deactivated specific mode");

                    // Safely deactivate goal-specific models
                    DeactivateGoalModels();
                }

                OnPropertyChanged(nameof(CurrentModelStatus));
                Debug.WriteLine("ToggleSpecificMode: Completed successfully");
            }
            catch (Exception innerEx)
            {
                // Restore original state if the toggle failed
                Debug.WriteLine($"ToggleSpecificMode inner exception: {innerEx.GetType().Name}: {innerEx.Message}");
                Debug.WriteLine($"Stack trace: {innerEx.StackTrace}");

                try
                {
                    // Restore original state
                    IsSpecificModeActive = originalState;
                    OnPropertyChanged(nameof(IsSpecificModeActive));
                    CurrentModelStatus = "Error toggling specific mode";
                    OnPropertyChanged(nameof(CurrentModelStatus));
                }
                catch (Exception restoreEx)
                {
                    Debug.WriteLine($"Error restoring state: {restoreEx.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ToggleSpecificMode outer exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            HandleError("Error toggling specific mode", ex);
        }
    }

    private void DeactivateGoalModels()
    {
        try
        {
            Debug.WriteLine("DeactivateGoalModels: Starting");

            if (ActiveModels == null)
            {
                Debug.WriteLine("DeactivateGoalModels: ActiveModels is null");
                return;
            }

            // Create a copy of the collection to avoid modification during enumeration
            var specificModels = ActiveModels
                .Where(m => m?.Type == ModelType.GoalSpecific)
                .ToList();

            Debug.WriteLine($"DeactivateGoalModels: Found {specificModels.Count} goal-specific models");

            foreach (var model in specificModels)
            {
                try
                {
                    if (model != null)
                    {
                        DeactivateModel(model);
                        Debug.WriteLine($"DeactivateGoalModels: Deactivated model '{model.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error deactivating model {model?.Name}: {ex.Message}");
                }
            }

            Debug.WriteLine("DeactivateGoalModels: Complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DeactivateGoalModels exception: {ex.Message}");
        }
    }

    private void ActivateModel(NeuralNetworkModel model)
    {
        if (model == null)
        {
            Debug.WriteLine("ActivateModel: Received null model, ignoring");
            return;
        }

        try
        {
            Debug.WriteLine($"ActivateModel: Beginning to activate model {model.Name}");

            // Check ActiveModels initialization
            if (ActiveModels == null)
            {
                Debug.WriteLine("ActivateModel: ActiveModels was null, initializing");
                ActiveModels = new ObservableCollection<NeuralNetworkModel>();
                OnPropertyChanged(nameof(ActiveModels));
            }

            // Check if mode is correct for this model type
            if ((model.Type == ModelType.General && !IsGeneralModeActive) ||
                (model.Type == ModelType.GoalSpecific && !IsSpecificModeActive))
            {
                Debug.WriteLine($"ActivateModel: Cannot activate {model.Name}: Incompatible mode");
                CurrentModelStatus = $"Cannot activate {model.Name}: Incompatible mode";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            // Check if not already active - create a safe copy of the list to avoid modification during enumeration
            bool isAlreadyActive = false;
            try
            {
                isAlreadyActive = ActiveModels?.Any(m => m?.Id == model.Id) ?? false;
            }
            catch (Exception checkEx)
            {
                Debug.WriteLine($"Error checking if model is active: {checkEx.Message}");
                isAlreadyActive = false;
            }

            if (!isAlreadyActive)
            {
                Debug.WriteLine($"ActivateModel: Adding model {model.Name} to active models");
                ActiveModels.Add(model);
                CurrentModelStatus = $"Model '{model.Name}' activated";

                // Validate that the model was actually added
                bool wasAdded = ActiveModels.Contains(model);
                Debug.WriteLine($"ActivateModel: Model was added successfully: {wasAdded}");

                // Start model monitoring for system inputs
                try
                {
                    StartModelMonitoring(model);
                }
                catch (Exception monitorEx)
                {
                    Debug.WriteLine($"Warning: Error starting model monitoring: {monitorEx.Message}");
                    // Continue even if monitoring fails
                }
            }
            else
            {
                Debug.WriteLine($"ActivateModel: Model {model.Name} is already active");
            }

            OnPropertyChanged(nameof(ActiveModelsCount));
            OnPropertyChanged(nameof(CurrentModelStatus));
            Debug.WriteLine("ActivateModel: Method completed successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ActivateModel exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            HandleError($"Error activating model: {model?.Name}", ex);
        }
    }

    private void DeactivateModel(NeuralNetworkModel model)
    {
        if (model == null) return;

        try
        {
            if (ActiveModels.Contains(model))
            {
                // Stop any monitoring
                StopModelMonitoring(model);

                ActiveModels.Remove(model);
                CurrentModelStatus = $"Model '{model.Name}' deactivated";
            }
            OnPropertyChanged(nameof(ActiveModelsCount));
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
        catch (Exception ex)
        {
            HandleError($"Error deactivating model: {model?.Name}", ex);
        }
    }

    private void LoadSpecificGoal(SpecificGoal goal)
    {
        if (goal == null)
        {
            Debug.WriteLine("LoadSpecificGoal: goal is null");
            return;
        }

        try
        {
            Debug.WriteLine($"LoadSpecificGoal: Loading goal '{goal.Name}'");

            // Create a model from the goal
            var goalModel = new NeuralNetworkModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Goal: {goal.Name}",
                Description = goal.Description,
                Type = ModelType.GoalSpecific,
                AssociatedGoalId = goal.Id
            };

            // Add to available models
            if (AvailableModels == null)
            {
                Debug.WriteLine("LoadSpecificGoal: AvailableModels was null, initializing");
                AvailableModels = new ObservableCollection<NeuralNetworkModel>();
            }

            AvailableModels.Add(goalModel);
            Debug.WriteLine("LoadSpecificGoal: Added goal model to available models");

            // Automatically activate if in specific mode
            if (IsSpecificModeActive)
            {
                Debug.WriteLine("LoadSpecificGoal: Specific mode is active, activating goal model");
                ActivateModel(goalModel);
            }

            CurrentModelStatus = $"Loaded goal '{goal.Name}'";
            OnPropertyChanged(nameof(CurrentModelStatus));
            Debug.WriteLine("LoadSpecificGoal: Complete");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadSpecificGoal exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            HandleError($"Error loading goal: {goal?.Name}", ex);
        }
    }

    private void ShareModel(NeuralNetworkModel model)
    {
        if (model == null) return;

        try
        {
            // Generate shareable link or code
            var shareCode = $"SHARE-{model.Id.Substring(0, 8)}";

            // In a real app, you would upload to a server and get a real share code

            CurrentModelStatus = $"Model '{model.Name}' shared with code: {shareCode}";
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
        catch (Exception ex)
        {
            HandleError($"Error sharing model: {model?.Name}", ex);
        }
    }

    private void CommunicateWithModel(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        try
        {
            if (ActiveModels.Count == 0)
            {
                CurrentModelStatus = "No active models to communicate with";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            // For demo purposes, just update the UI
            LastModelOutput = $"Response to '{message}': Processing your request...";

            // Simulate model processing using the new recommended approach
            // Use Dispatcher.Dispatch with Task.Delay instead
            Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ =>
            {
                Application.Current.Dispatcher.Dispatch(() =>
                {
                    LastModelOutput = $"Response to '{message}': I'll assist with that task right away.";
                    OnPropertyChanged(nameof(LastModelOutput));
                });
            });

            OnPropertyChanged(nameof(LastModelOutput));
        }
        catch (Exception ex)
        {
            HandleError("Error communicating with model", ex);
        }
    }

    private void ExportModel(NeuralNetworkModel model)
    {
        if (model == null) return;

        CurrentModelStatus = $"Exporting model '{model.Name}' for sharing...";
        OnPropertyChanged(nameof(CurrentModelStatus));

        // Simulate export process
        Task.Delay(1000).ContinueWith(_ =>
        {
            Application.Current.Dispatcher.Dispatch(() =>
            {
                CurrentModelStatus = $"Model '{model.Name}' exported successfully";
                OnPropertyChanged(nameof(CurrentModelStatus));
                DisplayAlert("Export Successful", $"Model '{model.Name}' has been exported and is ready for sharing.", "OK");
            });
        });
    }

    private async void ImportModel()
    {
        OnImportModelClicked(this, EventArgs.Empty);
    }

    private async void OnImportModelClicked(object sender, EventArgs e)
    {
        Debug.WriteLine("Import Model button clicked");
        try
        {
            CurrentModelStatus = "Opening file picker...";
            OnPropertyChanged(nameof(CurrentModelStatus));

            // Try using the simplest form of FilePicker
            try
            {
                // Use FilePickerImplementation directly for debugging
                var fileResult = await FilePicker.Default.PickAsync();

                if (fileResult == null)
                {
                    Debug.WriteLine("File selection canceled");
                    CurrentModelStatus = "Model import canceled";
                    OnPropertyChanged(nameof(CurrentModelStatus));
                    return;
                }

                Debug.WriteLine($"File selected: {fileResult.FileName}");
                IsLoading = true;
                OnPropertyChanged(nameof(IsLoading));

                try
                {
                    // Process the selected file
                    await ProcessSelectedModelFile(fileResult);
                }
                finally
                {
                    IsLoading = false;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
            catch (Exception pickerEx)
            {
                Debug.WriteLine($"Error with FilePicker: {pickerEx}");
                await DisplayAlert("Error", $"Could not open file picker: {pickerEx.Message}", "OK");
                CurrentModelStatus = "Error opening file picker";
                OnPropertyChanged(nameof(CurrentModelStatus));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnImportModelClicked exception: {ex}");
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async Task ProcessSelectedModelFile(FileResult fileResult)
    {
        try
        {
            // Display file details
            await DisplayAlert("File Selected",
                $"Name: {fileResult.FileName}\nSize: {await GetFileSizeAsync(fileResult)} KB",
                "Continue");

            // Copy file to app directory
            var modelDestinationPath = await CopyModelToAppDirectoryAsync(fileResult);

            if (string.IsNullOrEmpty(modelDestinationPath))
            {
                CurrentModelStatus = "Failed to copy model file";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            // Determine the model type
            var modelTypeResult = await DisplayActionSheet(
                "Select Model Type",
                "Cancel", null,
                "General", "Input Specific", "Goal Specific");

            if (modelTypeResult == "Cancel" || string.IsNullOrEmpty(modelTypeResult))
            {
                CurrentModelStatus = "Model import canceled - no type selected";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            ModelType modelType = ModelType.General;
            switch (modelTypeResult)
            {
                case "Input Specific":
                    modelType = ModelType.InputSpecific;
                    break;
                case "Goal Specific":
                    modelType = ModelType.GoalSpecific;
                    break;
            }

            // Create the model object
            var modelName = Path.GetFileNameWithoutExtension(fileResult.FileName);
            var importedModel = new NeuralNetworkModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = modelName,
                Description = $"Imported model from {fileResult.FileName}",
                Type = modelType,
                AccuracyScore = 0.8,
                LastTrainedDate = DateTime.Now
            };

            // Add to available models
            AvailableModels.Add(importedModel);
            CurrentModelStatus = $"Model '{importedModel.Name}' imported successfully";
            OnPropertyChanged(nameof(CurrentModelStatus));

            await DisplayAlert("Import Success",
                $"The model '{importedModel.Name}' has been imported and is ready to use.",
                "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ProcessSelectedModelFile error: {ex}");
            await DisplayAlert("Import Failed",
                $"Error importing model: {ex.Message}",
                "OK");
            CurrentModelStatus = "Error processing model file";
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
    }

    private async Task<string> GetFileSizeAsync(FileResult fileResult)
    {
        try
        {
            var stream = await fileResult.OpenReadAsync();
            return (stream.Length / 1024.0).ToString("F2");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting file size: {ex.Message}");
            return "Unknown";
        }
    }

    private async Task<string> CopyModelToAppDirectoryAsync(FileResult fileResult)
    {
        try
        {
            // Create models directory if it doesn't exist
            var modelsDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models");
            if (!Directory.Exists(modelsDirectory))
            {
                Directory.CreateDirectory(modelsDirectory);
            }

            // Create specific directory based on model type
            var modelTypeDirectory = Path.Combine(modelsDirectory, "ImportedModels");
            if (!Directory.Exists(modelTypeDirectory))
            {
                Directory.CreateDirectory(modelTypeDirectory);
            }

            // Generate destination path with unique name to avoid overwriting
            var fileName = Path.GetFileName(fileResult.FileName);
            var uniqueFileName = EnsureUniqueFileName(modelTypeDirectory, fileName);
            var destinationPath = Path.Combine(modelTypeDirectory, uniqueFileName);

            // Read the file
            using (var sourceStream = await fileResult.OpenReadAsync())
            using (var destinationStream = File.Create(destinationPath))
            {
                // Copy the file 
                await sourceStream.CopyToAsync(destinationStream);
            }

            Debug.WriteLine($"Model file copied to: {destinationPath}");
            return destinationPath;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error copying model file: {ex}");
            await DisplayAlert("Copy Error",
                $"Failed to copy model file: {ex.Message}",
                "OK");
            return null;
        }
    }

    private string EnsureUniqueFileName(string directory, string fileName)
    {
        string nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        string extension = Path.GetExtension(fileName);
        string finalFileName = fileName;
        int count = 1;

        while (File.Exists(Path.Combine(directory, finalFileName)))
        {
            finalFileName = $"{nameWithoutExtension}_{count}{extension}";
            count++;
        }

        return finalFileName;
    }

    private async void ManageTraining()
    {
        await DisplayAlert("Manage Training Data",
            "This would open an interface to manage the training data used by your models, including adding new data, cleaning existing data, or viewing model training performance.",
            "OK");

        await Shell.Current.GoToAsync("///orient");
    }

    private void ViewModelPerformance()
    {
        if (ActiveModels.Count == 0)
        {
            DisplayAlert("No Active Models", "Please activate a model first to view its performance metrics.", "OK");
            return;
        }

        DisplayAlert("Model Performance",
            "Your active models have processed 724 inputs today with an average accuracy of 92.7%. CPU usage average: 12%, Memory usage: 485MB.",
            "OK");
    }

    private void SubscribeToInputNotifications()
    {
        // Simulate occasional model activity using a timer
        var timer = new System.Threading.Timer(_ =>
        {
            if (ActiveModels.Count > 0 && IsGeneralModeActive)
            {
                Application.Current.Dispatcher.Dispatch(() =>
                {
                    IsModelCommunicating = true;
                    OnPropertyChanged(nameof(IsModelCommunicating));

                    var outputMessages = new[] {
                        "Detected user searching for sales data, opening relevant spreadsheets",
                        "Recognized meeting preparation pattern, loading presentation",
                        "Identified repeated text entry, suggesting automation",
                        "Observed file organization pattern, recommending folder structure"
                    };

                    var randomMessage = outputMessages[new Random().Next(outputMessages.Length)];
                    LastModelOutput = randomMessage;
                    OnPropertyChanged(nameof(LastModelOutput));

                    // Turn off communicating status after a delay
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Dispatch(() =>
                        {
                            IsModelCommunicating = false;
                            OnPropertyChanged(nameof(IsModelCommunicating));
                        });
                    });
                });
            }
        }, null, TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(15));
    }

    private void StartModelMonitoring(NeuralNetworkModel model)
    {
        // In a real app, start threads or services to monitor inputs
        Debug.WriteLine($"Starting monitoring for model: {model.Name}");
    }

    private void StopModelMonitoring(NeuralNetworkModel model)
    {
        // In a real app, stop threads or services
        Debug.WriteLine($"Stopping monitoring for model: {model.Name}");
    }

    private void HandleError(string context, Exception ex)
    {
        Debug.WriteLine($"{context}: {ex.Message}");
        CurrentModelStatus = $"Error: {context}";
        OnPropertyChanged(nameof(CurrentModelStatus));
    }

    private void OnGeneralModeToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            Debug.WriteLine($"OnGeneralModeToggled: Toggle value = {e.Value}");

            // Use direct approach with dispatcher to avoid command-related issues
            Dispatcher.Dispatch(() =>
            {
                try
                {
                    Debug.WriteLine("OnGeneralModeToggled: Using direct toggle implementation");

                    // Direct implementation without using command
                    if (e.Value != IsGeneralModeActive)
                    {
                        try
                        {
                            ToggleGeneralMode();
                        }
                        catch (Exception toggleEx)
                        {
                            Debug.WriteLine($"Error in toggle method: {toggleEx.Message}");
                            // Fallback - just set the property directly
                            IsGeneralModeActive = e.Value;
                            OnPropertyChanged(nameof(IsGeneralModeActive));

                            // Ensure consistent state
                            if (IsGeneralModeActive && IsSpecificModeActive)
                            {
                                IsSpecificModeActive = false;
                                OnPropertyChanged(nameof(IsSpecificModeActive));
                            }
                        }
                    }
                    else
                    {
                        Debug.WriteLine("OnGeneralModeToggled: Value already matches IsGeneralModeActive, ignoring");
                    }
                }
                catch (Exception dispatchEx)
                {
                    Debug.WriteLine($"OnGeneralModeToggled dispatch error: {dispatchEx.Message}");

                    // Last resort - just set the property and hope for the best
                    try
                    {
                        IsGeneralModeActive = e.Value;
                        OnPropertyChanged(nameof(IsGeneralModeActive));

                        CurrentModelStatus = $"General mode {(e.Value ? "activated" : "deactivated")} (emergency fallback)";
                        OnPropertyChanged(nameof(CurrentModelStatus));
                    }
                    catch (Exception finalEx)
                    {
                        Debug.WriteLine($"OnGeneralModeToggled final error: {finalEx.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnGeneralModeToggled exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    private void OnSpecificModeToggled(object sender, ToggledEventArgs e)
    {
        try
        {
            Debug.WriteLine($"OnSpecificModeToggled: Value={e.Value}");

            // Ensure handlers don't conflict
            if (e.Value != IsSpecificModeActive)
            {
                // Use our safer direct implementation rather than Command
                Dispatcher.Dispatch(() =>
                {
                    try
                    {
                        Debug.WriteLine("OnSpecificModeToggled: Using direct toggle implementation");
                        ToggleSpecificMode();
                    }
                    catch (Exception toggleEx)
                    {
                        Debug.WriteLine($"OnSpecificModeToggled toggle error: {toggleEx.Message}");

                        // Last resort - just set the property value
                        try
                        {
                            IsSpecificModeActive = e.Value;
                            OnPropertyChanged(nameof(IsSpecificModeActive));

                            // Ensure UI consistency
                            if (IsSpecificModeActive && IsGeneralModeActive)
                            {
                                IsGeneralModeActive = false;
                                OnPropertyChanged(nameof(IsGeneralModeActive));
                            }

                            CurrentModelStatus = $"Specific mode {(e.Value ? "activated" : "deactivated")} (emergency fallback)";
                            OnPropertyChanged(nameof(CurrentModelStatus));
                        }
                        catch (Exception lastEx)
                        {
                            Debug.WriteLine($"OnSpecificModeToggled last resort error: {lastEx.Message}");
                        }
                    }
                });
            }
            else
            {
                Debug.WriteLine("OnSpecificModeToggled: Value already matches IsSpecificModeActive, ignoring");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"OnSpecificModeToggled exception: {ex.GetType().Name}: {ex.Message}");
            Debug.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    // Add a helper method to safely handle mode conflicts
    private void EnsureConsistentModeState()
    {
        try
        {
            // Specific and General modes are mutually exclusive
            if (IsSpecificModeActive && IsGeneralModeActive)
            {
                // Default to the most recently changed mode
                IsGeneralModeActive = false;
                OnPropertyChanged(nameof(IsGeneralModeActive));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error ensuring consistent mode state: {ex.Message}");
        }
    }

    // Add a diagnostic method to check converter registration
    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Check if converters are available through app resources
            Debug.WriteLine("Checking for converters in resources:");
            if (Application.Current?.Resources != null)
            {
                var hasColorConverter = Application.Current.Resources.TryGetValue("BoolToColorConverter", out _);
                Debug.WriteLine($"BoolToColorConverter found: {hasColorConverter}");

                var hasIntColorConverter = Application.Current.Resources.TryGetValue("IntToColorConverter", out _);
                Debug.WriteLine($"IntToColorConverter found: {hasIntColorConverter}");

                var hasIntBoolConverter = Application.Current.Resources.TryGetValue("IntToBoolConverter", out _);
                Debug.WriteLine($"IntToBoolConverter found: {hasIntBoolConverter}");

                // If any required converters are missing, try to register them
                if (!hasColorConverter || !hasIntColorConverter || !hasIntBoolConverter)
                {
                    Debug.WriteLine("Attempting to register missing converters");

                    if (!hasColorConverter && Application.Current.Resources != null)
                        Application.Current.Resources.Add("BoolToColorConverter", new CSimple.Converters.BoolToColorConverter());

                    if (!hasIntColorConverter && Application.Current.Resources != null)
                        Application.Current.Resources.Add("IntToColorConverter", new CSimple.Converters.IntToColorConverter());

                    if (!hasIntBoolConverter && Application.Current.Resources != null)
                        Application.Current.Resources.Add("IntToBoolConverter", new CSimple.Converters.IntToBoolConverter());
                }
            }
            else
            {
                Debug.WriteLine("Application.Current?.Resources is null");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking converters: {ex.Message}");
        }
    }

    // Add these methods for HuggingFace integration
    private async void OnHuggingFaceSearchClicked(object sender, EventArgs e)
    {
        try
        {
            CurrentModelStatus = "Searching HuggingFace models...";
            OnPropertyChanged(nameof(CurrentModelStatus));
            IsLoading = true;
            OnPropertyChanged(nameof(IsLoading));

            string query = HuggingFaceSearchEntry.Text?.Trim() ?? "";
            string category = null;

            // Get selected category filter
            if (HuggingFaceCategoryPicker.SelectedIndex > 0)
            {
                var categoryFilters = _huggingFaceService.GetModelCategoryFilters();
                string selectedCategory = HuggingFaceCategoryPicker.SelectedItem?.ToString();
                if (selectedCategory != null && categoryFilters.ContainsKey(selectedCategory))
                {
                    category = categoryFilters[selectedCategory];
                }
            }

            // Search for models
            _searchResults = await _huggingFaceService.SearchModelsAsync(query, category, 10);

            if (_searchResults.Count > 0)
            {
                // Display results in a selection dialog
                var modelNames = _searchResults.Select(m => m.ModelId ?? m.Id).ToArray();
                string selectedModel = await DisplayActionSheet(
                    "Select a HuggingFace Model",
                    "Cancel",
                    null,
                    modelNames);

                if (selectedModel != "Cancel" && !string.IsNullOrEmpty(selectedModel))
                {
                    var model = _searchResults.FirstOrDefault(m => (m.ModelId ?? m.Id) == selectedModel);
                    if (model != null)
                    {
                        await ShowModelDetailsAndImport(model);
                    }
                }
                else
                {
                    CurrentModelStatus = "Model selection canceled";
                }
            }
            else
            {
                await DisplayAlert("No Results", "No models found matching your search criteria.", "OK");
                CurrentModelStatus = "No HuggingFace models found";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error searching HuggingFace: {ex}");
            await DisplayAlert("Search Error", $"Failed to search HuggingFace: {ex.Message}", "OK");
            CurrentModelStatus = "Error searching HuggingFace";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
    }

    private async void OnImportFromHuggingFaceClicked(object sender, EventArgs e)
    {
        try
        {
            // Show a dialog for entering a model ID directly
            string modelId = await DisplayPromptAsync(
                "Import HuggingFace Model",
                "Enter the model ID (e.g., 'openai/whisper-small')",
                "Import",
                "Cancel");

            if (string.IsNullOrEmpty(modelId))
            {
                return;
            }

            CurrentModelStatus = $"Getting details for {modelId}...";
            OnPropertyChanged(nameof(CurrentModelStatus));
            IsLoading = true;
            OnPropertyChanged(nameof(IsLoading));

            // Get model details
            var modelDetails = await _huggingFaceService.GetModelDetailsAsync(modelId);

            if (modelDetails != null)
            {
                await ShowModelDetailsAndImport(modelDetails);
            }
            else
            {
                await DisplayAlert("Model Not Found", $"Could not find model with ID: {modelId}", "OK");
                CurrentModelStatus = "Model not found";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error importing from HuggingFace: {ex}");
            await DisplayAlert("Import Error", $"Failed to import model: {ex.Message}", "OK");
            CurrentModelStatus = "Error importing from HuggingFace";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsLoading));
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
    }

    private async Task ShowModelDetailsAndImport(HuggingFaceModel model)
    {
        try
        {
            // Show model details
            bool importConfirmed = await DisplayAlert(
                "Model Details",
                $"Name: {model.ModelId ?? model.Id}\n" +
                $"Author: {model.Author}\n" +
                $"Type: {model.Pipeline_tag}\n" +
                $"Downloads: {model.Downloads}\n\n" +
                $"Description: {model.Description?.Substring(0, Math.Min(200, model.Description?.Length ?? 0))}\n\n" +
                "Do you want to import this model?",
                "Import",
                "Cancel"
            );

            if (!importConfirmed)
            {
                CurrentModelStatus = "Import canceled";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            CurrentModelStatus = $"Preparing to import {model.ModelId ?? model.Id}...";
            OnPropertyChanged(nameof(CurrentModelStatus));

            // Get model details if not already available
            HuggingFaceModelDetails modelDetails = model as HuggingFaceModelDetails;
            if (modelDetails == null || modelDetails.Files == null || modelDetails.Files.Count == 0)
            {
                modelDetails = await _huggingFaceService.GetModelDetailsAsync(model.ModelId ?? model.Id);
                Debug.WriteLine($"Retrieved model details, files count: {modelDetails?.Files?.Count ?? 0}");
            }

            // If we still don't have files, show options
            List<string> filesToDownload = new List<string>();

            if (modelDetails == null || modelDetails.Files == null || modelDetails.Files.Count == 0)
            {
                // Show options dialog first
                string option = await DisplayActionSheet(
                    "Model Import Options",
                    "Cancel",
                    null,
                    "Enter File Name Manually",
                    "Import Model Configuration",
                    "Use Python Script");

                switch (option)
                {
                    case "Enter File Name Manually":
                        string filename = await DisplayPromptAsync(
                            "Model File",
                            "Please enter a file name to download (e.g., 'pytorch_model.bin', 'model.safetensors')",
                            initialValue: "pytorch_model.bin");

                        if (string.IsNullOrEmpty(filename))
                        {
                            CurrentModelStatus = "Model import canceled - no file specified";
                            OnPropertyChanged(nameof(CurrentModelStatus));
                            return;
                        }

                        // Create model details with just this file
                        if (modelDetails == null)
                        {
                            modelDetails = new HuggingFaceModelDetails
                            {
                                Id = model.Id,
                                ModelId = model.ModelId,
                                Author = model.Author,
                                Description = model.Description,
                                Pipeline_tag = model.Pipeline_tag
                            };
                        }

                        filesToDownload.Add(filename);
                        break;

                    case "Import Model Configuration":
                        // Just import config.json which is often enough for structure information
                        if (modelDetails == null)
                        {
                            modelDetails = new HuggingFaceModelDetails
                            {
                                Id = model.Id,
                                ModelId = model.ModelId,
                                Author = model.Author,
                                Description = model.Description,
                                Pipeline_tag = model.Pipeline_tag
                            };
                        }

                        filesToDownload.Add("config.json");

                        // Also show the Python code for reference
                        await DisplayAlert(
                            "Using Python transformers",
                            $"This model can be used in Python with:\n\n" +
                            $"from transformers import AutoModel\n" +
                            $"model = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\")\n\n" +
                            $"The app will download the configuration file only.",
                            "OK");
                        break;

                    case "Use Python Script":
                        await DisplayAlert(
                            "Python Code for this Model",
                            $"# Load model directly\n" +
                            $"from transformers import AutoModel\n" +
                            $"model = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\", trust_remote_code=True)\n\n" +
                            $"# For C#/.NET integration:\n" +
                            $"1. Export the model to ONNX format in Python\n" +
                            $"2. Use Microsoft.ML.OnnxRuntime in your C# app\n" +
                            $"3. Or use ML.NET's Model Builder\n\n" +
                            $"Would you like to download model info anyway?",
                            "Yes", "No");

                        // Create some basic model info only
                        var infoDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models", "HuggingFace",
                            (model.ModelId ?? model.Id).Replace("/", "_").Replace("\\", "_"));

                        Directory.CreateDirectory(infoDirectory);
                        File.WriteAllText(
                            Path.Combine(infoDirectory, "model_info.txt"),
                            $"Model ID: {model.ModelId ?? model.Id}\n" +
                            $"Author: {model.Author}\n" +
                            $"Description: {model.Description}\n" +
                            $"Type: {model.Pipeline_tag}\n" +
                            $"Python code:\n" +
                            $"from transformers import AutoModel\n" +
                            $"model = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\", trust_remote_code=True)\n"
                        );

                        // Create and add the model reference
                        var pythonReferenceModel = new NeuralNetworkModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = GetFriendlyModelName(model.ModelId ?? model.Id) + " (Python)",
                            Description = model.Description ?? "Imported from HuggingFace (requires Python)",
                            Type = model.RecommendedModelType,
                            AccuracyScore = 0.85,
                            LastTrainedDate = DateTime.Now
                        };

                        AvailableModels.Add(pythonReferenceModel);
                        CurrentModelStatus = $"Added reference to {pythonReferenceModel.Name}";
                        OnPropertyChanged(nameof(CurrentModelStatus));

                        await DisplayAlert("Reference Added",
                            $"A reference to '{pythonReferenceModel.Name}' has been added. Use Python code to access the actual model.",
                            "OK");
                        return;

                    default:
                        CurrentModelStatus = "Model import canceled";
                        OnPropertyChanged(nameof(CurrentModelStatus));
                        return;
                }
            }
            else
            {
                // We have files, select which ones to download
                // First get recommended files based on common patterns
                var recommendedFiles = GetRecommendedFiles(modelDetails.Files);

                if (recommendedFiles.Count > 0)
                {
                    // Show them for selection
                    string option = await DisplayActionSheet(
                        "Select File to Import",
                        "Cancel",
                        "Download ALL Files",
                        recommendedFiles.ToArray());

                    if (option == "Download ALL Files")
                    {
                        // Choose all recommended files
                        filesToDownload.AddRange(recommendedFiles);
                    }
                    else if (option != "Cancel" && !string.IsNullOrEmpty(option))
                    {
                        filesToDownload.Add(option);

                        // Also ask about config and tokenizer files if available
                        var configFile = modelDetails.Files.FirstOrDefault(f => f.Contains("config.json"));
                        if (configFile != null && !filesToDownload.Contains(configFile))
                        {
                            bool downloadConfig = await DisplayAlert("Download Configuration",
                                $"Would you also like to download the configuration file ({configFile})?",
                                "Yes", "No");
                            if (downloadConfig)
                            {
                                filesToDownload.Add(configFile);
                            }
                        }

                        var tokenizerFile = modelDetails.Files.FirstOrDefault(f =>
                            f.Contains("tokenizer.") || f.Contains("vocab.") || f.Contains("merges."));
                        if (tokenizerFile != null && !filesToDownload.Contains(tokenizerFile))
                        {
                            bool downloadTokenizer = await DisplayAlert("Download Tokenizer",
                                $"Would you also like to download the tokenizer file ({tokenizerFile})?",
                                "Yes", "No");
                            if (downloadTokenizer)
                            {
                                filesToDownload.Add(tokenizerFile);
                            }
                        }
                    }
                    else
                    {
                        // Canceled
                        CurrentModelStatus = "File selection canceled";
                        OnPropertyChanged(nameof(CurrentModelStatus));
                        return;
                    }
                }
                else
                {
                    // No recommended files found, show all files
                    string selectedFile = await DisplayActionSheet(
                        "Select File to Import",
                        "Cancel",
                        null,
                        modelDetails.Files.ToArray());

                    if (selectedFile != "Cancel" && !string.IsNullOrEmpty(selectedFile))
                    {
                        filesToDownload.Add(selectedFile);
                    }
                    else
                    {
                        CurrentModelStatus = "File selection canceled";
                        OnPropertyChanged(nameof(CurrentModelStatus));
                        return;
                    }
                }
            }

            if (filesToDownload.Count == 0)
            {
                await DisplayAlert("Import Error", "No files selected for download", "OK");
                CurrentModelStatus = "No files selected";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            // Show Python usage instructions for this model
            await DisplayAlert("Model Usage Information",
                $"This model ({model.ModelId ?? model.Id}) can be used in Python with:\n\n" +
                $"from transformers import AutoModel, AutoTokenizer\n" +
                $"tokenizer = AutoTokenizer.from_pretrained(\"{model.ModelId ?? model.Id}\")\n" +
                $"model = AutoModel.from_pretrained(\"{model.ModelId ?? model.Id}\")\n\n" +
                $"For use in .NET, consider:\n" +
                $"- Export to ONNX format in Python then use OnnxRuntime in .NET\n" +
                $"- Use ML.NET Model Builder with this model\n" +
                $"- Use Python interop from your .NET application",
                "Continue with Download");

            // Continue with the download process
            // Create model directory
            var modelsDirectory = Path.Combine(FileSystem.AppDataDirectory, "Models");
            var huggingFaceDirectory = Path.Combine(modelsDirectory, "HuggingFace");
            var modelDirectory = Path.Combine(huggingFaceDirectory,
                (model.ModelId ?? model.Id).Replace("/", "_").Replace("\\", "_"));

            if (!Directory.Exists(modelsDirectory))
                Directory.CreateDirectory(modelsDirectory);
            if (!Directory.Exists(huggingFaceDirectory))
                Directory.CreateDirectory(huggingFaceDirectory);
            if (!Directory.Exists(modelDirectory))
                Directory.CreateDirectory(modelDirectory);

            // Download files
            bool anyDownloadSucceeded = false;
            foreach (var file in filesToDownload)
            {
                CurrentModelStatus = $"Downloading {file}...";
                OnPropertyChanged(nameof(CurrentModelStatus));

                string destinationPath = Path.Combine(modelDirectory, Path.GetFileName(file));
                bool downloadSuccess = await _huggingFaceService.DownloadModelFileAsync(
                    model.ModelId ?? model.Id,
                    file,
                    destinationPath);

                if (downloadSuccess)
                {
                    anyDownloadSucceeded = true;
                    Debug.WriteLine($"Successfully downloaded {file}");
                }
                else
                {
                    await DisplayAlert("Download Notice", $"Could not download {file}", "Continue");
                    Debug.WriteLine($"Failed to download {file}");
                }
            }

            if (!anyDownloadSucceeded)
            {
                CurrentModelStatus = "All downloads failed";
                OnPropertyChanged(nameof(CurrentModelStatus));
                await DisplayAlert("Import Failed", "Could not download any of the model files", "OK");
                return;
            }

            // Create and add the model
            var importedModel = new NeuralNetworkModel
            {
                Id = Guid.NewGuid().ToString(),
                Name = GetFriendlyModelName(model.ModelId ?? model.Id),
                Description = model.Description ?? "Imported from HuggingFace",
                Type = model.RecommendedModelType,
                AccuracyScore = 0.85,
                LastTrainedDate = DateTime.Now
            };

            AvailableModels.Add(importedModel);
            CurrentModelStatus = $"Successfully imported {importedModel.Name}";
            OnPropertyChanged(nameof(CurrentModelStatus));

            await DisplayAlert("Import Success",
                $"Model '{importedModel.Name}' has been imported and is ready to use.\n\n" +
                $"Saved in: {modelDirectory}\n\n" +
                $"Use with Python transformers library or export to ONNX format for .NET integration.",
                "OK");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling model details: {ex}");
            await DisplayAlert("Import Error", $"Failed to import model: {ex.Message}", "OK");
            CurrentModelStatus = "Error importing model";
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
    }

    // Helper method to find recommended files from a model's file list
    private List<string> GetRecommendedFiles(List<string> files)
    {
        // Priority file extensions
        var priorityExtensions = new[] {
            ".bin", ".safetensors", ".onnx", ".gguf", ".pt",
            ".model", ".h5", ".ckpt", ".weights"
        };

        var priorityFilePatterns = new[] {
            "pytorch_model", "model.", "weights.", "encoder.",
            "decoder.", "tokenizer.", "embedding."
        };

        // First check for priority file patterns with priority extensions
        var result = files.Where(f =>
            priorityFilePatterns.Any(p => f.Contains(p)) &&
            priorityExtensions.Any(e => f.EndsWith(e))
        ).ToList();

        // If none found, just get files with priority extensions
        if (result.Count == 0)
        {
            result = files.Where(f => priorityExtensions.Any(e => f.EndsWith(e))).ToList();
        }

        // If still none, look for any json configuration files
        if (result.Count == 0)
        {
            result = files.Where(f => f.EndsWith(".json")).ToList();
        }

        // If we have too many, limit the results
        if (result.Count > 5)
        {
            // Prioritize smaller file names that are likely to be the main model weights
            result = result.OrderBy(f => f.Length).Take(5).ToList();
        }

        return result;
    }

    // Add this method right after any other helper methods and before the model classes
    private string GetFriendlyModelName(string modelId)
    {
        // Remove organization prefix if present
        var name = modelId;
        if (name.Contains("/"))
        {
            var parts = name.Split('/');
            name = parts.Last();
        }

        // Replace dashes and underscores with spaces and title case
        name = name.Replace("-", " ").Replace("_", " ");

        // Title case the name
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        return textInfo.ToTitleCase(name);
    }
}

// Model classes to support functionality
public class NeuralNetworkModel
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public ModelType Type { get; set; }
    public string AssociatedGoalId { get; set; }
    public bool IsActive { get; set; }
    public double AccuracyScore { get; set; } = 0.75;
    public DateTime LastTrainedDate { get; set; } = DateTime.Now.AddDays(-10);
    public string TrainingStatus => AccuracyScore > 0.9 ? "Excellent" : AccuracyScore > 0.8 ? "Good" : "Needs Training";
    public string AccuracyDisplay => $"{AccuracyScore:P0}";
}

public enum ModelType
{
    General,
    InputSpecific,
    GoalSpecific
}

public class SpecificGoal
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Category { get; set; } = "General";
    public int DownloadCount { get; set; } = 0;
    public string DownloadCountDisplay => DownloadCount > 1000 ? $"{DownloadCount / 1000}K" : DownloadCount.ToString();
}
