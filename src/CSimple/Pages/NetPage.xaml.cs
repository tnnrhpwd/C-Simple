using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Input;

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

    // Commands
    public ICommand ToggleGeneralModeCommand { get; private set; }
    public ICommand ToggleSpecificModeCommand { get; private set; }
    public ICommand ActivateModelCommand { get; private set; }
    public ICommand DeactivateModelCommand { get; private set; }
    public ICommand LoadSpecificGoalCommand { get; private set; }
    public ICommand ShareModelCommand { get; private set; }
    public ICommand CommunicateWithModelCommand { get; private set; }

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

        CheckUserLoggedIn();

        // Load sample models for demo
        LoadSampleModels();
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
        // Add sample models
        AvailableModels.Add(new NeuralNetworkModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "General Assistant",
            Description = "Monitors all inputs and suggests actions",
            Type = ModelType.General
        });

        AvailableModels.Add(new NeuralNetworkModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Text Analyzer",
            Description = "Specializes in text input analysis",
            Type = ModelType.InputSpecific
        });

        AvailableModels.Add(new NeuralNetworkModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Audio Assistant",
            Description = "Processes audio inputs for voice commands",
            Type = ModelType.InputSpecific
        });

        // Add sample specific goals
        AvailableGoals.Add(new SpecificGoal
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Create Sales Report",
            Description = "Automatically generates sales reports from data"
        });

        AvailableGoals.Add(new SpecificGoal
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Email Processing",
            Description = "Manages and categorizes incoming emails"
        });
    }

    private void ToggleGeneralMode()
    {
        try
        {
            IsGeneralModeActive = !IsGeneralModeActive;
            OnPropertyChanged(nameof(IsGeneralModeActive));

            if (IsGeneralModeActive)
            {
                CurrentModelStatus = "General mode activated";
            }
            else
            {
                CurrentModelStatus = "General mode deactivated";
                // Deactivate general models if needed
                var generalModels = ActiveModels.Where(m => m.Type == ModelType.General).ToList();
                foreach (var model in generalModels)
                {
                    DeactivateModel(model);
                }
            }

            OnPropertyChanged(nameof(CurrentModelStatus));
        }
        catch (Exception ex)
        {
            HandleError("Error toggling general mode", ex);
        }
    }

    private void ToggleSpecificMode()
    {
        try
        {
            IsSpecificModeActive = !IsSpecificModeActive;
            OnPropertyChanged(nameof(IsSpecificModeActive));

            if (IsSpecificModeActive)
            {
                CurrentModelStatus = "Specific mode activated";
            }
            else
            {
                CurrentModelStatus = "Specific mode deactivated";
                // Deactivate specific goal models if needed
                var specificModels = ActiveModels.Where(m => m.Type == ModelType.GoalSpecific).ToList();
                foreach (var model in specificModels)
                {
                    DeactivateModel(model);
                }
            }

            OnPropertyChanged(nameof(CurrentModelStatus));
        }
        catch (Exception ex)
        {
            HandleError("Error toggling specific mode", ex);
        }
    }

    private void ActivateModel(NeuralNetworkModel model)
    {
        if (model == null) return;

        try
        {
            // Check if mode is correct for this model type
            if (model.Type == ModelType.General && !IsGeneralModeActive ||
                model.Type == ModelType.GoalSpecific && !IsSpecificModeActive)
            {
                CurrentModelStatus = $"Cannot activate {model.Name}: Incompatible mode";
                OnPropertyChanged(nameof(CurrentModelStatus));
                return;
            }

            // Check if not already active
            if (!ActiveModels.Contains(model))
            {
                ActiveModels.Add(model);
                CurrentModelStatus = $"Model '{model.Name}' activated";

                // Start model monitoring for system inputs
                StartModelMonitoring(model);
            }
            OnPropertyChanged(nameof(ActiveModelsCount));
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
        catch (Exception ex)
        {
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
        if (goal == null) return;

        try
        {
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
            AvailableModels.Add(goalModel);

            // Automatically activate if in specific mode
            if (IsSpecificModeActive)
            {
                ActivateModel(goalModel);
            }

            CurrentModelStatus = $"Loaded goal '{goal.Name}'";
            OnPropertyChanged(nameof(CurrentModelStatus));
        }
        catch (Exception ex)
        {
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

            // Simulate model processing
            // In a real app, you would send to your ML backend

            // Update after "processing"
            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                LastModelOutput = $"Response to '{message}': I'll assist with that task right away.";
                OnPropertyChanged(nameof(LastModelOutput));
                return false;
            });

            OnPropertyChanged(nameof(LastModelOutput));
        }
        catch (Exception ex)
        {
            HandleError("Error communicating with model", ex);
        }
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
}
