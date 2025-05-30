﻿using CSimple.Models;
using CSimple.Services;
using CSimple.ViewModels; // Add ViewModel namespace
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Pages
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class NetPage : ContentPage
    {
        private readonly NetPageViewModel _viewModel;

        public NetPage(NetPageViewModel viewModel) // Inject ViewModel
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                // Add error handling for component initialization
                Debug.WriteLine($"Error initializing components: {ex.Message}");
                // Continue execution even if InitializeComponent fails
            }

            _viewModel = viewModel;
            BindingContext = _viewModel; // Set BindingContext

            // Wire up UI interaction delegates
            _viewModel.ShowAlert = (title, message, cancel) => DisplayAlert(title, message, cancel);
            _viewModel.ShowConfirmation = (title, message, accept, cancel) => DisplayAlert(title, message, accept, cancel);
            _viewModel.ShowActionSheet = (title, cancel, destruction, buttons) => DisplayActionSheet(title, cancel, destruction, buttons);
            _viewModel.ShowPrompt = (title, message, accept, cancel, initialValue) => DisplayPromptAsync(title, message, accept, cancel, initialValue: initialValue);
            _viewModel.PickFile = async () => await FilePicker.Default.PickAsync(new PickOptions());
            _viewModel.NavigateTo = async (route) => await Shell.Current.GoToAsync(route);
            _viewModel.ShowModelSelectionDialog = ShowHuggingFaceModelSelection; // Custom method for this UI

            // Check login status (can remain here or move to VM if navigation service is used)
            CheckUserLoggedIn();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CheckUserLoggedInAsync(); // Check login on appearing
            await _viewModel.LoadDataAsync(); // Load data when page appears
            CheckConverters(); // Keep converter check here

            // Add delay to ensure UI is rendered before refreshing pickers
            await Task.Delay(500);
            EnsurePickersHaveCorrectValues();
        }

        private void EnsurePickersHaveCorrectValues()
        {
            try
            {
                Debug.WriteLine("Refreshing model input type pickers");

                // Find all pickers in the CollectionView items
                var contentGrid = this.FindByName<Grid>("ContentGrid");
                if (contentGrid == null)
                {
                    Debug.WriteLine("ContentGrid not found");
                    return;
                }

                // Get CollectionView that contains model items
                var modelsCollection = this.FindByName<CollectionView>("ModelsCollectionView");
                if (modelsCollection == null)
                {
                    Debug.WriteLine("ModelsCollectionView not found");
                    return;
                }

                // Force refresh the collection to update bindings
                var models = _viewModel.AvailableModels.ToList();
                foreach (var model in models)
                {
                    Debug.WriteLine($"Model {model.Name} has InputType: {model.InputType}");
                    // Force a property change notification
                    model.InputType = model.InputType;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error ensuring pickers have correct values: {ex.Message}");
            }
        }

        // --- Login & Navigation (kept in code-behind for Shell interaction) ---
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
            try { await Shell.Current.GoToAsync($"///login"); }
            catch (Exception ex) { Debug.WriteLine($"Error navigating to login: {ex.Message}"); }
        }

        private async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                var userToken = await SecureStorage.GetAsync("userToken");
                return !string.IsNullOrEmpty(userToken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user token: {ex.Message}");
                return false;
            }
        }
        private async Task CheckUserLoggedInAsync() // Keep async version if needed elsewhere
        {
            if (!await IsUserLoggedInAsync()) NavigateLogin();
        }

        // --- Event Handlers ---

        // Event handlers now mostly delegate to ViewModel commands or methods
        private void OnGeneralModeToggled(object sender, ToggledEventArgs e)
        {
            // Update ViewModel property directly if binding doesn't work reliably,
            // or ensure the command handles the toggle correctly.
            // If using Command, this handler might not be needed if binding is two-way.
            if (_viewModel.IsGeneralModeActive != e.Value)
            {
                // If the command is bound correctly, it should handle the logic.
                // If not, manually trigger the command or update the VM property.
                // _viewModel.IsGeneralModeActive = e.Value; // Example direct update
                if (_viewModel.ToggleGeneralModeCommand.CanExecute(null))
                {
                    _viewModel.ToggleGeneralModeCommand.Execute(null);
                }
                Debug.WriteLine($"View: GeneralMode Toggled Event. New Value: {e.Value}");
            }
        }

        private void OnSpecificModeToggled(object sender, ToggledEventArgs e)
        {
            if (_viewModel.IsSpecificModeActive != e.Value)
            {
                // Similar to GeneralMode, rely on command binding or trigger manually.
                // _viewModel.IsSpecificModeActive = e.Value; // Example direct update
                if (_viewModel.ToggleSpecificModeCommand.CanExecute(null))
                {
                    _viewModel.ToggleSpecificModeCommand.Execute(null);
                }
                Debug.WriteLine($"View: SpecificMode Toggled Event. New Value: {e.Value}");
            }
        }

        private async void OnImportModelClicked(object sender, EventArgs e)
        {
            // Trigger the ViewModel's import logic
            if (_viewModel.ImportModelCommand.CanExecute(null))
            {
                _viewModel.ImportModelCommand.Execute(null);
            }
            await Task.CompletedTask; // Added to match original async void signature if needed
        }

        private async void OnHuggingFaceSearchClicked(object sender, EventArgs e)
        {
            // Trigger the ViewModel's search logic
            if (_viewModel.HuggingFaceSearchCommand.CanExecute(null))
            {
                _viewModel.HuggingFaceSearchCommand.Execute(null);
            }
            await Task.CompletedTask; // Added to match original async void signature if needed
        }

        private async void OnImportFromHuggingFaceClicked(object sender, EventArgs e)
        {
            // Trigger the ViewModel's direct import logic
            if (_viewModel.ImportFromHuggingFaceCommand.CanExecute(null))
            {
                _viewModel.ImportFromHuggingFaceCommand.Execute(null);
            }
            await Task.CompletedTask; // Added to match original async void signature if needed
        }

        // MODIFIED: Input type change handler with better feedback
        private void OnModelInputTypeChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is Picker picker)
                {
                    // Get the binding context of the picker, which should be the model
                    if (picker.BindingContext is NeuralNetworkModel model)
                    {
                        if (picker.SelectedItem is ModelInputType selectedInputType)
                        {
                            Debug.WriteLine($"Picker selected index: {picker.SelectedIndex}, value: {selectedInputType}");

                            // Create tuple parameter for command
                            var param = (model, selectedInputType);

                            // Execute the command
                            if (_viewModel.UpdateModelInputTypeCommand.CanExecute(param))
                            {
                                _viewModel.UpdateModelInputTypeCommand.Execute(param);
                                Debug.WriteLine($"Input type for model {model.Name} changed to {selectedInputType}");
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"WARNING: Selected item is not a ModelInputType: {picker.SelectedItem?.GetType().Name ?? "null"}");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"WARNING: Picker binding context is not a NeuralNetworkModel: {picker.BindingContext?.GetType().Name ?? "null"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnModelInputTypeChanged: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // --- UI Specific Helpers ---

        private async Task<CSimple.Models.HuggingFaceModel> ShowHuggingFaceModelSelection(List<CSimple.Models.HuggingFaceModel> searchResults)
        {
            if (searchResults == null || searchResults.Count == 0) return null;

            var modelNames = searchResults.Select(m => m.ModelId ?? m.Id).ToArray();
            string selectedModelName = await DisplayActionSheet(
                "Select a HuggingFace Model",
                "Cancel",
                null,
                modelNames);

            if (selectedModelName != "Cancel" && !string.IsNullOrEmpty(selectedModelName))
            {
                return searchResults.FirstOrDefault(m => (m.ModelId ?? m.Id) == selectedModelName);
            }
            return null;
        }

        private void CheckConverters()
        {
            // Keep converter check logic here as it relates to UI resources
            try
            {
                Debug.WriteLine("Checking for converters in resources:");
                if (Application.Current?.Resources != null)
                {
                    bool hasColorConverter = Application.Current.Resources.ContainsKey("BoolToColorConverter");
                    bool hasIntColorConverter = Application.Current.Resources.ContainsKey("IntToColorConverter");
                    bool hasIntBoolConverter = Application.Current.Resources.ContainsKey("IntToBoolConverter");
                    Debug.WriteLine($"Converters Found - BoolToColor: {hasColorConverter}, IntToColor: {hasIntColorConverter}, IntToBool: {hasIntBoolConverter}");

                    // Optionally register if missing (though they should be in App.xaml)
                }
                else { Debug.WriteLine("Application.Current?.Resources is null"); }
            }
            catch (Exception ex) { Debug.WriteLine($"Error checking converters: {ex.Message}"); }
        }

        // Remove properties, commands, and methods that were moved to the ViewModel
        // e.g., AvailableModels, ActiveModels, IsGeneralModeActive, IsSpecificModeActive, AvailableGoals,
        // CurrentModelStatus, LastModelOutput, ActiveModelsCount, IsLoading, IsModelCommunicating,
        // ToggleGeneralModeCommand, ToggleSpecificModeCommand, ActivateModelCommand, etc.
        // ToggleGeneralMode(), ToggleSpecificMode(), ActivateModel(), DeactivateModel(), LoadSpecificGoal(),
        // ShareModel(), CommunicateWithModel(), ExportModel(), ImportModel(), ManageTraining(), ViewModelPerformance(),
        // LoadPersistedModelsAsync(), LoadSampleGoals(), ShowModelDetailsAndImport(), SavePersistedModelsAsync(), etc.
    }

    // Remove Model definitions (NeuralNetworkModel, ModelType, SpecificGoal) - they will be moved
}
