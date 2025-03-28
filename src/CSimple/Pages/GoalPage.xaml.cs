﻿using CSimple.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace CSimple.Pages
{
    public partial class GoalPage : ContentPage
    {
        public bool ShowNewGoal { get; set; } = false;
        public bool ShowMyGoals { get; set; } = false;
        public string NewGoalText { get; set; } = string.Empty;
        public ObservableCollection<string> MyGoals { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<DataItem> AllDataItems { get; set; } = new ObservableCollection<DataItem>();
        public string CreateGoalButtonText => ShowNewGoal ? "Cancel Goal" : "Create Goal";
        public string MyGoalsButtonText => ShowMyGoals ? "Hide Goals" : "My Goals";
        public ICommand ToggleCreateGoalCommand { get; }
        public ICommand ToggleMyGoalsCommand { get; }
        public ICommand SubmitGoalCommand { get; }
        private readonly GoalService _goalService;

        public GoalPage()
        {
            InitializeComponent();
            // Initialize Commands
            ToggleCreateGoalCommand = new Command(OnToggleCreateGoal);
            ToggleMyGoalsCommand = new Command(OnToggleMyGoals);
            SubmitGoalCommand = new Command(OnSubmitGoal);

            // Initialize services
            _goalService = ServiceProvider.GetService<GoalService>();

            // Bind the context
            BindingContext = this;
            CheckUserLoggedIn();
            // Load goals from file
            _ = LoadGoalsFromFile();
        }

        private async void CheckUserLoggedIn()
        {
            if (!await _goalService.IsUserLoggedInAsync())
            {
                Debug.WriteLine("User is not logged in, navigating to login...");
                NavigateLogin();
            }
            else
            {
                Debug.WriteLine("User is logged in.");
                await LoadGoalsFromBackend();
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

        private void OnToggleCreateGoal()
        {
            ShowNewGoal = !ShowNewGoal;
            OnPropertyChanged(nameof(ShowNewGoal));
            OnPropertyChanged(nameof(CreateGoalButtonText));
        }

        private void OnToggleMyGoals()
        {
            ShowMyGoals = !ShowMyGoals;
            OnPropertyChanged(nameof(ShowMyGoals));
            OnPropertyChanged(nameof(MyGoalsButtonText));
        }

        private async void OnSubmitGoal()
        {
            if (!string.IsNullOrWhiteSpace(NewGoalText))
            {
                MyGoals.Add(NewGoalText);
                await _goalService.SaveGoalsToFile(MyGoals);
                await _goalService.SaveGoalToBackend(NewGoalText);
                NewGoalText = string.Empty;
                OnPropertyChanged(nameof(NewGoalText));
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
        }

        private async Task LoadGoalsFromBackend()
        {
            await _goalService.LoadGoalsFromBackend(MyGoals, AllDataItems);
        }

        private async Task LoadGoalsFromFile()
        {
            await _goalService.LoadGoalsFromFile(MyGoals);
        }
    }
}
