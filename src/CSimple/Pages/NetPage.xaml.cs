using CSimple.Models;
using CSimple.Services;
using CSimple.ViewModels; // Add ViewModel namespace
using static CSimple.ViewModels.NetPageViewModel; // Add static import for nested types
using Microsoft.Maui.Controls.Xaml;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.DependencyInjection;
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
            _viewModel.PickFile = async () => await FilePicker.Default.PickAsync(new PickOptions()); _viewModel.NavigateTo = async (route) => await Shell.Current.GoToAsync(route);
            _viewModel.ShowModelSelectionDialog = ShowHuggingFaceModelSelection; // Custom method for this UI            // Set up chat scroll functionality
            _viewModel.ScrollToBottom = () =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (FindByName("ChatCollectionView") is CollectionView collectionView &&
                        _viewModel.ChatMessages.Count > 0)
                    {
                        var lastItem = _viewModel.ChatMessages.LastOrDefault();
                        if (lastItem != null)
                        {
                            collectionView.ScrollTo(lastItem, animate: true);
                        }
                    }
                });
            };

            // Check login status (can remain here or move to VM if navigation service is used)
            CheckUserLoggedIn();
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await CheckUserLoggedInAsync(); // Check login on appearing
            await _viewModel.LoadDataAsync(); // Load data when page appears
            CheckConverters(); // Keep converter check here

            // Automatically select and activate the most recent text model
            SelectAndActivateRecentTextModel();

            // Add delay to ensure UI is rendered before refreshing pickers
            await Task.Delay(500);
            EnsurePickersHaveCorrectValues();
        }
        private void EnsurePickersHaveCorrectValues()
        {
            try
            {
                // Simplified converter check - only verify once and don't repeat the check
                // This removes the duplicate "Checking for converters in resources:" messages
                var allConvertersExist = Resources.ContainsKey("BoolToColorConverter") &&
                                        Resources.ContainsKey("IntToColorConverter") &&
                                        Resources.ContainsKey("IntToBoolConverter");

                if (!allConvertersExist)
                {
                    Debug.WriteLine("Warning: Some converters missing from resources");
                }
                // Remove the redundant "Completed refreshing" message since no actual refreshing occurs
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
        }        // MODIFIED: Input type change handler with better feedback
        private void OnModelInputTypeChanged(object sender, EventArgs e)
        {
            try
            {
                if (sender is Picker picker)
                {
                    // Get the binding context of the picker, which should be the model
                    if (picker.BindingContext is NeuralNetworkModel model)
                    {
                        // With the new approach, we get the ModelInputTypeDisplayItem from SelectedItem
                        if (picker.SelectedItem is ModelInputTypeDisplayItem selectedDisplayItem)
                        {
                            var selectedInputType = selectedDisplayItem.Value;
                            Debug.WriteLine($"Picker selected index: {picker.SelectedIndex}, value: {selectedInputType}");

                            // Create tuple parameter for command
                            var param = (model, selectedInputType);

                            // Execute the command
                            if (_viewModel.UpdateModelInputTypeCommand.CanExecute(param))
                            {
                                _viewModel.UpdateModelInputTypeCommand.Execute(param);
#if DEBUG
                                Debug.WriteLine($"Input type for model {model.Name} changed to {selectedInputType}");
#endif
                            }
                        }
                        else
                        {
#if DEBUG
                            Debug.WriteLine($"WARNING: Selected item is not a ModelInputTypeDisplayItem: {picker.SelectedItem?.GetType().Name ?? "null"}");
                            Debug.WriteLine($"SelectedItem value: {picker.SelectedItem}");
                            Debug.WriteLine($"SelectedIndex: {picker.SelectedIndex}");
#endif

                            // Fallback: Try to get the display item by index from the ViewModel
                            if (picker.SelectedIndex >= 0 && _viewModel.ModelInputTypeDisplayItems != null &&
                                picker.SelectedIndex < _viewModel.ModelInputTypeDisplayItems.Count)
                            {
                                var displayItem = _viewModel.ModelInputTypeDisplayItems[picker.SelectedIndex];
                                var indexedInputType = displayItem.Value;
#if DEBUG
                                Debug.WriteLine($"Retrieved enum value by index: {indexedInputType}");
#endif
                                var param = (model, indexedInputType);
                                if (_viewModel.UpdateModelInputTypeCommand.CanExecute(param))
                                {
                                    _viewModel.UpdateModelInputTypeCommand.Execute(param);
#if DEBUG
                                    Debug.WriteLine($"Input type for model {model.Name} changed to {indexedInputType} (via index)");
#endif
                                }
                            }
                        }
                    }
                    else
                    {
#if DEBUG
                        Debug.WriteLine($"WARNING: Picker binding context is not a NeuralNetworkModel: {picker.BindingContext?.GetType().Name ?? "null"}");
#endif
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in OnModelInputTypeChanged: {ex.Message}\n{ex.StackTrace}");
            }
        }

        // NEW: Media selection event handlers
        private async void OnSelectImageClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    // Update the image display
                    if (FindByName("SelectedImage") is Image selectedImage)
                    {
                        selectedImage.Source = ImageSource.FromFile(result.FullPath);
                        selectedImage.IsVisible = true;
                    }

                    // Store the selected image in the view model if needed
                    // _viewModel.SelectedImagePath = result.FullPath;

                    Debug.WriteLine($"Selected image: {result.FileName}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting image: {ex.Message}");
                await DisplayAlert("Error", "Failed to select image. Please try again.", "OK");
            }
        }

        private async void OnSelectAudioClicked(object sender, EventArgs e)
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an audio file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.iOS, new[] { "public.audio" } },
                        { DevicePlatform.Android, new[] { "audio/*" } },
                        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".aac" } },
                        { DevicePlatform.macOS, new[] { "mp3", "wav", "m4a", "aac" } }
                    })
                });

                if (result != null)
                {
                    // Store the selected audio file path in the view model if needed
                    // _viewModel.SelectedAudioPath = result.FullPath;

                    Debug.WriteLine($"Selected audio: {result.FileName}");
                    await DisplayAlert("Audio Selected", $"Selected: {result.FileName}", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error selecting audio: {ex.Message}");
                await DisplayAlert("Error", "Failed to select audio file. Please try again.", "OK");
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

        private async void SelectAndActivateRecentTextModel()
        {
            try
            {
                // Get DataService from the DI container
                var serviceProvider = Application.Current.MainPage.Handler.MauiContext.Services;
                var dataService = serviceProvider.GetService<DataService>();

                // Check if auto-selection is enabled
                var settingsService = new SettingsService(dataService);
                var hardwareCapabilities = await settingsService.GetHardwareCapabilitiesAsync();

                if (!hardwareCapabilities.AutoSelectModel)
                {
                    Debug.WriteLine("Auto-model selection is disabled");
                    return;
                }

                // Use the SettingsService recommendation logic
                string recommendedModelName = settingsService.GetRecommendedModelBasedOnHardware();
                Debug.WriteLine($"Hardware-based recommendation: {recommendedModelName}");

                // Try to find and activate the recommended model
                var availableModels = _viewModel.AvailableModels;
                var recommendedModel = availableModels?.FirstOrDefault(m =>
                    string.Equals(m.Name, recommendedModelName, StringComparison.OrdinalIgnoreCase));

                if (recommendedModel != null)
                {
                    Debug.WriteLine($"Auto-selecting recommended model: {recommendedModel.Name}");

                    // Activate the recommended model using the command
                    if (_viewModel.ActivateModelCommand.CanExecute(recommendedModel))
                    {
                        _viewModel.ActivateModelCommand.Execute(recommendedModel);
                    }

                    // Update the selected model in SettingsService
                    await settingsService.SetActiveModelAsync(recommendedModel.Name);
                }
                else
                {
                    Debug.WriteLine($"Recommended model '{recommendedModelName}' not found in available models");

                    // Fallback to hardware compatibility check with existing logic
                    SelectModelWithOriginalLogic(hardwareCapabilities);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in auto-model selection: {ex.Message}");

                // Fallback to original method if there's an error
                SelectModelWithOriginalLogic();
            }
        }

        private void SelectModelWithOriginalLogic(SettingsService.HardwareCapabilities capabilities = null)
        {
            try
            {
                int gpuCapabilityIndex = Preferences.Get("ModelCompat_GpuCapabilityIndex", 0);
                string[] gpuLevels = {
                    "CPU Only",
                    "NVIDIA GTX 10xx (6GB+)",
                    "NVIDIA RTX 20xx/30xx (8GB+)",
                    "NVIDIA RTX 40xx (12GB+)",
                    "Apple M1/M2",
                    "AMD RDNA2 (8GB+)",
                    "A100/H100/FP8 (24GB+)",
                    "Other/Unknown"
                };
                string gpuLevel = gpuLevels[Math.Clamp(gpuCapabilityIndex, 0, gpuLevels.Length - 1)];
                string vramStr = Preferences.Get("ModelCompat_MaxVram", "4");
                int parsedVram;
                int maxVram = int.TryParse(vramStr, out parsedVram) ? parsedVram : 4;
                bool allowLargeModels = Preferences.Get("ModelCompat_AllowLargeModels", false);

                bool IsModelCompatible(NeuralNetworkModel model)
                {
                    var name = (model.HuggingFaceModelId ?? model.Name ?? "").ToLowerInvariant();
                    // CPU Only: block all models that mention GPU, CUDA, Llama, DeepSeek, etc.
                    if (gpuLevel == "CPU Only" && (name.Contains("cuda") || name.Contains("gpu") || name.Contains("llama") || name.Contains("deepseek")))
                        return false;
                    // Block large models if not allowed
                    if (!allowLargeModels && (name.Contains("70b") || name.Contains("65b") || name.Contains("33b") || name.Contains("large")))
                        return false;
                    // VRAM check
                    if (model.Description != null && model.Description.ToLower().Contains("requires"))
                    {
                        var desc = model.Description.ToLower();
                        if (desc.Contains("8gb") && maxVram < 8) return false;
                        if (desc.Contains("12gb") && maxVram < 12) return false;
                        if (desc.Contains("16gb") && maxVram < 16) return false;
                        if (desc.Contains("24gb") && maxVram < 24) return false;
                    }
                    return true;
                }

                // Find the most recent compatible text model
                var recentTextModel = _viewModel.AvailableModels
                    .Where(m => m.InputType == ModelInputType.Text && IsModelCompatible(m))
                    .OrderByDescending(m => m.LastUsed)
                    .LastOrDefault();

                // Fallback: if none found, pick the last compatible text model
                if (recentTextModel == null)
                {
                    recentTextModel = _viewModel.AvailableModels
                        .Where(m => m.InputType == ModelInputType.Text && IsModelCompatible(m))
                        .LastOrDefault();
                }

                // Activate if not already active
                if (recentTextModel != null && !_viewModel.ActiveModels.Contains(recentTextModel))
                {
                    if (_viewModel.ActivateModelCommand.CanExecute(recentTextModel))
                    {
                        _viewModel.ActivateModelCommand.Execute(recentTextModel);
                        Debug.WriteLine($"Auto-activated recent compatible text model: {recentTextModel.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error auto-activating recent compatible text model: {ex.Message}");
            }
        }
    }
}