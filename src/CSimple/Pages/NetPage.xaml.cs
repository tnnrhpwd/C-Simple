﻿using Microsoft.Maui.Storage;
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
