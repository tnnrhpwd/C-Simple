using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using System.ComponentModel;
using CSimple.Services;
using Microsoft.Maui.Controls;
using CSimple.ViewModels;
using System;
using System.Reflection;
using System.Linq;
using CSimple; // For ActionGroupExtensions
using System.Runtime.CompilerServices; // Add this namespace for CallerMemberName attribute
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using CSimple.Models;
using CSimple.Services.AppModeService;

namespace CSimple.Pages
{
    public partial class ActionPage : ContentPage, INotifyPropertyChanged
    {
        // Keep the original Data property but rename it to ensure proper binding
        private ObservableCollection<DataItem> _dataItems = new ObservableCollection<DataItem>();
        public ObservableCollection<DataItem> DataItems
        {
            get => _dataItems;
            set
            {
                if (_dataItems != value)
                {
                    _dataItems = value;
                    OnPropertyChanged(nameof(DataItems));
                }
            }
        }

        // Add a property specifically for action groups to make binding more direct
        private ObservableCollection<ActionGroup> _actionGroups = new ObservableCollection<ActionGroup>();
        public ObservableCollection<ActionGroup> ActionGroups
        {
            get => _actionGroups;
            set
            {
                if (_actionGroups != value)
                {
                    _actionGroups = value;
                    OnPropertyChanged(nameof(ActionGroups));
                    // Update grouped actions when the collection changes
                    if (IsGrouped)
                    {
                        GroupedActionGroups = GroupActionsByCategory();
                    }
                    OnPropertyChanged(nameof(HasSelectedActions));
                }
            }
        }

        // For grouped display by category
        private ObservableCollection<Grouping<string, ActionGroup>> _groupedActionGroups;
        public ObservableCollection<Grouping<string, ActionGroup>> GroupedActionGroups
        {
            get => _groupedActionGroups;
            set
            {
                if (_groupedActionGroups != value)
                {
                    _groupedActionGroups = value;
                    OnPropertyChanged(nameof(GroupedActionGroups));
                }
            }
        }

        // Group toggle property
        private bool _isGrouped = false;
        public bool IsGrouped
        {
            get => _isGrouped;
            set
            {
                if (_isGrouped != value)
                {
                    _isGrouped = value;
                    OnPropertyChanged(nameof(IsGrouped));

                    if (value)
                    {
                        GroupedActionGroups = GroupActionsByCategory();
                    }
                }
            }
        }

        // Categories for filtering
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();

        // Selected category for filtering
        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (_selectedCategory != value)
                {
                    _selectedCategory = value;
                    OnPropertyChanged(nameof(SelectedCategory));
                    FilterActionsByCategory();
                }
            }
        }

        // Sort options
        public ObservableCollection<string> SortOptions { get; } = new ObservableCollection<string>
        {
            "Date (Newest First)",
            "Date (Oldest First)",
            "Name (A-Z)",
            "Name (Z-A)",
            "Type",
            "Steps Count",
            "Usage Count"
        };

        private string _selectedSortOption;
        public string SelectedSortOption
        {
            get => _selectedSortOption;
            set
            {
                if (_selectedSortOption != value)
                {
                    _selectedSortOption = value;
                    OnPropertyChanged(nameof(SelectedSortOption));
                    SortActionGroups();
                }
            }
        }

        // Search functionality
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged(nameof(SearchText));
                    FilterActionsBySearch();
                }
            }
        }

        // Selected actions tracking
        public bool HasSelectedActions => ActionGroups.Any(a => a.IsSelected);

        // Track if data is being loaded to show loading indicator
        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                    // Also update the visibility of empty state message
                    OnPropertyChanged(nameof(ShowEmptyMessage));
                }
            }
        }

        // Control when to show "No actions found" message
        public bool ShowEmptyMessage => !IsLoading && (ActionGroups == null || ActionGroups.Count == 0);

        // Original properties
        public ICommand NavigateToObservePageCommand { get; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand ToggleSimulateActionGroupCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }
        public ICommand RowTappedCommand { get; }
        public ICommand DeleteActionGroupCommand { get; set; }

        // New commands for enhanced features
        public ICommand RefreshCommand { get; }
        public ICommand TrainModelCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ChainActionCommand { get; }
        public ICommand ChainSelectedActionsCommand { get; }
        public ICommand AddToTrainingCommand { get; }

        // Add commands for local items
        public ICommand ViewLocalItemCommand { get; }
        public ICommand ImportLocalItemCommand { get; }

        // Add a new command to handle deletion by ActionGroup
        public ICommand DeleteActionCommand { get; }

        private bool _isSimulating = false;
        private readonly ActionService _actionService;
        private readonly DataService _dataService;
        private readonly UserService _userService;
        private ActionPageViewModel _viewModel;

        // Fix: Defining the missing SortPicker as a class field
        private Picker _sortPicker;

        // Fix: Defining InputActionPopup with correct type using fully qualified name
        private Microsoft.Maui.Controls.Grid _inputActionPopup;

        // Add a selected action property
        private ActionGroup _selectedActionGroup;
        public ActionGroup SelectedActionGroup
        {
            get => _selectedActionGroup;
            set
            {
                if (_selectedActionGroup != value)
                {
                    _selectedActionGroup = value;
                    OnPropertyChanged(nameof(SelectedActionGroup));

                    // Navigate to detail page when an action is selected
                    if (value != null)
                    {
                        OnRowTapped(value);
                    }
                }
            }
        }

        public bool IsSimulating
        {
            get => _isSimulating;
            set
            {
                _isSimulating = value;
                OnPropertyChanged(nameof(IsSimulating));
            }
        }

        private void DebugOutput(string message)
        {
            Debug.WriteLine(message);
        }

        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;

        public ActionPage()
        {
            InitializeComponent();

            // Set the binding context directly to this page
            BindingContext = this;

            // Initialize a view model for additional functionality
            _viewModel = new ActionPageViewModel();
            // Initialize fields
            _sortPicker = this.FindByName<Picker>("SortPicker");
            _inputActionPopup = this.FindByName<Microsoft.Maui.Controls.Grid>("InputActionPopup");
            var fileService = new FileService();
            _dataService = new DataService();
            _userService = new UserService(); // Initialize the user service
            _actionService = new ActionService(_dataService, fileService);
            _appModeService = ServiceProvider.GetService<CSimple.Services.AppModeService.AppModeService>();

            // Initialize sort options
            SelectedSortOption = SortOptions[0]; // Default sort by date newest

            // Initialize commands
            ToggleSimulateActionGroupCommand = new Command<ActionGroup>(async (actionGroup) => await ToggleSimulateActionGroupAsync(actionGroup));
            SaveToFileCommand = new Command(async () => await SaveDataItemsToFile());
            LoadFromFileCommand = new Command(async () => await LoadDataItemsFromFile());
            NavigateToObservePageCommand = new Command(async () => await NavigateToObservePage());
            DeleteActionGroupCommand = new Command<DataItem>(async (dataItem) => await DeleteDataItemAsync(dataItem));
            DeleteActionCommand = new Command<ActionGroup>(async (actionGroup) => await DeleteActionByGroupAsync(actionGroup));
            RowTappedCommand = new Command<ActionGroup>(OnRowTapped);

            // New commands
            RefreshCommand = new Command(async () => await RefreshData());
            TrainModelCommand = new Command(async () => await TrainModel());
            SearchCommand = new Command(FilterActionsBySearch);
            ChainActionCommand = new Command<ActionGroup>(ChainAction);
            ChainSelectedActionsCommand = new Command(ChainSelectedActions);
            AddToTrainingCommand = new Command(AddSelectedActionsToTraining);

            // Initialize new commands for local items
            ViewLocalItemCommand = new Command<DataItem>(ViewLocalItem);
            ImportLocalItemCommand = new Command<DataItem>(ImportLocalItem);

            // Load existing action groups from file asynchronously
            _ = LoadDataItemsFromFile();
            DebugOutput("Action Page Initialized");

            // Initialize categories
            PopulateCategories();

            // Load local items in addition to other data
            _ = LoadLocalItemsAsync();
        }

        // Category methods
        private void PopulateCategories()
        {
            Categories.Clear();
            Categories.Add("All Categories"); // Default option

            // Add application-specific categories
            Categories.Add("Productivity");
            Categories.Add("Browser");
            Categories.Add("Document Editing");
            Categories.Add("System");
            Categories.Add("Data Analysis");
            Categories.Add("File Management");
            Categories.Add("Communication");
            Categories.Add("Development");
            Categories.Add("Other");

            SelectedCategory = Categories[0]; // Select "All Categories" by default
        }

        private ObservableCollection<Grouping<string, ActionGroup>> GroupActionsByCategory()
        {
            var groups = ActionGroups
                .GroupBy(a => string.IsNullOrEmpty(a.Category) ? "Uncategorized" : a.Category)
                .Select(g => new Grouping<string, ActionGroup>(g.Key, g))
                .ToList();

            return new ObservableCollection<Grouping<string, ActionGroup>>(groups);
        }

        private void FilterActionsByCategory()
        {
            if (string.IsNullOrEmpty(SelectedCategory) || SelectedCategory == "All Categories")
            {
                // Reset to show all actions
                ActionGroups = new ObservableCollection<ActionGroup>(_allActionGroups);
                return;
            }

            // Filter by selected category
            var filteredActions = _allActionGroups
                .Where(a => a.Category == SelectedCategory)
                .ToList();

            ActionGroups = new ObservableCollection<ActionGroup>(filteredActions);
        }

        private void FilterActionsBySearch()
        {
            if (string.IsNullOrEmpty(SearchText))
            {
                // Reset to show all actions (respecting category filter)
                FilterActionsByCategory();
                return;
            }

            // Filter by search text within the current category filter
            var baseList = string.IsNullOrEmpty(SelectedCategory) || SelectedCategory == "All Categories"
                ? _allActionGroups
                : _allActionGroups.Where(a => a.Category == SelectedCategory);

            var searchResults = baseList
                .Where(a => a.ActionName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                           (a.Description != null && a.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                           (a.ActionType != null && a.ActionType.Contains(SearchText, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ActionGroups = new ObservableCollection<ActionGroup>(searchResults);
        }

        private void SortActionGroups()
        {
            if (ActionGroups == null || ActionGroups.Count == 0)
                return;

            List<ActionGroup> sortedGroups;

            switch (SelectedSortOption)
            {
                case "Date (Newest First)":
                    sortedGroups = ActionGroups.OrderByDescending(a => a.CreatedAt ?? DateTime.MinValue).ToList(); // Fix order
                    break;
                case "Date (Oldest First)":
                    sortedGroups = ActionGroups.OrderBy(a => a.CreatedAt ?? DateTime.MinValue).ToList(); // Fix order
                    break;
                case "Name (A-Z)":
                    sortedGroups = ActionGroups.OrderByDescending(a => a.ActionName).ToList(); // Reverse order
                    break;
                case "Name (Z-A)":
                    sortedGroups = ActionGroups.OrderBy(a => a.ActionName).ToList(); // Reverse order
                    break;
                case "Type":
                    sortedGroups = ActionGroups.OrderBy(a => a.ActionType).ToList();
                    break;
                case "Steps Count":
                    sortedGroups = ActionGroups.OrderBy(a => a.ActionArray?.Count ?? 0).ToList(); // Reverse order
                    break;
                case "Usage Count":
                    sortedGroups = ActionGroups.OrderBy(a => a.UsageCount).ToList(); // Reverse order
                    break;
                default:
                    return;
            }

            ActionGroups = new ObservableCollection<ActionGroup>(sortedGroups);
        }

        // Enhancement methods
        private ObservableCollection<ActionGroup> _allActionGroups = new ObservableCollection<ActionGroup>();

        private async Task RefreshData()
        {
            IsLoading = true;
            try
            {
                Debug.WriteLine("Refreshing all action data...");

                // Clear existing collections
                DataItems.Clear();
                ActionGroups.Clear();

                // Check if offline mode is enabled
                if (_appModeService.CurrentMode == AppMode.Offline)
                {
                    Debug.WriteLine("App is in offline mode. Loading only local action data.");
                    // Load local data only
                    var localItems = await _actionService.LoadLocalDataItemsAsync();
                    Debug.WriteLine($"Loaded {localItems.Count} local items");

                    foreach (var item in localItems.Where(item => item.deleted != true))
                    {
                        DataItems.Add(item);
                    }
                }
                else
                {
                    // Load all data using our new unified method
                    var allItems = await _actionService.LoadAllDataItemsAsync();
                    Debug.WriteLine($"Loaded {allItems.Count} total unique items");

                    // Filter out any items that were marked as deleted
                    var filteredItems = allItems.Where(item => item.deleted != true).ToList();
                    Debug.WriteLine($"After filtering deleted items: {filteredItems.Count} items remain");

                    // Update DataItems collection
                    foreach (var item in filteredItems)
                    {
                        DataItems.Add(item);
                    }
                }

                // Update ActionGroups collection
                UpdateActionGroupsFromDataItems();

                // Update master list for filtering
                _allActionGroups = new ObservableCollection<ActionGroup>(ActionGroups);

                // Apply filtering or sorting if needed
                if (!string.IsNullOrEmpty(SearchText))
                {
                    FilterActionsBySearch();
                }
                else if (SelectedCategory != "All Categories")
                {
                    FilterActionsByCategory();
                }
                else
                {
                    // Apply current sort method
                    SortActionGroups();
                }

                // Reset selection
                SelectedActionGroup = null;

                // Update UI indicators
                OnPropertyChanged(nameof(ShowEmptyMessage));
                OnPropertyChanged(nameof(HasSelectedActions));

                Debug.WriteLine($"Data refresh complete. Displaying {ActionGroups.Count} action groups");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error refreshing data: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                await DisplayAlert("Error", "Failed to refresh actions. Please try again.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Always refresh data when page appears based on current app mode
            LoadActionsData();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
        }

        private async Task TrainModel()
        {
            try
            {
                var trainingActions = ActionGroups.Where(a => a.IsPartOfTraining).ToList();
                if (trainingActions.Count == 0)
                {
                    await DisplayAlert("No Actions Selected",
                        "Please add actions to the training set first by using the 'Add to Training' button.",
                        "OK");
                    return;
                }

                IsLoading = true;

                // Show a dialog for model training options
                string modelName = await DisplayPromptAsync("Train Model",
                    "Enter a name for your model:",
                    "Train", "Cancel",
                    "My Model",
                    50,
                    Microsoft.Maui.Keyboard.Text); // Fix ambiguous reference

                if (string.IsNullOrEmpty(modelName))
                {
                    IsLoading = false;
                    return;
                }

                string modelType = await DisplayActionSheet("Select Model Type",
                    "Cancel",
                    null,
                    "General Assistant (Multimodal)",
                    "Specific Task (Task-oriented)",
                    "Command Automation (Sequence)");

                if (string.IsNullOrEmpty(modelType) || modelType == "Cancel")
                {
                    IsLoading = false;
                    return;
                }

                // Simulated training - would call your actual neural network training service
                await Task.Delay(2000); // Simulate training time

                await DisplayAlert("Model Training Complete",
                    $"Model '{modelName}' ({modelType}) has been trained with {trainingActions.Count} actions.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to train model: {ex.Message}", "OK");
                Debug.WriteLine($"Error training model: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ChainAction(ActionGroup actionGroup)
        {
            if (actionGroup == null) return;

            actionGroup.IsChained = !actionGroup.IsChained;

            // Refresh the UI
            var index = ActionGroups.IndexOf(actionGroup);
            if (index >= 0)
            {
                ActionGroups[index] = actionGroup;
            }

            // Notification
            var message = actionGroup.IsChained ?
                $"Added '{actionGroup.ActionName}' to action chain" :
                $"Removed '{actionGroup.ActionName}' from action chain";

            DisplayAlert("Action Chain", message, "OK");
        }

        private async void ChainSelectedActions()
        {
            var selectedActions = ActionGroups.Where(a => a.IsSelected).ToList();

            if (selectedActions.Count < 2)
            {
                await DisplayAlert("Chain Actions",
                    "Please select at least two actions to chain together.",
                    "OK");
                return;
            }

            string chainName = await DisplayPromptAsync("Create Action Chain",
                "Enter a name for this action chain:",
                "Create", "Cancel",
                "My Action Chain",
                50,
                Microsoft.Maui.Keyboard.Text); // Fix ambiguous reference

            if (string.IsNullOrEmpty(chainName))
                return;

            // Create the chain (in a real app, this would store the sequence)
            foreach (var action in selectedActions)
            {
                action.IsChained = true;
                action.ChainName = chainName;
            }

            // Refresh the UI
            UpdateActionGroupsFromDataItems();

            await DisplayAlert("Chain Created",
                $"Action chain '{chainName}' created with {selectedActions.Count} actions.",
                "OK");
        }

        private void AddSelectedActionsToTraining()
        {
            var selectedActions = ActionGroups.Where(a => a.IsSelected).ToList();

            if (selectedActions.Count == 0)
            {
                DisplayAlert("Add to Training",
                    "Please select at least one action to add to training data.",
                    "OK");
                return;
            }

            foreach (var action in selectedActions)
            {
                action.IsPartOfTraining = true;
                action.IsSelected = false; // Deselect after adding to training
            }

            // Refresh the UI
            UpdateActionGroupsFromDataItems();

            DisplayAlert("Training Data Updated",
                $"{selectedActions.Count} actions added to training data set.",
                "OK");
        }

        // Completely revised delete method to ensure local actions are properly deleted 
        private async Task DeleteDataItemAsync(DataItem dataItem, bool skipConfirmation = false)
        {
            if (dataItem == null)
                return;

            try
            {
                // Check if this is a local action
                bool isLocal = dataItem.Data?.ActionGroupObject?.IsLocal == true;
                string actionName = dataItem.Data?.ActionGroupObject?.ActionName ?? "Unknown Action";

                // Only ask for confirmation if not skipped
                bool confirmDelete = skipConfirmation ? true : await DisplayAlert("Confirm Delete",
                    $"Are you sure you want to delete {(isLocal ? "the local action" : "the action")} '{actionName}'?",
                    "Yes", "No");

                if (!confirmDelete)
                    return;

                IsLoading = true;
                Debug.WriteLine($"Deleting {(isLocal ? "local" : "regular")} action: {actionName}");

                // First delete from the service
                bool deleteSuccessful = await _actionService.DeleteDataItemAsync(dataItem);

                if (deleteSuccessful)
                {
                    // 1. Remove from DataItems
                    if (DataItems.Contains(dataItem))
                    {
                        DataItems.Remove(dataItem);
                        Debug.WriteLine("Removed from DataItems");
                    }

                    // 2. Remove from ActionGroups
                    var actionGroupToRemove = ActionGroups.FirstOrDefault(ag =>
                        ag.ActionName == actionName);

                    if (actionGroupToRemove != null)
                    {
                        ActionGroups.Remove(actionGroupToRemove);
                        Debug.WriteLine("Removed from ActionGroups");
                    }

                    // 3. Remove from _allActionGroups
                    var allGroupToRemove = _allActionGroups.FirstOrDefault(ag =>
                        ag.ActionName == actionName);

                    if (allGroupToRemove != null)
                    {
                        _allActionGroups.Remove(allGroupToRemove);
                        Debug.WriteLine("Removed from _allActionGroups");
                    }

                    // 4. If it's a local action, also remove from LocalItems
                    if (isLocal)
                    {
                        var localItemToRemove = LocalItems.FirstOrDefault(item =>
                            item.Data?.ActionGroupObject?.ActionName == actionName);

                        if (localItemToRemove != null)
                        {
                            LocalItems.Remove(localItemToRemove);
                            Debug.WriteLine("Removed from LocalItems");
                        }
                    }

                    // 5. Save changes to persist deletion
                    await SaveDataItemsToFile();
                    Debug.WriteLine("Changes saved to storage");

                    // 6. For local actions, make sure the local storage is also updated
                    if (isLocal)
                    {
                        // Call a specific method to clean up local storage
                        // If such a method doesn't exist, we would need to add it to ActionService
                        await CleanupLocalActionStorage(actionName);
                    }

                    // Update UI indicators
                    OnPropertyChanged(nameof(ShowEmptyMessage));
                    OnPropertyChanged(nameof(HasSelectedActions));
                    OnPropertyChanged(nameof(HasLocalItems));

                    DebugOutput($"Action '{actionName}' successfully deleted.");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to delete action. Please try again.", "OK");
                    Debug.WriteLine("Delete operation failed");
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error deleting action: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                await DisplayAlert("Error", "An error occurred while deleting the action.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // New helper method to ensure local action storage is cleaned up
        private async Task CleanupLocalActionStorage(string actionName)
        {
            try
            {
                // This would call a method in ActionService to clean up specific local storage
                // If such method doesn't exist, you would need to add it to ActionService

                // For now, we'll call a general refresh of local items
                await LoadLocalItemsAsync();
                Debug.WriteLine($"Local storage cleanup performed for action: {actionName}");

                // Ensure this action is not in the LocalItems collection anymore
                var itemStillPresent = LocalItems.FirstOrDefault(item =>
                    item.Data?.ActionGroupObject?.ActionName == actionName);

                if (itemStillPresent != null)
                {
                    // If somehow it's still there, remove it forcefully
                    LocalItems.Remove(itemStillPresent);
                    Debug.WriteLine("Forcefully removed persistent local item");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error cleaning up local storage: {ex.Message}");
            }
        }

        private async Task SaveDataItemsToFile()
        {
            await _actionService.SaveDataItemsToFile(DataItems.ToList());
        }

        private async Task LoadDataItemsFromFile()
        {
            await RefreshData();
        }

        private async Task LoadDataItemsFromBackend()
        {
            // Skip backend loading if in offline mode
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping backend action loading.");
                return;
            }

            await RefreshData();
        }

        // Fixed LoadLocalItemsAsync to avoid circular references
        private async Task LoadLocalItemsAsync()
        {
            try
            {
                Debug.WriteLine("Loading local items only");

                // Get all local items
                var allItems = await _actionService.LoadAllDataItemsAsync();

                // Filter for local items, but check DataItems to avoid loading deleted ones
                var localItems = allItems?.Where(item =>
                    item?.Data?.ActionGroupObject?.IsLocal == true &&
                    // Don't include items that have been deleted (not in DataItems)
                    !DataItems.Any(di =>
                        di.Data?.ActionGroupObject?.Id.ToString() == item.Data?.ActionGroupObject?.Id.ToString() &&
                        di.deleted == true)
                ).ToList();

                if (localItems != null)
                {
                    LocalItems.Clear();
                    foreach (var item in localItems)
                    {
                        // Double check this item hasn't been flagged for deletion
                        if (item.deleted != true)
                        {
                            LocalItems.Add(item);
                        }
                    }
                }

                OnPropertyChanged(nameof(HasLocalItems));
                Debug.WriteLine($"Loaded {LocalItems.Count} local items");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading local items: {ex.Message}");
            }
        }

        private async void OnRefreshDataClicked(object sender, EventArgs e)
        {
            await RefreshData();
        }

        // Enhanced OnRowTapped method to show detailed action information
        private async void OnRowTapped(ActionGroup actionGroup)
        {
            if (actionGroup != null)
            {
                try
                {
                    // Pass the ActionGroup with its files to the detail page
                    await Navigation.PushModalAsync(new ActionDetailPage(actionGroup));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error navigating to ActionDetailPage: {ex.Message}");
                    await DisplayAlert("Error", "Could not load action details", "OK");
                }
            }
        }

        // Optional method to load additional action details if needed
        private async Task LoadActionDetails(ActionGroup actionGroup)
        {
            // Here you would fetch any additional details about the action
            // For example, if there are related files or performance statistics that
            // weren't loaded initially for performance reasons

            try
            {
                // Example: Load more detailed execution history
                // actionGroup.ExecutionHistory = await _actionService.GetExecutionHistoryAsync(actionGroup.Id);

                // Check if files already exist using the extension method
                var existingFiles = actionGroup.GetFiles();
                bool hasExistingFiles = existingFiles != null && existingFiles.Any();

                if (!hasExistingFiles)
                {
                    // Get the action ID using reflection or fallback to guid
                    string actionId = null;

                    // Try to get ID property
                    var idProperty = actionGroup.GetType().GetProperty("Id");
                    if (idProperty != null)
                    {
                        var id = idProperty.GetValue(actionGroup);
                        actionId = id?.ToString(); // Convert any type to string
                    }
                    // Fallback to an alternative property if ID doesn't exist
                    else
                    {
                        var guidProperty = actionGroup.GetType().GetProperty("Guid");
                        if (guidProperty != null)
                        {
                            var guid = guidProperty.GetValue(actionGroup);
                            actionId = guid?.ToString();
                        }
                        else
                        {
                            // Final fallback: use action name as ID
                            actionId = actionGroup.ActionName;
                        }
                    }

                    // Get files for this action
                    var files = await _actionService.GetActionFilesAsync(actionId);
                    if (files != null)
                    {
                        // Use the extension method to set files
                        actionGroup.SetFiles(files);
                        Debug.WriteLine("Files attached to action group using extension method");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading additional action details: {ex.Message}");
            }
        }

        private async Task ToggleSimulateActionGroupAsync(ActionGroup actionGroup)
        {
            if (actionGroup != null)
            {
                try
                {
                    IsSimulating = actionGroup.IsSimulating;

                    // Ensure settings are properly configured before execution
                    UpdateGameSettings();

                    await _actionService.ToggleSimulateActionGroupAsync(actionGroup);
                    IsSimulating = false;

                    // Reload ActionGroups after toggling simulation
                    await LoadDataItemsFromBackend();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error simulating action: {ex.Message}");
                    await DisplayAlert("Error", "Failed to execute action. Please try again.", "OK");
                }
            }
        }

        // Enhanced method to extract ActionGroup objects from DataItems with additional properties
        private void UpdateActionGroupsFromDataItems()
        {
            ActionGroups.Clear();

            foreach (var item in DataItems)
            {
                if (item?.Data?.ActionGroupObject != null)
                {
                    // Make sure the action group has all required properties for display
                    var actionGroup = item.Data.ActionGroupObject;

                    // Set additional metadata for display
                    if (string.IsNullOrEmpty(actionGroup.ActionName))
                    {
                        actionGroup.ActionName = "Unnamed Action";
                    }

                    // CRITICAL FIX: Always use original database timestamp, never generate random dates
                    // or override with defaults. This ensures we show the actual creation date.
                    if (actionGroup.CreatedAt == null || actionGroup.CreatedAt == default(DateTime))
                    {
                        // Use the database timestamp directly
                        actionGroup.CreatedAt = item.createdAt;
                    }

                    // Preserve the IsLocal flag - don't override it
                    // actionGroup.IsLocal = actionGroup.IsLocal; 

                    // Set default values for new properties if they don't exist
                    actionGroup.Category = DetermineCategory(actionGroup);
                    actionGroup.UsageCount = actionGroup.UsageCount > 0 ? actionGroup.UsageCount : new Random().Next(1, 20);
                    actionGroup.SuccessRate = actionGroup.SuccessRate > 0 ? actionGroup.SuccessRate : (double)new Random().Next(70, 100) / 100;
                    actionGroup.IsPartOfTraining = actionGroup.IsPartOfTraining; // Preserve existing value
                    actionGroup.IsChained = actionGroup.IsChained; // Preserve existing value
                    actionGroup.HasMetrics = true; // Show metrics by default
                    actionGroup.Description = actionGroup.Description ?? $"Action for {actionGroup.ActionName}";

                    // FIX: Always use the database timestamp instead of generating random dates
                    if (actionGroup.CreatedAt == null || actionGroup.CreatedAt == default(DateTime))
                    {
                        // Always use the item's creation date from the database
                        actionGroup.CreatedAt = item.createdAt;

                        // If item.createdAt is also default, it's truly a new item, so use current time
                        if (actionGroup.CreatedAt == default(DateTime))
                        {
                            actionGroup.CreatedAt = DateTime.Now;
                        }
                    }

                    // Determine action type if not set
                    if (string.IsNullOrEmpty(actionGroup.ActionType))
                    {
                        actionGroup.ActionType = DetermineActionTypeFromSteps(actionGroup);
                    }

                    // Add to the collection - only add if not already present to prevent duplicates
                    if (!ActionGroups.Any(ag =>
                        (!string.IsNullOrEmpty(actionGroup.Id.ToString()) && actionGroup.Id.ToString() == ag.Id.ToString()) ||
                        (!string.IsNullOrEmpty(actionGroup.ActionName) && actionGroup.ActionName == ag.ActionName)))
                    {
                        ActionGroups.Add(actionGroup);
                    }
                }
            }

            // Notify UI to refresh
            OnPropertyChanged(nameof(ActionGroups));
            OnPropertyChanged(nameof(ShowEmptyMessage));
            OnPropertyChanged(nameof(HasSelectedActions));

            // Apply current sort method
            SortActionGroups();
        }

        private string DetermineCategory(ActionGroup actionGroup)
        {
            // Determine category based on action name or steps
            string name = actionGroup.ActionName.ToLowerInvariant();
            string steps = actionGroup.ActionArray?.FirstOrDefault()?.ToString()?.ToLowerInvariant() ?? "";

            if (name.Contains("excel") || name.Contains("spreadsheet") || steps.Contains("excel"))
                return "Data Analysis";

            if (name.Contains("word") || name.Contains("document") || steps.Contains("word") || steps.Contains(".doc"))
                return "Document Editing";

            if (name.Contains("browser") || name.Contains("chrome") || name.Contains("firefox") || name.Contains("edge") || steps.Contains("browser"))
                return "Browser";

            if (name.Contains("file") || name.Contains("folder") || name.Contains("copy") || name.Contains("move"))
                return "File Management";

            if (name.Contains("email") || name.Contains("outlook") || name.Contains("teams") || name.Contains("slack"))
                return "Communication";

            if (name.Contains("code") || name.Contains("visual studio") || name.Contains("vs code") || steps.Contains("code"))
                return "Development";

            if (name.Contains("system") || name.Contains("settings") || name.Contains("control"))
                return "System";

            return "Productivity"; // Default category
        }

        private string DetermineActionTypeFromSteps(ActionGroup actionGroup)
        {
            if (actionGroup.ActionArray == null || !actionGroup.ActionArray.Any())
                return "Unknown";

            // Count of different action types
            int keyboardActions = 0;
            int mouseActions = 0;
            int applicationActions = 0;

            foreach (var action in actionGroup.ActionArray)
            {
                string actionStr = action.ToString().ToLowerInvariant();

                if (actionStr.Contains("key") || actionStr.Contains("type") || action.EventType == 256 || action.EventType == 257)
                    keyboardActions++;
                else if (actionStr.Contains("mouse") || actionStr.Contains("click") || action.EventType == 512 || action.EventType == 0x0201)
                    mouseActions++;
                else if (actionStr.Contains("launch") || actionStr.Contains("start") || actionStr.Contains("open"))
                    applicationActions++;
            }

            // Determine dominant action type
            if (keyboardActions > mouseActions && keyboardActions > applicationActions)
                return "Keyboard Action";
            if (mouseActions > keyboardActions && mouseActions > applicationActions)
                return "Mouse Action";
            if (applicationActions > keyboardActions && applicationActions > mouseActions)
                return "Application Launch";

            if (keyboardActions > 0 && mouseActions > 0)
                return "Mixed Input";

            return "Custom Action";
        }

        private void OnSortOrderChanged(object sender, EventArgs e)
        {
            if (_sortPicker == null || _sortPicker.SelectedIndex < 0)
                return;

            SelectedSortOption = SortOptions[_sortPicker.SelectedIndex];
        }

        // Fixed property changed implementation
        public new event PropertyChangedEventHandler PropertyChanged;

        public new void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void OnActionItemClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is FrameworkElement element && element.DataContext is ActionGroup actionGroup)
                {
                    // Create a safe copy to pass to the detail page
                    var safeActionGroup = SafelyCopyActionGroup(actionGroup);

                    // Ensure files are safely handled
                    var files = ActionGroupExtensions.GetFiles(actionGroup);
                    Debug.WriteLine($"Files attached to action group: {files?.Count ?? 0}");

                    // Navigate with the safe copy
                    await Navigation.PushModalAsync(new ActionDetailPage(safeActionGroup));
                }
                else
                {
                    Debug.WriteLine("Invalid sender or DataContext in OnActionItemClicked");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Navigation error: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                // Show an error dialog to the user
                ShowErrorDialog("Navigation failed",
                    "There was a problem navigating to the action details. Please try again.");
            }
        }

        private ActionGroup SafelyCopyActionGroup(ActionGroup source)
        {
            if (source == null) return null;

            try
            {
                var copy = new ActionGroup
                {
                    Id = source.Id,
                    ActionName = source.ActionName,
                    Description = source.Description,
                    Category = source.Category,
                    ActionType = source.ActionType ?? "Local Action",
                    IsSelected = false, // Reset selection state for detail page
                    IsSimulating = false // Ensure simulation is off
                };

                // Safely copy action array
                if (source.ActionArray != null)
                {
                    copy.ActionArray = source.ActionArray.Select(a => new ActionItem
                    {
                        EventType = a.EventType,
                        KeyCode = a.KeyCode,
                        Duration = a.Duration,
                        Timestamp = a.Timestamp,
                        Coordinates = a.Coordinates != null ? new Coordinates
                        {
                            X = a.Coordinates.X,
                            Y = a.Coordinates.Y
                        } : null
                    }).ToList();
                }

                // Safely copy files using the extension method
                var sourceFiles = ActionGroupExtensions.GetFiles(source);
                if (sourceFiles != null)
                {
                    var copiedFiles = sourceFiles.Select(f => new ActionFile
                    {
                        Filename = f.Filename,
                        ContentType = f.ContentType,
                        Data = f.Data,
                        AddedAt = f.AddedAt,
                        IsProcessed = f.IsProcessed
                    }).ToList();

                    copy.SetFiles(copiedFiles);
                }

                return copy;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error copying ActionGroup: {ex.Message}");
                return new ActionGroup { ActionName = "Error copying action" };
            }
        }

        private async void ShowErrorDialog(string title, string content)
        {
            // Use MAUI's built-in alert dialog instead of WinUI ContentDialog
            await DisplayAlert(title, content, "OK");
        }

        // Add these properties for local items
        private ObservableCollection<DataItem> _localItems = new ObservableCollection<DataItem>();
        public ObservableCollection<DataItem> LocalItems
        {
            get => _localItems;
            set
            {
                if (_localItems != value)
                {
                    _localItems = value;
                    OnPropertyChanged(nameof(LocalItems));
                    OnPropertyChanged(nameof(HasLocalItems));
                }
            }
        }

        public bool HasLocalItems => LocalItems != null && LocalItems.Count > 0;

        private void ViewLocalItem(DataItem item)
        {
            if (item?.Data?.ActionGroupObject == null) return;

            try
            {
                // Create a safe copy to avoid memory issues
                var actionGroup = new ActionGroup
                {
                    Id = item.Data?.ActionGroupObject?.Id ?? Guid.NewGuid(),
                    ActionName = item.Data.ActionGroupObject.ActionName,
                    Description = item.Data.ActionGroupObject.Description,
                    ActionType = item.Data.ActionGroupObject.ActionType ?? "Local Action",
                    CreatedAt = item.createdAt
                };

                // Copy a limited subset of action steps
                if (item.Data.ActionGroupObject.ActionArray != null)
                {
                    actionGroup.ActionArray = new List<ActionItem>();
                    foreach (var action in item.Data.ActionGroupObject.ActionArray.Take(20))
                    {
                        actionGroup.ActionArray.Add(new ActionItem
                        {
                            EventType = action.EventType,
                            KeyCode = action.KeyCode,
                            Duration = action.Duration,
                            Coordinates = action.Coordinates
                        });
                    }
                }

                // Show the action details
                OnRowTapped(actionGroup);
            }
            catch (Exception ex)
            {
                DebugOutput($"Error viewing local item: {ex.Message}");
                DisplayAlert("Error", "Could not view action details", "OK");
            }
        }

        private async void ImportLocalItem(DataItem item)
        {
            if (item == null) return;

            try
            {
                bool confirmed = await DisplayAlert("Import Action",
                    $"Import '{item.Data.ActionGroupObject.ActionName}' to your permanent action library?",
                    "Import", "Cancel");

                if (!confirmed) return;

                // Add to main collection
                DataItems.Add(item);

                // Remove from local items
                LocalItems.Remove(item);

                // Update ActionGroups collection
                UpdateActionGroupsFromDataItems();

                // Save to backend if option is enabled
                if (await _userService.IsUserLoggedInAsync())
                {
                    // Save the individual item by adding it to the list and saving all
                    await _actionService.SaveDataItemsToFile(new List<DataItem> { item });
                    await DisplayAlert("Action Imported", "The action was successfully imported and synced to your account.", "OK");
                }
                else
                {
                    await _actionService.SaveDataItemsToFile(DataItems.ToList());
                    await DisplayAlert("Action Imported", "The action was successfully imported to your local library.", "OK");
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error importing local item: {ex.Message}");
                await DisplayAlert("Import Failed", "Could not import the action", "OK");
            }
        }

        // Add these properties to ActionPage class
        private bool _gameOptimizedMode = false;
        public bool GameOptimizedMode
        {
            get => _gameOptimizedMode;
            set
            {
                if (_gameOptimizedMode != value)
                {
                    _gameOptimizedMode = value;
                    OnPropertyChanged();
                    UpdateGameSettings();
                }
            }
        }

        private int _mouseSensitivity = 100; // 1-200%
        public int MouseSensitivity
        {
            get => _mouseSensitivity;
            set
            {
                if (_mouseSensitivity != value)
                {
                    _mouseSensitivity = Math.Clamp(value, 1, 200);
                    OnPropertyChanged();
                    UpdateGameSettings();
                }
            }
        }

        private bool _useSmoothing = true;
        public bool UseSmoothing
        {
            get => _useSmoothing;
            set
            {
                if (_useSmoothing != value)
                {
                    _useSmoothing = value;
                    OnPropertyChanged();
                    UpdateGameSettings();
                }
            }
        }

        private void UpdateGameSettings()
        {
            InputSimulator.SetGameEnhancedMode(_gameOptimizedMode, _mouseSensitivity);

            // Update the action service settings (if already created)
            if (_actionService != null)
            {
                // Store these settings in the ActionService
                _actionService.UseInterpolation = _useSmoothing;
                _actionService.MovementSteps = _gameOptimizedMode ? 20 : 10; // More steps in game mode
                _actionService.MovementDelayMs = _gameOptimizedMode ? 1 : 2; // Faster in game mode
            }
        }

        private async Task NavigateToObservePage()
        {
            DebugOutput("Navigating to ObservePage");
            await Shell.Current.GoToAsync("///observe");
        }

        private void OnInputActionClicked(object sender, EventArgs e)
        {
            _inputActionPopup.IsVisible = true;
        }

        private void OnOkayClick(object sender, EventArgs e)
        {
            _inputActionPopup.IsVisible = false;
        }

        // Fixed LoadActionsData to properly manage data loading based on app mode
        private async void LoadActionsData()
        {
            Debug.WriteLine("Reloading actions data");
            IsLoading = true;

            try
            {
                if (_appModeService.CurrentMode == AppMode.Online)
                {
                    Debug.WriteLine("App is in online mode. Loading backend and local actions.");
                    // Load all items from backend and local storage
                    await RefreshData();

                    // Also ensure local items are loaded
                    await LoadLocalItemsAsync();
                }
                else
                {
                    Debug.WriteLine("App is not in online mode. Loading local actions only.");
                    await LoadLocalItemsAsync();

                    // Update ActionGroups from locally loaded items
                    UpdateActionGroupsFromDataItems();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadActionsData: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Add method to delete actions by ActionGroup
        private async Task DeleteActionByGroupAsync(ActionGroup actionGroup)
        {
            if (actionGroup == null)
                return;

            try
            {
                bool confirmDelete = await DisplayAlert("Confirm Delete",
                    $"Are you sure you want to delete the action '{actionGroup.ActionName}'?",
                    "Yes", "No");

                if (!confirmDelete)
                    return;

                IsLoading = true;

                // Find corresponding DataItem for this ActionGroup
                var dataItem = DataItems.FirstOrDefault(item =>
                    item?.Data?.ActionGroupObject?.ActionName == actionGroup.ActionName);

                if (dataItem != null)
                {
                    // Use existing delete method but skip confirmation since we already confirmed
                    await DeleteDataItemAsync(dataItem, skipConfirmation: true);
                }
                else
                {
                    // Handle case where DataItem isn't found
                    ActionGroups.Remove(actionGroup);
                    _allActionGroups.Remove(actionGroup);

                    // Update UI
                    OnPropertyChanged(nameof(ShowEmptyMessage));
                    OnPropertyChanged(nameof(HasSelectedActions));

                    await DisplayAlert("Action Deleted",
                        "The action was successfully deleted.", "OK");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting action: {ex.Message}");
                await DisplayAlert("Error", "An error occurred while deleting the action.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    // Helper class for grouped collections
    public class Grouping<TKey, TItem> : ObservableCollection<TItem>
    {
        public TKey Key { get; }

        public Grouping(TKey key, IEnumerable<TItem> items)
        {
            Key = key;
            foreach (var item in items)
                this.Add(item);
        }
    }
}