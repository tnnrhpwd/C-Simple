using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Input;
using System.ComponentModel;
using CSimple.Services;

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

            // Set the BindingContext to the current instance of ActionPage
            BindingContext = this;

            // Initialize fields
            SortPicker = this.FindByName<Picker>("SortPicker");
            InputActionPopup = this.FindByName<ContentView>("InputActionPopup");

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
            SortPicker.SelectedIndex = 1; // default to CreatedAt Descending
            await LoadDataItemsFromBackend();
            OnSortOrderChanged(SortPicker, EventArgs.Empty); // Ensure data is sorted after loading
        }

        private async Task NavigateToObservePage()
        {
            DebugOutput("Navigating to ObservePage");
            await Shell.Current.GoToAsync("///observe");
        }

        private void OnInputActionClicked(object sender, EventArgs e)
        {
            InputActionPopup.IsVisible = true;
        }

        private void OnOkayClick(object sender, EventArgs e)
        {
            InputActionPopup.IsVisible = false;
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
            var items = await _actionService.LoadDataItemsFromBackend();
            Data.Clear();
            foreach (var item in items)
            {
                Data.Add(item);
            }
        }

        private void OnSortOrderChanged(object sender, EventArgs e)
        {
            if (SortPicker.SelectedIndex < 0 || Data == null || Data.Count == 0)
                return;

            var sortedList = ActionService.SortDataItems(Data.ToList(), SortPicker.SelectedIndex);

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