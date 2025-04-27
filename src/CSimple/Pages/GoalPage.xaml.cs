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

namespace CSimple.Pages
{
    public partial class GoalPage : ContentPage, INotifyPropertyChanged
    {
        private readonly GoalService _goalService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;
        private readonly OrientPageViewModel _orientPageViewModel; // Added
        private readonly FileService _fileService; // Added FileService

        private bool _showNewGoal = false;
        public bool ShowNewGoal
        {
            get => _showNewGoal;
            set => SetProperty(ref _showNewGoal, value, onChanged: () => OnPropertyChanged(nameof(CreateGoalButtonText)));
        }

        public string CreateGoalButtonText => ShowNewGoal ? "Cancel Goal" : "Create Goal";

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
        public GoalPage(GoalService goalService, CSimple.Services.AppModeService.AppModeService appModeService, OrientPageViewModel orientPageViewModel, FileService fileService) // Added FileService
        {
            InitializeComponent();

            _goalService = goalService; // Use injected service
            _appModeService = appModeService; // Use injected service
            _orientPageViewModel = orientPageViewModel; // Store injected ViewModel
            _fileService = fileService; // Store injected FileService

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

            _ = LoadInitialDataAsync(); // Combined loading
        }

        // Add the missing LoadInitialDataAsync method
        private async Task LoadInitialDataAsync()
        {
            // Load goals
            await LoadGoalsAsync();

            // Load available pipelines
            await LoadAvailablePipelinesAsync();
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

        private void OnDownloadGoal(Goal goal)
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
            _ = SaveGoalsAsync();
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
            await SaveGoalsAsync();

            await _goalService.SaveGoalToBackend(newGoal);

            ClearGoalForm();
            ShowNewGoal = false;
        }

        private async Task OnDeleteGoal(Goal goalToDelete)
        {
            if (goalToDelete == null) return;

            bool confirm = await DisplayAlert("Confirm Delete", $"Are you sure you want to delete the goal '{goalToDelete.Title}'?", "Yes", "No");
            if (confirm)
            {
                MyGoals.Remove(goalToDelete);
                await SaveGoalsAsync();

                await _goalService.DeleteGoalFromBackend(goalToDelete.Id);
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

        private async Task SaveGoalsAsync()
        {
            await _goalService.SaveGoalsToFile(MyGoals);
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            Debug.WriteLine("GoalPage Appearing");
            // Reload pipelines in case new ones were created/deleted elsewhere
            await LoadInitialDataAsync();
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
