using CSimple.Models;
using CSimple.Services;
using CSimple.Services.AppModeService;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace CSimple.Pages
{
    public partial class GoalPage : ContentPage, INotifyPropertyChanged
    {
        private readonly GoalService _goalService;
        private readonly CSimple.Services.AppModeService.AppModeService _appModeService;

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

        public ICommand ToggleCreateGoalCommand { get; }
        public ICommand SubmitGoalCommand { get; }
        public ICommand DeleteGoalCommand { get; }
        public ICommand EditGoalCommand { get; }

        public GoalPage()
        {
            InitializeComponent();

            _goalService = ServiceProvider.GetService<GoalService>();
            _appModeService = ServiceProvider.GetService<CSimple.Services.AppModeService.AppModeService>();

            ToggleCreateGoalCommand = new Command(OnToggleCreateGoal);
            SubmitGoalCommand = new Command(async () => await OnSubmitGoal());
            DeleteGoalCommand = new Command<Goal>(async (goal) => await OnDeleteGoal(goal));
            EditGoalCommand = new Command<Goal>(OnEditGoal);

            BindingContext = this;

            _ = LoadGoalsAsync();
        }

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
            await LoadGoalsAsync();
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null, Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
            storage = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
