using CSimple.Models;
using CSimple.Services;
using CSimple.Services.AppModeService;
using CSimple.ViewModels; // Added for OrientPageViewModel
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Storage;
using System.Text.Json;
using CSimple.Services.AppModeService;

namespace CSimple.Pages
{
    public partial class GoalPage : ContentPage, INotifyPropertyChanged
    {
        #region Private Fields
        private readonly GoalService _goalService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private readonly OrientPageViewModel _orientPageViewModel; // Added
        private readonly FileService _fileService; // Added FileService
        private readonly DataService _dataService; // Added for backend integration

        private bool _showNewGoal = false;
        private bool _isLoading = false;
        private bool _hasError = false;
        private string _errorMessage = string.Empty;
        private bool _isUserLoggedIn = false;
        #endregion
        public bool ShowNewGoal
        {
            get => _showNewGoal;
            set => SetProperty(ref _showNewGoal, value, onChanged: () => OnPropertyChanged(nameof(CreateGoalButtonText)));
        }

        public string CreateGoalButtonText => ShowNewGoal ? "Cancel Goal" : "Create Goal";

        // Loading and Error States (similar to PlanPage)
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        // User state
        public bool IsUserLoggedIn
        {
            get => _isUserLoggedIn;
            set => SetProperty(ref _isUserLoggedIn, value);
        }

        public ObservableCollection<Goal> MyGoals { get; set; } = new ObservableCollection<Goal>();
        public ObservableCollection<DataItem> AllDataItems { get; set; } = new ObservableCollection<DataItem>();

        private string _newGoalTitle = string.Empty;
        public string NewGoalTitle
        {
            get => _newGoalTitle;
            set => SetProperty(ref _newGoalTitle, value);
        }

        private string _newGoalDescription = string.Empty;
        public string NewGoalDescription
        {
            get => _newGoalDescription;
            set => SetProperty(ref _newGoalDescription, value);
        }

        private int _goalPriority = 3;
        public int GoalPriority
        {
            get => _goalPriority;
            set => SetProperty(ref _goalPriority, value);
        }

        private DateTime _goalDeadline = DateTime.Today.AddDays(7);
        public DateTime GoalDeadline
        {
            get => _goalDeadline;
            set => SetProperty(ref _goalDeadline, value);
        }

        private bool _shareGoal = false;
        public bool ShareGoal
        {
            get => _shareGoal;
            set => SetProperty(ref _shareGoal, value);
        }

        private string _selectedGoalType;
        public string SelectedGoalType
        {
            get => _selectedGoalType;
            set => SetProperty(ref _selectedGoalType, value);
        }

        public ObservableCollection<string> GoalTypes { get; } = new ObservableCollection<string>
        {
            "Personal", "Work", "Learning", "Health", "Finance", "Other"
        };

        // --- AI Improvement Section ---
        private string _improvementSuggestion = "Click 'Run Improvement Pipeline' to get AI suggestions.";
        public string ImprovementSuggestion
        {
            get => _improvementSuggestion;
            set => SetProperty(ref _improvementSuggestion, value);
        }

        private string _aiPromptInput = "Suggest future improvements given my PC recorded data.";
        public string AiPromptInput
        {
            get => _aiPromptInput;
            set => SetProperty(ref _aiPromptInput, value);
        }

        private bool _isPipelineRunning = false;
        public bool IsPipelineRunning
        {
            get => _isPipelineRunning;
            set => SetProperty(ref _isPipelineRunning, value);
        }

        // Properties for Pipeline Selection
        public ObservableCollection<string> AvailablePipelines { get; } = new ObservableCollection<string>();

        private string _selectedPipelineName;
        public string SelectedPipelineName
        {
            get => _selectedPipelineName;
            set => SetProperty(ref _selectedPipelineName, value);
        }
        // --- End AI Improvement Section ---


        public ICommand ToggleCreateGoalCommand { get; }
        public ICommand SubmitGoalCommand { get; }
        public ICommand DeleteGoalCommand { get; }
        public ICommand EditGoalCommand { get; }
        public ICommand RunImprovementPipelineCommand { get; } // Added

        // --- Tab Selection Properties ---
        private bool _isMyGoalsSelected = true; // Default to My Goals tab
        public bool IsMyGoalsSelected
        {
            get => _isMyGoalsSelected;
            set => SetProperty(ref _isMyGoalsSelected, value);
        }

        private bool _isSharedGoalsSelected;
        public bool IsSharedGoalsSelected
        {
            get => _isSharedGoalsSelected;
            set => SetProperty(ref _isSharedGoalsSelected, value);
        }

        private bool _isDiscoverSelected;
        public bool IsDiscoverSelected
        {
            get => _isDiscoverSelected;
            set => SetProperty(ref _isDiscoverSelected, value);
        }

        // Properties for search (referenced in XAML)
        private string _sharedGoalSearchQuery;
        public string SharedGoalSearchQuery
        {
            get => _sharedGoalSearchQuery;
            set => SetProperty(ref _sharedGoalSearchQuery, value);
        }

        private string _discoverSearchQuery;
        public string DiscoverSearchQuery
        {
            get => _discoverSearchQuery;
            set => SetProperty(ref _discoverSearchQuery, value);
        }

        // Shared goals collection (referenced in XAML)
        public ObservableCollection<Goal> SharedGoals { get; set; } = new ObservableCollection<Goal>();

        // Discover goals collection (referenced in XAML)
        public ObservableCollection<Goal> DiscoverGoals { get; set; } = new ObservableCollection<Goal>();

        // --- Additional Commands ---
        public ICommand SwitchToMyGoalsCommand { get; }
        public ICommand SwitchToSharedGoalsCommand { get; }
        public ICommand SwitchToDiscoverGoalsCommand { get; }
        public ICommand FilterCategoryCommand { get; }
        public ICommand UnshareGoalCommand { get; }
        public ICommand DownloadGoalCommand { get; }

        // Modified constructor to accept OrientPageViewModel and FileService
        public GoalPage(GoalService goalService, CSimple.Services.AppModeService.AppModeService appModeService, OrientPageViewModel orientPageViewModel, FileService fileService, DataService dataService) // Added DataService
        {
            InitializeComponent();

            _goalService = goalService; // Use injected service
            _appModeService = appModeService; // Use injected service
            _orientPageViewModel = orientPageViewModel; // Store injected ViewModel
            _fileService = fileService; // Store injected FileService
            _dataService = dataService; // Store injected DataService

            // Initialize existing commands
            ToggleCreateGoalCommand = new Command(OnToggleCreateGoal);
            SubmitGoalCommand = new Command(async () => await OnSubmitGoal());
            DeleteGoalCommand = new Command<Goal>(async (goal) => await OnDeleteGoal(goal));
            EditGoalCommand = new Command<Goal>(OnEditGoal);
            RunImprovementPipelineCommand = new Command(async () => await OnRunPipeline(), () => !IsPipelineRunning && !string.IsNullOrEmpty(SelectedPipelineName)); // Disable if no pipeline selected

            // Initialize new tab commands
            SwitchToMyGoalsCommand = new Command(() => SwitchTab(TabType.MyGoals));
            SwitchToSharedGoalsCommand = new Command(() => SwitchTab(TabType.SharedGoals));
            SwitchToDiscoverGoalsCommand = new Command(() => SwitchTab(TabType.Discover));

            // Initialize other new commands
            FilterCategoryCommand = new Command<string>(OnFilterCategory);
            UnshareGoalCommand = new Command<Goal>(OnUnshareGoal);
            DownloadGoalCommand = new Command<Goal>(OnDownloadGoal);

            BindingContext = this;

            _ = InitializePageAsync(); // Initialize with backend integration
        }

        private async Task InitializePageAsync()
        {
            try
            {
                IsLoading = true;
                HasError = false;

                // Check user authentication status
                await CheckUserLoggedInAsync();

                // Load initial data
                await LoadInitialDataAsync();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                Debug.WriteLine($"Error initializing page: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Add the missing LoadInitialDataAsync method
        private async Task LoadInitialDataAsync()
        {
            // Load goals
            await LoadGoalsAsync();

            // Load available pipelines
            await LoadAvailablePipelinesAsync();
        }

        private async Task CheckUserLoggedInAsync()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Using local goals only.");
                IsUserLoggedIn = false;
                await LoadGoalsFromFile();
                return;
            }

            var isLoggedIn = await IsUserLoggedInAsync();
            IsUserLoggedIn = isLoggedIn;

            if (!isLoggedIn)
            {
                Debug.WriteLine("User is not logged in.");
                // In PlanPage, it navigates to login, but here we'll just show local goals
            }
            else
            {
                Debug.WriteLine("User is logged in.");
                await LoadGoalsFromBackend();
            }
        }

        // Method to load available pipeline names
        private async Task LoadAvailablePipelinesAsync()
        {
            try
            {
                var pipelines = await _fileService.ListPipelinesAsync();
                AvailablePipelines.Clear();
                if (pipelines != null)
                {
                    foreach (var pipeline in pipelines.OrderBy(p => p.Name))
                    {
                        AvailablePipelines.Add(pipeline.Name);
                    }
                }
                // Update command CanExecute state after loading
                ((Command)RunImprovementPipelineCommand).ChangeCanExecute();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading available pipelines: {ex.Message}");
                // Handle error appropriately, maybe show an alert
            }
        }

        // Add this method to initialize sample data for SharedGoals and DiscoverGoals
        private void InitializeSampleData()
        {
            // Add sample shared goals
            SharedGoals.Add(new Goal
            {
                Title = "Team Project Milestone",
                Description = "Complete the first milestone of our team project by end of month",
                Priority = 4,
                Deadline = DateTime.Today.AddDays(14),
                GoalType = "Work",
                SharedWith = 3,
                SharedDate = DateTime.Today.AddDays(-5)
            });

            SharedGoals.Add(new Goal
            {
                Title = "Study Group Goals",
                Description = "Weekly study sessions for upcoming certification",
                Priority = 3,
                Deadline = DateTime.Today.AddDays(30),
                GoalType = "Learning",
                SharedWith = 5,
                SharedDate = DateTime.Today.AddDays(-2)
            });

            // Add sample discover goals
            DiscoverGoals.Add(new Goal
            {
                Title = "30-Day Fitness Challenge",
                Description = "Daily exercise routine to improve health and energy levels",
                Priority = 5,
                Deadline = DateTime.Today.AddDays(30),
                GoalType = "Health",
                Creator = "FitnessPro",
                Rating = 4.8,
                Downloads = 2345,
                CreatorImage = "https://picsum.photos/200" // placeholder image URL
            });

            DiscoverGoals.Add(new Goal
            {
                Title = "Budget Management Plan",
                Description = "Step-by-step plan to track and optimize personal finances",
                Priority = 4,
                Deadline = DateTime.Today.AddDays(90),
                GoalType = "Finance",
                Creator = "MoneyMentor",
                Rating = 4.5,
                Downloads = 1872,
                CreatorImage = "https://picsum.photos/201" // placeholder image URL
            });

            DiscoverGoals.Add(new Goal
            {
                Title = "Daily Productivity System",
                Description = "Framework for maximizing your daily productivity",
                Priority = 3,
                Deadline = DateTime.Today.AddDays(1),
                GoalType = "Productivity",
                Creator = "ProductivityGuru",
                Rating = 4.9,
                Downloads = 3105,
                CreatorImage = "https://picsum.photos/202" // placeholder image URL
            });
        }

        // Tab switching enum and method
        private enum TabType { MyGoals, SharedGoals, Discover }

        private void SwitchTab(TabType tab)
        {
            IsMyGoalsSelected = tab == TabType.MyGoals;
            IsSharedGoalsSelected = tab == TabType.SharedGoals;
            IsDiscoverSelected = tab == TabType.Discover;
        }

        // Command implementations for new commands
        private void OnFilterCategory(string category)
        {
            if (string.IsNullOrEmpty(category)) return;

            Debug.WriteLine($"Filtering goals by category: {category}");
            // TODO: Implement category filtering for discover goals
        }

        private void OnUnshareGoal(Goal goal)
        {
            if (goal == null) return;

            Debug.WriteLine($"Unsharing goal: {goal.Title}");
            // TODO: Implement unsharing functionality
            SharedGoals.Remove(goal);
        }

        private async void OnDownloadGoal(Goal goal)
        {
            if (goal == null) return;

            Debug.WriteLine($"Downloading goal: {goal.Title}");
            // TODO: Implement download functionality

            // Simplified example - copy to MyGoals
            var downloadedGoal = new Goal
            {
                Title = goal.Title,
                Description = goal.Description,
                Priority = goal.Priority,
                Deadline = goal.Deadline,
                GoalType = goal.GoalType,
                CreatedAt = DateTime.UtcNow
            };

            MyGoals.Add(downloadedGoal);
            await SaveGoalsToFile();
        }

        // --- AI Improvement Method ---
        private async Task OnRunPipeline()
        {
            if (IsPipelineRunning || string.IsNullOrEmpty(SelectedPipelineName))
            {
                if (string.IsNullOrEmpty(SelectedPipelineName))
                {
                    await DisplayAlert("Select Pipeline", "Please select a pipeline from the dropdown.", "OK");
                }
                return;
            }


            IsPipelineRunning = true;
            ImprovementSuggestion = "Loading pipeline and running analysis, please wait...";
            ((Command)RunImprovementPipelineCommand).ChangeCanExecute(); // Update button state

            try
            {
                // Use the prompt from the input field
                string prompt = string.IsNullOrWhiteSpace(AiPromptInput)
                    ? "Suggest future improvements given my PC recorded data."
                    : AiPromptInput;

                // Execute the selected pipeline by name via OrientPageViewModel
                string result = await _orientPageViewModel.ExecutePipelineByNameAsync(SelectedPipelineName, prompt);

                ImprovementSuggestion = result; // Display the result
            }
            catch (Exception ex)
            {
                ImprovementSuggestion = $"Error running pipeline '{SelectedPipelineName}': {ex.Message}";
                Debug.WriteLine($"Error executing pipeline '{SelectedPipelineName}': {ex}");
            }
            finally
            {
                IsPipelineRunning = false;
                ((Command)RunImprovementPipelineCommand).ChangeCanExecute(); // Update button state
            }
        }
        // --- End AI Improvement Method ---


        private void OnToggleCreateGoal()
        {
            ShowNewGoal = !ShowNewGoal;
            if (!ShowNewGoal)
            {
                ClearGoalForm();
            }
        }

        private async Task OnSubmitGoal()
        {
            if (string.IsNullOrWhiteSpace(NewGoalTitle))
            {
                await DisplayAlert("Missing Title", "Please enter a title for the goal.", "OK");
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;

                var newGoal = new Goal
                {
                    Title = NewGoalTitle,
                    Description = NewGoalDescription,
                    Priority = GoalPriority,
                    Deadline = GoalDeadline,
                    IsShared = ShareGoal,
                    GoalType = SelectedGoalType,
                    CreatedAt = DateTime.UtcNow
                };

                MyGoals.Insert(0, newGoal);
                await SaveGoalsToFile();

                // Save to backend if online
                if (_appModeService.CurrentMode != AppMode.Offline)
                {
                    await SaveGoalToBackend(newGoal);
                }

                ClearGoalForm();
                ShowNewGoal = false;

                await DisplayAlert("Success", "Goal created successfully!", "OK");
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                await DisplayAlert("Error", $"Failed to create goal: {ex.Message}", "OK");
                Debug.WriteLine($"Error creating goal: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task OnDeleteGoal(Goal goalToDelete)
        {
            if (goalToDelete == null) return;

            try
            {
                IsLoading = true;
                HasError = false;

                bool confirm = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the goal '{goalToDelete.Title}'?", "Yes", "No");
                if (confirm)
                {
                    MyGoals.Remove(goalToDelete);
                    await SaveGoalsToFile();

                    // Delete from backend if online
                    if (_appModeService.CurrentMode != AppMode.Offline)
                    {
                        await _goalService.DeleteGoalFromBackend(goalToDelete.Id);
                    }

                    await DisplayAlert("Success", "Goal deleted successfully!", "OK");
                }
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = ex.Message;
                await DisplayAlert("Error", $"Failed to delete goal: {ex.Message}", "OK");
                Debug.WriteLine($"Error deleting goal: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void OnEditGoal(Goal goalToEdit)
        {
            if (goalToEdit == null) return;

            NewGoalTitle = goalToEdit.Title;
            NewGoalDescription = goalToEdit.Description;
            GoalPriority = goalToEdit.Priority;
            GoalDeadline = goalToEdit.Deadline;
            ShareGoal = goalToEdit.IsShared;
            SelectedGoalType = goalToEdit.GoalType;

            ShowNewGoal = true;

            DisplayAlert("Edit Goal", "Goal details loaded into the form. Modify and submit to save changes (will create a new entry for now).", "OK");
        }

        private void ClearGoalForm()
        {
            NewGoalTitle = string.Empty;
            NewGoalDescription = string.Empty;
            GoalPriority = 3;
            GoalDeadline = DateTime.Today.AddDays(7);
            ShareGoal = false;
            SelectedGoalType = null;
        }

        private async Task LoadGoalsAsync()
        {
            Debug.WriteLine("Loading goals...");
            await _goalService.GetLocalGoalsAsync(MyGoals);
            OnPropertyChanged(nameof(MyGoals));
        }

        private async Task SaveGoalsToFile()
        {
            await _goalService.SaveGoalsToFile(MyGoals);
        }

        private async Task LoadPublicGoalsFromBackend()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping public goal loading.");
                return;
            }

            try
            {
                IsLoading = true;

                Debug.WriteLine("LoadPublicGoalsFromBackend called - loading from backend API");

                // Call the backend public endpoint
                var publicGoalsData = await _dataService.GetPublicPlansAsync();

                // Convert DataItems to Goals for public display
                var publicGoalItems = new List<Goal>();

                foreach (var dataItem in publicGoalsData.Data ?? new List<DataItem>())
                {
                    try
                    {
                        var publicGoal = ConvertDataItemToPublicGoal(dataItem);
                        if (publicGoal != null)
                        {
                            publicGoalItems.Add(publicGoal);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error converting data item to public goal: {ex.Message}");
                    }
                }

                SharedGoals.Clear();
                foreach (var goal in publicGoalItems)
                {
                    SharedGoals.Add(goal);
                }

                Debug.WriteLine($"Successfully loaded {SharedGoals.Count} public goals from backend");

                // Clear any errors since we successfully loaded data
                HasError = false;
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading public goals: {ex.Message}");
                HasError = true;
                ErrorMessage = "Failed to load public goals. Please try again later.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private Goal ConvertDataItemToPublicGoal(DataItem dataItem)
        {
            if (dataItem?.Data?.Text == null) return null;

            var publicGoal = new Goal
            {
                Id = dataItem._id ?? Guid.NewGuid().ToString(),
                CreatedAt = dataItem.createdAt,
                SharedDate = dataItem.createdAt
            };

            // Parse the pipe-delimited text to extract goal information
            var parts = dataItem.Data.Text.Split('|');
            foreach (var part in parts)
            {
                var keyValue = part.Split(':', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim().ToLower();
                var value = keyValue[1].Trim();

                switch (key)
                {
                    case "title":
                        publicGoal.Title = value;
                        break;
                    case "goal":
                        publicGoal.Description = value;
                        break;
                    case "description":
                        publicGoal.Description = value;
                        break;
                    case "priority":
                        if (int.TryParse(value, out int priority))
                            publicGoal.Priority = priority;
                        break;
                    case "deadline":
                        if (DateTime.TryParse(value, out DateTime deadline))
                            publicGoal.Deadline = deadline;
                        break;
                    case "goaltype":
                        publicGoal.GoalType = value;
                        break;
                }
            }

            // Set default values for shared goals
            publicGoal.IsShared = true;
            publicGoal.SharedWith = 1; // Default to 1 for now

            return publicGoal;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("GoalPage Appearing");

            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                await LoadGoalsFromFile();
            }
            else
            {
                await LoadGoalsFromBackend();
                await LoadPublicGoalsFromBackend();
            }
        }

        private async void CheckUserLoggedIn()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Using local goals only.");
                return;
            }

            if (!await _goalService.IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                await NavigateLogin();
            }
            else
            {
                Debug.WriteLine("User is logged in. Backend operations enabled.");
            }
        }

        private async Task<bool> IsUserLoggedInAsync()
        {
            try
            {
                var userToken = await SecureStorage.GetAsync("userToken");
                if (!string.IsNullOrEmpty(userToken))
                {
                    Debug.WriteLine("User token found: " + userToken);
                    return true;
                }
                else
                {
                    Debug.WriteLine("No user token found.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error retrieving user token: {ex.Message}");
                return false;
            }
        }

        private async Task LoadGoalsFromBackend()
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Skipping backend goal loading.");
                return;
            }

            try
            {
                IsLoading = true;
                HasError = false;

                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    IsUserLoggedIn = false;
                    await LoadGoalsFromFile(); // Fallback to local/sample data
                    return;
                }

                // First, check if the user is still logged in
                var isLoggedIn = await _dataService.IsLoggedInAsync();
                if (!isLoggedIn)
                {
                    Debug.WriteLine("User authentication check failed. Falling back to local data.");
                    IsUserLoggedIn = false;
                    await LoadGoalsFromFile();
                    return;
                }

                var data = "Goal";
                Debug.WriteLine($"Making request to backend for goals with data: {data}");

                var goals = await _dataService.GetDataAsync(data, token);
                var myGoalItems = ProcessMyGoalsFromBackend(goals.Data?.Cast<DataItem>().ToList() ?? new List<DataItem>());

                MyGoals.Clear();
                AllDataItems.Clear();

                foreach (var goal in myGoalItems)
                {
                    MyGoals.Add(goal);
                }

                foreach (var item in goals.Data ?? new List<DataItem>())
                {
                    AllDataItems.Add(item);
                }

                await SaveGoalsToFile();

                Debug.WriteLine($"Successfully loaded {MyGoals.Count} goals from backend");
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine("Unauthorized access - user session expired");
                HasError = true;
                ErrorMessage = "Your session has expired. Please log in again.";
                IsUserLoggedIn = false;

                // Clear sensitive data and fallback to local storage
                await LoadGoalsFromFile();
            }
            catch (JsonException jsonEx)
            {
                Debug.WriteLine($"JSON parsing error: {jsonEx.Message}. Server may have returned HTML instead of JSON.");
                HasError = true;
                ErrorMessage = "🔧 BACKEND CONFIGURATION ISSUE: Enable debug build to use local backend, or check production backend deployment.";

                Debug.WriteLine("🚨 BACKEND CONFIGURATION ISSUE DETECTED");
                Debug.WriteLine($"📋 Current Environment: {BackendConfigService.CurrentEnvironment}");
                Debug.WriteLine($"🌐 Backend URL: {BackendConfigService.ApiEndpoints.GetBaseUrl()}");

                // Fallback to local storage when server returns HTML/invalid JSON
                await LoadGoalsFromFile();
            }
            catch (HttpRequestException httpEx)
            {
                Debug.WriteLine($"Network error loading goals: {httpEx.Message}");
                HasError = true;
                ErrorMessage = "Network error. Check your internet connection and server status. Loading saved goals instead.";

                // Fallback to local storage on network errors
                await LoadGoalsFromFile();
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine("Request timed out loading goals from backend");
                HasError = true;
                ErrorMessage = "Request timed out. The server may be slow or unavailable. Loading saved goals instead.";

                // Fallback to local storage on timeout
                await LoadGoalsFromFile();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected error loading goals from backend: {ex.Message}");
                Debug.WriteLine($"Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");

                HasError = true;
                ErrorMessage = "An unexpected error occurred while loading goals. Loading saved goals instead.";

                // Fallback to local storage on any other unexpected error
                await LoadGoalsFromFile();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadGoalsFromFile()
        {
            try
            {
                IsLoading = true;
                Debug.WriteLine("LoadGoalsFromFile called - No sample data loaded. Backend connection required.");

                await Task.Delay(50); // Small delay to show loading state

                // Clear collections - no sample data
                MyGoals.Clear();
                AllDataItems.Clear();

                Debug.WriteLine("No local goals loaded. Please ensure backend is running and accessible.");

                // Show informative message about backend requirement
                HasError = true;
                ErrorMessage = _appModeService.CurrentMode == AppMode.Offline
                    ? "Offline mode: No local goals available. Switch to online mode to access your goals."
                    : "No goals loaded. Please ensure the backend server is running and accessible.";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LoadGoalsFromFile: {ex.Message}");
                HasError = true;
                ErrorMessage = "Unable to load goals.";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private List<Goal> ProcessMyGoalsFromBackend(List<DataItem> goalItems)
        {
            var myGoalItems = new List<Goal>();

            foreach (var dataItem in goalItems)
            {
                try
                {
                    var goalItem = ParseDataItemToGoalItem(dataItem);
                    if (goalItem != null)
                    {
                        myGoalItems.Add(goalItem);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error parsing goal item: {ex.Message}");
                }
            }

            return myGoalItems;
        }

        private Goal ParseDataItemToGoalItem(DataItem dataItem)
        {
            if (dataItem?.Data?.Text == null) return null;

            var goalItem = new Goal
            {
                Id = dataItem._id ?? Guid.NewGuid().ToString(),
                CreatedAt = dataItem.createdAt
            };

            // Parse the text format: "Goal:description|Title:title|Description:description|..."
            var parts = dataItem.Data.Text.Split('|');
            foreach (var part in parts)
            {
                var keyValue = part.Split(':', 2);
                if (keyValue.Length != 2) continue;

                var key = keyValue[0].Trim();
                var value = keyValue[1].Trim();

                switch (key.ToLower())
                {
                    case "goal":
                        goalItem.Description = value;
                        break;
                    case "title":
                        goalItem.Title = value;
                        break;
                    case "description":
                        goalItem.Description = value;
                        break;
                    case "priority":
                        if (int.TryParse(value, out int priority))
                            goalItem.Priority = priority;
                        break;
                    case "deadline":
                        if (DateTime.TryParse(value, out DateTime deadline))
                            goalItem.Deadline = deadline;
                        break;
                    case "public":
                        goalItem.IsShared = bool.TryParse(value, out bool isShared) && isShared;
                        break;
                    case "goaltype":
                        goalItem.GoalType = value;
                        break;
                }
            }

            // Set default title if none found
            if (string.IsNullOrEmpty(goalItem.Title) && !string.IsNullOrEmpty(goalItem.Description))
            {
                goalItem.Title = goalItem.Description.Length > 50
                    ? goalItem.Description.Substring(0, 50) + "..."
                    : goalItem.Description;
            }

            return goalItem;
        }

        private async Task SaveGoalToBackend(Goal goal)
        {
            if (_appModeService.CurrentMode == AppMode.Offline)
            {
                Debug.WriteLine("App is in offline mode. Goal saved locally only.");
                return;
            }

            try
            {
                var token = await SecureStorage.GetAsync("userToken");
                if (string.IsNullOrEmpty(token))
                {
                    Debug.WriteLine("User is not logged in.");
                    return;
                }

                var goalData = CreateGoalDataString(goal);
                var response = await _dataService.CreateDataAsync(goalData, token);
                if (response.DataIsSuccess)
                {
                    Debug.WriteLine("Goal saved to backend");
                }
                else
                {
                    Debug.WriteLine("Failed to save goal to backend");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error saving goal to backend: {ex.Message}");
            }
        }

        private string CreateGoalDataString(Goal goal)
        {
            var parts = new List<string>();

            if (!string.IsNullOrEmpty(goal.Title))
                parts.Add($"Title:{goal.Title}");

            if (!string.IsNullOrEmpty(goal.Description))
                parts.Add($"Goal:{goal.Description}");

            if (!string.IsNullOrEmpty(goal.GoalType))
                parts.Add($"GoalType:{goal.GoalType}");

            if (goal.IsShared)
                parts.Add($"Public:{goal.IsShared}");

            parts.Add($"Priority:{goal.Priority}");
            parts.Add($"Deadline:{goal.Deadline:yyyy-MM-dd}");

            return string.Join("|", parts);
        }

        async Task NavigateLogin()
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

        // Fix warning CS0108 by using 'new' keyword
        public new event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null, Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        // Fix warning CS0114 by using 'new' keyword
        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
