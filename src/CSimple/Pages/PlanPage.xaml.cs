using Microsoft.Maui.Storage;
using Microsoft.Maui.Graphics;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Input;

namespace CSimple.Pages
{
    public partial class PlanPage : ContentPage
    {
        public bool ShowNewPlan { get; set; } = false;
        public bool ShowMyPlans { get; set; } = false;
        public string NewPlanText { get; set; } = string.Empty;
        public ObservableCollection<string> MyPlans { get; set; } = new ObservableCollection<string>();
        public string CreatePlanButtonText => ShowNewPlan ? "Cancel Plan" : "Create Plan";
        public string MyPlansButtonText => ShowMyPlans ? "Hide Plans" : "My Plans";
        public ICommand ToggleCreatePlanCommand { get; }
        public ICommand ToggleMyPlansCommand { get; }
        public ICommand SubmitPlanCommand { get; }
        private readonly DataService _dataService;
        private readonly FileService _fileService;

        public PlanPage()
        {
            InitializeComponent();
            // Initialize Commands
            ToggleCreatePlanCommand = new Command(OnToggleCreatePlan);
            ToggleMyPlansCommand = new Command(OnToggleMyPlans);
            SubmitPlanCommand = new Command(OnSubmitPlan);
            // Initialize services
            _dataService = new DataService();
            _fileService = new FileService();
            // Bind the context
            BindingContext = this;
            CheckUserLoggedIn();
            // Load plans from file
            _ = LoadPlansFromFile();
            // Populate calendar
            PopulateCalendar(DateTime.Now);
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
                await LoadPlansFromBackend();
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

        private void OnToggleCreatePlan()
        {
            ShowNewPlan = !ShowNewPlan;
            OnPropertyChanged(nameof(ShowNewPlan));
            OnPropertyChanged(nameof(CreatePlanButtonText));
        }

        private void OnToggleMyPlans()
        {
            ShowMyPlans = !ShowMyPlans;
            OnPropertyChanged(nameof(ShowMyPlans));
            OnPropertyChanged(nameof(MyPlansButtonText));
        }

        private async void OnSubmitPlan()
        {
            if (!string.IsNullOrWhiteSpace(NewPlanText))
            {
                MyPlans.Add(NewPlanText);
                await SavePlansToFile();
                await SavePlanToBackend(NewPlanText);
                NewPlanText = string.Empty;
                OnPropertyChanged(nameof(NewPlanText));
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }

        private async Task LoadPlansFromBackend()
        {
            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return;
                }

                var data = "Plan";
                var plans = await _dataService.GetDataAsync(data, token);
                var formattedPlans = FormatPlansFromBackend(plans.Data);

                MyPlans.Clear();
                foreach (var plan in formattedPlans)
                {
                    MyPlans.Add(plan);
                }

                await SavePlansToFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading plans from backend: {ex.Message}");
            }
        }

        private ObservableCollection<string> FormatPlansFromBackend(IEnumerable<string> planStrings)
        {
            var formattedPlans = new ObservableCollection<string>();

            foreach (var planString in planStrings)
            {
                if (planString.Contains("|Plan"))
                {
                    formattedPlans.Add(planString);
                }
            }

            return formattedPlans;
        }

        private async Task SavePlanToBackend(string plan)
        {
            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return;
                }

                var data = $"Plan:{plan}";
                var response = await _dataService.CreateDataAsync(data, token);
                if (response.DataIsSuccess)
                {
                    Debug.WriteLine("Plan saved to backend");
                }
                else
                {
                    Debug.WriteLine("Failed to save plan to backend");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving plan to backend: {ex.Message}");
            }
        }

        private async Task SavePlansToFile()
        {
            try
            {
                await _fileService.SavePlansAsync(MyPlans);
                Debug.WriteLine("Plans saved to file");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving plans to file: {ex.Message}");
            }
        }

        private async Task LoadPlansFromFile()
        {
            try
            {
                var loadedPlans = await _fileService.LoadPlansAsync();
                MyPlans.Clear();
                foreach (var plan in loadedPlans)
                {
                    MyPlans.Add(plan);
                }
                Debug.WriteLine("Plans loaded from file");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading plans from file: {ex.Message}");
            }
        }

        private void PopulateCalendar(DateTime date)
        {
            var firstDayOfMonth = new DateTime(date.Year, date.Month, 1);
            var daysInMonth = DateTime.DaysInMonth(date.Year, date.Month);
            var startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            for (int i = 0; i < daysInMonth; i++)
            {
                var dayButton = new Button
                {
                    Text = (i + 1).ToString(),
                    BackgroundColor = Colors.White,
                    TextColor = Colors.Black
                };

                var row = (i + startDayOfWeek) / 7 + 1;
                var column = (i + startDayOfWeek) % 7;

                Grid.SetRow(dayButton, row);
                Grid.SetColumn(dayButton, column);
                CalendarGrid.Children.Add(dayButton);
            }
        }
    }
}
