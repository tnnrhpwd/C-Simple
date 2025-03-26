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

namespace CSimple.Pages
{
    public partial class ActionPage : ContentPage
    {
        public ObservableCollection<DataItem> Data { get; set; } = new ObservableCollection<DataItem>();
        public ICommand NavigateToObservePageCommand { get; }
        public ICommand SaveActionCommand { get; set; }
        public ICommand ToggleSimulateActionGroupCommand { get; set; }
        public ICommand SaveToFileCommand { get; set; }
        public ICommand LoadFromFileCommand { get; set; }
        public ICommand RowTappedCommand { get; }
        public ICommand DeleteActionGroupCommand { get; set; }
        private bool _isSimulating = false;
        private readonly ActionService _actionService;
        private ActionPageViewModel _viewModel;

        // Fix: Defining the missing SortPicker as a class field
        private Picker _sortPicker;

        // Fix: Defining InputActionPopup with correct type
        private Grid _inputActionPopup;

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

        public ActionPage()
        {
            InitializeComponent();
            _viewModel = new ActionPageViewModel();
            BindingContext = _viewModel;

            // Initialize fields - using null instead of FindByName since we don't have these elements anymore
            _sortPicker = null; // We'll need to add this element to the XAML or handle this another way
            _inputActionPopup = this.FindByName<Grid>("InputActionPopup");

            var fileService = new FileService();
            var dataService = new DataService();
            _actionService = new ActionService(dataService, fileService);

            // Initialize commands
            ToggleSimulateActionGroupCommand = new Command<ActionGroup>(async (actionGroup) => await ToggleSimulateActionGroupAsync(actionGroup));
            SaveToFileCommand = new Command(async () => await SaveDataItemsToFile());
            LoadFromFileCommand = new Command(async () => await LoadDataItemsFromFile());
            NavigateToObservePageCommand = new Command(async () => await NavigateToObservePage());
            DeleteActionGroupCommand = new Command<DataItem>(async (dataItem) => await DeleteDataItemAsync(dataItem));

            RowTappedCommand = new Command<ActionGroup>(OnRowTapped);

            // Load existing action groups from file asynchronously
            _ = LoadDataItemsFromFile(); // Ignore the returned task since we only need to ensure it's running
            DebugOutput("Action Page Initialized");
        }

        private async Task DeleteDataItemAsync(DataItem dataItem)
        {
            if (dataItem == null)
                return;

            try
            {
                bool confirmDelete = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the action group '{dataItem.ToString()}'?", "Yes", "No");
                if (confirmDelete)
                {
                    if (await _actionService.DeleteDataItemAsync(dataItem))
                    {
                        // Remove the item from the UI list
                        Data.Remove(dataItem);
                        DebugOutput($"Action Group {dataItem.ToString()} deleted.");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugOutput($"Error deleting action group: {ex.Message}");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            // Load data from backend
            await LoadDataItemsFromBackend();

            // Sort data directly using the default index
            if (Data?.Count > 0)
            {
                var sortedList = ActionService.SortDataItems(Data.ToList(), 1); // Use default index 1

                Data.Clear();
                foreach (var item in sortedList)
                {
                    Data.Add(item);
                }

                // This is important to notify the view that the Data source has been updated
                OnPropertyChanged(nameof(Data));

                // Log data count for debugging
                DebugOutput($"Data collection now has {Data.Count} items");
            }
            else
            {
                DebugOutput("Data collection is empty or null after loading from backend");
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

        private async void OnRowTapped(ActionGroup actionGroup)
        {
            var actionDetailPage = new ActionDetailPage(actionGroup);
            await Navigation.PushModalAsync(actionDetailPage);
        }

        private async Task ToggleSimulateActionGroupAsync(ActionGroup actionGroup)
        {
            if (actionGroup != null)
            {
                IsSimulating = actionGroup.IsSimulating;
                await _actionService.ToggleSimulateActionGroupAsync(actionGroup);
                IsSimulating = false;

                // Reload ActionGroups after toggling simulation
                await LoadDataItemsFromBackend();
            }
        }

        private async Task SaveDataItemsToFile()
        {
            await _actionService.SaveDataItemsToFile(Data.ToList());
        }

        private async Task LoadDataItemsFromFile()
        {
            var items = await _actionService.LoadDataItemsFromFile();
            Data.Clear();
            foreach (var item in items)
            {
                Data.Add(item);
            }
        }

        private async Task LoadDataItemsFromBackend()
        {
            try
            {
                var items = await _actionService.LoadDataItemsFromBackend();

                // Clear existing data
                Data.Clear();

                // Add new items
                foreach (var item in items)
                {
                    Data.Add(item);
                }

                // Notify UI that collection has changed
                OnPropertyChanged(nameof(Data));

                // Debug output for validation
                DebugOutput($"Loaded {items.Count} items from backend");
            }
            catch (Exception ex)
            {
                DebugOutput($"Error loading data from backend: {ex.Message}");
            }
        }

        private void OnSortOrderChanged(object sender, EventArgs e)
        {
            // Fix: Check if SortPicker exists and has a selection
            if (_sortPicker == null || _sortPicker.SelectedIndex < 0 || Data == null || Data.Count == 0)
                return;

            var sortedList = ActionService.SortDataItems(Data.ToList(), _sortPicker.SelectedIndex);

            Data.Clear();
            foreach (var item in sortedList)
            {
                Data.Add(item);
            }

            OnPropertyChanged(nameof(Data));
        }

        public new event PropertyChangedEventHandler PropertyChanged;

        public new void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void OnRefreshDataClicked(object sender, EventArgs e)
        {
            await LoadDataItemsFromBackend();
        }
    }
}