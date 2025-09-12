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
                    // Unsubscribe from old collection changes
                    if (_actionGroups != null)
                    {
                        _actionGroups.CollectionChanged -= ActionGroups_CollectionChanged;
                        UnsubscribeFromSelectionChanges(_actionGroups);
                    }

                    _actionGroups = value;

                    // Subscribe to new collection changes
                    if (_actionGroups != null)
                    {
                        _actionGroups.CollectionChanged += ActionGroups_CollectionChanged;
                        SubscribeToSelectionChanges(_actionGroups);
                    }

                    OnPropertyChanged(nameof(ActionGroups));
                    // Update grouped actions when the collection changes
                    if (IsGrouped)
                    {
                        GroupedActionGroups = GroupActionsByCategory();
                    }
                    UpdateSelectionProperties();
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
            "Usage Count",
            "Size (Largest First)",
            "Size (Smallest First)"
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

        // Selected actions tracking - Fix to ensure this property updates properly
        public bool HasSelectedActions
        {
            get
            {
                bool hasSelected = ActionGroups?.Any(a => a.IsSelected) == true;
                // Debug.WriteLine($"HasSelectedActions called: {hasSelected} (ActionGroups count: {ActionGroups?.Count ?? 0})");
                return hasSelected;
            }
        }

        // Properties for conditional button visibility
        public bool HasMultipleSelectedActions
        {
            get
            {
                int selectedCount = ActionGroups?.Count(a => a.IsSelected) ?? 0;
                return selectedCount >= 2;
            }
        }

        public bool HasAtLeastOneSelectedAction
        {
            get
            {
                return ActionGroups?.Any(a => a.IsSelected) == true;
            }
        }

        // Helper method to update all selection-related properties
        private void UpdateSelectionProperties()
        {
            OnPropertyChanged(nameof(HasSelectedActions));
            OnPropertyChanged(nameof(HasMultipleSelectedActions));
            OnPropertyChanged(nameof(HasAtLeastOneSelectedAction));
        }

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

        // Add a command for deleting multiple selected actions
        public ICommand DeleteSelectedActionsCommand { get; }

        private bool _isSimulating = false;
        private readonly ActionService _actionService;
        private readonly DataService _dataService;
        private readonly UserService _userService;
        private ActionPageViewModel _viewModel;
        private readonly SortingService _sortingService;
        private readonly FilteringService _filteringService;
        private readonly ActionGroupService _actionGroupService;
        private readonly GameSettingsService _gameSettingsService;
        private readonly ActionGroupCopierService _actionGroupCopierService;
        private readonly DialogService _dialogService;

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

        // Add missing field declaration
        private DateTime _lastMoveTime = DateTime.MinValue;

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
            var appPathService = new AppPathService();
            var fileService = new FileService(appPathService);
            _dataService = new DataService();
            _userService = new UserService(); // Initialize the user service
            _actionService = new ActionService(_dataService, fileService);
            _appModeService = ServiceProvider.GetService<CSimple.Services.AppModeService.AppModeService>();
            _sortingService = new SortingService();
            _filteringService = new FilteringService();
            _actionGroupService = new ActionGroupService(_actionService);
            _gameSettingsService = new GameSettingsService(_actionService);
            _actionGroupCopierService = new ActionGroupCopierService();
            _dialogService = new DialogService();

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

            // Fix for CS1503: Use a lambda to properly convert the method group
            RefreshCommand = new Command(() => OnRefreshDataClicked());
            TrainModelCommand = new Command(async () => await TrainModel());
            SearchCommand = new Command(FilterActionsBySearch);
            ChainActionCommand = new Command<ActionGroup>(ChainAction);
            ChainSelectedActionsCommand = new Command(ChainSelectedActions);
            AddToTrainingCommand = new Command(AddSelectedActionsToTraining);

            // Initialize delete selected actions command
            DeleteSelectedActionsCommand = new Command(DeleteSelectedActions);

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

            var filteredActions = _filteringService.FilterActions(_allActionGroups, SearchText, SelectedCategory);
            ActionGroups = new ObservableCollection<ActionGroup>(filteredActions);
        }

        private void FilterActionsBySearch()
        {
            var filteredActions = _filteringService.FilterActions(_allActionGroups, SearchText, SelectedCategory);
            ActionGroups = new ObservableCollection<ActionGroup>(filteredActions);
        }

        private void SortActionGroups()
        {
            var sortedGroups = _sortingService.SortActionGroups(ActionGroups.ToList(), SelectedSortOption);
            ActionGroups = new ObservableCollection<ActionGroup>(sortedGroups);
        }

        // Enhancement methods
        private ObservableCollection<ActionGroup> _allActionGroups = new ObservableCollection<ActionGroup>();

        private async Task RefreshData()
        {
            IsLoading = true;
            try
            {
                Debug.WriteLine("Refreshing action data based on current app mode...");

                // Clear existing collections
                DataItems.Clear();
                ActionGroups.Clear();
                LocalItems.Clear();
                _allActionGroups.Clear();

                // Refresh all data according to current app mode
                Debug.WriteLine("Complete page refresh requested");
                await _actionService.RefreshDataAsync(DataItems, ActionGroups, LocalItems);

                // Update ActionGroups collection
                _actionGroupService.UpdateActionGroupsFromDataItems(DataItems, ActionGroups, _allActionGroups);

                // Apply filtering or sorting
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
                    SortActionGroups();
                }

                // Reset selection
                SelectedActionGroup = null;

                // Update UI indicators
                OnPropertyChanged(nameof(ShowEmptyMessage));
                OnPropertyChanged(nameof(HasLocalItems));
                UpdateSelectionProperties();

                Debug.WriteLine($"Data refresh complete. Displaying {ActionGroups.Count} action groups and {LocalItems.Count} local items");
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

        protected override void OnAppearing()
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
                // Navigate to NetPage with parameter to scroll to general purpose models
                await Shell.Current.GoToAsync("///net?scrollToModels=true");
                
                Debug.WriteLine("✅ Navigated to NetPage for model training with scroll parameter");
                
                return;

                // Original training logic commented out - can be re-enabled if needed
                /*
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
                */
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to navigate to model training: {ex.Message}", "OK");
                Debug.WriteLine($"Error navigating to model training: {ex.Message}");
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

            // Generate intelligent chain name based on selected actions
            string suggestedName = GenerateChainName(selectedActions);
            
            string chainName = await DisplayPromptAsync("Create Action Chain",
                "Enter a name for this action chain:",
                "Create", "Cancel",
                suggestedName,
                100,
                Microsoft.Maui.Keyboard.Text);

            if (string.IsNullOrEmpty(chainName))
                return;

            try 
            {
                IsLoading = true;

                // Create new chained action that combines all selected actions
                var chainedAction = await CreateChainedActionAsync(selectedActions, chainName);
                
                // Add the chained action to our collections
                var chainedDataItem = new DataItem
                {
                    Data = new DataObject { ActionGroupObject = chainedAction }
                };
                
                DataItems.Add(chainedDataItem);
                ActionGroups.Add(chainedAction);
                _allActionGroups.Add(chainedAction);

                // Clear selections from original actions (but keep them)
                foreach (var action in selectedActions)
                {
                    action.IsSelected = false;
                }

                // Save the changes
                await SaveDataItemsToFile();

                // Refresh the UI
                UpdateActionGroupsFromDataItems();

                await DisplayAlert("Chain Created",
                    $"Action chain '{chainName}' created successfully with {selectedActions.Count} sub-actions.\n\nOriginal actions are preserved.",
                    "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating action chain: {ex.Message}");
                await DisplayAlert("Error", "Failed to create action chain. Please try again.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Generates an intelligent name for chained actions based on their content
        /// </summary>
        private string GenerateChainName(List<ActionGroup> selectedActions)
        {
            try
            {
                var actionTypes = selectedActions.Select(a => a.ActionType?.Replace(" ", "")).Distinct().ToList();
                var categories = selectedActions.Select(a => a.Category).Distinct().ToList();
                
                string baseName;
                if (actionTypes.Count == 1 && !string.IsNullOrEmpty(actionTypes.First()))
                {
                    baseName = $"{actionTypes.First()} Chain";
                }
                else if (categories.Count == 1 && !string.IsNullOrEmpty(categories.First()))
                {
                    baseName = $"{categories.First()} Workflow";
                }
                else
                {
                    baseName = "Action Sequence";
                }

                string timestamp = DateTime.Now.ToString("MMdd_HHmm");
                return $"{baseName} {timestamp}";
            }
            catch
            {
                return $"Action Chain {DateTime.Now:MMdd_HHmm}";
            }
        }

        /// <summary>
        /// Creates a new ActionGroup that represents a chain of the selected actions
        /// </summary>
        private async Task<ActionGroup> CreateChainedActionAsync(List<ActionGroup> selectedActions, string chainName)
        {
            // Create combined action array from all selected actions
            var combinedActions = new List<ActionItem>();
            var combinedModifiers = new List<ActionModifier>();
            var combinedFiles = new List<ActionFile>();
            
            foreach (var action in selectedActions)
            {
                // Combine all action items from each action
                if (action.ActionArray != null)
                {
                    combinedActions.AddRange(action.ActionArray);
                }
                
                if (action.ActionModifiers != null)
                {
                    combinedModifiers.AddRange(action.ActionModifiers);
                }
                
                if (action.Files != null)
                {
                    combinedFiles.AddRange(action.Files);
                }
            }

            // Determine the most common category
            var mostCommonCategory = selectedActions
                .GroupBy(a => a.Category)
                .OrderByDescending(g => g.Count())
                .First().Key;

            // Create the chained action
            var chainedAction = new ActionGroup
            {
                Id = Guid.NewGuid(),
                ActionName = chainName,
                ActionArray = combinedActions,
                ActionModifiers = combinedModifiers,
                Files = combinedFiles,
                Category = mostCommonCategory,
                ActionType = "Action Chain",
                CreatedAt = DateTime.Now,
                Description = $"Chain of {selectedActions.Count} actions: {string.Join(", ", selectedActions.Select(a => a.ActionName))}",
                IsChained = true,
                ChainName = chainName,
                UsageCount = 0,
                SuccessRate = selectedActions.Average(a => a.SuccessRate),
                IsPartOfTraining = false,
                IsSelected = false,
                IsLocal = false
            };

            // Generate formatted action array description
            var actionNames = selectedActions.Select(a => a.ActionName).ToList();
            chainedAction.ActionArrayFormatted = $"Chained sequence: {string.Join(" → ", actionNames)}";

            return chainedAction;
        }

        private async void AddSelectedActionsToTraining()
        {
            var selectedActions = ActionGroups.Where(a => a.IsSelected).ToList();

            if (selectedActions.Count == 0)
            {
                await DisplayAlert("Add to Training",
                    "Please select at least one action to add to training data.",
                    "OK");
                return;
            }

            try
            {
                IsLoading = true;
                ActionGroup chainedAction = null;

                // If multiple actions selected, chain them first
                if (selectedActions.Count > 1)
                {
                    string chainName = GenerateChainName(selectedActions);
                    chainName = await DisplayPromptAsync("Create Training Chain",
                        "Enter a name for the training action chain:",
                        "Create", "Cancel",
                        chainName,
                        100,
                        Microsoft.Maui.Keyboard.Text);

                    if (string.IsNullOrEmpty(chainName))
                    {
                        IsLoading = false;
                        return;
                    }

                    // Create the chained action
                    chainedAction = await CreateChainedActionAsync(selectedActions, chainName);
                    
                    // Add the chained action to our collections
                    chainedAction.IsLocal = true; // Mark as local action
                    var chainedDataItem = new DataItem
                    {
                        Data = new DataObject { ActionGroupObject = chainedAction }
                    };
                    
                    DataItems.Add(chainedDataItem);
                    ActionGroups.Add(chainedAction);
                    _allActionGroups.Add(chainedAction);
                }
                else
                {
                    // Single action selected, use it directly
                    chainedAction = selectedActions.First();
                }

                // Mark the action (chained or single) for training
                chainedAction.IsPartOfTraining = true;

                // Clear selections from all actions
                foreach (var action in ActionGroups)
                {
                    action.IsSelected = false;
                }

                // Save changes
                await SaveDataItemsToFile();

                // Refresh the UI
                UpdateActionGroupsFromDataItems();

                // Navigate to NetPage with parameter to scroll to training section and auto-select the chained action
                string actionId = chainedAction.Id.ToString();
                await Shell.Current.GoToAsync($"///net?scrollToTraining=true&selectAction={actionId}");

                Debug.WriteLine($"✅ Navigated to NetPage with training scroll and action selection: {actionId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error adding to training: {ex.Message}");
                await DisplayAlert("Error", "Failed to add actions to training. Please try again.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
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
                    UpdateSelectionProperties();
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

        private async void OnRefreshDataClicked(object sender = null, EventArgs e = null)
        {
            try
            {
                // Show loading indicator
                IsLoading = true;

                // Reset page state before data reload
                await ResetPageState();

                // Clear all collections first
                DataItems.Clear();
                ActionGroups.Clear();
                LocalItems.Clear();
                _allActionGroups.Clear();

                // Reset filters and search
                if (_sortPicker != null)
                    _sortPicker.SelectedIndex = 0;
                SearchText = string.Empty;
                SelectedCategory = "All Categories";

                Debug.WriteLine("Refreshing all data sources");

                // First load all local items directly from the file system
                await ForceReloadLocalItemsAsync();

                // Then refresh all data according to current app mode
                await RefreshData();

                // Force UI update for all collections
                OnPropertyChanged(nameof(DataItems));
                OnPropertyChanged(nameof(ActionGroups));
                OnPropertyChanged(nameof(LocalItems));
                OnPropertyChanged(nameof(ShowEmptyMessage));
                OnPropertyChanged(nameof(HasLocalItems));
                UpdateSelectionProperties();

                // Provide user feedback
                string modeMessage = _appModeService?.CurrentMode == AppMode.Online ?
                    "Page completely refreshed with data from local and backend storage" :
                    "Page completely refreshed with data from local storage";

                await DisplayAlert("Refresh Complete", modeMessage, "OK");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in complete page refresh: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
                await DisplayAlert("Refresh Error", "Failed to refresh the page. Please try again.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // New method to force reload all local items without filtering
        private async Task ForceReloadLocalItemsAsync()
        {
            try
            {
                Debug.WriteLine("Force reloading ALL local items including new ones");

                // Get all local items without filtering
                var allLocalItems = await _actionService.LoadAllDataItemsAsync();

                // Filter only for local items but don't exclude based on DataItems
                var localItems = allLocalItems?.Where(item =>
                    item?.Data?.ActionGroupObject?.IsLocal == true &&
                    item.deleted != true  // Only filter out explicitly deleted items
                ).ToList();

                if (localItems != null)
                {
                    LocalItems.Clear();
                    foreach (var item in localItems)
                    {
                        LocalItems.Add(item);
                        // Debug.WriteLine($"Added local item: {item?.Data?.ActionGroupObject?.ActionName}");
                    }
                }

                // Make sure we add any local items to the main collections if they aren't there
                foreach (var localItem in LocalItems)
                {
                    if (localItem?.Data?.ActionGroupObject != null)
                    {
                        // Check if this item exists in DataItems
                        bool existsInDataItems = DataItems.Any(di =>
                            di?.Data?.ActionGroupObject?.ActionName == localItem.Data.ActionGroupObject.ActionName);

                        if (!existsInDataItems)
                        {
                            // Add to DataItems if it's not there
                            DataItems.Add(localItem);
                            Debug.WriteLine($"Added missing local item to DataItems: {localItem.Data.ActionGroupObject.ActionName}");
                        }
                    }
                }

                // Update ActionGroups to reflect these changes
                UpdateActionGroupsFromDataItems();

                OnPropertyChanged(nameof(HasLocalItems));
                Debug.WriteLine($"Force loaded {LocalItems.Count} local items");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error force loading local items: {ex.Message}");
            }
        }

        // New method to reset page state completely
        private async Task ResetPageState()
        {
            try
            {
                // Reset selection state
                SelectedActionGroup = null;

                // Reset any cached data
                _lastMoveTime = DateTime.MinValue;

                // Reset any UI element states if needed
                foreach (var action in ActionGroups)
                {
                    action.IsSelected = false;
                    action.IsSimulating = false;
                }

                // Clear input fields and reset view state
                if (_inputActionPopup != null)
                    _inputActionPopup.IsVisible = false;

                // Let the UI update before continuing with data refresh
                await Task.Delay(50);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error resetting page state: {ex.Message}");
            }
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
            // Temporarily unsubscribe collection change handler to avoid multiple events
            if (_actionGroups != null)
            {
                _actionGroups.CollectionChanged -= ActionGroups_CollectionChanged;
            }

            // Use the existing service method
            _actionGroupService.UpdateActionGroupsFromDataItems(DataItems, ActionGroups, _allActionGroups);

            // Re-subscribe to collection changes
            if (_actionGroups != null)
            {
                _actionGroups.CollectionChanged += ActionGroups_CollectionChanged;
                // Re-subscribe to all items in the collection
                SubscribeToSelectionChanges(_actionGroups);
            }

            OnPropertyChanged(nameof(ActionGroups));
            OnPropertyChanged(nameof(ShowEmptyMessage));
            UpdateSelectionProperties();
            SortActionGroups();
        }

        private string DetermineCategory(ActionGroup actionGroup)
        {
            return _actionService.DetermineCategory(actionGroup);
        }

        private string DetermineActionTypeFromSteps(ActionGroup actionGroup)
        {
            return _actionService.DetermineActionTypeFromSteps(actionGroup);
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
                    var safeActionGroup = _actionGroupCopierService.SafelyCopyActionGroup(actionGroup);

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
                await _dialogService.ShowErrorDialog("Navigation failed",
                    "There was a problem navigating to the action details. Please try again.");
            }
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
            _gameSettingsService.UpdateGameSettings(GameOptimizedMode, MouseSensitivity, UseSmoothing);
        }

        // Fix for CS1998: Remove async keyword as there's no await operation in this method
        private Task NavigateToObservePage()
        {
            DebugOutput("Navigating to ObservePage");
            return Shell.Current.GoToAsync("///observe");
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
                // First attempt to load all local items directly
                await ForceReloadLocalItemsAsync();

                if (_appModeService?.CurrentMode == AppMode.Online)
                {
                    Debug.WriteLine("App is in online mode. Loading backend actions.");
                    // Load backend items and merge with local items
                    await RefreshData();
                }
                else
                {
                    Debug.WriteLine("App is in offline mode.");
                    // Update ActionGroups from loaded items
                    UpdateActionGroupsFromDataItems();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadActionsData: {ex.Message}");
                Debug.WriteLine(ex.StackTrace);
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
                    UpdateSelectionProperties();
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

        // Fixed method to delete selected actions with better debugging and error handling
        private async void DeleteSelectedActions()
        {
            Debug.WriteLine("⚠️ DeleteSelectedActions method called");

            try
            {
                // Get all selected actions
                var selectedActions = ActionGroups.Where(a => a.IsSelected).ToList();
                Debug.WriteLine($"🔍 Found {selectedActions.Count} selected action(s) for deletion");

                if (selectedActions.Count == 0)
                {
                    Debug.WriteLine("⚠️ No actions selected, showing alert");
                    await DisplayAlert("Delete Actions",
                        "Please select at least one action to delete.",
                        "OK");
                    return;
                }

                // Confirm deletion
                Debug.WriteLine("❓ Requesting deletion confirmation");
                bool confirmDelete = await DisplayAlert("Confirm Delete",
                    $"Are you sure you want to delete {selectedActions.Count} selected action{(selectedActions.Count > 1 ? "s" : "")}?",
                    "Yes", "No");

                if (!confirmDelete)
                {
                    Debug.WriteLine("❌ User cancelled deletion");
                    return;
                }

                Debug.WriteLine("✅ Starting deletion process");
                IsLoading = true;

                int successCount = 0;
                List<string> idsToDelete = new List<string>();
                List<string> namesToDelete = new List<string>();

                // Process each selected action
                foreach (var action in selectedActions)
                {
                    Debug.WriteLine($"⏳ Processing deletion for: {action.ActionName}");

                    // Add identifying information for deletion
                    if (!string.IsNullOrEmpty(action.Id.ToString()))
                        idsToDelete.Add(action.Id.ToString());

                    if (!string.IsNullOrEmpty(action.ActionName))
                        namesToDelete.Add(action.ActionName);

                    // Find corresponding DataItem for this ActionGroup
                    var dataItem = DataItems.FirstOrDefault(item =>
                        item?.Data?.ActionGroupObject?.ActionName == action.ActionName);

                    if (dataItem != null)
                    {
                        Debug.WriteLine($"✓ Found matching DataItem for {action.ActionName}");

                        // Add the DataItem ID if available
                        if (!string.IsNullOrEmpty(dataItem._id))
                            idsToDelete.Add(dataItem._id);

                        // Remove from UI collections
                        DataItems.Remove(dataItem);
                    }

                    // Remove from UI collections
                    if (ActionGroups.Contains(action))
                    {
                        ActionGroups.Remove(action);
                        Debug.WriteLine($"✓ Removed {action.ActionName} from ActionGroups");
                    }

                    var allGroupItem = _allActionGroups.FirstOrDefault(ag => ag.ActionName == action.ActionName);
                    if (allGroupItem != null)
                    {
                        _allActionGroups.Remove(allGroupItem);
                        Debug.WriteLine($"✓ Removed {action.ActionName} from _allActionGroups");
                    }

                    // Also remove from local items collection
                    var localItemToRemove = LocalItems.FirstOrDefault(li =>
                        li.Data?.ActionGroupObject?.ActionName == action.ActionName);

                    if (localItemToRemove != null)
                    {
                        LocalItems.Remove(localItemToRemove);
                        Debug.WriteLine($"✓ Removed {action.ActionName} from LocalItems");
                    }

                    // Track for success message
                    successCount++;
                }

                // Use the file service directly to delete all identified items
                Debug.WriteLine($"🗑️ Deleting {idsToDelete.Count} IDs and {namesToDelete.Count} action names from storage");
                var appPathService = new AppPathService();
                var fileService = new FileService(appPathService);
                await fileService.DeleteDataItemsAsync(idsToDelete, namesToDelete);

                // Show result message
                Debug.WriteLine($"🏁 Deletion complete: {successCount} action(s) deleted");

                // Force update of collections
                UpdateActionGroupsFromDataItems();

                // Update UI indicators
                OnPropertyChanged(nameof(ActionGroups));
                OnPropertyChanged(nameof(ShowEmptyMessage));
                UpdateSelectionProperties();
                OnPropertyChanged(nameof(HasLocalItems));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ ERROR deleting selected actions: {ex.Message}");
                Debug.WriteLine($"❌ Exception stack trace: {ex.StackTrace}");
                await DisplayAlert("Error", "An error occurred while deleting the selected actions.", "OK");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OnDeleteSelectedButtonClicked(object sender, EventArgs e)
        {
            Debug.WriteLine("⚡ Delete Selected button directly clicked");

            // Check if the button should be enabled
            if (!HasSelectedActions)
            {
                Debug.WriteLine("❌ Button clicked but HasSelectedActions is false - no actions selected");
                return;
            }

            try
            {
                // Call the DeleteSelectedActions method directly
                DeleteSelectedActions();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Error in OnDeleteSelectedButtonClicked: {ex.Message}");
                await DisplayAlert("Error", "Failed to delete selected actions. Please try again.", "OK");
            }
            finally
            {
                // Force UI update for button state
                UpdateSelectionProperties();
            }
        }

        // Monitor collection changes
        private void ActionGroups_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                SubscribeToSelectionChanges(e.NewItems.Cast<ActionGroup>());
            }

            if (e.OldItems != null)
            {
                UnsubscribeFromSelectionChanges(e.OldItems.Cast<ActionGroup>());
            }

            // Always update selection properties when collection changes
            UpdateSelectionProperties();
        }

        // Subscribe to PropertyChanged events of each ActionGroup
        private void SubscribeToSelectionChanges(IEnumerable<ActionGroup> actionGroups)
        {
            foreach (var actionGroup in actionGroups)
            {
                actionGroup.PropertyChanged += ActionGroup_PropertyChanged;
            }
        }

        // Unsubscribe to avoid memory leaks
        private void UnsubscribeFromSelectionChanges(IEnumerable<ActionGroup> actionGroups)
        {
            foreach (var actionGroup in actionGroups)
            {
                actionGroup.PropertyChanged -= ActionGroup_PropertyChanged;
            }
        }

        // Handle PropertyChanged events from ActionGroup objects
        private void ActionGroup_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ActionGroup.IsSelected))
            {
                // Selection state changed, update selection properties
                Debug.WriteLine("Action selection changed, updating selection properties");
                UpdateSelectionProperties();
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